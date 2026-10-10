import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/auth/auth_models.dart';
import 'package:mobile/features/assets/asset_models.dart';
import 'package:mobile/features/preventive_maintenance/inspection_completion_sheet.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_models.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_page.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_repository.dart';
import 'package:mobile/features/preventive_maintenance/scanned_asset_pm_entry.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_controller.dart';

const testInspectorId = '11111111-1111-4111-8111-111111111111';
const testAssignedUserId = '22222222-2222-4222-8222-222222222222';
const testFormId = '33333333-3333-4333-8333-333333333333';
const testSchedule1Id = '44444444-4444-4444-8444-444444444441';
const testSchedule2Id = '44444444-4444-4444-8444-444444444442';
const testSchedule3Id = '44444444-4444-4444-8444-444444444443';
const testCancelledScheduleId = '44444444-4444-4444-8444-444444444444';
const testOtherBatchScheduleId = '44444444-4444-4444-8444-444444444445';
const testInspection1Id = '55555555-5555-4555-8555-555555555551';
const testAsset1Id = '66666666-6666-4666-8666-666666666661';
const testAsset2Id = '66666666-6666-4666-8666-666666666662';

int _cycleYear(String pmCycle) => int.parse(pmCycle.substring(0, 4));
int _cycleMonth(String pmCycle) => int.parse(pmCycle.substring(5, 7));

String _quarterForCycle(String pmCycle) =>
    'Q${((_cycleMonth(pmCycle) - 1) ~/ 3) + 1}';

String _academicYearForCycle(String pmCycle) {
  final year = _cycleYear(pmCycle);
  final startYear = _cycleMonth(pmCycle) >= 7 ? year : year - 1;
  return '$startYear-${startYear + 1}';
}

DateTime _deadlineForCycle(String pmCycle) => DateTime.utc(
  _cycleYear(pmCycle),
  _cycleMonth(pmCycle) + 1,
  0,
  15,
  59,
  59,
  999,
  999,
);
AuthUser testUser({List<String> roles = const ['Inspector']}) => AuthUser(
  id: testInspectorId,
  email: 'inspector@example.test',
  displayName: 'Test Inspector',
  roles: roles,
);

Asset testAsset({
  String id = testAsset1Id,
  String assetCode = 'FE-001',
  String status = 'Active',
  String department = 'GSD',
  String assetCategory = 'fire-extinguisher',
}) => Asset(
  id: id,
  assetCode: assetCode,
  assetCategory: assetCategory,
  building: 'Main Building',
  department: department,
  location: 'Floor 1',
  qrCodeValue: 'QR-$assetCode',
  status: status,
);

ScheduleOption makeSchedule({
  required String id,
  required String assetCode,
  String status = 'Due',
  String? pmCycle = '2026-05',
  DateTime? scheduleDate,
  String periodType = 'Quarter',
  String? quarter = 'Q2',
  String department = 'GSD',
  String assetCategory = 'fire-extinguisher',
  String? assignedToUserId,
}) {
  final cycleForDate = pmCycle ?? '2026-05';
  return ScheduleOption(
    id: id,
    assetId: '88888888-8888-4888-8888-888888888888',
    scheduleDate: scheduleDate ?? _deadlineForCycle(cycleForDate),
    pmCycle: pmCycle,
    periodType: periodType,
    status: status,
    quarter: periodType == 'Quarter' ? _quarterForCycle(cycleForDate) : quarter,
    semester: null,
    year: _cycleYear(cycleForDate),
    academicYear: _academicYearForCycle(cycleForDate),
    assignedToUserId: assignedToUserId,
    asset: ScheduleAssetOption(
      id: '88888888-8888-4888-8888-888888888888',
      assetCode: assetCode,
      assetCategory: assetCategory,
      building: 'Main Building',
      department: department,
      location: 'Floor 1',
    ),
  );
}

PreventiveMaintenanceInspection makeInspection({
  required String id,
  required String scheduleId,
}) {
  final now = DateTime.utc(2026, 6, 15);
  return PreventiveMaintenanceInspection(
    id: id,
    scheduleId: scheduleId,
    assetId: '88888888-8888-4888-8888-888888888888',
    inspectorUserId: testInspectorId,
    dateInspected: now,
    completedAt: now,
    isOperational: true,
    remarks: 'Good',
    actionsRecommendations: 'None',
    createdAt: now,
    updatedAt: now,
  );
}

