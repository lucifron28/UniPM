import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;

import 'package:mobile/api/api_client.dart';
import 'package:mobile/api/api_exception.dart';
import 'package:mobile/auth/auth_models.dart';
import 'package:mobile/auth/auth_repository.dart';
import 'package:mobile/auth/session_controller.dart';
import 'package:mobile/main.dart';

class FakeAuthGateway implements AuthGateway {
  FakeAuthGateway({required this.user});

  AuthUser user;
  bool loginSucceeds = true;
  bool logoutCalled = false;

  @override
  Future<LoginResult> login(String email, String password) async {
    if (!loginSucceeds) {
      throw const ApiException(
        statusCode: 401,
        message: 'Invalid credentials.',
      );
    }
    return LoginResult(accessToken: 'memory-only-access-token', user: user);
  }

  @override
  Future<AuthUser> currentUser() async => user;

  @override
  Future<void> logout() async => logoutCalled = true;
}

class CookieCapableFakeClient extends http.BaseClient {
  CookieCapableFakeClient(this.responseFactory);

  final http.Response Function(http.BaseRequest request) responseFactory;
  final requests = <http.BaseRequest>[];
  String? cookie;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    if (cookie != null) request.headers['cookie'] = cookie!;
    requests.add(request);
    final response = responseFactory(request);
    final setCookie = response.headers['set-cookie'];
    if (setCookie != null) cookie = setCookie.split(';').first;
    return http.StreamedResponse(
      Stream.value(utf8.encode(response.body)),
      response.statusCode,
      headers: response.headers,
      request: request,
    );
  }
}

class FakeAuthTransport {
  FakeAuthTransport({this.currentUserStatus = 200, this.protectedStatus = 401});

  final int currentUserStatus;
  final int protectedStatus;
  final clients = <CookieCapableFakeClient>[];

  HttpClientFactory get factory => () {
        final client = CookieCapableFakeClient(_responseFor);
        clients.add(client);
        return client;
      };

  Iterable<http.BaseRequest> get requests =>
      clients.expand((client) => client.requests);

  http.Response _responseFor(http.BaseRequest request) {
    switch (request.url.path) {
      case '/api/v1/auth/login':
        return http.Response(
          jsonEncode({
            'accessToken': 'memory-only-access-token',
            'expiresAtUtc': '2026-08-01T00:00:00Z',
            'user': _userJson(),
          }),
          200,
          headers: {
            'content-type': 'application/json',
            'set-cookie': 'unipm_refresh=server-only-value; Path=/api/v1/auth',
          },
        );
      case '/api/v1/auth/me':
        return currentUserStatus == 200
            ? http.Response(
                jsonEncode(_userJson()),
                200,
                headers: {'content-type': 'application/json'},
              )
            : http.Response('', currentUserStatus);
      case '/api/v1/auth/logout':
        return http.Response('', 204);
      default:
        return http.Response('', protectedStatus);
    }
  }

  Map<String, dynamic> _userJson() => <String, dynamic>{
        'id': testUser().id,
        'email': testUser().email,
        'displayName': testUser().displayName,
        'roles': testUser().roles,
      };
}

AuthUser testUser({List<String> roles = const ['Inspector']}) => AuthUser(
      id: '11111111-1111-4111-8111-111111111111',
      email: 'inspector@example.test',
      displayName: 'Synthetic Inspector',
      roles: roles,
    );

Future<({SessionController controller, FakeAuthGateway gateway})>
    pumpFoundation(
  WidgetTester tester, {
  List<String> roles = const ['Inspector'],
  bool loginSucceeds = true,
}) async {
  final gateway = FakeAuthGateway(user: testUser(roles: roles))
    ..loginSucceeds = loginSucceeds;
  final controller = SessionController(gateway);
  await tester.pumpWidget(UniPmApp(sessionController: controller));
  await tester.pumpAndSettle();
  return (controller: controller, gateway: gateway);
}

Future<void> signIn(WidgetTester tester) async {
  await tester.enterText(find.byType(TextFormField).at(0), 'inspector@example.test');
  await tester.enterText(find.byType(TextFormField).at(1), 'fictional-password');
  await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
  await tester.pumpAndSettle();
}

void expectRedirectsDisabled(Iterable<http.BaseRequest> requests) {
  for (final request in requests) {
    final typedRequest = request as http.Request;
    expect(typedRequest.followRedirects, isFalse);
    expect(typedRequest.maxRedirects, 0);
  }
}

