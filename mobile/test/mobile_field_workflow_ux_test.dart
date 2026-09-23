import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:mobile/auth/auth_models.dart';
import 'package:mobile/features/auth/home_page.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_models.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_repository.dart';
import 'package:mobile/ui/display_labels.dart';

void main() {
  test('formats backend category codes for display without changing codes', () {
    expect(displayAssetCategory('fire-alarm'), 'Fire Alarm');
    expect(
      displayAssetCategory('water_drinking_station'),
      'Water Drinking Station',
    );
    expect(displayAssetCategory(' fire-extinguisher '), 'Fire Extinguisher');
  });

  testWidgets('Inspector user does NOT see "Preventive-maintenance forms"', (
    tester,
  ) async {
    const user = AuthUser(
      id: '11111111-1111-4111-8111-111111111111',
      email: 'inspector@example.test',
      displayName: 'Synthetic Inspector',
      roles: ['Inspector'],
    );

    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: HomePage(user: user, onOpenPreventiveMaintenance: _noop),
        ),
      ),
    );

    expect(find.text('Preventive-maintenance forms'), findsNothing);
    expect(
      find.text('Create, resume, submit, and acknowledge PM forms.'),
      findsNothing,
    );
  });

  testWidgets('GSD user DOES see "Preventive-maintenance forms"', (
    tester,
  ) async {
    const gsdUser = AuthUser(
      id: '22222222-2222-4222-8222-222222222222',
      email: 'gsd@example.test',
      displayName: 'GSD Officer',
      roles: ['GSD'],
    );

    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: HomePage(user: gsdUser, onOpenPreventiveMaintenance: _noop),
        ),
      ),
    );

    expect(find.text('Preventive-maintenance forms'), findsOneWidget);
    expect(
      find.text('Create, resume, submit, and acknowledge PM forms.'),
      findsOneWidget,
    );
  });

  testWidgets(
    'My PM Tasks surfaces assigned Due/Ongoing/Overdue schedules even when no draft exists',
    (tester) async {
      const inspectorId = '11111111-1111-4111-8111-111111111111';
      const user = AuthUser(
        id: inspectorId,
        email: 'inspector@example.test',
        displayName: 'Assigned Inspector',
        roles: ['Inspector'],
      );

      final schedules = [
        ScheduleOption(
          id: 'sched-1',
          assetId: 'asset-1',
          scheduleDate: DateTime(2026, 6, 15),
          pmCycle: '2026-06',
          periodType: 'Quarter',
          status: 'Due',
          quarter: 'Q2',
          semester: null,
          year: 2026,
          academicYear: '2025-2026',
          assignedToUserId: inspectorId,
          asset: const ScheduleAssetOption(
            id: 'asset-1',
            assetCode: 'FE-01',
            assetCategory: 'fire-extinguisher',
            building: 'Science Hall',
            department: 'College of Science',
            location: 'Room 101',
          ),
        ),
        ScheduleOption(
          id: 'sched-2',
          assetId: 'asset-2',
          scheduleDate: DateTime(2026, 6, 15),
          pmCycle: '2026-06',
          periodType: 'Quarter',
          status: 'Ongoing',
          quarter: 'Q2',
          semester: null,
          year: 2026,
          academicYear: '2025-2026',
          assignedToUserId: inspectorId,
          asset: const ScheduleAssetOption(
            id: 'asset-2',
            assetCode: 'FE-02',
            assetCategory: 'fire-extinguisher',
            building: 'Science Hall',
            department: 'College of Science',
            location: 'Room 102',
          ),
        ),
        ScheduleOption(
          id: 'sched-cancelled',
          assetId: 'asset-3',
          scheduleDate: DateTime(2026, 6, 15),
          pmCycle: '2026-06',
          periodType: 'Quarter',
          status: 'Cancelled',
          quarter: 'Q2',
          semester: null,
          year: 2026,
          academicYear: '2025-2026',
          assignedToUserId: inspectorId,
          asset: const ScheduleAssetOption(
            id: 'asset-3',
            assetCode: 'FE-03',
            assetCategory: 'fire-extinguisher',
            building: 'Science Hall',
            department: 'College of Science',
            location: 'Room 103',
          ),
        ),
      ];

      final repo = _FakePmRepository(forms: [], schedules: schedules);

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: HomePage(
              user: user,
              preventiveMaintenanceRepository: repo,
              onScanQr: _noop,
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('My PM Tasks'), findsOneWidget);
      expect(find.text('No active PM batches'), findsNothing);
      expect(find.text('Fire Extinguisher'), findsOneWidget);
      expect(find.text('College of Science'), findsOneWidget);
      expect(find.text('Cycle: 2026-06'), findsOneWidget);
      // Cancelled schedule is excluded from batch total (2 instead of 3)
      expect(find.text('0 of 2 assets inspected'), findsOneWidget);
      expect(find.text('Start inspection'), findsOneWidget);
    },
  );

  testWidgets(
    'My PM Tasks strictly isolates assignments and maintains GSD visibility',
    (tester) async {
      const inspectorAliceId = '11111111-1111-4111-8111-111111111111';
      const inspectorBobId = '22222222-2222-4222-8222-222222222222';
      const gsdId = '33333333-3333-4333-8333-333333333333';

      const aliceUser = AuthUser(
        id: inspectorAliceId,
        email: 'alice@example.test',
        displayName: 'Inspector Alice',
        roles: ['Inspector'],
      );

      const gsdUser = AuthUser(
        id: gsdId,
        email: 'gsd@example.test',
        displayName: 'GSD Supervisor',
        roles: ['GSD'],
      );

      final schedules = [
        // Assigned to Alice
        ScheduleOption(
          id: 'sched-alice',
          assetId: 'asset-alice',
          scheduleDate: DateTime(2026, 6, 15),
          pmCycle: '2026-06',
          periodType: 'Quarter',
          status: 'Due',
          quarter: 'Q2',
          semester: null,
          year: 2026,
          academicYear: '2025-2026',
          assignedToUserId: inspectorAliceId,
          asset: const ScheduleAssetOption(
            id: 'asset-alice',
            assetCode: 'ALICE-01',
            assetCategory: 'fire-extinguisher',
            building: 'Physics Wing',
            department: 'Department of Physics',
            location: 'Lab 1',
          ),
        ),
        // Assigned to Bob
        ScheduleOption(
          id: 'sched-bob',
          assetId: 'asset-bob',
          scheduleDate: DateTime(2026, 6, 15),
          pmCycle: '2026-06',
          periodType: 'Quarter',
          status: 'Due',
          quarter: 'Q2',
          semester: null,
          year: 2026,
          academicYear: '2025-2026',
          assignedToUserId: inspectorBobId,
          asset: const ScheduleAssetOption(
            id: 'asset-bob',
            assetCode: 'BOB-01',
            assetCategory: 'emergency-light',
            building: 'Chemistry Wing',
            department: 'Department of Chemistry',
            location: 'Lab 2',
          ),
        ),
        // Unassigned, which requires GSD assignment before field work.
        ScheduleOption(
          id: 'sched-unassigned',
          assetId: 'asset-unassigned',
          scheduleDate: DateTime(2026, 6, 15),
          pmCycle: '2026-06',
          periodType: 'Quarter',
          status: 'Due',
          quarter: 'Q2',
          semester: null,
          year: 2026,
          academicYear: '2025-2026',
          assignedToUserId: null,
          asset: const ScheduleAssetOption(
            id: 'asset-unassigned',
            assetCode: 'UNASSIGNED-01',
            assetCategory: 'water-drinking-station',
            building: 'Biology Wing',
            department: 'Department of Biology',
            location: 'Hallway',
          ),
        ),
      ];

      final repo = _FakePmRepository(forms: [], schedules: schedules);
      PmBatchScope? startedBatch;

      // 1. Test Inspector Alice visibility
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: HomePage(
              user: aliceUser,
              preventiveMaintenanceRepository: repo,
              onScanQr: _noop,
              onStartBatch: (scope) => startedBatch = scope,
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      // Alice sees her assigned schedule under My PM Tasks
      expect(find.text('Department of Physics'), findsOneWidget);

      // Alice does NOT see Bob's assigned schedule
      expect(find.text('Department of Chemistry'), findsNothing);
      // Unassigned work also remains unavailable until GSD assigns a worker.
      expect(find.text('Department of Biology'), findsNothing);
      expect(find.text('Available PM Tasks'), findsNothing);

      final startInspection = find.text('Start inspection').first;
      await tester.ensureVisible(startInspection);
      await tester.pumpAndSettle();
      await tester.tap(startInspection);
      expect(startedBatch?.department, 'Department of Physics');
      expect(startedBatch?.assetCategory, 'fire-extinguisher');
      expect(startedBatch?.pmCycle, '2026-06');
      // 2. Test GSD visibility: GSD sees all relevant schedules under My PM Tasks
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: HomePage(
              user: gsdUser,
              preventiveMaintenanceRepository: repo,
              onScanQr: _noop,
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Department of Physics'), findsOneWidget);
      expect(find.text('Department of Chemistry'), findsOneWidget);
      await tester.scrollUntilVisible(
        find.text('Department of Biology'),
        200,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.pumpAndSettle();
      expect(find.text('Department of Biology'), findsOneWidget);
      // GSD does not separate unassigned into "Available PM Tasks"
      expect(find.text('Available PM Tasks'), findsNothing);
    },
  );
}

void _noop() {}

class _FakePmRepository implements PreventiveMaintenanceRepository {
  _FakePmRepository({this.forms = const [], this.schedules = const []});

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
  Future<List<ReferenceOption>> listQuarters() async => [];

  @override
  Future<List<ReferenceOption>> listPeriodTypes() async => [];
}
