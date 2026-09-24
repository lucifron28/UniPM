import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:mobile/auth/auth_models.dart';
import 'package:mobile/features/assets/asset_models.dart';
import 'package:mobile/features/auth/home_page.dart';
import 'package:mobile/features/preventive_maintenance/inspection_completion_sheet.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_models.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_repository.dart';

const testUserId = '11111111-1111-4111-8111-111111111111';

AuthUser createTestUser({List<String> roles = const ['Inspector']}) => AuthUser(
  id: testUserId,
  email: 'inspector@university.edu.test',
  displayName: 'Skilled Inspector',
  roles: roles,
);

PreventiveMaintenanceForm createTestForm({
  required String id,
  String status = 'Draft',
  String assetCategory = 'fire-extinguisher',
  String department = 'College of Science',
  String pmCycle = '2026-06',
  int rowCount = 2,
}) => PreventiveMaintenanceForm(
  id: id,
  fileNumber: status == 'Submitted' ? 'PM-2026-0001' : null,
  assetCategory: assetCategory,
  building: 'Science Hall',
  department: department,
  pmCycle: pmCycle,
  periodType: 'Quarter',
  quarter: 'Q2',
  semester: null,
  year: 2026,
  academicYear: '2025-2026',
  status: status,
  createdByUserId: testUserId,
  submittedByUserId: status == 'Submitted' ? testUserId : null,
  submittedAt: status == 'Submitted' ? DateTime(2026, 6, 20) : null,
  fieldWorkCompletedAt: null,
  createdAt: DateTime(2026, 6, 1),
  updatedAt: DateTime(2026, 6, 20),
  inspections: List.generate(
    rowCount,
    (index) => PreventiveMaintenanceInspection(
      id: 'row-$index',
      scheduleId: 'sched-$index',
      assetId: 'asset-$index',
      inspectorUserId: testUserId,
      dateInspected: DateTime(2026, 6, 15),
      isOperational: true,
      remarks: 'Working normally',
      actionsRecommendations: null,
      createdAt: DateTime(2026, 6, 15),
      updatedAt: DateTime(2026, 6, 15),
    ),
  ),
);

ScheduleOption createTestSchedule({
  required String id,
  required String assetId,
  required String assetCode,
  String assetCategory = 'fire-extinguisher',
  String department = 'College of Science',
  String pmCycle = '2026-06',
}) => ScheduleOption(
  id: id,
  assetId: assetId,
  scheduleDate: DateTime(2026, 6, 15),
  pmCycle: pmCycle,
  periodType: 'Quarter',
  status: 'Due',
  quarter: 'Q2',
  semester: null,
  year: 2026,
  academicYear: '2025-2026',
  assignedToUserId: testUserId,
  asset: ScheduleAssetOption(
    id: assetId,
    assetCode: assetCode,
    assetCategory: assetCategory,
    building: 'Science Hall',
    department: department,
    location: 'Room 101',
  ),
);

class FakeWorkflowPmRepository implements PreventiveMaintenanceRepository {
  FakeWorkflowPmRepository({this.forms = const [], this.schedules = const []});

  final List<PreventiveMaintenanceForm> forms;
  final List<ScheduleOption> schedules;

  @override
  Future<List<PreventiveMaintenanceForm>> listForms() async => forms;

  @override
  Future<List<ScheduleOption>> listSchedules({String? assetId}) async =>
      schedules;

  @override
  Future<PreventiveMaintenanceForm> getForm(String id) async =>
      forms.firstWhere((f) => f.id == id);

  @override
  Future<PreventiveMaintenanceForm> createForm(
    CreatePreventiveMaintenanceFormInput input,
  ) async => throw UnimplementedError();

  @override
  Future<PreventiveMaintenanceForm> submitForm(String formId) async =>
      throw UnimplementedError();

  @override
  Future<PreventiveMaintenanceAcknowledgement> acknowledgeForm(
    String formId,
    AcknowledgePreventiveMaintenanceInput input,
  ) async => throw UnimplementedError();

  @override
  Future<PreventiveMaintenanceInspection> addInspection(
    String formId,
    AddInspectionInput input,
  ) async => throw UnimplementedError();

  @override
  Future<PreventiveMaintenanceInspection> updateInspection(
    String formId,
    String inspectionId,
    UpdateInspectionInput input,
  ) async => throw UnimplementedError();

  @override
  Future<void> deleteInspection(String formId, String inspectionId) async =>
      throw UnimplementedError();

  @override
  Future<List<ReferenceOption>> listAssetCategories() async => [];

  @override
  Future<List<ReferenceOption>> listPeriodTypes() async => [];

  @override
  Future<List<ReferenceOption>> listQuarters() async => [];
}

