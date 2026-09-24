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
  String? pmCycle = '2026-06',
  String department = 'GSD',
  String assetCategory = 'fire-extinguisher',
  String? assignedToUserId,
}) => ScheduleOption(
  id: id,
  assetId: '88888888-8888-4888-8888-888888888888',
  scheduleDate: DateTime.utc(2026, 6, 15),
  pmCycle: pmCycle,
  periodType: 'Quarter',
  status: status,
  quarter: 'Q2',
  semester: null,
  year: 2026,
  academicYear: '2025-2026',
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
  String? pmCycle = '2026-06',
  List<PreventiveMaintenanceInspection> inspections = const [],
}) {
  final now = DateTime.utc(2026, 6, 15);
  return PreventiveMaintenanceForm(
    id: id,
    fileNumber: 'PM-2026-06-001',
    assetCategory: assetCategory,
    building: null,
    department: department,
    pmCycle: pmCycle,
    periodType: 'Quarter',
    quarter: 'Q2',
    semester: null,
    year: 2026,
    academicYear: '2025-2026',
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

  @override
  Future<List<PreventiveMaintenanceForm>> listForms() async => forms;

  @override
  Future<PreventiveMaintenanceForm> getForm(String id) async =>
      forms.singleWhere((f) => f.id == id);

  @override
  Future<PreventiveMaintenanceForm> createForm(
    CreatePreventiveMaintenanceFormInput input,
  ) async {
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

Future<void> scrollTo(WidgetTester tester, Finder finder) async {
  final listView = find.byType(ListView).last;
  final scrollable = find
      .descendant(of: listView, matching: find.byType(Scrollable))
      .first;
  await tester.scrollUntilVisible(finder, 300, scrollable: scrollable);
  await tester.pumpAndSettle();
}

void main() {
  group('ScheduleOption model', () {
    test('deserializes assignedToUserId when provided', () {
      final json = {
        'id': '11111111-1111-4111-8111-111111111111',
        'assetId': '22222222-2222-4222-8222-222222222222',
        'scheduleDate': '2026-06-15T00:00:00.000Z',
        'pmCycle': '2026-06',
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
      expect(option.pmCycle, '2026-06');
      expect(option.status, 'Due');
    });

    test('deserializes assignedToUserId as null when omitted or null', () {
      final json = {
        'id': '11111111-1111-4111-8111-111111111111',
        'assetId': '22222222-2222-4222-8222-222222222222',
        'scheduleDate': '2026-06-15T00:00:00.000Z',
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

  group('Batch denominator and progress calculation in PreventiveMaintenancePage', () {
    testWidgets(
      'denominator includes already-inspected and uninspected schedules, excludes Cancelled schedules',
      (tester) async {
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
        // Schedule 3: uninspected, in batch
        final s3 = makeSchedule(
          id: testSchedule3Id,
          assetCode: 'FE-003',
          status: 'Due',
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
            ),
          ),
        );
        await tester.pumpAndSettle();

        // 1. Verify dropdown only lists unattached schedules (s2, s3).
        // S1 (already inspected), S4 (cancelled), and S5 (different department) should not be available to attach.
        final scheduleDropdownFinder = find.byKey(
          const Key('inspection-schedule'),
        );
        expect(scheduleDropdownFinder, findsOneWidget);

        // Open the dropdown
        await tester.tap(scheduleDropdownFinder);
        await tester.pumpAndSettle();

        // FE-002 and FE-003 are unattached and non-cancelled
        expect(find.textContaining('FE-002'), findsWidgets);
        expect(find.textContaining('FE-003'), findsWidgets);
        // FE-001 (already inspected) and FE-004 (cancelled) must not be in the dropdown
        expect(find.textContaining('FE-001'), findsNothing);
        expect(find.textContaining('FE-004'), findsNothing);

        // Select FE-002
        await tester.tap(find.textContaining('FE-002').last);
        await tester.pumpAndSettle();

        // Tap Add Inspection row
        await scrollTo(tester, find.byKey(const Key('add-inspection-button')));
        await tester.tap(find.byKey(const Key('add-inspection-button')));
        await tester.pumpAndSettle();

        // 2. Verify InspectionCompletionSheet is shown with correct completed/total ratio:
        // Completed = 2 (s1 + s2)
        // Total = 3 (s1 + s2 + s3; excludes s4 Cancelled and s5 different department)
        expect(find.byType(InspectionCompletionSheet), findsOneWidget);
        expect(find.text('Inspection Recorded'), findsOneWidget);
        expect(
          find.text('Asset FE-002 successfully inspected.'),
          findsOneWidget,
        );
        expect(find.text('2 of 3 assets inspected'), findsOneWidget);
        expect(find.text('67%'), findsOneWidget);

        // Verify actions
        expect(find.byKey(const Key('sheet-next-asset')), findsOneWidget);
        expect(find.byKey(const Key('sheet-view-batch')), findsOneWidget);
      },
    );
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
              pmCycle: '2026-06',
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
      expect(find.text('2026-06'), findsOneWidget);
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
              pmCycle: '2026-06',
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
          pmCycle: '2026-06',
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
        expect(find.text('PM Cycle: 2026-06'), findsOneWidget);
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
          pmCycle: '2026-06',
          assignedToUserId: testInspectorId,
        );
        final existingInspection = makeInspection(
          id: testInspection1Id,
          scheduleId: testSchedule1Id,
        );
        final existingDraftForm = makeForm(
          id: testFormId,
          status: 'Draft',
          pmCycle: '2026-06',
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
        expect(find.text('PM Cycle: 2026-06'), findsOneWidget);
        expect(find.byKey(const Key('schedule-status')), findsOneWidget);
        expect(find.text('Schedule status: Ongoing'), findsOneWidget);

        // Check action button is 'Resume Inspection' with key resume-pm
        expect(find.byKey(const Key('resume-pm')), findsOneWidget);
        expect(find.text('Resume Inspection'), findsOneWidget);
        expect(find.byKey(const Key('start-pm')), findsNothing);
      },
    );
  });
}
