import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:mobile/main.dart' as app;

const _syntheticInspectorEmail = 'inspector@unipm.local';
const _syntheticAssetCode = 'DEMO-EL-001';
const _smokePassword = String.fromEnvironment('UNIPM_SMOKE_PASSWORD');
const _apiLevel = String.fromEnvironment('UNIPM_SMOKE_API');

void main() {
  final binding = IntegrationTestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('synthetic authenticated field-work compatibility smoke', (
    tester,
  ) async {
    if (_smokePassword.isEmpty) {
      fail('The generated synthetic development password was not provided.');
    }

    app.main();
    await tester.pump();
    await _waitFor(
      tester,
      find.text('Use your UniPM field-work account.'),
      'login screen',
    );
    expect(find.text('Sign in'), findsNWidgets(2));
    expect(find.byType(TextFormField), findsNWidgets(2));
    _checkForFlutterRenderingErrors(tester, 'login');
    await binding.convertFlutterSurfaceToImage();
    await binding.takeScreenshot('android-api-$_apiLevel-login');
    debugPrint('SMOKE_RESULT login_screen=PASS');

    await tester.enterText(
      find.byType(TextFormField).at(0),
      _syntheticInspectorEmail,
    );
    await tester.enterText(find.byType(TextFormField).at(1), _smokePassword);
    final signInButton = find.widgetWithText(FilledButton, 'Sign in');
    await tester.ensureVisible(signInButton);
    await tester.tap(signInButton);
    await _waitFor(
      tester,
      find.text('Welcome, Maintenance Inspector'),
      'authenticated dashboard',
      timeout: const Duration(seconds: 60),
    );
    _checkForFlutterRenderingErrors(tester, 'authenticated dashboard');
    await binding.takeScreenshot('android-api-$_apiLevel-dashboard');
    debugPrint('SMOKE_RESULT authenticated_navigation=PASS');
    debugPrint('SMOKE_RESULT dashboard_responsive_layout=PASS');

    final searchAction = find.byKey(const Key('search-assets'));
    await tester.ensureVisible(searchAction);
    await tester.tap(searchAction);
    await _waitFor(
      tester,
      find.byKey(const Key('asset-search-input')),
      'asset search',
    );
    await tester.enterText(
      find.byKey(const Key('asset-search-input')),
      _syntheticAssetCode,
    );
    await _waitFor(
      tester,
      find.text(_syntheticAssetCode),
      'synthetic asset search result',
      timeout: const Duration(seconds: 45),
    );
    await tester.tap(find.text(_syntheticAssetCode).last);
    await _waitFor(
      tester,
      find.byKey(const Key('asset-code')),
      'asset details',
    );
    expect(find.text(_syntheticAssetCode), findsOneWidget);
    _checkForFlutterRenderingErrors(tester, 'asset details');
    await binding.takeScreenshot('android-api-$_apiLevel-asset-details');
    debugPrint('SMOKE_RESULT asset_lookup_and_details=PASS');

    final historyAction = find.byKey(const Key('view-asset-history'));
    await tester.ensureVisible(historyAction);
    await tester.tap(historyAction);
    await _waitFor(
      tester,
      find.text('Maintenance history'),
      'maintenance history',
      timeout: const Duration(seconds: 45),
    );
    final historyCards = find.byWidgetPredicate((widget) {
      final key = widget.key;
      return key is ValueKey<String> &&
          key.value.startsWith('asset-history-record-');
    });
    await _waitForAny(
      tester,
      [historyCards],
      'acknowledged maintenance-history record',
      timeout: const Duration(seconds: 45),
    );
    expect(historyCards, findsAtLeastNWidgets(1));
    _checkForFlutterRenderingErrors(tester, 'maintenance history');
    await binding.takeScreenshot('android-api-$_apiLevel-history');
    debugPrint('SMOKE_RESULT maintenance_history_display=PASS');

    await tester.pageBack();
    await _waitFor(tester, find.text('Asset found'), 'return to asset details');
    final scheduleSummary = find.textContaining('PM cycle:');
    await _waitFor(
      tester,
      scheduleSummary,
      'preventive-maintenance schedule summary',
      timeout: const Duration(seconds: 45),
    );
    _checkForFlutterRenderingErrors(tester, 'PM schedule');
    await binding.takeScreenshot('android-api-$_apiLevel-schedule');
    debugPrint('SMOKE_RESULT preventive_maintenance_schedule=PASS');

    final startInspection = find.byKey(const Key('start-pm'));
    final resumeInspection = find.byKey(const Key('resume-pm'));
    final entryReady = await _waitForAny(
      tester,
      [startInspection, resumeInspection],
      'available inspection entry action',
      timeout: const Duration(seconds: 45),
    );
    if (!entryReady) {
      debugPrint(
        'SMOKE_RESULT inspection_form_entry=BLOCKED (no usable Draft action appeared after the authenticated schedule/form lookup)',
      );
      fail('The synthetic schedule did not reach an inspection-entry screen.');
    }

    final entryAction = startInspection.evaluate().isNotEmpty
        ? startInspection
        : resumeInspection;
    await tester.ensureVisible(entryAction);
    await tester.tap(entryAction);
    await _waitFor(
      tester,
      find.byKey(const Key('new-inspection-date')),
      'inspection-entry form',
      timeout: const Duration(seconds: 60),
    );
    final today = DateTime.now().toUtc().toIso8601String().substring(0, 10);
    await tester.enterText(find.byKey(const Key('new-inspection-date')), today);
    await tester.enterText(
      find.byKey(const Key('new-inspection-remarks')),
      'Synthetic Android compatibility smoke entry.',
    );
    await tester.ensureVisible(find.byKey(const Key('add-inspection-button')));
    _checkForFlutterRenderingErrors(tester, 'inspection-entry form');
    await binding.takeScreenshot('android-api-$_apiLevel-inspection-form');
    await tester.tap(find.byKey(const Key('add-inspection-button')));
    await _waitFor(
      tester,
      find.text('Inspection Recorded'),
      'saved synthetic inspection-entry confirmation',
      timeout: const Duration(seconds: 60),
    );
    _checkForFlutterRenderingErrors(tester, 'saved inspection entry');
    await binding.takeScreenshot('android-api-$_apiLevel-inspection-saved');
    debugPrint('SMOKE_RESULT preventive_maintenance_inspection_entry=PASS');
    debugPrint('SMOKE_RESULT visible_layout_overflow=PASS');
    debugPrint(
      'SMOKE_RESULT offline_draft_persistence=NOT_EXECUTED (device network toggle/restart not part of this smoke)',
    );
    debugPrint('SMOKE_RESULT reconnect_synchronization=NOT_EXECUTED');
    debugPrint('SMOKE_RESULT live_error_or_conflict_recovery=NOT_EXECUTED');
  });
}

