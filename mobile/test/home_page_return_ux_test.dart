import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/auth/auth_models.dart';
import 'package:mobile/features/auth/home_page.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_models.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_repository.dart';
import 'package:mobile/ui/widgets/batch_pm_card.dart';

const _inspectorId = '11111111-1111-4111-8111-111111111111';
const _otherInspectorId = '22222222-2222-4222-8222-222222222222';
const _draftId = '33333333-3333-4333-8333-333333333333';
const _otherDraftId = '44444444-4444-4444-8444-444444444444';

const _inspector = AuthUser(
  id: _inspectorId,
  email: 'inspector@example.test',
  displayName: 'Synthetic Inspector',
  roles: ['Inspector'],
);

const _gsdUser = AuthUser(
  id: '55555555-5555-4555-8555-555555555555',
  email: 'gsd@example.test',
  displayName: 'Synthetic GSD User',
  roles: ['GSD'],
);

class _MutablePmRepository implements PreventiveMaintenanceRepository {
  _MutablePmRepository({required this.forms, required this.schedules});

  List<PreventiveMaintenanceForm> forms;
  List<ScheduleOption> schedules;
  int listFormsCalls = 0;
  int listSchedulesCalls = 0;

  @override
  Future<List<PreventiveMaintenanceForm>> listForms() async {
    listFormsCalls++;
    return List.unmodifiable(forms);
  }

  @override
  Future<List<ScheduleOption>> listSchedules({String? assetId}) async {
    listSchedulesCalls++;
    return List.unmodifiable(
      schedules.where(
        (schedule) => assetId == null || schedule.assetId == assetId,
      ),
    );
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

PreventiveMaintenanceForm _form({
  required String id,
  String status = 'Draft',
  String pmCycle = '2026-05',
  String createdByUserId = _inspectorId,
  List<PreventiveMaintenanceInspection> inspections = const [],
}) {
  final cycleParts = pmCycle.split('-');
  final cycleYear = int.parse(cycleParts[0]);
  final cycleMonth = int.parse(cycleParts[1]);
  final quarterNumber = ((cycleMonth - 1) ~/ 3) + 1;
  final now = DateTime.utc(cycleYear, cycleMonth, 20, 8);
  final isSubmitted = status == 'Submitted';
  return PreventiveMaintenanceForm(
    id: id,
    fileNumber: 'PM-$cycleYear-${cycleMonth.toString().padLeft(2, '0')}-001',
    assetCategory: 'fire-extinguisher',
    building: 'Science Hall',
    department: 'Facilities',
    pmCycle: pmCycle,
    periodType: 'Quarter',
    quarter: 'Q$quarterNumber',
    semester: null,
    year: cycleYear,
    academicYear: '2025-2026',
    status: status,
    createdByUserId: createdByUserId,
    submittedByUserId: isSubmitted ? createdByUserId : null,
    submittedAt: isSubmitted ? now : null,
    fieldWorkCompletedAt: isSubmitted ? now : null,
    createdAt: now,
    updatedAt: now,
    inspections: inspections,
  );
}

PreventiveMaintenanceInspection _inspection(
  int index, {
  required String scheduleId,
}) {
  final completedAt = DateTime.utc(2026, 5, 20, 9 + index);
  return PreventiveMaintenanceInspection(
    id: 'inspection-$index',
    scheduleId: scheduleId,
    assetId: 'asset-$index',
    inspectorUserId: _inspectorId,
    dateInspected: completedAt,
    completedAt: completedAt,
    isOperational: true,
    remarks: 'Routine check',
    actionsRecommendations: 'None',
    createdAt: completedAt,
    updatedAt: completedAt,
  );
}

ScheduleOption _schedule({
  required String id,
  required String cycle,
  required String status,
  String department = 'Facilities',
  String assetCategory = 'fire-extinguisher',
  String assignedToUserId = _inspectorId,
}) {
  final parts = cycle.split('-');
  final year = int.parse(parts[0]);
  final month = int.parse(parts[1]);
  final quarterNumber = ((month - 1) ~/ 3) + 1;
  return ScheduleOption(
    id: id,
    assetId: '$id-asset',
    scheduleDate: DateTime.utc(year, month + 1, 0, 15, 59, 59, 999, 999),
    pmCycle: cycle,
    periodType: 'Quarter',
    status: status,
    quarter: 'Q$quarterNumber',
    semester: null,
    year: year,
    academicYear: '2025-2026',
    assignedToUserId: assignedToUserId,
    asset: ScheduleAssetOption(
      id: '$id-asset',
      assetCode: 'FE-$id',
      assetCategory: assetCategory,
      building: 'Science Hall',
      department: department,
      location: 'Laboratory',
    ),
  );
}

class _WorkflowPage extends StatelessWidget {
  const _WorkflowPage({
    required this.title,
    required this.actionLabel,
    required this.onAction,
  });

  final String title;
  final String actionLabel;
  final VoidCallback onAction;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(title)),
      body: Center(
        child: FilledButton(
          key: const Key('child-workflow-action'),
          onPressed: onAction,
          child: Text(actionLabel),
        ),
      ),
    );
  }
}

