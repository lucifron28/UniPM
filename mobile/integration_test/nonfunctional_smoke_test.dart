import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:http/http.dart' as http;
import 'package:mobile/api/api_client.dart';
import 'package:mobile/auth/auth_repository.dart';
import 'package:mobile/features/maintenance_history/asset_maintenance_history_models.dart';
import 'package:mobile/features/preventive_maintenance/offline_inspection_repository.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_models.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_repository.dart';
import 'package:mobile/features/preventive_maintenance/pm_cycle_presentation.dart';
import 'package:mobile/main.dart' as app;

import 'support/visible_scroll_interaction.dart';

const _syntheticInspectorEmail = 'inspector@unipm.local';
const _syntheticAssetCode = 'DEMO-EL-001';
const _syntheticAssetId = '30000000-0000-4000-8000-000000000001';
const _inspectionAssetCode = 'DEMO-EL-003';
const _inspectionAssetId = '30000000-0000-4000-8000-000000000003';
const _inspectionScheduleId = '31000000-0000-4000-8000-000000000003';
const _offlineAssetCode = 'DEMO-FE-003';
const _offlineAssetId = '10000000-0000-4000-8000-000000000003';
const _offlineScheduleId = '11000000-0000-4000-8000-000000000003';
const _expectedHistoryInspectionId = '34000000-0000-4000-8000-000000000001';
const _apiBaseUrl = String.fromEnvironment('UNIPM_API_BASE_URL');
const _smokePassword = String.fromEnvironment('UNIPM_SMOKE_PASSWORD');
const _smokeUserId = String.fromEnvironment('UNIPM_SMOKE_USER_ID');
const _apiLevel = String.fromEnvironment('UNIPM_SMOKE_API');
const _smokePhase = String.fromEnvironment(
  'UNIPM_SMOKE_PHASE',
  defaultValue: 'prepare',
);

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();
  if (_smokePhase == 'restart') {
    testWidgets(
      'offline Draft persists and synchronizes after process restart',
      _captureSmokeFailures(
        'offline-restart-sync',
        (tester, setStage) => _runAfterProcessRestart(tester, setStage),
      ),
    );
    return;
  }
  if (_smokePhase != 'prepare') {
    test(
      'valid smoke phase',
      () => fail('Unknown smoke phase "$_smokePhase".'),
    );
    return;
  }

  testWidgets(
    'asset details and acknowledged maintenance history',
    _captureSmokeFailures('asset-history', _runAssetHistoryScenario),
  );
  testWidgets(
    'online inspection Draft save, reopen, edit, and persistence',
    _captureSmokeFailures('online-inspection', _runOnlineInspectionScenario),
  );
  testWidgets(
    'offline Draft creation and local persistence before restart',
    _captureSmokeFailures('offline-draft-prepare', _runOfflineDraftScenario),
  );
}

Future<_SmokeServerVerifier> _startAuthenticatedSession(
  WidgetTester tester,
) async {
  _validateSmokeEnvironment();
  debugPrint('SMOKE_CONTEXT android_api=$_apiLevel');
  debugPrint(
    'SMOKE_CONTEXT fixture_scope=isolated_ephemeral_database api=$_apiLevel',
  );

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
  debugPrint('SMOKE_RESULT login_screen=PASS');

  final emailField = find.byType(TextFormField).at(0);
  await _scrollUntilVisible(
    tester,
    emailField,
    description: 'synthetic inspector email field',
  );
  await tester.enterText(emailField, _syntheticInspectorEmail);
  final passwordField = find.byType(TextFormField).at(1);
  await _scrollUntilVisible(
    tester,
    passwordField,
    description: 'synthetic inspector password field',
  );
  await tester.enterText(passwordField, _smokePassword);
  await _tapAfterScrolling(
    tester,
    find.widgetWithText(FilledButton, 'Sign in'),
    'Sign in',
  );
  await _waitFor(
    tester,
    find.text('Welcome, Maintenance Inspector'),
    'authenticated dashboard',
    timeout: const Duration(seconds: 60),
  );
  _checkForFlutterRenderingErrors(tester, 'authenticated dashboard');
  debugPrint('SMOKE_RESULT authenticated_navigation=PASS');
  debugPrint('SMOKE_RESULT dashboard_responsive_layout=PASS');

  final verifier = _SmokeServerVerifier();
  await verifier.signIn();
  debugPrint('SMOKE_TEST_USER_ID=' + verifier.userId);
  return verifier;
}

void _validateSmokeEnvironment() {
  if (_smokePassword.isEmpty) {
    fail('The generated synthetic development password was not provided.');
  }
  if (_apiBaseUrl.isEmpty) {
    fail('The synthetic API URL was not provided to the integration test.');
  }
}

