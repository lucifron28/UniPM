import 'dart:convert';
import 'dart:async';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

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

class LocalAuthServer {
  LocalAuthServer({required this.currentUserStatus});

  final int currentUserStatus;
  final paths = <String>[];
  final authorizationHeaders = <String?>[];
  final cookieHeaders = <String?>[];
  bool loginSetCookie = false;
  HttpServer? _server;
  StreamSubscription<HttpRequest>? _subscription;

  Uri get baseUrl => Uri.parse('http://127.0.0.1:${_server!.port}/');

  Future<void> start() async {
    _server = await HttpServer.bind(InternetAddress.loopbackIPv4, 0);
    _subscription = _server!.listen((request) async {
      final path = request.uri.path;
      paths.add(path);
      authorizationHeaders.add(
        request.headers.value(HttpHeaders.authorizationHeader),
      );
      cookieHeaders.add(request.headers.value(HttpHeaders.cookieHeader));

      if (path == '/api/v1/auth/login') {
        loginSetCookie = true;
        request.response.headers.add(
          HttpHeaders.setCookieHeader,
          'unipm_refresh=server-only-value; Path=/api/v1/auth',
        );
        await _writeJson(request, 200, <String, dynamic>{
          'accessToken': 'memory-only-access-token',
          'expiresAtUtc': '2026-08-01T00:00:00Z',
          'user': _userJson(),
        });
        return;
      }

      if (path == '/api/v1/auth/me') {
        if (currentUserStatus == 200) {
          await _writeJson(request, 200, _userJson());
        } else {
          request.response.statusCode = currentUserStatus;
          await request.response.close();
        }
        return;
      }

      if (path == '/api/v1/auth/logout') {
        request.response.statusCode = 204;
        await request.response.close();
        return;
      }

      request.response.statusCode = 404;
      await request.response.close();
    });
  }

  Future<void> close() async {
    await _subscription?.cancel();
    await _server?.close(force: true);
  }

  Map<String, dynamic> _userJson() => <String, dynamic>{
        'id': testUser().id,
        'email': testUser().email,
        'displayName': testUser().displayName,
        'roles': testUser().roles,
      };

  Future<void> _writeJson(
    HttpRequest request,
    int status,
    Map<String, dynamic> body,
  ) async {
    request.response.statusCode = status;
    request.response.headers.contentType = ContentType.json;
    request.response.write(jsonEncode(body));
    await request.response.close();
  }
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

  testWidgets('failed login still shows invalid credentials', (tester) async {
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

  test('native transport does not forward login cookies to me or logout', () async {
    final server = LocalAuthServer(currentUserStatus: 200);
    await server.start();
    try {
      final client = ApiClient(baseUrl: server.baseUrl);
      final controller = SessionController(AuthRepository(client));
      client.configureSession(
        accessTokenProvider: () => controller.accessToken,
        terminalAuthFailureHandler:
            controller.handleTerminalAuthenticationFailure,
      );

      await controller.login('inspector@example.test', 'fictional-password');
      await controller.logout();

      expect(server.loginSetCookie, isTrue);
      expect(server.paths, [
        '/api/v1/auth/login',
        '/api/v1/auth/me',
        '/api/v1/auth/logout',
      ]);
      expect(server.authorizationHeaders[1], 'Bearer memory-only-access-token');
      expect(server.cookieHeaders[1], isNull);
      expect(server.cookieHeaders[2], isNull);
      client.dispose();
    } finally {
      await server.close();
    }
  });

  testWidgets('protected 401 clears session without refresh or replay', (tester) async {
    final server = LocalAuthServer(currentUserStatus: 401);
    await server.start();
    try {
      final client = ApiClient(baseUrl: server.baseUrl);
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
      expect(controller.status, SessionStatus.signedOut);
      expect(find.text('Your session expired. Please sign in again.'), findsOneWidget);
      expect(server.paths, [
        '/api/v1/auth/login',
        '/api/v1/auth/me',
      ]);
      expect(server.authorizationHeaders[1], 'Bearer memory-only-access-token');
      expect(server.cookieHeaders[1], isNull);
      client.dispose();
    } finally {
      await server.close();
    }
  });
}
