import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:mobile/api/api_client.dart';
import 'package:mobile/api/api_exception.dart';
import 'package:mobile/auth/auth_models.dart';
import 'package:mobile/auth/auth_repository.dart';
import 'package:mobile/auth/session_controller.dart';
import 'package:mobile/main.dart';
import 'package:mobile/storage/secure_session_store.dart';

class FakeSessionCookieStore implements SessionCookieStore {
  String? cookie;
  int clearCalls = 0;

  @override
  Future<String?> readRefreshCookie() async => cookie;

  @override
  Future<void> writeRefreshCookie(String value) async => cookie = value;

  @override
  Future<void> clearRefreshCookie() async {
    clearCalls++;
    cookie = null;
  }
}

class FakeAuthGateway implements AuthGateway {
  FakeAuthGateway({required this.user});

  AuthUser user;
  bool loginSucceeds = true;
  bool logoutCalled = false;
  int refreshCalls = 0;

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
  Future<LoginResult> refresh() async {
    refreshCalls++;
    return LoginResult(accessToken: 'refreshed-memory-token', user: user);
  }

  @override
  Future<AuthUser> currentUser() async => user;

  @override
  Future<void> logout() async => logoutCalled = true;
}

AuthUser testUser({List<String> roles = const ['Inspector']}) => AuthUser(
      id: '11111111-1111-4111-8111-111111111111',
      email: 'inspector@example.test',
      displayName: 'Synthetic Inspector',
      roles: roles,
    );

Future<({SessionController controller, FakeSessionCookieStore store})>
    pumpFoundation(
  WidgetTester tester, {
  List<String> roles = const ['Inspector'],
  bool loginSucceeds = true,
}) async {
  final store = FakeSessionCookieStore();
  final gateway = FakeAuthGateway(user: testUser(roles: roles))
    ..loginSucceeds = loginSucceeds;
  final controller = SessionController(gateway, store);
  await tester.pumpWidget(
    UniPmApp(sessionController: controller),
  );
  await tester.pumpAndSettle();
  return (controller: controller, store: store);
}

void main() {
  testWidgets('successful login renders the authenticated shell', (tester) async {
    await pumpFoundation(tester);

    await tester.enterText(find.byType(TextFormField).at(0), 'inspector@example.test');
    await tester.enterText(find.byType(TextFormField).at(1), 'fictional-password');
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pumpAndSettle();

    expect(find.text('Welcome, Synthetic Inspector'), findsOneWidget);
    expect(find.text('Inspector'), findsOneWidget);
  });

  testWidgets('failed login shows a safe error and clears session material', (tester) async {
    final result = await pumpFoundation(tester, loginSucceeds: false);

    await tester.enterText(find.byType(TextFormField).at(0), 'wrong@example.test');
    await tester.enterText(find.byType(TextFormField).at(1), 'wrong-password');
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pumpAndSettle();

    expect(find.text('Invalid email or password.'), findsOneWidget);
    expect(result.controller.accessToken, isNull);
    expect(result.store.cookie, isNull);
    expect(result.store.clearCalls, greaterThan(0));
  });

  testWidgets('unsupported roles are rejected from the mobile shell', (tester) async {
    await pumpFoundation(tester, roles: const ['DepartmentHead']);

    await tester.enterText(find.byType(TextFormField).at(0), 'head@example.test');
    await tester.enterText(find.byType(TextFormField).at(1), 'fictional-password');
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pumpAndSettle();

    expect(
      find.text('Mobile field access is not available for this role.'),
      findsOneWidget,
    );
    expect(find.text('Log out'), findsOneWidget);
  });

  testWidgets('logout clears the session and returns to login', (tester) async {
    final result = await pumpFoundation(tester);

    await tester.enterText(find.byType(TextFormField).at(0), 'inspector@example.test');
    await tester.enterText(find.byType(TextFormField).at(1), 'fictional-password');
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Log out'));
    await tester.pumpAndSettle();

    expect(
      find.widgetWithText(FilledButton, 'Sign in'),
      findsOneWidget,
    );
    expect(result.controller.accessToken, isNull);
    expect(result.store.cookie, isNull);
  });

  test('auth/me sends the in-memory bearer access token', () async {
    final store = FakeSessionCookieStore();
    String? memoryToken = 'memory-token';
    http.Request? capturedRequest;
    final client = ApiClient(
      baseUrl: Uri.parse('http://localhost:5000/'),
      cookieStore: store,
      httpClient: MockClient((request) async {
        capturedRequest = request;
        return http.Response(
          jsonEncode({
            'id': testUser().id,
            'email': testUser().email,
            'displayName': testUser().displayName,
            'roles': testUser().roles,
          }),
          200,
          headers: {'content-type': 'application/json'},
        );
      }),
    );
    client.configureSession(
      accessTokenProvider: () => memoryToken,
      refreshHandler: () async => memoryToken,
      terminalAuthFailureHandler: () async {},
    );

    await AuthRepository(client).currentUser();

    expect(capturedRequest?.url.path, '/api/v1/auth/me');
    expect(
      capturedRequest?.headers['authorization'],
      'Bearer memory-token',
    );
    client.dispose();
  });

  test('a protected 401 performs one refresh and one replay', () async {
    final store = FakeSessionCookieStore();
    var memoryToken = 'old-token';
    var refreshCalls = 0;
    final requests = <http.Request>[];
    final client = ApiClient(
      baseUrl: Uri.parse('http://localhost:5000/'),
      cookieStore: store,
      httpClient: MockClient((request) async {
        requests.add(request);
        if (requests.length == 1) return http.Response('', 401);
        return http.Response(
          jsonEncode({'ok': true}),
          200,
          headers: {'content-type': 'application/json'},
        );
      }),
    );
    client.configureSession(
      accessTokenProvider: () => memoryToken,
      refreshHandler: () async {
        refreshCalls++;
        memoryToken = 'new-token';
        return memoryToken;
      },
      terminalAuthFailureHandler: () async {},
    );

    await client.getJson('/api/v1/auth/me');

    expect(refreshCalls, 1);
    expect(requests, hasLength(2));
    expect(requests[0].headers['authorization'], 'Bearer old-token');
    expect(requests[1].headers['authorization'], 'Bearer new-token');
    client.dispose();
  });

  test('a replayed 401 clears memory and secure session material', () async {
    final store = FakeSessionCookieStore()..cookie = 'unipm_refresh=old';
    final gateway = FakeAuthGateway(user: testUser());
    final controller = SessionController(gateway, store);
    await controller.login('inspector@example.test', 'fictional-password');

    final requests = <http.Request>[];
    final client = ApiClient(
      baseUrl: Uri.parse('http://localhost:5000/'),
      cookieStore: store,
      httpClient: MockClient((request) async {
        requests.add(request);
        return http.Response('', 401);
      }),
    );
    client.configureSession(
      accessTokenProvider: () => controller.accessToken,
      refreshHandler: controller.refreshForRequest,
      terminalAuthFailureHandler:
          controller.handleTerminalAuthenticationFailure,
    );

    await expectLater(
      client.getJson('/api/v1/auth/me'),
      throwsA(isA<ApiException>()),
    );

    expect(requests, hasLength(2));
    expect(gateway.refreshCalls, 1);
    expect(controller.accessToken, isNull);
    expect(controller.status, SessionStatus.signedOut);
    expect(store.cookie, isNull);
    expect(store.clearCalls, greaterThan(0));
    client.dispose();
  });
}