Future<void> _runAssetHistoryScenario(
  WidgetTester tester,
  void Function(String stage) setStage,
) async {
  setStage('startup-login');
  final serverVerifier = await _startAuthenticatedSession(tester);

  setStage('asset-search-result');
  await _openAssetFromDashboard(tester, _syntheticAssetCode);
  expect(find.text(_syntheticAssetCode), findsOneWidget);
  _checkForFlutterRenderingErrors(tester, 'asset details');
  debugPrint('SMOKE_RESULT asset_lookup_and_details=PASS');

  setStage('asset-history-api');
  final apiHistory = await serverVerifier.getHistory(_syntheticAssetId);
  final expectedApiHistory = apiHistory.where(
    (record) => record.id == _expectedHistoryInspectionId,
  );
  expect(expectedApiHistory, hasLength(1));
  const expectedHistoryRemark =
      'Emergency light passed the simulated power interruption test.';
  expect(expectedApiHistory.single.remarks, expectedHistoryRemark);
  debugPrint(
    'SMOKE_RESULT maintenance_history_api=PASS '
    'status=200 record=$_expectedHistoryInspectionId',
  );

  setStage('asset-history');
  await _tapAfterScrolling(
    tester,
    find.byKey(const Key('view-asset-history')),
    'View maintenance history',
  );
  await _waitFor(
    tester,
    find.text('Maintenance history'),
    'maintenance-history route',
    timeout: const Duration(seconds: 45),
  );
  await _waitFor(
    tester,
    find.byKey(const Key('asset-history-list')),
    'maintenance-history list after API response',
    timeout: const Duration(seconds: 45),
  );
  final expectedHistoryRecord = find.byKey(
    const ValueKey('asset-history-record-$_expectedHistoryInspectionId'),
  );
  await _scrollUntilVisible(
    tester,
    expectedHistoryRecord,
    description: 'seeded acknowledged maintenance-history record',
  );
  expect(expectedHistoryRecord, findsOneWidget);
  expect(find.text(expectedHistoryRemark), findsOneWidget);
  _checkForFlutterRenderingErrors(tester, 'maintenance history');
  debugPrint('SMOKE_RESULT maintenance_history_display=PASS');

  await tester.pageBack();
  final assetLookupTitle = find.text('Asset lookup');
  await _waitFor(
    tester,
    assetLookupTitle,
    'Asset lookup/details route after closing maintenance history',
  );
  _verifyVisibleOnScreen(
    tester,
    assetLookupTitle,
    'Asset lookup route title after closing maintenance history',
  );
  debugPrint('SMOKE_RESULT history_return_to_asset_details=PASS');

  await tester.pageBack();
  await _waitFor(
    tester,
    find.byKey(const Key('asset-search-input')),
    'return from asset details to asset search',
  );
  await tester.pageBack();
  await _waitFor(
    tester,
    find.text('Welcome, Maintenance Inspector'),
    'return from asset search to dashboard',
  );
}