void main() {
  group('Field-Worker Home Dashboard UX', () {
    testWidgets(
      'renders quick action buttons: Scan QR, Enter Code, and Search',
      (tester) async {
        bool scanCalled = false;
        bool codeCalled = false;
        bool searchCalled = false;

        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: HomePage(
                user: createTestUser(),
                onScanQr: () => scanCalled = true,
                onEnterAssetCode: () => codeCalled = true,
                onSearchAssets: () => searchCalled = true,
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.text('Identify Asset'), findsOneWidget);
        expect(find.byKey(const Key('scan-asset-qr')), findsOneWidget);
        expect(find.byKey(const Key('enter-asset-code')), findsOneWidget);
        expect(find.byKey(const Key('search-assets')), findsOneWidget);

        await tester.tap(find.byKey(const Key('scan-asset-qr')));
        expect(scanCalled, isTrue);

        await tester.tap(find.byKey(const Key('enter-asset-code')));
        expect(codeCalled, isTrue);

        await tester.tap(find.byKey(const Key('search-assets')));
        expect(searchCalled, isTrue);
      },
    );

    testWidgets(
      'displays active draft batch progress cards and submitted batches',
      (tester) async {
        final draftForm = createTestForm(
          id: 'draft-form-1',
          status: 'Draft',
          rowCount: 2,
        );
        final submittedForm = createTestForm(
          id: 'sub-form-2',
          status: 'Submitted',
          rowCount: 3,
        );
        final schedules = [
          createTestSchedule(id: 's1', assetId: 'a1', assetCode: 'FE-01'),
          createTestSchedule(id: 's2', assetId: 'a2', assetCode: 'FE-02'),
          createTestSchedule(id: 's3', assetId: 'a3', assetCode: 'FE-03'),
        ];

        final repository = FakeWorkflowPmRepository(
          forms: [draftForm, submittedForm],
          schedules: schedules,
        );

        String? openedFormId;

        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: HomePage(
                user: createTestUser(),
                preventiveMaintenanceRepository: repository,
                onOpenForm: (id) => openedFormId = id,
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();

        expect(find.text('My PM Tasks'), findsOneWidget);
        expect(find.text('2 of 3 assets inspected'), findsOneWidget);
        expect(find.text('Continue PM batch'), findsOneWidget);
        await tester.ensureVisible(find.text('Continue PM batch'));
        await tester.pumpAndSettle();
        await tester.tap(find.text('Continue PM batch'));
        await tester.pumpAndSettle();
        expect(openedFormId, 'draft-form-1');

        await tester.ensureVisible(find.text('Capture Signature'));
        await tester.pumpAndSettle();
        expect(find.text('Awaiting Acknowledgement'), findsOneWidget);
        expect(find.text('Capture Signature'), findsOneWidget);
      },
    );
  });

  group('Inspection Completion Bottom Sheet', () {
    testWidgets('shows inspection confirmation, batch progress, and actions', (
      tester,
    ) async {
      bool nextAssetCalled = false;
      bool viewBatchCalled = false;

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: Builder(
              builder: (context) => FilledButton(
                onPressed: () => InspectionCompletionSheet.show(
                  context,
                  assetCode: 'FE-CS-005',
                  department: 'College of Science',
                  assetCategory: 'fire-extinguisher',
                  pmCycle: '2026-06',
                  completedCount: 4,
                  totalCount: 6,
                  onNextAsset: () => nextAssetCalled = true,
                  onViewBatch: () => viewBatchCalled = true,
                ),
                child: const Text('Open Sheet'),
              ),
            ),
          ),
        ),
      );

      await tester.tap(find.text('Open Sheet'));
      await tester.pumpAndSettle();

      expect(find.text('Inspection Recorded'), findsOneWidget);
      expect(
        find.text('Asset FE-CS-005 successfully inspected.'),
        findsOneWidget,
      );
      expect(find.text('Department: College of Science'), findsOneWidget);
      expect(find.text('4 of 6 assets inspected'), findsOneWidget);
      expect(find.text('67%'), findsOneWidget);
      expect(find.byKey(const Key('sheet-next-asset')), findsOneWidget);
      expect(find.byKey(const Key('sheet-view-batch')), findsOneWidget);

      await tester.tap(find.byKey(const Key('sheet-next-asset')));
      expect(nextAssetCalled, isTrue);

      await tester.tap(find.byKey(const Key('sheet-view-batch')));
      expect(viewBatchCalled, isTrue);
    });
  });

  group('Authoritative Batch Identity & Grouping', () {
    test('Department + AssetCategory + PmCycle grouping matches correctly', () {
      const asset = Asset(
        id: 'asset-uuid',
        assetCode: 'FE-01',
        assetCategory: 'fire-extinguisher',
        building: 'Building A',
        department: 'Engineering',
        location: 'Lobby',
        qrCodeValue: 'UNIPM-FE-1',
        status: 'Active',
      );

      final schedule = createTestSchedule(
        id: 'sched-1',
        assetId: 'asset-uuid',
        assetCode: 'FE-01',
        department: 'Engineering',
        pmCycle: '2026-06',
      );

      final grouping = PreventiveMaintenanceGrouping.fromAssetAndSchedule(
        asset,
        schedule,
      );

      final matchingForm = createTestForm(
        id: 'form-1',
        department: 'Engineering',
        assetCategory: 'fire-extinguisher',
        pmCycle: '2026-06',
      );

      final differentCycleForm = createTestForm(
        id: 'form-2',
        department: 'Engineering',
        assetCategory: 'fire-extinguisher',
        pmCycle: '2026-09',
      );

      final differentDeptForm = createTestForm(
        id: 'form-3',
        department: 'Nursing',
        assetCategory: 'fire-extinguisher',
        pmCycle: '2026-06',
      );

      expect(grouping.matches(matchingForm), isTrue);
      expect(grouping.matches(differentCycleForm), isFalse);
      expect(grouping.matches(differentDeptForm), isFalse);
    });
  });
}
