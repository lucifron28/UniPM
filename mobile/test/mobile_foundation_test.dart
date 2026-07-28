import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

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
  Future<LoginResult> refresh() async =>
      LoginResult(accessToken: 'refreshed-memory-token', user: user);

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
}