Future<void> _runOnlineInspectionScenario(
  WidgetTester tester,
  void Function(String stage) setStage,
) async {
  setStage('startup-login');
  final serverVerifier = await _startAuthenticatedSession(tester);

  setStage('pm-fixture-check');
  final inspectionSchedules = await serverVerifier.listSchedules(
    _inspectionAssetId,
  );
  final inspectionSchedule = inspectionSchedules.singleWhere(
    (schedule) => schedule.id == _inspectionScheduleId,
  );
  expect(inspectionSchedule.asset?.assetCode, _inspectionAssetCode);
  expect(inspectionSchedule.status, 'Due');
  expect(inspectionSchedule.canonicalPmCycle, '2026-06');
  expect(inspectionSchedule.isEligibleForFieldWork, isTrue);
  expect(
    inspectionSchedule.assignedToUserId,
    serverVerifier.userId,
    reason: 'The synthetic PM schedule must be assigned to the smoke user.',
  );
  expect(
    await serverVerifier.listDraftsForInspectionFixture(
      assetCategory: 'emergency-light',
      department: 'Campus Facilities',
      pmCycle: inspectionSchedule.canonicalPmCycle,
    ),
    isEmpty,
  );
  debugPrint(
    'SMOKE_FIXTURE inspection_asset=$_inspectionAssetCode '
    'schedule=$_inspectionScheduleId status=${inspectionSchedule.status} '
    'cycle=${inspectionSchedule.canonicalPmCycle} assigned_to_smoke_user=PASS',
  );

  setStage('dashboard-pm-schedule');
  await _openAssetFromDashboard(tester, _inspectionAssetCode);
  final pmEntry = find.byKey(ValueKey('pm-entry-$_inspectionAssetId'));
  await _scrollUntilVisible(
    tester,
    pmEntry,
    description:
        'preventive-maintenance schedule panel for $_inspectionAssetCode',
  );
  final expectedCycleText =
      'PM cycle: ' + formatPmCycle(inspectionSchedule.canonicalPmCycle);
  final scheduleSummary = find.text(expectedCycleText);
  await _waitFor(
    tester,
    scheduleSummary,
    'PM cycle summary matching API schedule $_inspectionScheduleId',
    timeout: const Duration(seconds: 45),
  );
  await _scrollUntilVisible(
    tester,
    scheduleSummary,
    description:
        'PM cycle summary matching API schedule $_inspectionScheduleId',
  );
  expect(scheduleSummary, findsOneWidget);
  final expectedDueDateText =
      'Due date: ' + formatPmCycleDueDate(inspectionSchedule.canonicalPmCycle);
  final dueDateSummary = find.text(expectedDueDateText);
  await _scrollUntilVisible(
    tester,
    dueDateSummary,
    description: 'PM due date matching API schedule $_inspectionScheduleId',
  );
  expect(dueDateSummary, findsOneWidget);
  _checkForFlutterRenderingErrors(tester, 'PM schedule');
  debugPrint(
    'SMOKE_RESULT preventive_maintenance_schedule=PASS '
    'schedule=$_inspectionScheduleId cycle=${inspectionSchedule.canonicalPmCycle} '
    'cycle_text=VERIFIED due_date_text=VERIFIED',
  );

  setStage('pm-form-open');
  final startInspection = find.byKey(const Key('start-pm'));
  await _scrollUntilVisible(
    tester,
    startInspection,
    description: 'Start Inspection for $_inspectionAssetCode',
  );
  expect(find.byKey(const Key('resume-pm')), findsNothing);
  expect(
    tester.widget<FilledButton>(startInspection).onPressed,
    isNotNull,
    reason: 'The fixture schedule must have an enabled Start action.',
  );
  await _tapAfterScrolling(
    tester,
    startInspection,
    'Start Inspection for $_inspectionAssetCode',
  );
  await _waitFor(
    tester,
    find.byKey(const Key('new-inspection-date')),
    'inspection-entry form',
    timeout: const Duration(seconds: 60),
  );
  final today = DateTime.now().toUtc().toIso8601String().substring(0, 10);
  final newInspectionDate = find.byKey(const Key('new-inspection-date'));
  await _scrollUntilVisible(
    tester,
    newInspectionDate,
    description: 'new inspection date field',
  );
  await tester.enterText(newInspectionDate, today);
  final onlineRemarks =
      'Android compatibility smoke online save API ' + _apiLevel;
  final newInspectionRemarks = find.byKey(const Key('new-inspection-remarks'));
  await _scrollUntilVisible(
    tester,
    newInspectionRemarks,
    description: 'new inspection remarks field',
  );
  await tester.enterText(newInspectionRemarks, onlineRemarks);
  _checkForFlutterRenderingErrors(tester, 'inspection-entry form');

  setStage('pm-form-save');
  final addInspection = find.byKey(const Key('add-inspection-button'));
  await _scrollUntilVisible(
    tester,
    addInspection,
    description: 'Save inspection',
  );
  expect(tester.widget<FilledButton>(addInspection).onPressed, isNotNull);
  await _tapAfterScrolling(tester, addInspection, 'Save inspection');
  await _waitFor(
    tester,
    find.text('Inspection Recorded'),
    'saved synthetic inspection-entry confirmation',
    timeout: const Duration(seconds: 60),
  );
  _checkForFlutterRenderingErrors(tester, 'saved inspection entry');
  await _tapVisibleOnScreen(
    tester,
    find.byKey(const Key('sheet-view-batch')),
    'View inspection batch from saved confirmation',
  );
  final onlineForm = await serverVerifier.findFormWithInspectionRemarks(
    onlineRemarks,
  );
  expect(onlineForm.status, 'Draft');
  expect(onlineForm.inspections, hasLength(1));
  final onlineInspection = onlineForm.inspections.single;
  expect(onlineInspection.assetCode, _inspectionAssetCode);
  expect(onlineInspection.scheduleId, _inspectionScheduleId);
  debugPrint('SMOKE_RESULT preventive_maintenance_inspection_save=PASS');
  debugPrint(
    'SMOKE_SERVER_STATE form_status=${onlineForm.status} '
    'inspection_count=${onlineForm.inspections.length} '
    'asset=$_inspectionAssetCode online_remarks=VERIFIED',
  );

  setStage('pm-form-reopen');
  await tester.pageBack();
  await _waitFor(
    tester,
    find.text('Asset lookup'),
    'Asset lookup/details route after closing the saved Draft',
  );
  final resumeInspection = find.byKey(const Key('resume-pm'));
  await _scrollUntilVisible(
    tester,
    resumeInspection,
    description: 'Resume Inspection for the saved Draft',
  );
  expect(find.byKey(const Key('start-pm')), findsNothing);
  expect(tester.widget<FilledButton>(resumeInspection).onPressed, isNotNull);
  await _tapAfterScrolling(
    tester,
    resumeInspection,
    'Resume Inspection for the saved Draft',
  );
  final inspectionRemarks = find.byKey(
    Key('inspection-remarks-' + onlineInspection.id),
  );
  await _scrollUntilVisible(
    tester,
    inspectionRemarks,
    description: 'saved inspection row after reopening the Draft',
  );
  expect(
    tester.widget<TextField>(inspectionRemarks).controller?.text,
    onlineRemarks,
  );
  expect(tester.widget<TextField>(inspectionRemarks).enabled, isTrue);
  debugPrint('SMOKE_RESULT preventive_maintenance_inspection_reopen=PASS');

  setStage('pm-form-edit');
  final onlineEditedRemarks =
      'Android compatibility smoke online edit API ' + _apiLevel;
  await tester.enterText(inspectionRemarks, onlineEditedRemarks);
  final saveOnlineInspection = find.byKey(
    ValueKey('save-inspection-' + onlineInspection.id),
  );
  await _scrollUntilVisible(
    tester,
    saveOnlineInspection,
    description: 'Save edited inspection row',
  );
  expect(
    tester.widget<FilledButton>(saveOnlineInspection).onPressed,
    isNotNull,
  );
  await _tapAfterScrolling(
    tester,
    saveOnlineInspection,
    'Save edited inspection row',
  );
  final editedOnlineForm = await _waitForServerInspectionRemarks(
    tester,
    serverVerifier,
    onlineForm.id,
    onlineInspection.id,
    onlineEditedRemarks,
  );
  expect(editedOnlineForm.status, 'Draft');
  expect(editedOnlineForm.inspections.single.remarks, onlineEditedRemarks);
  debugPrint('SMOKE_RESULT preventive_maintenance_inspection_edit=PASS');
  debugPrint(
    'SMOKE_SERVER_STATE after_online_edit form_status=${editedOnlineForm.status} '
    'inspection_count=${editedOnlineForm.inspections.length} remarks=VERIFIED',
  );

  setStage('pm-form-reopen-after-edit');
  await tester.pageBack();
  await _waitFor(
    tester,
    find.text('Asset lookup'),
    'Asset lookup/details route after closing the edited Draft',
  );
  await _scrollUntilVisible(
    tester,
    resumeInspection,
    description: 'Resume Inspection after editing the saved Draft',
  );
  expect(find.byKey(const Key('start-pm')), findsNothing);
  expect(tester.widget<FilledButton>(resumeInspection).onPressed, isNotNull);
  await _tapAfterScrolling(
    tester,
    resumeInspection,
    'Resume Inspection after editing the saved Draft',
  );
  final editedInspectionRemarks = find.byKey(
    Key('inspection-remarks-' + onlineInspection.id),
  );
  await _scrollUntilVisible(
    tester,
    editedInspectionRemarks,
    description: 'edited inspection row after reopening the Draft',
  );
  expect(
    tester.widget<TextField>(editedInspectionRemarks).controller?.text,
    onlineEditedRemarks,
  );
  debugPrint('SMOKE_RESULT preventive_maintenance_inspection_edit_reopen=PASS');
}

