import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';
import 'package:http/http.dart' as http;
import 'package:mobile/api/api_client.dart';
import 'package:mobile/auth/auth_repository.dart';
import 'package:mobile/features/maintenance_history/asset_maintenance_history_models.dart';
import 'package:mobile/features/preventive_maintenance/offline_inspection_repository.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_models.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_repository.dart';
import 'package:mobile/main.dart' as app;

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

  testWidgets(
    'synthetic authenticated field-work compatibility smoke',
    _captureSmokeFailures((tester, setStage) async {
    setStage('startup-login');
    if (_smokePassword.isEmpty) {
      fail('The generated synthetic development password was not provided.');
    }
    if (_apiBaseUrl.isEmpty) {
      fail('The synthetic API URL was not provided to the integration test.');
    }
    if (_smokePhase != 'prepare' && _smokePhase != 'restart') {
      fail('Unknown synthetic smoke phase "$_smokePhase".');
    }

    debugPrint('SMOKE_CONTEXT android_api=$_apiLevel');
    debugPrint(
      'SMOKE_CONTEXT fixture_scope=isolated_ephemeral_database api=$_apiLevel',
    );
    if (_smokePhase == 'restart') {
      await _runAfterProcessRestart(tester, setStage);
      return;
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
    debugPrint('SMOKE_RESULT authenticated_navigation=PASS');
    debugPrint('SMOKE_RESULT dashboard_responsive_layout=PASS');

    setStage('dashboard-search');
    final searchAction = find.byKey(const Key('search-assets'));
    final dashboardScrollable = find.descendant(
      of: find.byType(RefreshIndicator),
      matching: find.byType(Scrollable),
    );
    final dashboardText = _visibleTextSnapshot(tester).join(' | ');
    debugPrint(
      'SMOKE_DIAGNOSTIC dashboard_search_key_count=${searchAction.evaluate().length} '
      'dashboard_scrollable_count=${dashboardScrollable.evaluate().length} '
      'dashboard_text=$dashboardText',
    );
    try {
      await tester.scrollUntilVisible(
        searchAction,
        240,
        scrollable: dashboardScrollable.first,
        maxScrolls: 12,
      );
      debugPrint('SMOKE_RESULT dashboard_search_action_visible=PASS');
    } catch (error) {
      debugPrint('SMOKE_FAILURE stage=dashboard-search');
      debugPrint(
        'SMOKE_DIAGNOSTIC dashboard_visible_text=' +
            _visibleTextSnapshot(tester).join(' | '),
      );
      debugPrint(
        'SMOKE_DIAGNOSTIC dashboard_search_key_count=' +
            searchAction.evaluate().length.toString() +
            ' scrollables=' +
            dashboardScrollable.evaluate().length.toString() +
            ' error=' +
            error.toString().replaceAll('\n', ' '),
      );
      debugPrint('SMOKE_CAPTURE_SCREEN=dashboard-search');
      rethrow;
    }
    setStage('asset-search-result');
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
    final assetSearchResult = find.byKey(
      ValueKey('asset-search-result-' + _syntheticAssetCode),
    );
    try {
      await _waitFor(
        tester,
        assetSearchResult,
        'synthetic asset search result card',
        timeout: const Duration(seconds: 45),
      );
      await _scrollUntilVisible(tester, assetSearchResult);
    } catch (error) {
      debugPrint('SMOKE_FAILURE stage=asset-search-result');
      debugPrint(
        'SMOKE_DIAGNOSTIC asset_search_visible_text=' +
            _visibleTextSnapshot(tester).join(' | '),
      );
      debugPrint(
        'SMOKE_DIAGNOSTIC result_card_key_count=' +
            assetSearchResult.evaluate().length.toString() +
            ' search_page_count=' +
            find.text('Search Assets').evaluate().length.toString() +
            ' search_input_count=' +
            find.byKey(const Key('asset-search-input')).evaluate().length.toString() +
            ' wait_error=' +
            error.toString().replaceAll('\n', ' '),
      );
      debugPrint('SMOKE_CAPTURE_SCREEN=asset-search-result');
      rethrow;
    }
    setStage('asset-details');
    try {
      await tester.tap(assetSearchResult);
      await _waitFor(
        tester,
        find.text('Asset lookup'),
        'asset lookup page after selecting a search result',
        timeout: const Duration(seconds: 15),
      );
      await _waitFor(
        tester,
        find.byKey(const Key('asset-code')),
        'asset details',
      );
      debugPrint(
        'SMOKE_SCREEN asset_details=' +
            _visibleTextSnapshot(tester).join(' | '),
      );
    } catch (error) {
      _logAssetDetailsFailure(tester, error);
      rethrow;
    }
    expect(find.text(_syntheticAssetCode), findsOneWidget);
    _checkForFlutterRenderingErrors(tester, 'asset details');
    debugPrint('SMOKE_RESULT asset_lookup_and_details=PASS');

    setStage('asset-history');
    final serverVerifier = _SmokeServerVerifier();
    await serverVerifier.signIn();
    debugPrint('SMOKE_TEST_USER_ID=' + serverVerifier.userId);
    final apiHistory = await serverVerifier.getHistory(_syntheticAssetId);
    final expectedApiHistory = apiHistory.where(
      (record) => record.id == _expectedHistoryInspectionId,
    );
    expect(expectedApiHistory, hasLength(1));
    expect(
      expectedApiHistory.single.remarks,
      'Emergency light passed the simulated power interruption test.',
    );
    debugPrint(
      'SMOKE_RESULT maintenance_history_api=PASS '
      'status=200 record=$_expectedHistoryInspectionId',
    );
    final historyAction = find.byKey(const Key('view-asset-history'));
    await _scrollUntilVisible(tester, historyAction);
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
    const expectedHistoryRemark =
        'Emergency light passed the simulated power interruption test.';
    await _waitFor(
      tester,
      find.text(expectedHistoryRemark),
      'expected acknowledged history record for $_syntheticAssetCode',
    );
    expect(
      find.byKey(
        const ValueKey('asset-history-record-$_expectedHistoryInspectionId'),
      ),
      findsOneWidget,
    );
    expect(find.text(expectedHistoryRemark), findsOneWidget);
    _checkForFlutterRenderingErrors(tester, 'maintenance history');
    debugPrint('SMOKE_RESULT maintenance_history_display=PASS');

    await tester.pageBack();
    await _waitFor(tester, find.text('Asset found'), 'return to asset details');
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
    setStage('dashboard-pm-schedule');
    await _openAssetFromDashboard(tester, _inspectionAssetCode);
    final scheduleSummary = find.textContaining('PM cycle:');
    await _waitFor(
      tester,
      scheduleSummary,
      'preventive-maintenance schedule summary',
      timeout: const Duration(seconds: 45),
    );
    _checkForFlutterRenderingErrors(tester, 'PM schedule');
    debugPrint('SMOKE_RESULT preventive_maintenance_schedule=PASS');

    setStage('pm-fixture-check');
    final inspectionSchedules = await serverVerifier.listSchedules(
      _inspectionAssetId,
    );
    final inspectionSchedule = inspectionSchedules.singleWhere(
      (schedule) => schedule.id == _inspectionScheduleId,
    );
    expect(inspectionSchedule.asset?.assetCode, _inspectionAssetCode);
    expect(inspectionSchedule.status, 'Due');
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
        pmCycle: '2026-06',
      ),
      isEmpty,
    );
    debugPrint(
      'SMOKE_FIXTURE inspection_asset=$_inspectionAssetCode '
      'schedule=$_inspectionScheduleId status=${inspectionSchedule.status} '
      'assigned_to_smoke_user=PASS',
    );

    setStage('pm-form-open');
    final startInspection = find.byKey(const Key('start-pm'));
    await _waitFor(
      tester,
      startInspection,
      'Start Inspection action for $_inspectionAssetCode',
      timeout: const Duration(seconds: 45),
    );
    expect(find.byKey(const Key('resume-pm')), findsNothing);
    await _scrollUntilVisible(tester, startInspection);
    expect(
      tester.widget<FilledButton>(startInspection).onPressed,
      isNotNull,
      reason: 'The fixture schedule must have an enabled Start action.',
    );
    await tester.tap(startInspection);
    await _waitFor(
      tester,
      find.byKey(const Key('new-inspection-date')),
      'inspection-entry form',
      timeout: const Duration(seconds: 60),
    );
    final today = DateTime.now().toUtc().toIso8601String().substring(0, 10);
    await tester.enterText(find.byKey(const Key('new-inspection-date')), today);
    final onlineRemarks = 'Android compatibility smoke online save API $_apiLevel';
    await tester.enterText(
      find.byKey(const Key('new-inspection-remarks')),
      onlineRemarks,
    );
    final addInspection = find.byKey(const Key('add-inspection-button'));
    await _scrollUntilVisible(tester, addInspection);
    _checkForFlutterRenderingErrors(tester, 'inspection-entry form');
    setStage('pm-form-save');
    await tester.tap(addInspection);
    await _waitFor(
      tester,
      find.text('Inspection Recorded'),
      'saved synthetic inspection-entry confirmation',
      timeout: const Duration(seconds: 60),
    );
    _checkForFlutterRenderingErrors(tester, 'saved inspection entry');
    await tester.tap(find.byKey(const Key('sheet-view-batch')));
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
    await _waitFor(tester, find.text('Asset found'), 'return to PM asset details');
    final resumeInspection = find.byKey(const Key('resume-pm'));
    await _waitFor(
      tester,
      resumeInspection,
      'Resume Inspection action for the saved Draft',
      timeout: const Duration(seconds: 45),
    );
    await _scrollUntilVisible(tester, resumeInspection);
    await tester.tap(resumeInspection);
    final inspectionRemarks = find.byKey(
      Key('inspection-remarks-${onlineInspection.id}'),
    );
    await _waitFor(
      tester,
      inspectionRemarks,
      'saved inspection row after reopening the Draft',
    );
    expect(
      tester.widget<TextField>(inspectionRemarks).controller?.text,
      onlineRemarks,
    );
    expect(tester.widget<TextField>(inspectionRemarks).enabled, isTrue);
    debugPrint('SMOKE_RESULT preventive_maintenance_inspection_reopen=PASS');

    setStage('pm-form-edit');
    final onlineEditedRemarks =
        'Android compatibility smoke online edit API $_apiLevel';
    await tester.enterText(inspectionRemarks, onlineEditedRemarks);
    final saveOnlineInspection = find.byKey(
      Key('save-inspection-${onlineInspection.id}'),
    );
    await _scrollUntilVisible(tester, saveOnlineInspection);
    await tester.tap(saveOnlineInspection);
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
      find.text('Asset found'),
      'return to PM asset details after edit',
    );
    await _scrollUntilVisible(tester, resumeInspection);
    await tester.tap(resumeInspection);
    final editedInspectionRemarks = find.byKey(
      Key('inspection-remarks-${onlineInspection.id}'),
    );
    await _waitFor(
      tester,
      editedInspectionRemarks,
      'edited online inspection row after reopening the Draft',
    );
    expect(
      tester.widget<TextField>(editedInspectionRemarks).controller?.text,
      onlineEditedRemarks,
    );
    debugPrint('SMOKE_RESULT preventive_maintenance_inspection_edit_reopen=PASS');

    setStage('offline-fixture-check');
    await tester.pageBack();
    await _waitFor(
      tester,
      find.text('Asset found'),
      'return to asset details after online form edit',
    );
    await tester.pageBack();
    await _waitFor(
      tester,
      find.byKey(const Key('asset-search-input')),
      'return to asset search before opening offline fixture',
    );
    await tester.pageBack();
    await _waitFor(
      tester,
      find.text('Welcome, Maintenance Inspector'),
      'return to dashboard before opening offline fixture',
    );
    await _openAssetFromDashboard(tester, _offlineAssetCode);
    expect(find.text(_offlineAssetCode), findsOneWidget);
    final offlineSchedules = await serverVerifier.listSchedules(_offlineAssetId);
    final offlineSchedule = offlineSchedules.singleWhere(
      (schedule) => schedule.id == _offlineScheduleId,
    );
    expect(offlineSchedule.asset?.assetCode, _offlineAssetCode);
    expect(offlineSchedule.status, 'Due');
    expect(offlineSchedule.isEligibleForFieldWork, isTrue);
    expect(offlineSchedule.assignedToUserId, serverVerifier.userId);
    expect(
      await serverVerifier.listDraftsForInspectionFixture(
        assetCategory: 'fire-extinguisher',
        department: 'CCMS',
        pmCycle: '2026-11',
      ),
      isEmpty,
    );
    final offlineStart = find.byKey(const Key('start-pm'));
    await _waitFor(
      tester,
      offlineStart,
      'eligible offline-fixture Start action for $_offlineAssetCode',
      timeout: const Duration(seconds: 45),
    );
    await _scrollUntilVisible(tester, offlineStart);
    expect(tester.widget<FilledButton>(offlineStart).onPressed, isNotNull);
    debugPrint(
      'SMOKE_FIXTURE offline_asset=$_offlineAssetCode schedule=$_offlineScheduleId '
      'status=${offlineSchedule.status} assigned_to_smoke_user=PASS',
    );

    setStage('offline-connectivity-check');
    debugPrint('SMOKE_NETWORK_REQUEST=offline');
    await _waitForBackendReachability(
      tester,
      reachable: false,
      timeout: const Duration(seconds: 30),
    );
    debugPrint('SMOKE_RESULT backend_disconnected=PASS');

    setStage('offline-draft-create');
    await tester.tap(offlineStart);
    await _waitFor(
      tester,
      find.byKey(const Key('new-inspection-date')),
      'new offline inspection form for $_offlineAssetCode',
      timeout: const Duration(seconds: 45),
    );
    final offlineDate = DateTime.now().toUtc().toIso8601String().substring(0, 10);
    final offlineRemarks =
        'Android compatibility smoke offline draft API $_apiLevel';
    await tester.enterText(
      find.byKey(const Key('new-inspection-date')),
      offlineDate,
    );
    await tester.enterText(
      find.byKey(const Key('new-inspection-remarks')),
      offlineRemarks,
    );
    final addOfflineInspection = find.byKey(
      const Key('add-inspection-button'),
    );
    await _scrollUntilVisible(tester, addOfflineInspection);
    await tester.tap(addOfflineInspection);
    await _waitFor(
      tester,
      find.text('Saved on this device'),
      'offline inspection saved locally',
      timeout: const Duration(seconds: 60),
    );
    await tester.tap(find.byKey(const Key('sheet-view-batch')));
    await _waitFor(
      tester,
      find.text('Offline draft saved securely'),
      'offline Draft status after creating and saving the inspection',
    );
    await _waitFor(
      tester,
      find.textContaining('2 change(s) for this draft are waiting to sync.'),
      'offline create-form and add-inspection writes queued locally',
    );
    final offlineRemarksField = find.byWidgetPredicate(
      (widget) =>
          widget is TextField && widget.controller?.text == offlineRemarks,
    );
    await _waitFor(
      tester,
      offlineRemarksField,
      'offline inspection row restored from the local Draft',
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
    }),
  );
}