void main() {
  testWidgets('successful login renders the authenticated shell', (tester) async {
    await pumpFoundation(tester);
    await signIn(tester);

    expect(find.text('Welcome, Synthetic Inspector'), findsOneWidget);
    expect(find.text('Inspector'), findsOneWidget);
  });

  testWidgets('app startup begins signed out', (tester) async {
    final result = await pumpFoundation(tester);

    expect(result.controller.status, SessionStatus.signedOut);
    expect(find.widgetWithText(FilledButton, 'Sign in'), findsOneWidget);
  });

  testWidgets('direct login 401 still shows invalid credentials', (tester) async {
    await pumpFoundation(tester, loginSucceeds: false);
    await signIn(tester);

    expect(find.text('Invalid email or password.'), findsOneWidget);
  });

  testWidgets('unsupported roles remain bounded', (tester) async {
    await pumpFoundation(tester, roles: const ['DepartmentHead']);
    await signIn(tester);

    expect(
      find.text('Mobile field access is not available for this role.'),
      findsOneWidget,
    );
    expect(find.text('Log out'), findsOneWidget);
  });

  testWidgets('logout clears the memory-only session', (tester) async {
    final result = await pumpFoundation(tester);
    await signIn(tester);
    await tester.tap(find.byTooltip('Log out'));
    await tester.pumpAndSettle();

    expect(find.widgetWithText(FilledButton, 'Sign in'), findsOneWidget);
    expect(result.controller.accessToken, isNull);
    expect(result.gateway.logoutCalled, isTrue);
  });

  test('fresh clients isolate Set-Cookie from me and logout', () async {
    final transport = FakeAuthTransport(currentUserStatus: 200);
    final client = ApiClient(
      baseUrl: Uri.parse('http://localhost:5000/'),
      httpClientFactory: transport.factory,
    );
    final controller = SessionController(AuthRepository(client));
    client.configureSession(
      accessTokenProvider: () => controller.accessToken,
      terminalAuthFailureHandler:
          controller.handleTerminalAuthenticationFailure,
    );

    await controller.login('inspector@example.test', 'fictional-password');
    await controller.logout();

    expect(transport.clients, hasLength(3));
    expect(transport.clients.first.cookie, 'unipm_refresh=server-only-value');
    expect(transport.requests.map((request) => request.url.path), [
      '/api/v1/auth/login',
      '/api/v1/auth/me',
      '/api/v1/auth/logout',
    ]);
    expect(transport.clients[1].requests.single.headers['authorization'],
        'Bearer memory-only-access-token');
    expect(transport.clients[1].requests.single.headers['cookie'], isNull);
    expect(transport.clients[2].requests.single.headers['cookie'], isNull);
    expectRedirectsDisabled(transport.requests);
    client.dispose();
  });

  testWidgets('post-login me 401 clears session and displays expired message',
      (tester) async {
    final transport = FakeAuthTransport(currentUserStatus: 401);
    final client = ApiClient(
      baseUrl: Uri.parse('http://localhost:5000/'),
      httpClientFactory: transport.factory,
    );
    final controller = SessionController(AuthRepository(client));
    client.configureSession(
      accessTokenProvider: () => controller.accessToken,
      terminalAuthFailureHandler:
          controller.handleTerminalAuthenticationFailure,
    );
    await tester.pumpWidget(UniPmApp(sessionController: controller));
    await tester.pumpAndSettle();
    await signIn(tester);

    expect(controller.accessToken, isNull);
    expect(controller.user, isNull);
    expect(controller.status, SessionStatus.signedOut);
    expect(find.text('Your session expired. Please sign in again.'), findsOneWidget);
    expect(transport.requests.map((request) => request.url.path), [
      '/api/v1/auth/login',
      '/api/v1/auth/me',
    ]);
    expect(transport.clients[1].requests.single.headers['authorization'],
        'Bearer memory-only-access-token');
    expect(transport.clients[1].requests.single.headers['cookie'], isNull);
    expectRedirectsDisabled(transport.requests);
    client.dispose();
  });

  testWidgets('authenticated protected 401 signs out without replay',
      (tester) async {
    final transport = FakeAuthTransport();
    final client = ApiClient(
      baseUrl: Uri.parse('http://localhost:5000/'),
      httpClientFactory: transport.factory,
    );
    final controller = SessionController(AuthRepository(client));
    client.configureSession(
      accessTokenProvider: () => controller.accessToken,
      terminalAuthFailureHandler:
          controller.handleTerminalAuthenticationFailure,
    );
    await tester.pumpWidget(UniPmApp(sessionController: controller));
    await tester.pumpAndSettle();
    await signIn(tester);
    await expectLater(
      client.getJson('/api/v1/assets'),
      throwsA(isA<ApiException>()),
    );
    await tester.pumpAndSettle();

    expect(controller.status, SessionStatus.signedOut);
    expect(find.text('Your session expired. Please sign in again.'), findsOneWidget);
    expect(transport.requests.map((request) => request.url.path), [
      '/api/v1/auth/login',
      '/api/v1/auth/me',
      '/api/v1/assets',
    ]);
    expect(transport.clients[2].requests.single.headers['authorization'],
        'Bearer memory-only-access-token');
    expectRedirectsDisabled(transport.requests);
    client.dispose();
  });
}