Future<void> _runOfflineDraftScenario(
  WidgetTester tester,
  void Function(String stage) setStage,
) async {
  setStage('startup-login');
  final serverVerifier = await _startAuthenticatedSession(tester);

  setStage('offline-fixture-check');
  final offlineSchedules = await serverVerifier.listSchedules(_offlineAssetId);
  final offlineSchedule = offlineSchedules.singleWhere(
    (schedule) => schedule.id == _offlineScheduleId,
  );
  expect(offlineSchedule.asset?.assetCode, _offlineAssetCode);
  expect(offlineSchedule.status, 'Due');
  expect(offlineSchedule.canonicalPmCycle, '2026-11');
  expect(offlineSchedule.isEligibleForFieldWork, isTrue);
  expect(offlineSchedule.assignedToUserId, serverVerifier.userId);
  expect(
    await serverVerifier.listDraftsForInspectionFixture(
      assetCategory: 'fire-extinguisher',
      department: 'CCMS',
      pmCycle: offlineSchedule.canonicalPmCycle,
    ),
    isEmpty,
  );
  debugPrint(
    'SMOKE_FIXTURE offline_asset=$_offlineAssetCode schedule=$_offlineScheduleId '
    'status=${offlineSchedule.status} cycle=${offlineSchedule.canonicalPmCycle} '
    'assigned_to_smoke_user=PASS',
  );

  await _openAssetFromDashboard(tester, _offlineAssetCode);
  final offlineStart = find.byKey(const Key('start-pm'));
  await _scrollUntilVisible(
    tester,
    offlineStart,
    description: 'eligible offline-fixture Start Inspection action',
  );
  expect(tester.widget<FilledButton>(offlineStart).onPressed, isNotNull);

  setStage('offline-connectivity-check');
  debugPrint('SMOKE_NETWORK_REQUEST=offline');
  await _waitForBackendReachability(
    tester,
    reachable: false,
    timeout: const Duration(seconds: 30),
  );
  debugPrint('SMOKE_RESULT backend_disconnected=PASS');

  setStage('offline-draft-create');
  await _tapAfterScrolling(tester, offlineStart, 'Offline Start Inspection');
  await _waitFor(
    tester,
    find.byKey(const Key('new-inspection-date')),
    'new offline inspection form for $_offlineAssetCode',
    timeout: const Duration(seconds: 45),
  );
  final offlineDate = DateTime.now().toUtc().toIso8601String().substring(0, 10);
  final offlineRemarks =
      'Android compatibility smoke offline draft API ' + _apiLevel;
  final offlineDateField = find.byKey(const Key('new-inspection-date'));
  await _scrollUntilVisible(
    tester,
    offlineDateField,
    description: 'offline inspection date field',
  );
  await tester.enterText(offlineDateField, offlineDate);
  final offlineRemarksEntry = find.byKey(const Key('new-inspection-remarks'));
  await _scrollUntilVisible(
    tester,
    offlineRemarksEntry,
    description: 'offline inspection remarks field',
  );
  await tester.enterText(offlineRemarksEntry, offlineRemarks);
  final addOfflineInspection = find.byKey(const Key('add-inspection-button'));
  await _scrollUntilVisible(
    tester,
    addOfflineInspection,
    description: 'Save offline inspection',
  );
  expect(
    tester.widget<FilledButton>(addOfflineInspection).onPressed,
    isNotNull,
  );
  await _tapAfterScrolling(
    tester,
    addOfflineInspection,
    'Save offline inspection',
  );
  await _waitFor(
    tester,
    find.text('Saved on this device'),
    'offline inspection saved locally',
    timeout: const Duration(seconds: 60),
  );
  await _tapVisibleOnScreen(
    tester,
    find.byKey(const Key('sheet-view-batch')),
    'View offline inspection batch from saved confirmation',
  );

  await _scrollUntilVisible(
    tester,
    find.text('Offline draft saved securely'),
    description: 'offline Draft persistence status',
    direction: _ScrollDirection.up,
  );
  await _scrollUntilVisible(
    tester,
    find.textContaining('2 change(s) for this draft are waiting to sync.'),
    description: 'offline create-form and add-inspection pending writes',
    direction: _ScrollDirection.up,
  );
  final offlineRemarksField = find.byWidgetPredicate(
    (widget) =>
        widget is TextField && widget.controller?.text == offlineRemarks,
  );
  await _scrollUntilVisible(
    tester,
    offlineRemarksField,
    description: 'offline inspection row restored from the local Draft',
  );
  expect(
    tester.widget<TextField>(offlineRemarksField).controller?.text,
    offlineRemarks,
  );
  debugPrint('SMOKE_RESULT offline_draft_creation_and_local_save=PASS');
  debugPrint(
    'SMOKE_LOCAL_STATE asset=$_offlineAssetCode schedule=$_offlineScheduleId '
    'queued_writes=2 remarks=VERIFIED',
  );
  debugPrint('SMOKE_PHASE_COMPLETE=offline-pending-restart');
  debugPrint('SMOKE_RESULT visible_layout_overflow=PASS');
  debugPrint('SMOKE_RESULT live_error_or_conflict_recovery=NOT_EXECUTED');
}