Future<void> _waitFor(
  WidgetTester tester,
  Finder finder,
  String description, {
  Duration timeout = const Duration(seconds: 30),
}) async {
  final deadline = DateTime.now().add(timeout);
  while (DateTime.now().isBefore(deadline)) {
    await tester.pump(const Duration(milliseconds: 250));
    _checkForFlutterRenderingErrors(tester, description);
    if (finder.evaluate().isNotEmpty) return;
  }
  fail('Timed out waiting for $description.');
}

Future<bool> _waitForAny(
  WidgetTester tester,
  List<Finder> finders,
  String description, {
  Duration timeout = const Duration(seconds: 30),
}) async {
  final deadline = DateTime.now().add(timeout);
  while (DateTime.now().isBefore(deadline)) {
    await tester.pump(const Duration(milliseconds: 250));
    _checkForFlutterRenderingErrors(tester, description);
    if (finders.any((finder) => finder.evaluate().isNotEmpty)) return true;
  }
  return finders.any((finder) => finder.evaluate().isNotEmpty);
}

void _checkForFlutterRenderingErrors(
  WidgetTester tester,
  String screenDescription,
) {
  final error = tester.takeException();
  if (error != null) {
    fail(
      'Flutter reported a runtime/layout error on $screenDescription: $error',
    );
  }
}
