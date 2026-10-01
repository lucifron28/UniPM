import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/auth/auth_models.dart';
import 'package:mobile/features/assets/asset_models.dart';
import 'package:mobile/features/preventive_maintenance/inspection_completion_sheet.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_models.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_repository.dart';
import 'package:mobile/features/preventive_maintenance/scanned_asset_pm_entry.dart';
import 'package:mobile/ui/widgets/batch_pm_card.dart';

const _assetId = '11111111-1111-4111-8111-111111111111';
const _novemberScheduleId = '22222222-2222-4222-8222-222222222222';
const _augustScheduleId = '33333333-3333-4333-8333-333333333333';
const _user = AuthUser(
  id: '44444444-4444-4444-8444-444444444444',
  email: 'gsd@example.test',
  displayName: 'Fictional GSD User',
  roles: ['GSD'],
);

const _asset = Asset(
  id: _assetId,
  assetCode: 'FE-001',
  assetCategory: 'fire-extinguisher',
  building: 'Main Building',
  department: 'GSD',
  location: 'Lobby',
  qrCodeValue: 'UNIPM-FE-001',
  status: 'Active',
);

ScheduleOption _schedule({
  required String id,
  required String pmCycle,
  required String status,
  DateTime? scheduleDate,
}) => ScheduleOption(
  id: id,
  assetId: _assetId,
  scheduleDate: scheduleDate ?? DateTime.parse('2026-11-30T12:00:00+08:00'),
  pmCycle: pmCycle,
  periodType: 'Quarter',
  status: status,
  quarter: 'Q${(int.parse(pmCycle.substring(5, 7)) - 1) ~/ 3 + 1}',
  semester: null,
  year: 2026,
  academicYear: null,
  asset: const ScheduleAssetOption(
    id: _assetId,
    assetCode: 'FE-001',
    assetCategory: 'fire-extinguisher',
    building: 'Main Building',
    department: 'GSD',
    location: 'Lobby',
  ),
);

class _PresentationRepository implements PreventiveMaintenanceRepository {
  _PresentationRepository(this.schedules);

  final List<ScheduleOption> schedules;

  @override
  Future<List<PreventiveMaintenanceForm>> listForms() async => const [];

  @override
  Future<List<ScheduleOption>> listSchedules({String? assetId}) async =>
      schedules
          .where((schedule) => assetId == null || schedule.assetId == assetId)
          .toList(growable: false);

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

Future<void> _pumpScannedEntry(
  WidgetTester tester,
  List<ScheduleOption> schedules,
) async {
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: SingleChildScrollView(
          child: ScannedAssetPmEntry(
            asset: _asset,
            repository: _PresentationRepository(schedules),
            user: _user,
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  testWidgets(
    'selected schedule uses canonical cycle and month-end over historical schedule date',
    (tester) async {
      await _pumpScannedEntry(tester, [
        _schedule(
          id: _novemberScheduleId,
          pmCycle: '2026-11',
          status: 'Due',
          scheduleDate: DateTime.parse('2026-11-01T00:00:00+08:00'),
        ),
      ]);

      expect(find.byKey(const Key('selected-pm-schedule')), findsOneWidget);
      expect(find.text('PM cycle: November 2026'), findsOneWidget);
      expect(find.text('Due date: November 30, 2026'), findsOneWidget);
      expect(find.text('Schedule status: Due'), findsOneWidget);
      expect(find.text('Due date: November 1, 2026'), findsNothing);
    },
  );

  testWidgets('canonical November cycle wins across timestamp offsets', (
    tester,
  ) async {
    final boundaryTimestamps = [
      DateTime.parse('2026-11-30T23:30:00+14:00'),
      DateTime.parse('2026-11-30T23:30:00-12:00'),
    ];

    for (final scheduleDate in boundaryTimestamps) {
      await _pumpScannedEntry(tester, [
        _schedule(
          id: _novemberScheduleId,
          pmCycle: '2026-11',
          status: 'Due',
          scheduleDate: scheduleDate,
        ),
      ]);

      expect(find.text('PM cycle: November 2026'), findsOneWidget);
      expect(find.text('Due date: November 30, 2026'), findsOneWidget);
      expect(find.text('Due date: December 1, 2026'), findsNothing);
    }
  });

  testWidgets('schedule dropdown labels show month, year, and status', (
    tester,
  ) async {
    await _pumpScannedEntry(tester, [
      _schedule(id: _novemberScheduleId, pmCycle: '2026-11', status: 'Due'),
      _schedule(
        id: _augustScheduleId,
        pmCycle: '2026-08',
        status: 'Ongoing',
        scheduleDate: DateTime.parse('2026-08-31T12:00:00+08:00'),
      ),
    ]);

    await tester.tap(find.byKey(const Key('pm-schedule-select')));
    await tester.pumpAndSettle();
    expect(find.text('November 2026 · Due'), findsOneWidget);
    expect(find.text('August 2026 · Ongoing'), findsOneWidget);

    await tester.tap(find.text('November 2026 · Due'));
    await tester.pumpAndSettle();
    expect(find.text('Due date: November 30, 2026'), findsOneWidget);
  });

  testWidgets('batch card retains a legacy cycle fallback without a due date', (
    tester,
  ) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: BatchPmCard(
            department: 'GSD',
            assetCategory: 'fire-extinguisher',
            pmCycle: 'Quarter 2026',
            status: 'Submitted',
            completedCount: 1,
            totalCount: 2,
          ),
        ),
      ),
    );

    expect(find.text('Cycle: Quarter 2026'), findsOneWidget);
    expect(find.text('Due date: Not recorded'), findsOneWidget);
  });

  testWidgets('batch card keeps inspection progress and form status', (
    tester,
  ) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: BatchPmCard(
            department: 'GSD',
            assetCategory: 'fire-extinguisher',
            pmCycle: '2026-11',
            status: 'Submitted',
            completedCount: 2,
            totalCount: 3,
          ),
        ),
      ),
    );

    expect(find.text('PM cycle: November 2026'), findsOneWidget);
    expect(find.text('Due date: November 30, 2026'), findsOneWidget);
    expect(find.text('2 of 3 assets inspected'), findsOneWidget);
    expect(find.text('67%'), findsOneWidget);
    expect(find.text('Awaiting acknowledgement'), findsOneWidget);
  });

  testWidgets('completion sheet shows cycle deadline and retained progress', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: InspectionCompletionSheet(
            assetCode: 'FE-001',
            department: 'GSD',
            assetCategory: 'fire-extinguisher',
            pmCycle: '2026-11',
            completedCount: 1,
            totalCount: 2,
            onNextAsset: () {},
            onViewBatch: () {},
          ),
        ),
      ),
    );

    expect(find.text('PM cycle: November 2026'), findsOneWidget);
    expect(find.text('Due date: November 30, 2026'), findsOneWidget);
    expect(find.text('1 of 2 assets inspected'), findsOneWidget);
    expect(find.text('50%'), findsOneWidget);
  });
}