Future<void> _runAfterProcessRestart(
  WidgetTester tester,
  void Function(String stage) setStage,
) async {
  _validateSmokeEnvironment();
  setStage('offline-app-restart');
  app.main();
  await tester.pump();
  await _waitFor(
    tester,
    find.text('Use your UniPM field-work account.'),
    'signed-out login screen after cold process restart',
    timeout: const Duration(seconds: 45),
  );
  await _waitForBackendReachability(
    tester,
    reachable: false,
    timeout: const Duration(seconds: 30),
  );
  if (_smokeUserId.isEmpty) {
    fail('The synthetic smoke user ID was not passed to the restart phase.');
  }

  setStage('offline-persistence-after-restart-before-reconnect');
  final storedSnapshot = await tester.runAsync<String?>(
    () => SecureOfflineSnapshotStore(
      apiBaseUrl: Uri.parse(_apiBaseUrl),
    ).read(_smokeUserId),
  );
  expect(
    storedSnapshot,
    isNotNull,
    reason: 'The encrypted offline snapshot must survive app process restart.',
  );
  expect(storedSnapshot, isNotEmpty);
  final snapshot = (jsonDecode(storedSnapshot!) as Map).cast<String, dynamic>();
  expect(snapshot['version'], 1);
  final offlineRemarks =
      'Android compatibility smoke offline draft API $_apiLevel';
  final storedForms = (snapshot['forms'] as List<dynamic>)
      .map(
        (value) => PreventiveMaintenanceForm.fromJson(
          (value as Map).cast<String, dynamic>(),
        ),
      )
      .where(
        (form) => form.inspections.any((row) => row.remarks == offlineRemarks),
      )
      .toList(growable: false);
  expect(storedForms, hasLength(1));
  final storedForm = storedForms.single;
  expect(storedForm.status, 'Draft');
  expect(storedForm.assetCategory, 'fire-extinguisher');
  expect(storedForm.department, 'CCMS');
  expect(storedForm.inspections, hasLength(1));
  final storedInspection = storedForm.inspections.single;
  expect(storedInspection.assetCode, _offlineAssetCode);
  expect(storedInspection.scheduleId, _offlineScheduleId);
  expect(storedInspection.inspectorUserId, _smokeUserId);
  expect(storedInspection.remarks, offlineRemarks);
  final pendingWrites = (snapshot['writes'] as List<dynamic>)
      .map((value) => (value as Map).cast<String, dynamic>())
      .where((write) => write['formId'] == storedForm.id)
      .toList(growable: false);
  expect(pendingWrites, hasLength(2));
  expect(pendingWrites.map((write) => write['state']), everyElement('pending'));
  expect(
    pendingWrites.map((write) => write['kind']),
    unorderedEquals(<String>['createForm', 'addInspection']),
  );
  debugPrint(
    'SMOKE_RESULT offline_draft_persistence_after_restart_before_reconnect=PASS',
  );
  debugPrint(
    'SMOKE_LOCAL_STATE after_restart_before_reconnect form_status=' +
        storedForm.status +
        ' inspection_count=' +
        storedForm.inspections.length.toString() +
        ' asset=' +
        _offlineAssetCode +
        ' schedule=' +
        _offlineScheduleId +
        ' pending_writes=' +
        pendingWrites.length.toString() +
        ' remarks=VERIFIED backend=UNREACHABLE',
  );
  debugPrint('SMOKE_RESTARTED_LOGIN=PASS');
  debugPrint('SMOKE_RESULT app_process_restart=PASS');

  setStage('reconnect-after-restart');
  debugPrint('SMOKE_NETWORK_REQUEST=online');
  await _waitForBackendReachability(
    tester,
    reachable: true,
    timeout: const Duration(seconds: 60),
  );
  debugPrint('SMOKE_RESULT backend_reconnection=PASS');
  final emailField = find.byType(TextFormField).at(0);
  await _scrollUntilVisible(
    tester,
    emailField,
    description: 'reconnection sign-in email field',
  );
  await tester.enterText(emailField, _syntheticInspectorEmail);
  final passwordField = find.byType(TextFormField).at(1);
  await _scrollUntilVisible(
    tester,
    passwordField,
    description: 'reconnection sign-in password field',
  );
  await tester.enterText(passwordField, _smokePassword);
  await _tapAfterScrolling(
    tester,
    find.widgetWithText(FilledButton, 'Sign in'),
    'Sign in after backend reconnection',
  );
  await _waitFor(
    tester,
    find.text('Welcome, Maintenance Inspector'),
    'dashboard after reconnection and same-account sign in',
    timeout: const Duration(seconds: 60),
  );

  final verifier = _SmokeServerVerifier();
  await verifier.signIn();
  final synchronizedForm = await _waitForServerFormWithInspectionRemarks(
    tester,
    verifier,
    offlineRemarks,
  );
  expect(synchronizedForm.status, 'Draft');
  expect(synchronizedForm.assetCategory, 'fire-extinguisher');
  expect(synchronizedForm.department, 'CCMS');
  expect(synchronizedForm.inspections, hasLength(1));
  final synchronizedInspection = synchronizedForm.inspections.single;
  expect(synchronizedInspection.assetCode, _offlineAssetCode);
  expect(synchronizedInspection.scheduleId, _offlineScheduleId);
  expect(synchronizedInspection.inspectorUserId, verifier.userId);
  expect(synchronizedInspection.remarks, offlineRemarks);
  final synchronizedSchedule = (await verifier.listSchedules(
    _offlineAssetId,
  )).singleWhere((schedule) => schedule.id == _offlineScheduleId);
  expect(synchronizedSchedule.status, 'Completed');
  await _waitForNoSyncBanner(tester);
  debugPrint('SMOKE_RESULT offline_draft_persistence_after_restart=PASS');
  debugPrint('SMOKE_RESULT reconnection_synchronization=PASS');
  debugPrint(
    'SMOKE_SERVER_STATE after_restart_sync form_status=${synchronizedForm.status} '
    'inspection_count=${synchronizedForm.inspections.length} '
    'asset=$_offlineAssetCode schedule_status=${synchronizedSchedule.status} '
    'remarks=VERIFIED pending_sync_banner=ABSENT',
  );

  setStage('reconnect-form-reopen');
  await _openAssetFromDashboard(tester, _offlineAssetCode);
  final resumeAfterSync = find.byKey(const Key('resume-pm'));
  await _scrollUntilVisible(
    tester,
    resumeAfterSync,
    description: 'Resume Inspection after the offline Draft synced',
  );
  expect(find.byKey(const Key('start-pm')), findsNothing);
  expect(tester.widget<FilledButton>(resumeAfterSync).onPressed, isNotNull);
  await _tapAfterScrolling(
    tester,
    resumeAfterSync,
    'Resume Inspection after the offline Draft synced',
  );
  final reopenedRemarks = find.byWidgetPredicate(
    (widget) =>
        widget is TextField && widget.controller?.text == offlineRemarks,
  );
  await _scrollUntilVisible(
    tester,
    reopenedRemarks,
    description:
        'synchronized offline inspection row after reopening the Draft',
  );
  expect(
    tester.widget<TextField>(reopenedRemarks).controller?.text,
    offlineRemarks,
  );
  debugPrint('SMOKE_RESULT offline_form_reopened_after_sync=PASS');
  debugPrint('SMOKE_RESULT visible_layout_overflow=PASS');
  debugPrint('SMOKE_RESULT live_error_or_conflict_recovery=NOT_EXECUTED');
}