PreventiveMaintenanceForm makeForm({
  required String id,
  String status = 'Draft',
  String assetCategory = 'fire-extinguisher',
  String department = 'GSD',
  String? pmCycle = '2026-05',
  List<PreventiveMaintenanceInspection> inspections = const [],
}) {
  final cycleForForm = pmCycle ?? '2026-05';
  final now = DateTime.utc(2026, 6, 15);
  return PreventiveMaintenanceForm(
    id: id,
    fileNumber:
        'PM-${_cycleYear(cycleForForm)}-${_cycleMonth(cycleForForm).toString().padLeft(2, '0')}-001',
    assetCategory: assetCategory,
    building: null,
    department: department,
    pmCycle: pmCycle,
    periodType: 'Quarter',
    quarter: _quarterForCycle(cycleForForm),
    semester: null,
    year: _cycleYear(cycleForForm),
    academicYear: _academicYearForCycle(cycleForForm),
    status: status,
    createdByUserId: testInspectorId,
    submittedByUserId: null,
    submittedAt: null,
    createdAt: now,
    updatedAt: now,
    inspections: inspections,
  );
}

class TestProgressRepository implements PreventiveMaintenanceRepository {
  TestProgressRepository({required this.forms, required this.schedules});

  List<PreventiveMaintenanceForm> forms;
  List<ScheduleOption> schedules;
  AddInspectionInput? lastAddedInput;
  int createFormCount = 0;

  @override
  Future<List<PreventiveMaintenanceForm>> listForms() async => forms;

  @override
  Future<PreventiveMaintenanceForm> getForm(String id) async =>
      forms.singleWhere((f) => f.id == id);

  @override
  Future<PreventiveMaintenanceForm> createForm(
    CreatePreventiveMaintenanceFormInput input,
  ) async {
    createFormCount++;
    throw UnimplementedError();
  }

  @override
  Future<PreventiveMaintenanceForm> submitForm(String formId) async {
    throw UnimplementedError();
  }

  @override
  Future<PreventiveMaintenanceAcknowledgement> acknowledgeForm(
    String formId,
    AcknowledgePreventiveMaintenanceInput input,
  ) async {
    throw UnimplementedError();
  }

  @override
  Future<List<ScheduleOption>> listSchedules({String? assetId}) async {
    if (assetId != null) {
      return schedules.where((s) => s.assetId == assetId).toList();
    }
    return schedules;
  }

  @override
  Future<List<ReferenceOption>> listAssetCategories() async => const [
    ReferenceOption(
      code: 'fire-extinguisher',
      displayName: 'Fire Extinguisher',
    ),
  ];

  @override
  Future<List<ReferenceOption>> listPeriodTypes() async => const [
    ReferenceOption(code: 'Quarter', displayName: 'Quarter'),
  ];

  @override
  Future<List<ReferenceOption>> listQuarters() async => const [
    ReferenceOption(code: 'Q2', displayName: 'Q2'),
  ];

  @override
  Future<PreventiveMaintenanceInspection> addInspection(
    String formId,
    AddInspectionInput input,
  ) async {
    lastAddedInput = input;
    final inspection = makeInspection(
      id: '55555555-5555-4555-8555-555555555552',
      scheduleId: input.scheduleId,
    );
    forms = forms.map((f) {
      if (f.id == formId) {
        return f.copyWith(inspections: [...f.inspections, inspection]);
      }
      return f;
    }).toList();
    return inspection;
  }

  @override
  Future<PreventiveMaintenanceInspection> updateInspection(
    String formId,
    String inspectionId,
    UpdateInspectionInput input,
  ) async {
    throw UnimplementedError();
  }

  @override
  Future<void> deleteInspection(String formId, String inspectionId) async {}
}

Future<void> scrollTo(
  WidgetTester tester,
  Finder finder, {
  double delta = 300,
}) async {
  final scrollable = find.byType(Scrollable).first;
  await tester.scrollUntilVisible(finder, delta, scrollable: scrollable);
  await tester.pumpAndSettle();
}

