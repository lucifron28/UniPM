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

  testWidgets('app startup begins signed out', (tester) async {
    final result = await pumpFoundation(tester);

    expect(result.controller.status, SessionStatus.signedOut);
    expect(find.widgetWithText(FilledButton, 'Sign in'), findsOneWidget);
  });

  testWidgets('failed login shows a safe error and clears memory session', (tester) async {
    final result = await pumpFoundation(tester, loginSucceeds: false);

    await tester.enterText(find.byType(TextFormField).at(0), 'wrong@example.test');
    await tester.enterText(find.byType(TextFormField).at(1), 'wrong-password');
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pumpAndSettle();

    expect(find.text('Invalid email or password.'), findsOneWidget);
    expect(result.controller.accessToken, isNull);
    expect(result.controller.status, SessionStatus.signedOut);
  });

  testWidgets('unsupported roles remain bounded', (tester) async {
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

  testWidgets('logout clears the memory-only session', (tester) async {
    final result = await pumpFoundation(tester);

    await tester.enterText(find.byType(TextFormField).at(0), 'inspector@example.test');
    await tester.enterText(find.byType(TextFormField).at(1), 'fictional-password');
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pumpAndSettle();
    await tester.tap(find.byTooltip('Log out'));
    await tester.pumpAndSettle();

    expect(find.widgetWithText(FilledButton, 'Sign in'), findsOneWidget);
    expect(result.controller.accessToken, isNull);
    expect(result.gateway.logoutCalled, isTrue);
  });

  test('auth/me sends bearer token and never attaches a Cookie header', () async {
    final requests = <http.Request>[];
    final client = ApiClient(
      baseUrl: Uri.parse('http://localhost:5000/'),
      httpClient: MockClient((request) async {
        requests.add(request);
        return http.Response(
          jsonEncode({
            'id': testUser().id,
            'email': testUser().email,
            'displayName': testUser().displayName,
            'roles': testUser().roles,
          }),
          200,
          headers: {
            'content-type': 'application/json',
            'set-cookie': 'unipm_refresh=server-only-value',
          },
        );
      }),
    );
    client.configureSession(
      accessTokenProvider: () => 'memory-token',
      terminalAuthFailureHandler: () async {},
    );

    await AuthRepository(client).currentUser();

    expect(requests, hasLength(1));
    expect(requests.single.url.path, '/api/v1/auth/me');
    expect(requests.single.headers['authorization'], 'Bearer memory-token');
    expect(requests.single.headers.containsKey('cookie'), isFalse);
    client.dispose();
  });

  test('protected 401 clears memory session without refresh or replay', () async {
    final gateway = FakeAuthGateway(user: testUser());
    final controller = SessionController(gateway);
    await controller.login('inspector@example.test', 'fictional-password');
    final requests = <http.Request>[];
    final client = ApiClient(
      baseUrl: Uri.parse('http://localhost:5000/'),
      httpClient: MockClient((request) async {
        requests.add(request);
        return http.Response('', 401);
      }),
    );
    client.configureSession(
      accessTokenProvider: () => controller.accessToken,
      terminalAuthFailureHandler:
          controller.handleTerminalAuthenticationFailure,
    );

    await expectLater(
      client.getJson('/api/v1/auth/me'),
      throwsA(isA<ApiException>()),
    );

    expect(requests, hasLength(1));
    expect(controller.accessToken, isNull);
    expect(controller.status, SessionStatus.signedOut);
    client.dispose();
  });
}