Future<void> _waitForBackendReachability(
  WidgetTester tester, {
  required bool reachable,
  required Duration timeout,
}) async {
  final stopwatch = Stopwatch()..start();
  http.Response? lastResponse;
  while (stopwatch.elapsed < timeout) {
    lastResponse = await tester.runAsync<http.Response?>(() async {
      try {
        return await http
            .get(Uri.parse(_apiBaseUrl).resolve('health/ready'))
            .timeout(const Duration(seconds: 3));
      } catch (_) {
        return null;
      }
    });
    if (lastResponse?.statusCode == 200) {
      if (reachable) {
        debugPrint('SMOKE_API request=GET /health/ready status=200');
        return;
      }
    }

    if (!reachable && lastResponse == null) {
      debugPrint('SMOKE_API request=GET /health/ready status=UNREACHABLE');
      return;
    }
    await tester.runAsync<void>(
      () => Future<void>.delayed(const Duration(milliseconds: 300)),
    );
  }

  if (reachable) {
    fail(
      'The synthetic backend did not return HTTP 200 after reconnection; '
      'last status=${lastResponse?.statusCode}.',
    );
  }
  fail(
    'The emulator still reached the synthetic backend after the offline '
    'transition; last status=${lastResponse?.statusCode}.',
  );
}

Future<PreventiveMaintenanceForm> _waitForServerFormWithInspectionRemarks(
  WidgetTester tester,
  _SmokeServerVerifier verifier,
  String expectedRemarks,
) async {
  final deadline = DateTime.now().add(const Duration(seconds: 60));
  List<PreventiveMaintenanceForm> lastMatches = const [];
  while (DateTime.now().isBefore(deadline)) {
    final forms = await verifier.listForms();
    lastMatches = forms
        .where(
          (form) =>
              form.inspections.any((row) => row.remarks == expectedRemarks),
        )
        .toList(growable: false);
    if (lastMatches.length == 1) return lastMatches.single;
    if (lastMatches.length > 1) {
      fail(
        'More than one server form contains the isolated offline fixture remarks.',
      );
    }
    await tester.pump(const Duration(milliseconds: 500));
  }
  fail(
    'The offline Draft was not restored to the server after process restart; '
    'matching form count=${lastMatches.length}.',
  );
}