void main() {
  group('ScheduleOption model', () {
    test('deserializes assignedToUserId when provided', () {
      final json = {
        'id': '11111111-1111-4111-8111-111111111111',
        'assetId': '22222222-2222-4222-8222-222222222222',
        'scheduleDate': '2026-05-31T15:59:59.9999999Z',
        'pmCycle': '2026-05',
        'periodType': 'Quarter',
        'status': 'Due',
        'quarter': 'Q2',
        'semester': null,
        'year': 2026,
        'academicYear': '2025-2026',
        'assignedToUserId': testAssignedUserId,
        'asset': {
          'id': '22222222-2222-4222-8222-222222222222',
          'assetCode': 'FE-001',
          'assetCategory': 'fire-extinguisher',
          'building': 'Main Building',
          'department': 'GSD',
          'location': 'Floor 1',
        },
      };

      final option = ScheduleOption.fromJson(json);
      expect(option.assignedToUserId, testAssignedUserId);
      expect(option.pmCycle, '2026-05');
      expect(option.status, 'Due');
    });

    test('deserializes assignedToUserId as null when omitted or null', () {
      final json = {
        'id': '11111111-1111-4111-8111-111111111111',
        'assetId': '22222222-2222-4222-8222-222222222222',
        'scheduleDate': '2026-05-31T15:59:59.9999999Z',
        'periodType': 'Quarter',
        'status': 'Due',
        'quarter': 'Q2',
        'semester': null,
        'year': 2026,
        'academicYear': '2025-2026',
        'asset': {
          'id': '22222222-2222-4222-8222-222222222222',
          'assetCode': 'FE-001',
          'assetCategory': 'fire-extinguisher',
          'building': 'Main Building',
          'department': 'GSD',
          'location': 'Floor 1',
        },
      };

      final option = ScheduleOption.fromJson(json);
      expect(option.assignedToUserId, isNull);
    });
  });

  group('Batch progress and submission in PreventiveMaintenanceDraftPage', () {
    testWidgets('gates submission on every non-cancelled schedule in the batch', (
      tester,
    ) async {
      final existingInspection = makeInspection(
        id: testInspection1Id,
        scheduleId: testSchedule1Id,
      );
      final initialForm = makeForm(
        id: testFormId,
        inspections: [existingInspection],
      );

      // Schedule 1: already inspected in form
      final s1 = makeSchedule(
        id: testSchedule1Id,
        assetCode: 'FE-001',
        status: 'Ongoing',
      );
      // Schedule 2: uninspected, in batch
      final s2 = makeSchedule(
        id: testSchedule2Id,
        assetCode: 'FE-002',
        status: 'Due',
      );
      // Schedule 3: Completed without a Draft row, so no new row is allowed.
      final s3 = makeSchedule(
        id: testSchedule3Id,
        assetCode: 'FE-003',
        status: 'Completed',
      );
      // Schedule 4: CANCELLED, in batch -> must be strictly excluded from batch total!
      final s4 = makeSchedule(
        id: testCancelledScheduleId,
        assetCode: 'FE-004',
        status: 'Cancelled',
      );
      // Schedule 5: another department -> not matching batch
      final s5 = makeSchedule(
        id: testOtherBatchScheduleId,
        assetCode: 'FE-005',
        department: 'College of Science',
        status: 'Due',
      );

      final repository = TestProgressRepository(
        forms: [initialForm],
        schedules: [s1, s2, s3, s4, s5],
      );

      final controller = PreventiveMaintenanceController(
        repository: repository,
        user: testUser(),
      );
      await tester.pumpWidget(
        MaterialApp(
          home: PreventiveMaintenanceDraftPage(
            controller: controller,
            formId: testFormId,
            preselectedScheduleId: testSchedule2Id,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await scrollTo(tester, find.byKey(const Key('submit-form-button')));
      final incompleteSubmitButton = tester.widget<FilledButton>(
        find.byKey(const Key('submit-form-button')),
      );
      expect(incompleteSubmitButton.onPressed, isNull);
      expect(
        find.text(
          'Inspect all eligible schedules before submitting (1 of 3 complete).',
        ),
        findsOneWidget,
      );

      // The scanned/preselected schedule is the only new inspection exposed.
      await scrollTo(
        tester,
        find.byKey(const Key('add-inspection-button')),
        delta: -300,
      );
      expect(find.byKey(const Key('inspection-schedule')), findsNothing);
      expect(find.text('Inspect FE-002'), findsOneWidget);
      expect(find.textContaining('FE-003'), findsNothing);
      expect(find.textContaining('FE-004'), findsNothing);

      // Tap Add Inspection row
      await tester.tap(find.byKey(const Key('add-inspection-button')));
      await tester.pumpAndSettle();

      // The progress count uses the same eligible schedules as the submit gate.
      expect(find.byType(InspectionCompletionSheet), findsOneWidget);
      expect(find.text('Inspection Recorded'), findsOneWidget);
      expect(find.text('Asset FE-002 successfully inspected.'), findsOneWidget);
      expect(find.text('2 of 3 assets inspected'), findsOneWidget);
      expect(find.text('67%'), findsOneWidget);

      // Verify actions
      expect(find.byKey(const Key('sheet-next-asset')), findsOneWidget);
      expect(find.byKey(const Key('sheet-view-batch')), findsOneWidget);
      await tester.tap(find.byKey(const Key('sheet-view-batch')));
      await tester.pumpAndSettle();
      await scrollTo(tester, find.byKey(const Key('submit-form-button')));
      final stillIncompleteSubmitButton = tester.widget<FilledButton>(
        find.byKey(const Key('submit-form-button')),
      );
      expect(stillIncompleteSubmitButton.onPressed, isNull);
      expect(
        find.text(
          'A Completed schedule has no row in this Draft. Contact GSD to review the batch.',
        ),
        findsOneWidget,
      );
    });

    testWidgets('enables submission when all batch schedules have rows', (
      tester,
    ) async {
      final form = makeForm(
        id: testFormId,
        inspections: [
          makeInspection(id: testInspection1Id, scheduleId: testSchedule1Id),
          makeInspection(
            id: '55555555-5555-4555-8555-555555555552',
            scheduleId: testSchedule2Id,
          ),
        ],
      );
      final repository = TestProgressRepository(
        forms: [form],
        schedules: [
          makeSchedule(
            id: testSchedule1Id,
            assetCode: 'FE-001',
            status: 'Completed',
          ),
          makeSchedule(
            id: testSchedule2Id,
            assetCode: 'FE-002',
            status: 'Completed',
          ),
          makeSchedule(
            id: testCancelledScheduleId,
            assetCode: 'FE-003',
            status: 'Cancelled',
          ),
        ],
      );
      final controller = PreventiveMaintenanceController(
        repository: repository,
        user: testUser(),
      );
      await tester.pumpWidget(
        MaterialApp(
          home: PreventiveMaintenanceDraftPage(
            controller: controller,
            formId: testFormId,
          ),
        ),
      );
      await tester.pumpAndSettle();
      await scrollTo(tester, find.byKey(const Key('submit-form-button')));
      final button = tester.widget<FilledButton>(
        find.byKey(const Key('submit-form-button')),
      );
      expect(button.onPressed, isNotNull);
    });

    testWidgets('legacy Drafts use the canonical cycle derived from their rows', (
      tester,
    ) async {
      final form = makeForm(
        id: testFormId,
        pmCycle: null,
        inspections: [
          makeInspection(id: testInspection1Id, scheduleId: testSchedule1Id),
        ],
      );
      final repository = TestProgressRepository(
        forms: [form],
        schedules: [
          makeSchedule(
            id: testSchedule1Id,
            assetCode: 'FE-001',
            status: 'Completed',
          ),
          makeSchedule(
            id: testSchedule2Id,
            assetCode: 'FE-002',
            pmCycle: null,
            scheduleDate: _deadlineForCycle('2026-05'),
            periodType: 'Annual',
            quarter: null,
          ),
          makeSchedule(
            id: testSchedule3Id,
            assetCode: 'FE-003',
            pmCycle: '2026-08',
            quarter: 'Q3',
          ),
          makeSchedule(
            id: testCancelledScheduleId,
            assetCode: 'FE-004',
            status: 'Cancelled',
          ),
          makeSchedule(
            id: testOtherBatchScheduleId,
            assetCode: 'FE-005',
            pmCycle: '2026-08',
            quarter: 'Q3',
          ),
        ],
      );
      final controller = PreventiveMaintenanceController(
        repository: repository,
        user: testUser(),
      );

      await tester.pumpWidget(
        MaterialApp(
          home: PreventiveMaintenanceDraftPage(
            controller: controller,
            formId: testFormId,
          ),
        ),
      );
      await tester.pumpAndSettle();
      await scrollTo(tester, find.byKey(const Key('submit-form-button')));

      expect(
        find.text(
          'Inspect all eligible schedules before submitting (1 of 2 complete).',
        ),
        findsOneWidget,
      );
      expect(
        tester
            .widget<FilledButton>(find.byKey(const Key('submit-form-button')))
            .onPressed,
        isNull,
      );
    });
  });

  group('InspectionCompletionSheet standalone', () {
    testWidgets('displays correct completed / total ratio and percentage', (
      tester,
    ) async {
      var nextAssetTapped = false;
      var viewBatchTapped = false;

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: InspectionCompletionSheet(
              assetCode: 'FE-100',
              department: 'GSD',
              assetCategory: 'fire-extinguisher',
              pmCycle: '2026-05',
              completedCount: 3,
              totalCount: 10,
              onNextAsset: () => nextAssetTapped = true,
              onViewBatch: () => viewBatchTapped = true,
            ),
          ),
        ),
      );

      expect(find.text('Inspection Recorded'), findsOneWidget);
      expect(find.text('Asset FE-100 successfully inspected.'), findsOneWidget);
      expect(find.text('Department: GSD'), findsOneWidget);
      expect(find.text('PM cycle: May 2026'), findsOneWidget);
      expect(find.text('Due date: May 31, 2026'), findsOneWidget);
      expect(find.text('3 of 10 assets inspected'), findsOneWidget);
      expect(find.text('30%'), findsOneWidget);

      await tester.tap(find.byKey(const Key('sheet-next-asset')));
      expect(nextAssetTapped, isTrue);

      await tester.tap(find.byKey(const Key('sheet-view-batch')));
      expect(viewBatchTapped, isTrue);
    });

    testWidgets('displays 100% when all assets are inspected', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: InspectionCompletionSheet(
              assetCode: 'FE-101',
              department: 'GSD',
              assetCategory: 'fire-extinguisher',
              pmCycle: '2026-05',
              completedCount: 5,
              totalCount: 5,
              onNextAsset: () {},
              onViewBatch: () {},
            ),
          ),
        ),
      );

      expect(find.text('5 of 5 assets inspected'), findsOneWidget);
      expect(find.text('100%'), findsOneWidget);
    });
  });

  group('ScannedAssetPmEntry asset PM detail card', () {
    testWidgets(
      'displays PM Cycle, schedule status, and Start Inspection button for uninspected schedule',
      (tester) async {
        final schedule = makeSchedule(
          id: testSchedule1Id,
          assetCode: 'FE-001',
          status: 'Due',
          pmCycle: '2026-05',
          assignedToUserId: testInspectorId,
        );
        final repository = TestProgressRepository(
          forms: [],
          schedules: [schedule],
        );

        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: SingleChildScrollView(
                child: ScannedAssetPmEntry(
                  asset: testAsset(id: schedule.assetId, assetCode: 'FE-001'),
                  repository: repository,
                  user: testUser(),
                ),
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();

        // Check PM Cycle and schedule status in asset PM detail card
        expect(find.byKey(const Key('selected-pm-schedule')), findsOneWidget);
        expect(find.byKey(const Key('schedule-pm-cycle')), findsOneWidget);
        expect(find.text('PM cycle: May 2026'), findsOneWidget);
        expect(find.text('Due date: May 31, 2026'), findsOneWidget);
        expect(find.byKey(const Key('schedule-status')), findsOneWidget);
        expect(find.text('Schedule status: Due'), findsOneWidget);

        // Check action button is 'Start Inspection' with key start-pm
        expect(find.byKey(const Key('start-pm')), findsOneWidget);
        expect(find.text('Start Inspection'), findsOneWidget);
        expect(find.byKey(const Key('resume-pm')), findsNothing);
      },
    );

    testWidgets(
      'displays PM Cycle, schedule status, and Resume Inspection button when inspection draft exists',
      (tester) async {
        final schedule = makeSchedule(
          id: testSchedule1Id,
          assetCode: 'FE-001',
          status: 'Ongoing',
          pmCycle: '2026-05',
          assignedToUserId: testInspectorId,
        );
        final existingInspection = makeInspection(
          id: testInspection1Id,
          scheduleId: testSchedule1Id,
        );
        final existingDraftForm = makeForm(
          id: testFormId,
          status: 'Draft',
          pmCycle: '2026-05',
          inspections: [existingInspection],
        );

        final repository = TestProgressRepository(
          forms: [existingDraftForm],
          schedules: [schedule],
        );

        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: SingleChildScrollView(
                child: ScannedAssetPmEntry(
                  asset: testAsset(id: schedule.assetId, assetCode: 'FE-001'),
                  repository: repository,
                  user: testUser(),
                ),
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();

        // Check PM Cycle and schedule status in asset PM detail card
        expect(find.byKey(const Key('selected-pm-schedule')), findsOneWidget);
        expect(find.byKey(const Key('schedule-pm-cycle')), findsOneWidget);
        expect(find.text('PM cycle: May 2026'), findsOneWidget);
        expect(find.text('Due date: May 31, 2026'), findsOneWidget);
        expect(find.byKey(const Key('schedule-status')), findsOneWidget);
        expect(find.text('Schedule status: Ongoing'), findsOneWidget);

        // Check action button is 'Resume Inspection' with key resume-pm
        expect(find.byKey(const Key('resume-pm')), findsOneWidget);
        expect(find.text('Resume Inspection'), findsOneWidget);
        expect(find.byKey(const Key('start-pm')), findsNothing);
      },
    );

    testWidgets(
      'resumes a Completed schedule Draft without a location prompt',
      (tester) async {
        final schedule = makeSchedule(
          id: testSchedule1Id,
          assetCode: 'FE-001',
          status: 'Completed',
          assignedToUserId: testInspectorId,
        );
        final existingDraft = makeForm(
          id: testFormId,
          inspections: [
            makeInspection(id: testInspection1Id, scheduleId: testSchedule1Id),
          ],
        );
        final repository = TestProgressRepository(
          forms: [existingDraft],
          schedules: [schedule],
        );
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: SingleChildScrollView(
                child: ScannedAssetPmEntry(
                  asset: testAsset(id: schedule.assetId, assetCode: 'FE-001'),
                  repository: repository,
                  user: testUser(),
                ),
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.byKey(const Key('schedule-status')), findsOneWidget);
        expect(find.text('Schedule status: Completed'), findsOneWidget);
        expect(find.byKey(const Key('resume-pm')), findsOneWidget);
        await tester.tap(find.byKey(const Key('resume-pm')));
        await tester.pumpAndSettle();

        expect(find.text('Verifying location...'), findsNothing);
        expect(repository.createFormCount, 0);
        expect(find.text('Resume inspection row'), findsOneWidget);
      },
    );

    testWidgets(
      'Completed schedule without a Draft row cannot start a new row',
      (tester) async {
        final schedule = makeSchedule(
          id: testSchedule1Id,
          assetCode: 'FE-001',
          status: 'Completed',
          assignedToUserId: testInspectorId,
        );
        final repository = TestProgressRepository(
          forms: [],
          schedules: [schedule],
        );

        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: SingleChildScrollView(
                child: ScannedAssetPmEntry(
                  asset: testAsset(id: schedule.assetId, assetCode: 'FE-001'),
                  repository: repository,
                  user: testUser(),
                ),
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(
          find.byKey(const Key('pm-entry-completed-no-draft')),
          findsOneWidget,
        );
        expect(find.byKey(const Key('start-pm')), findsNothing);
        expect(find.byKey(const Key('resume-pm')), findsNothing);
        expect(repository.createFormCount, 0);
      },
    );

    testWidgets('Start inspection does not require location access', (
      tester,
    ) async {
      final schedule = makeSchedule(
        id: testSchedule1Id,
        assetCode: 'FE-001',
        status: 'Due',
        assignedToUserId: testInspectorId,
      );
      final repository = TestProgressRepository(
        forms: [],
        schedules: [schedule],
      );
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: SingleChildScrollView(
              child: ScannedAssetPmEntry(
                asset: testAsset(id: schedule.assetId, assetCode: 'FE-001'),
                repository: repository,
                user: testUser(),
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const Key('start-pm')));
      await tester.pumpAndSettle();

      expect(
        find.byKey(const Key('location-verification-dialog')),
        findsNothing,
      );
      expect(repository.createFormCount, 1);
    });
  });
}