Future<void> _pushWorkflowPage(
  BuildContext context, {
  required String title,
  required String actionLabel,
  required VoidCallback onAction,
}) async {
  await Navigator.of(context).push<void>(
    MaterialPageRoute<void>(
      builder: (_) => _WorkflowPage(
        title: title,
        actionLabel: actionLabel,
        onAction: onAction,
      ),
    ),
  );
}

Future<void> _pumpHome(
  WidgetTester tester, {
  required AuthUser user,
  required _MutablePmRepository repository,
  Future<void> Function(BuildContext context, String formId)? onOpenForm,
  Future<void> Function(BuildContext context, PmBatchScope scope)? onStartBatch,
  Future<void> Function(BuildContext context, PreventiveMaintenanceForm form)?
  onOpenAcknowledgement,
  Future<void> Function(BuildContext context)? onSearchAssets,
  Future<void> Function(BuildContext context)? onOpenPreventiveMaintenance,
}) async {
  await tester.pumpWidget(
    MaterialApp(
      home: Builder(
        builder: (context) => Scaffold(
          body: HomePage(
            user: user,
            preventiveMaintenanceRepository: repository,
            onOpenForm: onOpenForm == null
                ? null
                : (formId) => onOpenForm(context, formId),
            onStartBatch: onStartBatch == null
                ? null
                : (scope) => onStartBatch(context, scope),
            onOpenAcknowledgement: onOpenAcknowledgement == null
                ? null
                : (form) => onOpenAcknowledgement(context, form),
            onSearchAssets: onSearchAssets == null
                ? null
                : () => onSearchAssets(context),
            onOpenPreventiveMaintenance: onOpenPreventiveMaintenance == null
                ? null
                : () => onOpenPreventiveMaintenance(context),
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('refreshes draft progress after the child route returns', (
    tester,
  ) async {
    final first = _inspection(1, scheduleId: 'schedule-1');
    final second = _inspection(2, scheduleId: 'schedule-2');
    final draft = _form(id: _draftId, inspections: [first, second]);
    final otherDraft = _form(
      id: _otherDraftId,
      createdByUserId: _otherInspectorId,
      inspections: [first],
    );
    final repository = _MutablePmRepository(
      forms: [draft, otherDraft],
      schedules: [
        _schedule(id: 'schedule-1', cycle: '2026-05', status: 'Completed'),
        _schedule(id: 'schedule-2', cycle: '2026-05', status: 'Completed'),
        _schedule(id: 'schedule-3', cycle: '2026-05', status: 'Due'),
      ],
    );
    String? openedFormId;

    await _pumpHome(
      tester,
      user: _inspector,
      repository: repository,
      onOpenForm: (context, formId) async {
        openedFormId = formId;
        await _pushWorkflowPage(
          context,
          title: 'Continue PM batch',
          actionLabel: 'Save inspection',
          onAction: () {
            repository.forms = [
              draft.copyWith(
                inspections: [
                  ...draft.inspections,
                  _inspection(3, scheduleId: 'schedule-3'),
                ],
              ),
              otherDraft,
            ];
            repository.schedules = [
              _schedule(
                id: 'schedule-1',
                cycle: '2026-05',
                status: 'Completed',
              ),
              _schedule(
                id: 'schedule-2',
                cycle: '2026-05',
                status: 'Completed',
              ),
              _schedule(
                id: 'schedule-3',
                cycle: '2026-05',
                status: 'Completed',
              ),
            ];
          },
        );
      },
    );

    expect(find.text('2 of 3 assets inspected'), findsOneWidget);
    expect(find.byKey(ValueKey('draft-card-$_otherDraftId')), findsNothing);
    expect(repository.listFormsCalls, 1);
    expect(repository.listSchedulesCalls, 1);

    final continueBatch = find.text('Continue PM batch').first;
    await tester.ensureVisible(continueBatch);
    await tester.pumpAndSettle();
    await tester.tap(continueBatch);
    await tester.pumpAndSettle();
    expect(find.text('Continue PM batch'), findsOneWidget);

    await tester.tap(find.byKey(const Key('child-workflow-action')));
    await tester.pumpAndSettle();
    expect(
      find.text('2 of 3 assets inspected', skipOffstage: false),
      findsOneWidget,
    );
    expect(repository.listFormsCalls, 1);
    expect(repository.listSchedulesCalls, 1);

    final backButton = find.byType(BackButton);
    expect(backButton, findsOneWidget);
    await tester.tap(backButton);
    await tester.pumpAndSettle();

    expect(openedFormId, _draftId);
    expect(repository.listFormsCalls, 2);
    expect(repository.listSchedulesCalls, 2);
    expect(find.text('3 of 3 assets inspected'), findsOneWidget);
    expect(find.byKey(ValueKey('draft-card-$_otherDraftId')), findsNothing);
  });

  testWidgets('refreshes submitted-form tasks after acknowledgement returns', (
    tester,
  ) async {
    final submitted = _form(
      id: 'submitted-form',
      status: 'Submitted',
      inspections: [
        _inspection(1, scheduleId: 'schedule-1'),
        _inspection(2, scheduleId: 'schedule-2'),
      ],
    );
    final repository = _MutablePmRepository(
      forms: [submitted],
      schedules: const [],
    );

    await _pumpHome(
      tester,
      user: _gsdUser,
      repository: repository,
      onOpenAcknowledgement: (context, form) => _pushWorkflowPage(
        context,
        title: 'Acknowledge PM form',
        actionLabel: 'Record acknowledgement',
        onAction: () {
          repository.forms = [form.copyWith(status: 'Acknowledged')];
        },
      ),
    );

    expect(find.text('Awaiting Acknowledgement'), findsOneWidget);
    expect(repository.listFormsCalls, 1);
    expect(repository.listSchedulesCalls, 1);
    final captureSignature = find.text('Capture Signature');
    await tester.ensureVisible(captureSignature);
    await tester.pumpAndSettle();
    await tester.tap(captureSignature);
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('child-workflow-action')));
    await tester.pumpAndSettle();
    expect(
      find.byKey(
        const ValueKey('ack-card-submitted-form'),
        skipOffstage: false,
      ),
      findsOneWidget,
    );
    expect(repository.listFormsCalls, 1);
    expect(repository.listSchedulesCalls, 1);
    await tester.tap(find.byType(BackButton));
    await tester.pumpAndSettle();

    expect(repository.listFormsCalls, 2);
    expect(repository.listSchedulesCalls, 2);
    expect(find.text('Awaiting Acknowledgement'), findsNothing);
    expect(find.byKey(const ValueKey('ack-card-submitted-form')), findsNothing);
    expect(find.text('No assigned PM tasks'), findsOneWidget);
  });

  testWidgets('refreshes new PM tasks after asset search returns', (
    tester,
  ) async {
    final repository = _MutablePmRepository(
      forms: const [],
      schedules: const [],
    );

    await _pumpHome(
      tester,
      user: _inspector,
      repository: repository,
      onStartBatch: (_, _) async {},
      onSearchAssets: (context) => _pushWorkflowPage(
        context,
        title: 'Search Assets',
        actionLabel: 'Return with PM task',
        onAction: () {
          repository.schedules = [
            _schedule(
              id: 'search-result-schedule',
              cycle: '2026-05',
              status: 'Due',
            ),
          ];
        },
      ),
    );

    expect(find.text('No assigned PM tasks'), findsOneWidget);
    await tester.tap(find.byKey(const Key('search-assets')));
    await tester.pumpAndSettle();

    await tester.tap(find.byKey(const Key('child-workflow-action')));
    await tester.pumpAndSettle();
    expect(
      find.text('No assigned PM tasks', skipOffstage: false),
      findsOneWidget,
    );
    expect(repository.listFormsCalls, 1);
    expect(repository.listSchedulesCalls, 1);
    await tester.tap(find.byType(BackButton));
    await tester.pumpAndSettle();

    expect(repository.listFormsCalls, 2);
    expect(repository.listSchedulesCalls, 2);
    expect(find.text('No assigned PM tasks'), findsNothing);
    expect(find.text('Start inspection'), findsOneWidget);
    expect(find.text('PM cycle: May 2026'), findsOneWidget);
  });

  testWidgets(
    'orders assigned tasks by urgency then cycle and keeps assignments',
    (tester) async {
      tester.view.physicalSize = const Size(800, 2600);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final repository = _MutablePmRepository(
        forms: [
          _form(
            id: _draftId,
            pmCycle: '2026-08',
            inspections: [_inspection(1, scheduleId: 'draft-schedule')],
          ),
        ],
        schedules: [
          _schedule(
            id: 'due-feb',
            cycle: '2026-02',
            status: 'Due',
            department: 'Student Affairs Office',
          ),
          _schedule(
            id: 'due-aug',
            cycle: '2026-08',
            status: 'Due',
            department: 'General Services Department',
          ),
          _schedule(
            id: 'ongoing-feb',
            cycle: '2026-02',
            status: 'Ongoing',
            department: 'Campus Facilities',
          ),
          _schedule(
            id: 'overdue-aug-student-affairs',
            cycle: '2026-08',
            status: 'Overdue',
            department: 'Student Affairs Office',
          ),
          _schedule(
            id: 'overdue-nov-campus-facilities',
            cycle: '2026-11',
            status: 'Overdue',
            department: 'Campus Facilities',
          ),
          _schedule(
            id: 'other-worker',
            cycle: '2026-02',
            status: 'Overdue',
            department: 'Other Department',
            assignedToUserId: _otherInspectorId,
          ),
        ],
      );

      await _pumpHome(tester, user: _inspector, repository: repository);

      final cards = tester.widgetList<BatchPmCard>(find.byType(BatchPmCard));
      expect(cards.map((card) => card.status).toList(), [
        'Overdue',
        'Overdue',
        'Ongoing',
        'In Progress',
        'Due',
        'Due',
      ]);
      expect(cards.map((card) => card.pmCycle).toList(), [
        '2026-08',
        '2026-11',
        '2026-02',
        '2026-08',
        '2026-02',
        '2026-08',
      ]);
      expect(find.text('Other Department'), findsNothing);
    },
  );

  testWidgets('refreshes active draft after the GSD PM form list returns', (
    tester,
  ) async {
    final repository = _MutablePmRepository(
      forms: const [],
      schedules: const [],
    );

    await _pumpHome(
      tester,
      user: _gsdUser,
      repository: repository,
      onOpenPreventiveMaintenance: (context) => _pushWorkflowPage(
        context,
        title: 'Preventive-maintenance forms',
        actionLabel: 'Create draft',
        onAction: () {
          repository.forms = [
            _form(
              id: 'new-admin-draft',
              createdByUserId: _gsdUser.id,
              inspections: [_inspection(1, scheduleId: 'schedule-1')],
            ),
          ];
        },
      ),
    );

    expect(find.text('No assigned PM tasks'), findsOneWidget);
    final adminForms = find.text('Preventive-maintenance forms');
    await tester.ensureVisible(adminForms);
    await tester.pumpAndSettle();
    await tester.tap(adminForms);
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('child-workflow-action')));
    await tester.pumpAndSettle();
    expect(
      find.text('No assigned PM tasks', skipOffstage: false),
      findsOneWidget,
    );
    expect(repository.listFormsCalls, 1);
    expect(repository.listSchedulesCalls, 1);
    await tester.tap(find.byType(BackButton));
    await tester.pumpAndSettle();

    expect(repository.listFormsCalls, 2);
    expect(repository.listSchedulesCalls, 2);
    expect(find.text('No assigned PM tasks'), findsNothing);
    final createdDraft = find.byKey(
      const ValueKey('draft-card-new-admin-draft'),
    );
    await tester.scrollUntilVisible(
      createdDraft,
      -250,
      scrollable: find.byType(Scrollable).first,
    );
    await tester.pumpAndSettle();
    expect(createdDraft, findsOneWidget);
    expect(find.text('1 of 1 assets inspected'), findsOneWidget);
  });
}