List<String> _visibleTextSnapshot(WidgetTester tester) {
  return tester
      .widgetList<Text>(find.byType(Text))
      .map(
        (widget) =>
            (widget.data ?? widget.textSpan?.toPlainText() ?? '').trim(),
      )
      .where((value) => value.isNotEmpty)
      .toList(growable: false);
}

typedef _SmokeTestBody =
    Future<void> Function(
      WidgetTester tester,
      void Function(String stage) setStage,
    );

Future<void> Function(WidgetTester tester) _captureSmokeFailures(
  String scenario,
  _SmokeTestBody body,
) {
  return (tester) async {
    var stage = 'integration-start';
    void setStage(String nextStage) {
      stage = nextStage;
      debugPrint('SMOKE_STAGE=$stage');
    }

    debugPrint('SMOKE_SCENARIO_START=$scenario');
    try {
      await body(tester, setStage);
      debugPrint('SMOKE_SCENARIO_RESULT=$scenario:PASS');
    } catch (error, stackTrace) {
      debugPrint('SMOKE_SCENARIO_RESULT=$scenario:FAIL');
      debugPrint(
        'SMOKE_FAILURE stage=$stage scenario=$scenario error=${error.toString()}',
      );
      debugPrint('SMOKE_CAPTURE_SCREEN=$scenario-$stage');
      debugPrint(
        'SMOKE_DIAGNOSTIC visible_text=' +
            _visibleTextSnapshot(tester).join(' | '),
      );
      debugPrint('SMOKE_STACK_TRACE_BEGIN scenario=$scenario');
      debugPrint(stackTrace.toString());
      debugPrint('SMOKE_STACK_TRACE_END scenario=$scenario');
      debugPrint('SMOKE_WIDGET_DIAGNOSTICS_BEGIN stage=$stage');
      debugDumpApp();
      debugDumpRenderTree();
      debugPrint('SMOKE_WIDGET_DIAGNOSTICS_END stage=$stage');
      await Future<void>.delayed(const Duration(seconds: 6));
      Error.throwWithStackTrace(error, stackTrace);
    }
  };
}

enum _ScrollDirection { up, down }

Future<void> _scrollUntilVisible(
  WidgetTester tester,
  Finder target, {
  required String description,
  _ScrollDirection direction = _ScrollDirection.down,
  Finder? scrollable,
}) => VisibleScrollInteraction.scrollUntilVisible(
  tester,
  target,
  description: description,
  scrollable: scrollable,
  towardEnd: direction == _ScrollDirection.down,
);

Future<void> _tapAfterScrolling(
  WidgetTester tester,
  Finder target,
  String description, {
  _ScrollDirection direction = _ScrollDirection.down,
  Finder? scrollable,
}) => VisibleScrollInteraction.tapAfterScrolling(
  tester,
  target,
  description,
  scrollable: scrollable,
  towardEnd: direction == _ScrollDirection.down,
);

Future<void> _tapVisibleOnScreen(
  WidgetTester tester,
  Finder target,
  String description,
) => VisibleScrollInteraction.tapVisibleOnScreen(tester, target, description);

void _verifyVisibleOnScreen(
  WidgetTester tester,
  Finder target,
  String description,
) => VisibleScrollInteraction.verifyVisible(tester, target, description);

void _verifyHitTestPosition(
  WidgetTester tester,
  Finder target,
  String description,
) => VisibleScrollInteraction.verifyTapReady(tester, target, description);

Future<PreventiveMaintenanceForm> _waitForServerInspectionRemarks(
  WidgetTester tester,
  _SmokeServerVerifier verifier,
  String formId,
  String inspectionId,
  String expectedRemarks,
) async {
  final deadline = DateTime.now().add(const Duration(seconds: 45));
  PreventiveMaintenanceForm? lastForm;
  while (DateTime.now().isBefore(deadline)) {
    lastForm = await verifier.getForm(formId);
    final inspection = lastForm.inspections
        .where((row) => row.id == inspectionId)
        .firstOrNull;
    if (inspection?.remarks == expectedRemarks) return lastForm;
    await tester.pump(const Duration(milliseconds: 500));
  }
  fail(
    'Server form $formId did not contain the expected synchronized inspection '
    'remarks; last form status was ${lastForm?.status}.',
  );
}

Future<void> _waitForNoSyncBanner(WidgetTester tester) async {
  final pendingTitles = <Finder>[
    find.text('Offline mode'),
    find.text('Changes waiting to sync'),
    find.text('Syncing saved changes'),
    find.text('Sync needs review'),
  ];
  final deadline = DateTime.now().add(const Duration(seconds: 45));
  while (DateTime.now().isBefore(deadline)) {
    await tester.pump(const Duration(milliseconds: 500));
    if (pendingTitles.every((finder) => finder.evaluate().isEmpty)) return;
  }
  fail('The offline sync banner did not clear after the server update.');
}

