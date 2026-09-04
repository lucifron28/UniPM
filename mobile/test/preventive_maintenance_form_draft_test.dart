import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;

import 'package:mobile/api/api_client.dart';
import 'package:mobile/api/api_exception.dart';
import 'package:mobile/auth/auth_models.dart';
import 'package:mobile/features/assets/asset_models.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_controller.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_models.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_page.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_repository.dart';
import 'package:mobile/features/preventive_maintenance/scanned_asset_pm_entry.dart';

const inspectorId = '11111111-1111-4111-8111-111111111111';
const otherUserId = '22222222-2222-4222-8222-222222222222';
const formId = '33333333-3333-4333-8333-333333333333';
const secondFormId = '44444444-4444-4444-8444-444444444444';
const firstScheduleId = '55555555-5555-4555-8555-555555555555';
const secondScheduleId = '66666666-6666-4666-8666-666666666666';
const firstInspectionId = '77777777-7777-4777-8777-777777777777';

AuthUser testUser({List<String> roles = const ['Inspector']}) => AuthUser(
  id: inspectorId,
  email: 'inspector@example.test',
  displayName: 'Synthetic Inspector',
  roles: roles,
);

void main() {
  testWidgets('registry shows draft metadata and row count', (tester) async {
    final repository = FakePreventiveMaintenanceRepository(
      forms: [
        testForm(id: formId, inspections: [testInspection()]),
      ],
    );

    await pumpPage(tester, repository);

    expect(find.text('Draft forms'), findsOneWidget);
    expect(find.textContaining('1 inspection row(s)'), findsOneWidget);
    expect(find.textContaining('fire-extinguisher'), findsOneWidget);
  });

  testWidgets('Inspector presentation defaults to forms created by that user', (
    tester,
  ) async {
    final repository = FakePreventiveMaintenanceRepository(
      forms: [
        testForm(id: formId, createdByUserId: inspectorId),
        testForm(id: secondFormId, createdByUserId: otherUserId),
      ],
    );

    await pumpPage(tester, repository);

    expect(find.byKey(Key('draft-form-$formId')), findsOneWidget);
    expect(find.byKey(Key('draft-form-$secondFormId')), findsNothing);
  });

  testWidgets('GSD presentation includes all returned Draft forms', (
    tester,
  ) async {
    final repository = FakePreventiveMaintenanceRepository(
      forms: [
        testForm(id: formId, createdByUserId: inspectorId),
        testForm(id: secondFormId, createdByUserId: otherUserId),
      ],
    );

    await pumpPage(tester, repository, user: testUser(roles: const ['GSD']));

    expect(find.byKey(Key('draft-form-$formId')), findsOneWidget);
    expect(find.byKey(Key('draft-form-$secondFormId')), findsOneWidget);
  });

  testWidgets('empty registry offers a bounded create state', (tester) async {
    final repository = FakePreventiveMaintenanceRepository();

    await pumpPage(tester, repository);

    expect(find.text('No draft forms yet.'), findsOneWidget);
    expect(find.text('Create draft'), findsOneWidget);
  });

  testWidgets('create draft sends all supported header fields', (tester) async {
    final repository = FakePreventiveMaintenanceRepository();

    await pumpPage(tester, repository);
    await tester.tap(find.text('New draft'));
    await tester.pumpAndSettle();
    await chooseDropdown(
      tester,
      const Key('form-asset-category'),
      'Fire extinguisher',
    );
    await chooseDropdown(tester, const Key('form-period-type'), 'Quarter');
    await tester.enterText(
      find.byKey(const Key('form-building')),
      'Main Building',
    );
    await tester.enterText(find.byKey(const Key('form-department')), 'GSD');
    await tester.enterText(find.byKey(const Key('form-year')), '2026');
    await tester.enterText(
      find.byKey(const Key('form-academic-year')),
      '2026-2027',
    );
    await scrollTo(tester, find.byKey(const Key('create-draft-button')));
    await tester.tap(find.byKey(const Key('create-draft-button')));
    await tester.pumpAndSettle();

    expect(repository.createdInput?.assetCategory, 'fire-extinguisher');
    expect(repository.createdInput?.building, 'Main Building');
    expect(repository.createdInput?.department, 'GSD');
    expect(repository.createdInput?.periodType, 'Quarter');
    expect(repository.createdInput?.year, 2026);
    expect(repository.createdInput?.academicYear, '2026-2027');
    expect(find.text('Draft form'), findsOneWidget);
  });

  testWidgets('reference-data failure offers retry without inventing options', (
    tester,
  ) async {
    final repository = FakePreventiveMaintenanceRepository(
      referenceFailures: 1,
    );

    await pumpPage(tester, repository);
    await tester.tap(find.text('New draft'));
    await tester.pumpAndSettle();

    expect(
      find.text('The mobile service is unavailable. Please try again.'),
      findsOneWidget,
    );
    await tester.tap(find.text('Retry'));
    await tester.pumpAndSettle();
    expect(find.text('Asset category'), findsOneWidget);
  });

  testWidgets('schedule loading and empty states wait for the request result', (
    tester,
  ) async {
    final schedules = Completer<List<ScheduleOption>>();
    final repository = FakePreventiveMaintenanceRepository(
      forms: [testForm(id: formId)],
      schedulesFuture: schedules.future,
    );

    await pumpPage(tester, repository);
    await tester.tap(find.byKey(Key('draft-form-$formId')));
    await tester.pump();
    await tester.pump();

    expect(find.byKey(const Key('schedules-loading')), findsOneWidget);
    expect(
      find.text('No compatible schedules are available for this Draft.'),
      findsNothing,
    );

    schedules.complete(const []);
    await tester.pumpAndSettle();

    expect(
      find.text('No compatible schedules are available for this Draft.'),
      findsOneWidget,
    );
  });

  testWidgets('schedule failure retains a retry state', (tester) async {
    final repository = FakePreventiveMaintenanceRepository(
      forms: [testForm(id: formId)],
      scheduleFailures: 1,
    );

    await pumpPage(tester, repository);
    await tester.tap(find.byKey(Key('draft-form-$formId')));
    await tester.pump();
    await tester.pump();

    expect(find.text('Schedules unavailable'), findsOneWidget);
    final retryButton = tester.widget<TextButton>(
      find.byKey(const Key('schedules-retry')),
    );
    retryButton.onPressed!();
    await tester.pumpAndSettle();
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('inspection-schedule')), findsOneWidget);
  });

  testWidgets('resuming a draft renders every existing row', (tester) async {
    final repository = FakePreventiveMaintenanceRepository(
      forms: [
        testForm(
          id: formId,
          inspections: [
            testInspection(),
            testInspection(id: secondScheduleId),
          ],
        ),
      ],
    );

    await pumpPage(tester, repository);
    await tester.tap(find.byKey(Key('draft-form-$formId')));
    await tester.pumpAndSettle();

    await scrollTo(tester, find.textContaining('Inspection rows (2)'));
    expect(find.textContaining('Inspection rows (2)'), findsOneWidget);
    expect(find.text('Schedule ID: $firstScheduleId'), findsOneWidget);
    await scrollTo(tester, find.text('Schedule ID: $secondScheduleId'));
    expect(find.text('Schedule ID: $secondScheduleId'), findsOneWidget);
  });

  testWidgets('adding a row persists the authenticated inspector ID', (
    tester,
  ) async {
    final repository = FakePreventiveMaintenanceRepository(
      forms: [testForm(id: formId)],
    );

    await pumpPage(tester, repository);
    await tester.tap(find.byKey(Key('draft-form-$formId')));
    await tester.pumpAndSettle();
    await scrollTo(tester, find.byKey(const Key('inspection-schedule')));
    await chooseDropdown(tester, const Key('inspection-schedule'), 'FE-001');
    await scrollTo(tester, find.byKey(const Key('add-inspection-button')));
    await tester.tap(find.byKey(const Key('add-inspection-button')));
    await tester.pumpAndSettle();

    expect(repository.addedInput?.scheduleId, firstScheduleId);
    expect(repository.addedInput?.inspectorUserId, inspectorId);
    expect(find.textContaining('Inspection rows (1)'), findsOneWidget);
  });

  test('duplicate schedules are blocked before another API write', () async {
    final repository = FakePreventiveMaintenanceRepository(
      forms: [
        testForm(id: formId, inspections: [testInspection()]),
      ],
    );
    final controller = PreventiveMaintenanceController(
      repository: repository,
      user: testUser(),
    );
    await controller.loadForms();
    controller.selectForm(controller.visibleDrafts.single);

    final added = await controller.addInspection(
      AddInspectionInput(
        scheduleId: firstScheduleId,
        inspectorUserId: inspectorId,
        dateInspected: DateTime(2026, 2, 10),
        isOperational: false,
        remarks: null,
        actionsRecommendations: null,
      ),
    );

    expect(added, isFalse);
    expect(repository.addCallCount, 0);
    expect(
      controller.errorMessage,
      'This schedule is already included in the draft.',
    );
    controller.dispose();
  });

  testWidgets('exact Draft inspection opens Resume PM in the existing editor', (
    tester,
  ) async {
    final repository = FakePreventiveMaintenanceRepository(
      forms: [
        testForm(
          id: formId,
          inspections: [
            testInspection(id: secondInspectionId),
            testInspection(),
          ],
        ),
      ],
      schedulesFuture: Future.value([testSchedule(firstScheduleId, 'FE-001')]),
    );

    await pumpScannedEntry(tester, repository);

    expect(find.byKey(const Key('resume-pm')), findsOneWidget);
    expect(find.byKey(const Key('start-pm')), findsNothing);
    await tester.tap(find.byKey(const Key('resume-pm')));
    await tester.pumpAndSettle();
    await scrollTo(tester, find.text('Resume inspection row'));

    expect(find.text('Draft form'), findsOneWidget);
    expect(find.text('Resume inspection row'), findsOneWidget);
    expect(find.text('Schedule ID: $firstScheduleId'), findsOneWidget);
    expect(repository.addCallCount, 0);
    expect(repository.createdInput, isNull);
    expect(repository.forms.single.inspections.map((row) => row.id), [
      secondInspectionId,
      firstInspectionId,
    ]);
  });

  testWidgets('compatible Draft opens with scanned schedule preselected', (
    tester,
  ) async {
    final repository = FakePreventiveMaintenanceRepository(
      forms: [testForm(id: formId)],
      schedulesFuture: Future.value([testSchedule(firstScheduleId, 'FE-001')]),
    );

    await pumpScannedEntry(tester, repository);

    expect(find.byKey(const Key('start-pm')), findsOneWidget);
    await tester.tap(find.byKey(const Key('start-pm')));
    await tester.pumpAndSettle();
    await scrollTo(tester, find.byKey(const Key('inspection-schedule')));

    final dropdown = tester.widget<DropdownButtonFormField<String>>(
      find.byKey(const Key('inspection-schedule')),
    );
    expect(dropdown.initialValue, firstScheduleId);
    expect(repository.requestedScheduleAssetIds.first, testAsset().id);
    expect(repository.createdInput, isNull);
    expect(repository.addCallCount, 0);
  });

  testWidgets('no compatible Draft creates a derived header before editing', (
    tester,
  ) async {
    final repository = FakePreventiveMaintenanceRepository(
      schedulesFuture: Future.value([testSchedule(firstScheduleId, 'FE-001')]),
    );

    await pumpScannedEntry(tester, repository);
    await tester.tap(find.byKey(const Key('start-pm')));
    await tester.pumpAndSettle();
    await scrollTo(tester, find.byKey(const Key('inspection-schedule')));

    expect(repository.createdInput?.assetCategory, 'fire-extinguisher');
    expect(repository.createdInput?.building, 'Main Building');
    expect(repository.createdInput?.department, 'GSD');
    expect(repository.createdInput?.periodType, 'Quarter');
    expect(repository.createdInput?.quarter, 'Q1');
    expect(repository.createdInput?.semester, isNull);
    expect(repository.createdInput?.year, 2026);
    expect(repository.createdInput?.academicYear, '2026-2027');
    expect(repository.addCallCount, 0);
    final dropdown = tester.widget<DropdownButtonFormField<String>>(
      find.byKey(const Key('inspection-schedule')),
    );
    expect(dropdown.initialValue, firstScheduleId);
  });

  test('multiple compatible Drafts require an explicit choice', () async {
    final repository = FakePreventiveMaintenanceRepository(
      forms: [
        testForm(id: formId),
        testForm(id: secondFormId),
      ],
    );
    final controller = PreventiveMaintenanceController(
      repository: repository,
      user: testUser(),
    );
    await controller.loadForms();

    final resolution = controller.resolveDraftFor(
      testAsset(),
      testSchedule(firstScheduleId, 'FE-001'),
    );

    expect(resolution.kind, PmDraftResolutionKind.choose);
    expect(resolution.forms.map((form) => form.id), [formId, secondFormId]);
    controller.dispose();
  });

  testWidgets('one eligible schedule is selected automatically', (
    tester,
  ) async {
    final repository = FakePreventiveMaintenanceRepository(
      schedulesFuture: Future.value([
        testSchedule(firstScheduleId, 'FE-001', status: 'Ongoing'),
      ]),
    );

    await pumpScannedEntry(tester, repository);

    expect(find.byKey(const Key('selected-pm-schedule')), findsOneWidget);
    expect(find.textContaining('Ongoing'), findsOneWidget);
    expect(find.byKey(const Key('start-pm')), findsOneWidget);
    expect(repository.requestedScheduleAssetIds, [testAsset().id]);
  });

  testWidgets('multiple eligible schedules require explicit selection', (
    tester,
  ) async {
    final repository = FakePreventiveMaintenanceRepository(
      schedulesFuture: Future.value([
        testSchedule(firstScheduleId, 'FE-001'),
        testSchedule(secondScheduleId, 'FE-001', status: 'Overdue'),
      ]),
    );

    await pumpScannedEntry(tester, repository);

    expect(find.byKey(const Key('pm-schedule-select')), findsOneWidget);
    expect(find.byKey(const Key('start-pm')), findsNothing);

    await tester.tap(find.byKey(const Key('pm-schedule-select')));
    await tester.pumpAndSettle();
    await tester.tap(find.textContaining('Overdue').last);
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('start-pm')), findsOneWidget);
  });

  testWidgets('completed and cancelled schedules are not eligible', (
    tester,
  ) async {
    final repository = FakePreventiveMaintenanceRepository(
      schedulesFuture: Future.value([
        testSchedule(firstScheduleId, 'FE-001', status: 'Completed'),
        testSchedule(secondScheduleId, 'FE-001', status: 'Cancelled'),
      ]),
    );

    await pumpScannedEntry(tester, repository);

    expect(find.byKey(const Key('pm-schedule-empty')), findsOneWidget);
    expect(find.byKey(const Key('start-pm')), findsNothing);
    expect(find.byKey(const Key('resume-pm')), findsNothing);
  });

  testWidgets('no schedules produces a bounded no-schedule state', (
    tester,
  ) async {
    final repository = FakePreventiveMaintenanceRepository(
      schedulesFuture: Future.value(const []),
    );

    await pumpScannedEntry(tester, repository);

    expect(find.byKey(const Key('pm-schedule-empty')), findsOneWidget);
    expect(
      find.text('No applicable PM schedules are available for this asset.'),
      findsOneWidget,
    );
  });

  testWidgets('schedule API failure can retry without rescanning', (
    tester,
  ) async {
    final repository = FakePreventiveMaintenanceRepository(
      scheduleFailures: 1,
      schedulesFuture: Future.value([testSchedule(firstScheduleId, 'FE-001')]),
    );

    await pumpScannedEntry(tester, repository);

    expect(find.byKey(const Key('pm-entry-error')), findsOneWidget);
    await tester.tap(find.text('Retry'));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('start-pm')), findsOneWidget);
    expect(repository.requestedScheduleAssetIds, [
      testAsset().id,
      testAsset().id,
    ]);
  });

  testWidgets('inactive and retired assets cannot start PM', (tester) async {
    for (final status in const ['Inactive', 'Retired']) {
      final repository = FakePreventiveMaintenanceRepository(
        schedulesFuture: Future.value([
          testSchedule(firstScheduleId, 'FE-001'),
        ]),
      );

      await pumpScannedEntry(
        tester,
        repository,
        asset: testAsset(status: status),
      );

      expect(find.byKey(const Key('pm-entry-blocked')), findsOneWidget);
      expect(find.textContaining(status), findsOneWidget);
      final button = tester.widget<FilledButton>(
        find.byKey(const Key('start-pm')),
      );
      expect(button.onPressed, isNull);

      await tester.pumpWidget(const SizedBox.shrink());
      await tester.pumpAndSettle();
    }
  });

  testWidgets('compatible Draft selection is required when ambiguous', (
    tester,
  ) async {
    final repository = FakePreventiveMaintenanceRepository(
      forms: [
        testForm(id: formId, fileNumber: 'PM-001'),
        testForm(id: secondFormId, fileNumber: 'PM-002'),
      ],
      schedulesFuture: Future.value([testSchedule(firstScheduleId, 'FE-001')]),
    );

    await pumpScannedEntry(tester, repository);

    expect(find.byKey(const Key('compatible-draft-select')), findsOneWidget);
    var button = tester.widget<FilledButton>(find.byKey(const Key('start-pm')));
    expect(button.onPressed, isNull);

    await tester.tap(find.byKey(const Key('compatible-draft-select')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('PM-002').last);
    await tester.pumpAndSettle();

    button = tester.widget<FilledButton>(find.byKey(const Key('start-pm')));
    expect(button.onPressed, isNotNull);
    expect(repository.createdInput, isNull);
    expect(repository.addCallCount, 0);
  });

  testWidgets('backing out of a new Draft editor does not create a row', (
    tester,
  ) async {
    final repository = FakePreventiveMaintenanceRepository(
      schedulesFuture: Future.value([testSchedule(firstScheduleId, 'FE-001')]),
    );

    await pumpScannedEntry(tester, repository);
    await tester.tap(find.byKey(const Key('start-pm')));
    await tester.pumpAndSettle();

    expect(repository.createdInput, isNotNull);
    expect(repository.addCallCount, 0);
    expect(find.byKey(const Key('inspection-schedule')), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(repository.addCallCount, 0);
  });

  testWidgets('inspection dates reject timestamps before an API write', (
    tester,
  ) async {
    final repository = FakePreventiveMaintenanceRepository(
      forms: [testForm(id: formId)],
    );

    await pumpPage(tester, repository);
    await tester.tap(find.byKey(Key('draft-form-$formId')));
    await tester.pumpAndSettle();
    await scrollTo(tester, find.byKey(const Key('inspection-schedule')));
    await chooseDropdown(tester, const Key('inspection-schedule'), 'FE-001');
    await tester.enterText(
      find.byKey(const Key('new-inspection-date')),
      '2026-02-10T10:00:00Z',
    );
    await scrollTo(tester, find.byKey(const Key('add-inspection-button')));
    await tester.tap(find.byKey(const Key('add-inspection-button')));
    await tester.pumpAndSettle();

    expect(repository.addCallCount, 0);
    expect(find.text('Enter a valid date.'), findsOneWidget);
  });

  testWidgets('submits a draft with multiple rows and locks the editor', (
    tester,
  ) async {
    final repository = FakePreventiveMaintenanceRepository(
      forms: [
        testForm(
          id: formId,
          inspections: [
            testInspection(),
            testInspection(id: secondInspectionId),
          ],
        ),
      ],
    );

    await pumpPage(tester, repository);
    await tester.tap(find.byKey(Key('draft-form-$formId')));
    await tester.pumpAndSettle();
    await scrollTo(tester, find.byKey(const Key('submit-form-button')));
    await tester.tap(find.byKey(const Key('submit-form-button')));
    await tester.pumpAndSettle();
    expect(find.text('Submit preventive-maintenance form?'), findsOneWidget);

    await tester.tap(find.byKey(const Key('confirm-submit-form')));
    await tester.pumpAndSettle();

    expect(repository.submittedFormId, formId);
    await tester.drag(find.byType(ListView).last, const Offset(0, 2000));
    await tester.pumpAndSettle();
    expect(find.text('Status: Submitted'), findsOneWidget);
    expect(find.text('PM-2026-0001'), findsOneWidget);
    expect(find.byKey(const Key('submit-form-button')), findsNothing);
    expect(find.text('Save row'), findsNothing);
    expect(find.text('Delete row'), findsNothing);
    expect(
      find.text('This form is no longer Draft and cannot be edited.'),
      findsOneWidget,
    );
  });

  testWidgets('submission confirmation can be cancelled without an API write', (
    tester,
  ) async {
    final repository = FakePreventiveMaintenanceRepository(
      forms: [
        testForm(id: formId, inspections: [testInspection()]),
      ],
    );

    await pumpPage(tester, repository);
    await tester.tap(find.byKey(Key('draft-form-$formId')));
    await tester.pumpAndSettle();
    await scrollTo(tester, find.byKey(const Key('submit-form-button')));
    await tester.tap(find.byKey(const Key('submit-form-button')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Cancel'));
    await tester.pumpAndSettle();

    expect(repository.submitCallCount, 0);
    expect(find.byKey(const Key('submit-form-button')), findsOneWidget);
  });

  test('empty draft submission is rejected before an API write', () async {
    final repository = FakePreventiveMaintenanceRepository(
      forms: [testForm(id: formId)],
    );
    final controller = PreventiveMaintenanceController(
      repository: repository,
      user: testUser(),
    );
    await controller.loadForms();
    controller.selectForm(controller.visibleDrafts.single);

    expect(await controller.submitForm(), isNull);
    expect(repository.submitCallCount, 0);
    expect(
      controller.errorMessage,
      'Add at least one inspection row before submitting this form.',
    );
    controller.dispose();
  });

  test(
    'submission conflicts are surfaced without leaking server details',
    () async {
      final repository = FakePreventiveMaintenanceRepository(
        forms: [
          testForm(id: formId, inspections: [testInspection()]),
        ],
        submitError: const ApiException(
          statusCode: 409,
          message: 'internal sequence details',
        ),
      );
      final controller = PreventiveMaintenanceController(
        repository: repository,
        user: testUser(),
      );
      await controller.loadForms();
      controller.selectForm(controller.visibleDrafts.single);

      expect(await controller.submitForm(), isNull);
      expect(repository.submitCallCount, 1);
      expect(
        controller.errorMessage,
        'This draft has a conflict. Refresh it and try again.',
      );
      expect(controller.errorMessage, isNot(contains('internal sequence')));
      controller.dispose();
    },
  );

  test('late form loading does not notify after controller disposal', () async {
    final pendingForms = Completer<List<PreventiveMaintenanceForm>>();
    final repository = FakePreventiveMaintenanceRepository(
      formsFuture: pendingForms.future,
    );
    final controller = PreventiveMaintenanceController(
      repository: repository,
      user: testUser(),
    );

    final loading = controller.loadForms();
    await Future<void>.delayed(Duration.zero);
    controller.dispose();
    pendingForms.complete(const []);

    await expectLater(loading, completes);
  });

  test('draft repository uses the authenticated API contract', () async {
    final transportState = DraftTransportState();
    final client = ApiClient(
      baseUrl: Uri.parse('http://localhost:5000/'),
      httpClientFactory: () => DraftTransport(transportState),
    );
    var terminalFailures = 0;
    client.configureSession(
      accessTokenProvider: () => 'inspector-access-token',
      terminalAuthFailureHandler: () async => terminalFailures++,
    );
    final repository = ApiPreventiveMaintenanceRepository(client);
    final inspectionDate = DateTime(2026, 2, 10);

    await repository.createForm(
      const CreatePreventiveMaintenanceFormInput(
        assetCategory: 'fire-extinguisher',
        building: 'Main Building',
        department: 'GSD',
        periodType: 'Quarter',
        quarter: 'Q1',
        semester: null,
        year: 2026,
        academicYear: '2026-2027',
      ),
    );
    await repository.addInspection(
      formId,
      AddInspectionInput(
        scheduleId: firstScheduleId,
        inspectorUserId: inspectorId,
        dateInspected: inspectionDate,
        isOperational: false,
        remarks: 'Low pressure',
        actionsRecommendations: 'Inspect gauge',
      ),
    );
    await repository.updateInspection(
      formId,
      firstInspectionId,
      UpdateInspectionInput(
        inspectorUserId: inspectorId,
        dateInspected: inspectionDate,
        isOperational: true,
        remarks: 'Updated',
        actionsRecommendations: 'Replace gauge',
      ),
    );
    await repository.deleteInspection(formId, secondInspectionId);

    final requests = transportState.requests;
    expect(requests, hasLength(4));
    for (final request in requests) {
      expect(request.headers['authorization'], 'Bearer inspector-access-token');
    }
    expect(requests[0].url.path, '/api/v1/preventive-maintenance-forms');
    expect(
      requests[1].url.path,
      '/api/v1/preventive-maintenance-forms/$formId/inspections',
    );
    expect(
      jsonDecode((requests[1] as http.Request).body),
      containsPair('scheduleId', firstScheduleId),
    );
    expect(
      requests[2].url.path,
      '/api/v1/preventive-maintenance-forms/$formId/inspections/$firstInspectionId',
    );
    expect(
      jsonDecode((requests[2] as http.Request).body),
      isNot(contains('scheduleId')),
    );
    expect(
      requests[3].url.path,
      '/api/v1/preventive-maintenance-forms/$formId/inspections/$secondInspectionId',
    );

    transportState.conflictResponses = 1;
    await expectLater(
      repository.addInspection(
        formId,
        AddInspectionInput(
          scheduleId: firstScheduleId,
          inspectorUserId: inspectorId,
          dateInspected: inspectionDate,
          isOperational: false,
          remarks: null,
          actionsRecommendations: null,
        ),
      ),
      throwsA(
        isA<ApiException>().having(
          (error) => error.statusCode,
          'status code',
          409,
        ),
      ),
    );

    transportState.unauthorizedResponses = 1;
    await expectLater(repository.listForms(), throwsA(isA<ApiException>()));
    expect(terminalFailures, 1);
    expect(
      transportState.requests.last.url.path,
      '/api/v1/preventive-maintenance-forms',
    );
    client.dispose();
  });

  test(
    'schedule lookup filters by exact asset ID and preserves metadata',
    () async {
      final transportState = DraftTransportState();
      final client = ApiClient(
        baseUrl: Uri.parse('http://localhost:5000/'),
        httpClientFactory: () => DraftTransport(transportState),
      );
      client.configureSession(
        accessTokenProvider: () => 'inspector-access-token',
        terminalAuthFailureHandler: () async {},
      );
      final repository = ApiPreventiveMaintenanceRepository(client);

      final schedules = await repository.listSchedules(assetId: testAsset().id);

      final request = transportState.requests.single;
      expect(request.url.path, '/api/v1/schedules');
      expect(request.url.queryParameters['assetId'], testAsset().id);
      expect(request.headers['authorization'], 'Bearer inspector-access-token');
      expect(schedules.single.assetId, testAsset().id);
      expect(schedules.single.periodType, 'Quarter');
      expect(schedules.single.quarter, 'Q1');
      expect(schedules.single.year, 2026);
      expect(schedules.single.academicYear, '2026-2027');
      expect(schedules.single.asset?.assetCategory, 'fire-extinguisher');
      client.dispose();
    },
  );

  testWidgets('editing a row persists date, condition, remarks, and action', (
    tester,
  ) async {
    final repository = FakePreventiveMaintenanceRepository(
      forms: [
        testForm(id: formId, inspections: [testInspection()]),
      ],
    );

    await pumpPage(tester, repository);
    await tester.tap(find.byKey(Key('draft-form-$formId')));
    await tester.pumpAndSettle();
    await scrollTo(
      tester,
      find.byKey(Key('inspection-date-$firstInspectionId')),
    );
    await tester.enterText(
      find.byKey(Key('inspection-date-$firstInspectionId')),
      '2026-02-10',
    );
    await tester.tap(find.text('Operational').last);
    await tester.enterText(
      find.byKey(Key('inspection-remarks-$firstInspectionId')),
      'Updated remarks',
    );
    await tester.enterText(
      find.byKey(Key('inspection-actions-$firstInspectionId')),
      'Replace filter',
    );
    await scrollTo(
      tester,
      find.byKey(Key('save-inspection-$firstInspectionId')),
    );
    await tester.tap(find.byKey(Key('save-inspection-$firstInspectionId')));
    await tester.pumpAndSettle();

    expect(repository.updatedInput?.inspectorUserId, inspectorId);
    expect(repository.updatedInput?.dateInspected, DateTime(2026, 2, 10));
    expect(repository.updatedInput?.isOperational, isTrue);
    expect(repository.updatedInput?.remarks, 'Updated remarks');
    expect(repository.updatedInput?.actionsRecommendations, 'Replace filter');
  });

  testWidgets('deleting a row requires confirmation and persists removal', (
    tester,
  ) async {
    final repository = FakePreventiveMaintenanceRepository(
      forms: [
        testForm(id: formId, inspections: [testInspection()]),
      ],
    );

    await pumpPage(tester, repository);
    await tester.tap(find.byKey(Key('draft-form-$formId')));
    await tester.pumpAndSettle();
    await scrollTo(tester, find.text('Delete row'));
    await tester.tap(find.text('Delete row'));
    await tester.pumpAndSettle();
    expect(find.text('Delete inspection row?'), findsOneWidget);
    await tester.tap(find.text('Delete draft row'));
    await tester.pumpAndSettle();

    expect(repository.deletedInspectionId, firstInspectionId);
    expect(find.text('Inspection rows (0)'), findsOneWidget);
  });

  testWidgets(
    'draft editor does not expose submission or acknowledgement actions',
    (tester) async {
      final repository = FakePreventiveMaintenanceRepository(
        forms: [testForm(id: formId)],
      );

      await pumpPage(tester, repository);
      await tester.tap(find.byKey(Key('draft-form-$formId')));
      await tester.pumpAndSettle();

      expect(find.text('Submit'), findsNothing);
      expect(find.text('Acknowledge'), findsNothing);
      expect(find.text('Signature'), findsNothing);
    },
  );
}

Future<void> pumpPage(
  WidgetTester tester,
  FakePreventiveMaintenanceRepository repository, {
  AuthUser? user,
}) async {
  await tester.pumpWidget(
    MaterialApp(
      home: PreventiveMaintenancePage(
        repository: repository,
        user: user ?? testUser(),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

Future<void> pumpScannedEntry(
  WidgetTester tester,
  FakePreventiveMaintenanceRepository repository, {
  Asset? asset,
  AuthUser? user,
}) async {
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: SingleChildScrollView(
          child: ScannedAssetPmEntry(
            asset: asset ?? testAsset(),
            repository: repository,
            user: user ?? testUser(),
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

Future<void> chooseDropdown(WidgetTester tester, Key key, String label) async {
  await scrollTo(tester, find.byKey(key));
  await tester.tap(find.byKey(key));
  await tester.pumpAndSettle();
  await tester.tap(find.textContaining(label).last);
  await tester.pumpAndSettle();
}

Future<void> scrollTo(WidgetTester tester, Finder finder) async {
  final listView = find.byType(ListView).last;
  final scrollable = find
      .descendant(of: listView, matching: find.byType(Scrollable))
      .first;
  await tester.scrollUntilVisible(finder, 300, scrollable: scrollable);
  await tester.pumpAndSettle();
}

PreventiveMaintenanceForm testForm({
  required String id,
  String? fileNumber,
  String createdByUserId = inspectorId,
  List<PreventiveMaintenanceInspection> inspections = const [],
  String status = 'Draft',
  String assetCategory = 'fire-extinguisher',
  String? building = 'Main Building',
  String? department = 'GSD',
  String periodType = 'Quarter',
  String? quarter = 'Q1',
  String? semester,
  int? year = 2026,
  String? academicYear = '2026-2027',
}) {
  final now = DateTime.utc(2026, 1, 15);
  return PreventiveMaintenanceForm(
    id: id,
    fileNumber: fileNumber,
    assetCategory: assetCategory,
    building: building,
    department: department,
    periodType: periodType,
    quarter: quarter,
    semester: semester,
    year: year,
    academicYear: academicYear,
    status: status,
    createdByUserId: createdByUserId,
    submittedByUserId: null,
    submittedAt: null,
    createdAt: now,
    updatedAt: now,
    inspections: inspections,
  );
}

Asset testAsset({String status = 'Active'}) => Asset(
  id: '88888888-8888-4888-8888-888888888888',
  assetCode: 'FE-001',
  assetCategory: 'fire-extinguisher',
  building: 'Main Building',
  department: 'GSD',
  location: 'Test Area',
  qrCodeValue: 'UNIPM-FIREEXTINGUISHER-88888888',
  status: status,
);

PreventiveMaintenanceInspection testInspection({
  String id = firstInspectionId,
}) {
  final now = DateTime.utc(2026, 1, 15);
  return PreventiveMaintenanceInspection(
    id: id,
    scheduleId: id == firstInspectionId ? firstScheduleId : secondScheduleId,
    assetId: '88888888-8888-4888-8888-888888888888',
    inspectorUserId: inspectorId,
    dateInspected: now,
    isOperational: false,
    remarks: 'Low pressure',
    actionsRecommendations: 'Inspect gauge',
    createdAt: now,
    updatedAt: now,
  );
}

PreventiveMaintenanceAcknowledgement testAcknowledgement(String formId) {
  return PreventiveMaintenanceAcknowledgement(
    id: '99999999-9999-4999-8999-999999999999',
    formId: formId,
    signatoryName: 'Synthetic Department Head',
    signatoryPosition: 'Department Head',
    signatureContentType: 'image/png',
    signatureChecksum: 'SYNTHETIC-CHECKSUM',
    capturedByUserId: inspectorId,
    acknowledgedAt: DateTime.utc(2026, 2, 10, 8),
  );
}

class FakePreventiveMaintenanceRepository
    implements PreventiveMaintenanceRepository {
  FakePreventiveMaintenanceRepository({
    List<PreventiveMaintenanceForm>? forms,
    this.referenceFailures = 0,
    this.scheduleFailures = 0,
    this.schedulesFuture,
    this.formsFuture,
    this.submitError,
    this.acknowledgementError,
  }) : forms = [...?forms];

  List<PreventiveMaintenanceForm> forms;
  int referenceFailures;
  int scheduleFailures;
  final Future<List<ScheduleOption>>? schedulesFuture;
  final Future<List<PreventiveMaintenanceForm>>? formsFuture;
  final ApiException? submitError;
  final ApiException? acknowledgementError;
  CreatePreventiveMaintenanceFormInput? createdInput;
  AddInspectionInput? addedInput;
  UpdateInspectionInput? updatedInput;
  String? deletedInspectionId;
  int addCallCount = 0;
  int submitCallCount = 0;
  String? submittedFormId;
  int acknowledgementCallCount = 0;
  AcknowledgePreventiveMaintenanceInput? acknowledgementInput;
  final requestedScheduleAssetIds = <String?>[];

  @override
  Future<List<PreventiveMaintenanceForm>> listForms() =>
      formsFuture ?? Future.value(forms);

  @override
  Future<PreventiveMaintenanceForm> getForm(String id) async {
    return forms.singleWhere((candidate) => candidate.id == id);
  }

  @override
  Future<PreventiveMaintenanceForm> createForm(
    CreatePreventiveMaintenanceFormInput input,
  ) async {
    createdInput = input;
    final created = testForm(
      id: '99999999-9999-4999-8999-999999999999',
      assetCategory: input.assetCategory,
      building: input.building,
      department: input.department,
      periodType: input.periodType,
      quarter: input.quarter,
      semester: input.semester,
      year: input.year,
      academicYear: input.academicYear,
    );
    forms = [created, ...forms];
    return created;
  }

  @override
  Future<PreventiveMaintenanceForm> submitForm(String formId) async {
    submitCallCount++;
    submittedFormId = formId;
    if (submitError != null) {
      throw submitError!;
    }
    final current = forms.singleWhere((candidate) => candidate.id == formId);
    final submitted = testForm(
      id: current.id,
      fileNumber: 'PM-2026-0001',
      createdByUserId: current.createdByUserId,
      inspections: current.inspections,
      status: 'Submitted',
      assetCategory: current.assetCategory,
      building: current.building,
      department: current.department,
      periodType: current.periodType,
      quarter: current.quarter,
      semester: current.semester,
      year: current.year,
      academicYear: current.academicYear,
    );
    forms = forms
        .map((candidate) => candidate.id == formId ? submitted : candidate)
        .toList(growable: false);
    return submitted;
  }

  @override
  Future<PreventiveMaintenanceAcknowledgement> acknowledgeForm(
    String formId,
    AcknowledgePreventiveMaintenanceInput input,
  ) async {
    acknowledgementCallCount++;
    acknowledgementInput = input;
    if (acknowledgementError != null) {
      throw acknowledgementError!;
    }
    final current = forms.singleWhere((candidate) => candidate.id == formId);
    forms = forms
        .map(
          (candidate) => candidate.id == formId
              ? candidate.copyWith(status: 'Acknowledged')
              : candidate,
        )
        .toList(growable: false);
    return testAcknowledgement(current.id);
  }

  @override
  Future<List<ScheduleOption>> listSchedules({String? assetId}) {
    requestedScheduleAssetIds.add(assetId);
    if (scheduleFailures > 0) {
      scheduleFailures--;
      return Future.error(
        const ApiException(
          message: 'The mobile service is unavailable. Please try again.',
        ),
      );
    }
    return schedulesFuture ??
        Future.value([
          testSchedule(firstScheduleId, 'FE-001'),
          testSchedule(secondScheduleId, 'FE-002'),
        ]);
  }

  @override
  Future<List<ReferenceOption>> listAssetCategories() async {
    _failReferenceIfNeeded();
    return const [
      ReferenceOption(
        code: 'fire-extinguisher',
        displayName: 'Fire extinguisher',
      ),
    ];
  }

  @override
  Future<List<ReferenceOption>> listPeriodTypes() async => const [
    ReferenceOption(code: 'Quarter', displayName: 'Quarter'),
  ];

  @override
  Future<List<ReferenceOption>> listQuarters() async => const [
    ReferenceOption(code: 'Q1', displayName: 'Q1'),
  ];

  @override
  Future<PreventiveMaintenanceInspection> addInspection(
    String formId,
    AddInspectionInput input,
  ) async {
    addCallCount++;
    addedInput = input;
    final row = testInspection(id: secondInspectionId);
    final form = forms.singleWhere((candidate) => candidate.id == formId);
    forms = forms
        .map(
          (candidate) => candidate.id == formId
              ? candidate.copyWith(inspections: [...form.inspections, row])
              : candidate,
        )
        .toList();
    return row;
  }

  @override
  Future<PreventiveMaintenanceInspection> updateInspection(
    String formId,
    String inspectionId,
    UpdateInspectionInput input,
  ) async {
    updatedInput = input;
    final current = testInspection();
    final row = PreventiveMaintenanceInspection(
      id: current.id,
      scheduleId: current.scheduleId,
      assetId: current.assetId,
      inspectorUserId: input.inspectorUserId,
      dateInspected: input.dateInspected,
      isOperational: input.isOperational,
      remarks: input.remarks,
      actionsRecommendations: input.actionsRecommendations,
      createdAt: current.createdAt,
      updatedAt: DateTime.utc(2026, 2, 10),
    );
    return row;
  }

  @override
  Future<void> deleteInspection(String formId, String inspectionId) async {
    deletedInspectionId = inspectionId;
  }

  void _failReferenceIfNeeded() {
    if (referenceFailures == 0) return;
    referenceFailures--;
    throw const ApiException(
      message: 'The mobile service is unavailable. Please try again.',
    );
  }
}

ScheduleOption testSchedule(
  String id,
  String assetCode, {
  String status = 'Due',
}) => ScheduleOption(
  id: id,
  assetId: '88888888-8888-4888-8888-888888888888',
  scheduleDate: DateTime.utc(2026, 1, 10),
  periodType: 'Quarter',
  status: status,
  quarter: 'Q1',
  semester: null,
  year: 2026,
  academicYear: '2026-2027',
  asset: ScheduleAssetOption(
    id: '88888888-8888-4888-8888-888888888888',
    assetCode: assetCode,
    assetCategory: 'fire-extinguisher',
    building: 'Main Building',
    department: 'GSD',
    location: 'Test Area',
  ),
);

const secondInspectionId = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';

class DraftTransport extends http.BaseClient {
  DraftTransport(this.owner);

  final DraftTransportState owner;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    owner.requests.add(request);
    final response = owner.responseFor(request);
    return http.StreamedResponse(
      Stream.value(utf8.encode(response.body)),
      response.statusCode,
      headers: response.headers,
      request: request,
    );
  }
}

class DraftTransportState {
  final requests = <http.BaseRequest>[];
  int conflictResponses = 0;
  int unauthorizedResponses = 0;

  http.Response responseFor(http.BaseRequest request) {
    if (conflictResponses > 0) {
      conflictResponses--;
      return http.Response('', 409);
    }
    if (unauthorizedResponses > 0) {
      unauthorizedResponses--;
      return http.Response('', 401);
    }

    switch (request.url.path) {
      case '/api/v1/schedules':
        return http.Response(jsonEncode([testScheduleJson()]), 200);
      case '/api/v1/preventive-maintenance-forms':
        return http.Response(jsonEncode(testFormJson()), 201);
      case '/api/v1/preventive-maintenance-forms/$formId/inspections':
      case '/api/v1/preventive-maintenance-forms/$formId/inspections/$firstInspectionId':
        return http.Response(jsonEncode(testInspectionJson()), 200);
      case '/api/v1/preventive-maintenance-forms/$formId/inspections/$secondInspectionId':
        return http.Response('', 204);
      default:
        return http.Response('', 404);
    }
  }

  Map<String, dynamic> testFormJson() => <String, dynamic>{
    'id': formId,
    'fileNumber': null,
    'assetCategory': 'fire-extinguisher',
    'building': 'Main Building',
    'department': 'GSD',
    'periodType': 'Quarter',
    'quarter': 'Q1',
    'semester': null,
    'year': 2026,
    'academicYear': '2026-2027',
    'status': 'Draft',
    'createdByUserId': inspectorId,
    'submittedByUserId': null,
    'submittedAt': null,
    'createdAt': '2026-01-15T00:00:00Z',
    'updatedAt': '2026-01-15T00:00:00Z',
    'inspections': [],
  };

  Map<String, dynamic> testInspectionJson() => <String, dynamic>{
    'id': firstInspectionId,
    'scheduleId': firstScheduleId,
    'assetId': '88888888-8888-4888-8888-888888888888',
    'inspectorUserId': inspectorId,
    'dateInspected': '2026-01-15T00:00:00Z',
    'isOperational': false,
    'remarks': 'Low pressure',
    'actionsRecommendations': 'Inspect gauge',
    'createdAt': '2026-01-15T00:00:00Z',
    'updatedAt': '2026-01-15T00:00:00Z',
  };

  Map<String, dynamic> testScheduleJson() => <String, dynamic>{
    'id': firstScheduleId,
    'assetId': testAsset().id,
    'scheduleDate': '2026-01-10T00:00:00Z',
    'periodType': 'Quarter',
    'status': 'Due',
    'quarter': 'Q1',
    'semester': null,
    'year': 2026,
    'academicYear': '2026-2027',
    'assignedToUserId': inspectorId,
    'completedAt': null,
    'asset': <String, dynamic>{
      'id': testAsset().id,
      'assetCode': 'FE-001',
      'assetCategory': 'fire-extinguisher',
      'building': 'Main Building',
      'department': 'GSD',
      'location': 'Test Area',
    },
  };
}