Future<void> _runAfterProcessRestart(
  WidgetTester tester,
  void Function(String stage) setStage,
) async {
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
  final snapshot =
      (jsonDecode(storedSnapshot!) as Map).cast<String, dynamic>();
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
        (form) =>
            form.inspections.any((row) => row.remarks == offlineRemarks),
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
  await tester.enterText(
    find.byType(TextFormField).at(0),
    _syntheticInspectorEmail,
  );
  await tester.enterText(find.byType(TextFormField).at(1), _smokePassword);
  await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
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
  final synchronizedSchedule = (await verifier.listSchedules(_offlineAssetId))
      .singleWhere((schedule) => schedule.id == _offlineScheduleId);
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
  await _waitFor(
    tester,
    resumeAfterSync,
    'Resume Inspection after the offline Draft synced',
    timeout: const Duration(seconds: 45),
  );
  await _scrollUntilVisible(tester, resumeAfterSync);
  await tester.tap(resumeAfterSync);
  final reopenedRemarks = find.byWidgetPredicate(
    (widget) =>
        widget is TextField && widget.controller?.text == offlineRemarks,
  );
  await _waitFor(
    tester,
    reopenedRemarks,
    'synchronized offline inspection row after reopening the Draft',
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
        .where((form) => form.inspections.any((row) => row.remarks == expectedRemarks))
        .toList(growable: false);
    if (lastMatches.length == 1) return lastMatches.single;
    if (lastMatches.length > 1) {
      fail('More than one server form contains the isolated offline fixture remarks.');
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
      .map((widget) => (widget.data ?? widget.textSpan?.toPlainText() ?? '').trim())
      .where((value) => value.isNotEmpty)
      .toList(growable: false);
}

typedef _SmokeTestBody = Future<void> Function(
  WidgetTester tester,
  void Function(String stage) setStage,
);

Future<void> Function(WidgetTester tester) _captureSmokeFailures(
  _SmokeTestBody body,
) {
  return (tester) async {
    var stage = 'integration-start';
    void setStage(String nextStage) {
      stage = nextStage;
      debugPrint('SMOKE_STAGE=$stage');
    }

    try {
      await body(tester, setStage);
    } catch (error, stackTrace) {
      debugPrint('SMOKE_FAILURE stage=$stage error=${error.toString()}');
      debugPrint('SMOKE_CAPTURE_SCREEN=$stage');
      debugPrint(
        'SMOKE_DIAGNOSTIC visible_text=' +
            _visibleTextSnapshot(tester).join(' | '),
      );
      debugPrint('SMOKE_WIDGET_DIAGNOSTICS_BEGIN stage=$stage');
      debugDumpApp();
      debugDumpRenderTree();
      debugPrint('SMOKE_WIDGET_DIAGNOSTICS_END stage=$stage');
      await Future<void>.delayed(const Duration(seconds: 6));
      Error.throwWithStackTrace(error, stackTrace);
    }
  };
}

Future<void> _scrollUntilVisible(WidgetTester tester, Finder target) async {
  final verticalScrollables = find
      .byWidgetPredicate(
        (widget) =>
            widget is Scrollable && widget.axisDirection == AxisDirection.down,
      )
      .evaluate();
  if (verticalScrollables.isEmpty) {
    fail('No vertical scrollable was built for the current screen.');
  }
  await tester.scrollUntilVisible(
    target,
    240,
    scrollable: find.byWidget(verticalScrollables.first.widget),
    maxScrolls: 18,
  );
}

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
  final dashboardScrollable = find.descendant(
    of: find.byType(RefreshIndicator),
    matching: find.byType(Scrollable),
  );
  final searchAction = find.byKey(const Key('search-assets'));
  await tester.scrollUntilVisible(
    searchAction,
    240,
    scrollable: dashboardScrollable.first,
    maxScrolls: 12,
  );
  await tester.tap(searchAction);
  final searchInput = find.byKey(const Key('asset-search-input'));
  await _waitFor(tester, searchInput, 'asset search after reconnection');
  await tester.enterText(searchInput, assetCode);
  final result = find.byKey(ValueKey('asset-search-result-$assetCode'));
  await _waitFor(
    tester,
    result,
    'reconnected asset search result for $assetCode',
    timeout: const Duration(seconds: 45),
  );
  await _scrollUntilVisible(tester, result);
  await tester.tap(result);
  await _waitFor(
    tester,
    find.byKey(const Key('asset-code')),
    'asset details after reconnection',
  );
}

class _SmokeServerVerifier {
  _SmokeServerVerifier() : _client = ApiClient(baseUrl: Uri.parse(_apiBaseUrl)) {
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

  Future<List<AssetMaintenanceHistoryRecord>> getHistory(
    String assetId,
  ) async {
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
    return decoded.map((value) {
      if (value is! Map) {
        throw const FormatException('Invalid maintenance-history record.');
      }
      return AssetMaintenanceHistoryRecord.fromJson(
        value.cast<String, dynamic>(),
      );
    }).toList(growable: false);
  }

  Future<List<ScheduleOption>> listSchedules(String assetId) =>
      _pmRepository.listSchedules(assetId: assetId);

  Future<List<PreventiveMaintenanceForm>> listForms() => _pmRepository.listForms();

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
    'SMOKE_DIAGNOSTIC visible_text=' +
        _visibleTextSnapshot(tester).join(' | '),
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