Future<void> _openAssetFromDashboard(
  WidgetTester tester,
  String assetCode,
) async {
  final searchAction = find.byKey(const Key('search-assets'));
  await _tapAfterScrolling(tester, searchAction, 'Open asset search');
  final searchInput = find.byKey(const Key('asset-search-input'));
  await _waitFor(tester, searchInput, 'asset search input');
  _verifyHitTestPosition(tester, searchInput, 'Asset search input');
  await tester.enterText(searchInput, assetCode);
  final result = find.byKey(ValueKey('asset-search-result-$assetCode'));
  await _waitFor(
    tester,
    result,
    'asset search result for $assetCode',
    timeout: const Duration(seconds: 45),
  );
  await _tapAfterScrolling(tester, result, 'Asset result $assetCode');
  try {
    await _waitFor(
      tester,
      find.text('Asset lookup'),
      'Asset lookup/details route after selecting $assetCode',
    );
    final assetCodeWidget = find.byKey(const Key('asset-code'));
    await _waitFor(tester, assetCodeWidget, 'asset details for $assetCode');
    expect(find.text(assetCode), findsOneWidget);
    _verifyVisibleOnScreen(
      tester,
      find.text('Asset lookup'),
      'Asset lookup title',
    );
  } catch (error) {
    _logAssetDetailsFailure(tester, error);
    rethrow;
  }
  debugPrint(
    'SMOKE_SCREEN asset_details=' + _visibleTextSnapshot(tester).join(' | '),
  );
  debugPrint('SMOKE_RESULT asset_lookup_and_details=PASS asset=$assetCode');
}

class _SmokeServerVerifier {
  _SmokeServerVerifier()
    : _client = ApiClient(baseUrl: Uri.parse(_apiBaseUrl)) {
    _client.configureSession(
      accessTokenProvider: () => _accessToken,
      terminalAuthFailureHandler: () async {},
    );
  }

  final ApiClient _client;
  late final AuthRepository _authRepository = AuthRepository(_client);
  late final ApiPreventiveMaintenanceRepository _pmRepository =
      ApiPreventiveMaintenanceRepository(_client);
  String? _accessToken;
  String? _userId;

  String get userId =>
      _userId ?? (throw StateError('Verifier is not signed in.'));

  Future<void> signIn() async {
    final result = await _authRepository.login(
      _syntheticInspectorEmail,
      _smokePassword,
    );
    _accessToken = result.accessToken;
    _userId = result.user.id;
  }

  Future<List<AssetMaintenanceHistoryRecord>> getHistory(String assetId) async {
    final response = await http.get(
      Uri.parse(_apiBaseUrl).resolve('api/v1/inspections/history/$assetId'),
      headers: <String, String>{
        'Accept': 'application/json',
        'Authorization': 'Bearer ${_accessToken!}',
      },
    );
    debugPrint(
      'SMOKE_API request=GET /api/v1/inspections/history/$assetId '
      'status=${response.statusCode}',
    );
    expect(response.statusCode, 200);
    final decoded = jsonDecode(response.body);
    if (decoded is! List) {
      throw const FormatException('Invalid maintenance-history API response.');
    }
    return decoded
        .map((value) {
          if (value is! Map) {
            throw const FormatException('Invalid maintenance-history record.');
          }
          return AssetMaintenanceHistoryRecord.fromJson(
            value.cast<String, dynamic>(),
          );
        })
        .toList(growable: false);
  }

  Future<List<ScheduleOption>> listSchedules(String assetId) =>
      _pmRepository.listSchedules(assetId: assetId);

  Future<List<PreventiveMaintenanceForm>> listForms() =>
      _pmRepository.listForms();

  Future<List<PreventiveMaintenanceForm>> listDraftsForInspectionFixture({
    required String assetCategory,
    required String department,
    required String pmCycle,
  }) async {
    final forms = await _pmRepository.listForms();
    return forms
        .where(
          (form) =>
              form.isDraft &&
              form.assetCategory == assetCategory &&
              form.department == department &&
              form.pmCycle == pmCycle,
        )
        .toList(growable: false);
  }

  Future<PreventiveMaintenanceForm> findFormWithInspectionRemarks(
    String remarks,
  ) async {
    final forms = await _pmRepository.listForms();
    final matches = forms
        .where((form) => form.inspections.any((row) => row.remarks == remarks))
        .toList(growable: false);
    expect(matches, hasLength(1));
    return matches.single;
  }

  Future<PreventiveMaintenanceForm> getForm(String formId) =>
      _pmRepository.getForm(formId);
}

void _logAssetDetailsFailure(WidgetTester tester, Object error) {
  debugPrint('SMOKE_FAILURE stage=asset-details');
  debugPrint(
    'SMOKE_DIAGNOSTIC visible_text=' + _visibleTextSnapshot(tester).join(' | '),
  );
  debugPrint(
    'SMOKE_DIAGNOSTIC widget_counts'
            ' asset_lookup_title=' +
        find.text('Asset lookup').evaluate().length.toString() +
        ' loading=' +
        find.text('Finding asset...').evaluate().length.toString() +
        ' asset_code=' +
        find.byKey(const Key('asset-code')).evaluate().length.toString() +
        ' invalid_qr=' +
        find.text('Invalid QR').evaluate().length.toString() +
        ' not_found=' +
        find.text('Asset not found').evaluate().length.toString() +
        ' lookup_error=' +
        find.text('Unable to look up asset').evaluate().length.toString() +
        ' search_page=' +
        find.text('Search Assets').evaluate().length.toString(),
  );
  debugPrint(
    'SMOKE_DIAGNOSTIC wait_error=' + error.toString().replaceAll('\n', ' '),
  );
  debugPrint('SMOKE_CAPTURE_SCREEN=asset-details');
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
