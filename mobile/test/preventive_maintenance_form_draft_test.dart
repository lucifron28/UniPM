import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:mobile/api/api_exception.dart';
import 'package:mobile/auth/auth_models.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_models.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_page.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_repository.dart';

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

  testWidgets('duplicate schedules are blocked before another API write', (
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
    await scrollTo(tester, find.byKey(const Key('inspection-schedule')));
    await chooseDropdown(tester, const Key('inspection-schedule'), 'FE-001');
    await scrollTo(tester, find.byKey(const Key('add-inspection-button')));
    await tester.tap(find.byKey(const Key('add-inspection-button')));
    await tester.pumpAndSettle();

    expect(repository.addCallCount, 0);
  });

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
  String createdByUserId = inspectorId,
  List<PreventiveMaintenanceInspection> inspections = const [],
  String status = 'Draft',
}) {
  final now = DateTime.utc(2026, 1, 15);
  return PreventiveMaintenanceForm(
    id: id,
    fileNumber: null,
    assetCategory: 'fire-extinguisher',
    building: 'Main Building',
    department: 'GSD',
    periodType: 'Quarter',
    quarter: 'Q1',
    semester: null,
    year: 2026,
    academicYear: '2026-2027',
    status: status,
    createdByUserId: createdByUserId,
    submittedByUserId: null,
    submittedAt: null,
    createdAt: now,
    updatedAt: now,
    inspections: inspections,
  );
}

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

class FakePreventiveMaintenanceRepository
    implements PreventiveMaintenanceRepository {
  FakePreventiveMaintenanceRepository({
    List<PreventiveMaintenanceForm>? forms,
    this.referenceFailures = 0,
  }) : forms = [...?forms];

  List<PreventiveMaintenanceForm> forms;
  int referenceFailures;
  CreatePreventiveMaintenanceFormInput? createdInput;
  AddInspectionInput? addedInput;
  UpdateInspectionInput? updatedInput;
  String? deletedInspectionId;
  int addCallCount = 0;

  @override
  Future<List<PreventiveMaintenanceForm>> listForms() async => forms;

  @override
  Future<PreventiveMaintenanceForm> getForm(String id) async {
    return forms.singleWhere((candidate) => candidate.id == id);
  }

  @override
  Future<PreventiveMaintenanceForm> createForm(
    CreatePreventiveMaintenanceFormInput input,
  ) async {
    createdInput = input;
    final created = testForm(id: '99999999-9999-4999-8999-999999999999');
    forms = [created, ...forms];
    return created;
  }

  @override
  Future<List<ScheduleOption>> listSchedules() async => [
    testSchedule(firstScheduleId, 'FE-001'),
    testSchedule(secondScheduleId, 'FE-002'),
  ];

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

ScheduleOption testSchedule(String id, String assetCode) => ScheduleOption(
  id: id,
  assetId: '88888888-8888-4888-8888-888888888888',
  scheduleDate: DateTime.utc(2026, 1, 10),
  periodType: 'Quarter',
  status: 'Due',
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
