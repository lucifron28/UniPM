import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/api/api_exception.dart';
import 'package:mobile/auth/auth_models.dart';
import 'package:mobile/features/assets/asset_models.dart';
import 'package:mobile/features/assets/asset_repository.dart';
import 'package:mobile/features/preventive_maintenance/offline_inspection_repository.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_controller.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_models.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_page.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_repository.dart';

const _userA = '11111111-1111-4111-8111-111111111111';
const _userB = '22222222-2222-4222-8222-222222222222';
const _formId = '33333333-3333-4333-8333-333333333333';
const _formBId = '33333333-3333-4333-8333-333333333334';
const _scheduleId = '44444444-4444-4444-8444-444444444444';
const _assetId = '55555555-5555-4555-8555-555555555555';
const _inspectionId = '66666666-6666-4666-8666-666666666666';
const _inspectionBId = '66666666-6666-4666-8666-666666666667';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  late _MemoryStore store;
  late _FakeConnectivity connectivity;
  late _FakeRemote remote;
  late OfflineInspectionRepository repository;

  OfflineInspectionRepository createRepository() => OfflineInspectionRepository(
    remote: remote,
    remoteAssets: remote,
    store: store,
    connectivity: connectivity,
  );

  Future<void> awaitRepositoryAction(
    WidgetTester tester,
    String step,
    Future<void> Function() action,
  ) async {
    final completed = await tester.runAsync<bool>(() async {
      await action().timeout(
        const Duration(seconds: 5),
        onTimeout: () => throw TimeoutException('$step did not complete.'),
      );
      return true;
    });
    expect(completed, isTrue, reason: '$step did not complete.');
  }

  Future<void> prepareConflict(WidgetTester tester) async {
    remote.forms = [
      _form(inspections: [_inspection()]),
    ];
    await awaitRepositoryAction(
      tester,
      'activate user',
      () => repository.activate(_userA),
    );
    await awaitRepositoryAction(tester, 'list forms', () async {
      await repository.listForms();
    });
    await awaitRepositoryAction(tester, 'cache schedules', () async {
      await repository.listSchedules();
    });
    connectivity.setConnected(false);
    remote.connected = false;
    await awaitRepositoryAction(tester, 'save local update', () async {
      await repository.updateInspection(
        _formId,
        _inspectionId,
        _updateInput(remarks: 'Local observation to review.'),
      );
    });
    remote.conflictNextUpdate = true;
    remote.connected = true;
    connectivity.connected = true;
    await awaitRepositoryAction(
      tester,
      'synchronize conflict',
      repository.syncNow,
    );
  }

  setUp(() {
    store = _MemoryStore();
    connectivity = _FakeConnectivity(connected: true);
    remote = _FakeRemote();
    repository = createRepository();
  });

  tearDown(() async {
    repository.dispose();
    await connectivity.dispose();
  });

  test(
    'offline draft and edits survive restart and retry without duplicates',
    () async {
      await repository.activate(_userA);
      await repository.listSchedules();
      connectivity.setConnected(false);
      remote.connected = false;

      final form = await repository.createForm(_createInput());
      final row = await repository.addInspection(form.id, _addInput());
      await expectLater(
        repository.addInspection(form.id, _addInput()),
        throwsA(isA<ApiException>()),
      );
      final updated = await repository.updateInspection(
        form.id,
        row.id,
        _updateInput(remarks: 'Battery backup requires follow-up.'),
      );
      expect(updated.remarks, 'Battery backup requires follow-up.');
      expect(repository.pendingCount, 2);

      await repository.deactivate();
      repository.dispose();
      repository = createRepository();
      await repository.activate(_userA);

      final restored = await repository.getForm(form.id);
      expect(restored.inspections.single.id, row.id);
      expect(
        restored.inspections.single.remarks,
        'Battery backup requires follow-up.',
      );
      expect(repository.pendingCount, 2);
      await _expectApiException(() => repository.submitForm(form.id));
      await _expectApiException(
        () => repository.acknowledgeForm(
          form.id,
          const AcknowledgePreventiveMaintenanceInput(
            signatoryName: 'Synthetic Reviewer',
            signatoryPosition: 'Department Head',
            signatureData: 'synthetic-signature',
            signatureContentType: 'image/png',
          ),
        ),
      );
      expect(remote.submitCalls, 0);
      expect(remote.acknowledgementCalls, 0);

      remote.connected = true;
      remote.failAcceptedAddOnce = true;
      connectivity.setConnected(true);
      await Future<void>.delayed(Duration.zero);
      await repository.syncNow();

      expect(repository.pendingCount, 0);
      expect(remote.forms.single.id, form.id);
      expect(remote.forms.single.inspections, hasLength(1));
      expect(remote.forms.single.inspections.single.id, row.id);
      expect(remote.addedInspectionIds, [row.id, row.id]);
    },
  );

  test(
    'offline cache and queue remain scoped to the authenticated user',
    () async {
      remote.connected = false;
      connectivity.setConnected(false);
      await repository.activate(_userA);
      final form = await repository.createForm(_createInput());
      expect(repository.pendingCount, 1);
      await repository.deactivate();

      await repository.activate(_userB);
      expect(repository.pendingCount, 0);
      expect(repository.hasCachedSchedules, isFalse);
      await expectLater(repository.listForms(), throwsA(isA<ApiException>()));
      await repository.deactivate();

      await repository.activate(_userA);
      expect(repository.pendingCount, 1);
      expect((await repository.getForm(form.id)).id, form.id);
    },
  );

  test(
    'cached schedules, assets, and references restore after restart offline',
    () async {
      await repository.activate(_userA);
      expect((await repository.listSchedules()).single.id, _scheduleId);
      expect(
        (await repository.listAssetCategories()).single.code,
        'fire-extinguisher',
      );
      expect((await repository.listPeriodTypes()).single.code, 'Quarter');
      expect((await repository.listQuarters()).single.code, 'Q1');

      repository.dispose();
      connectivity.setConnected(false);
      remote.connected = false;
      repository = createRepository();
      await repository.activate(_userA);

      expect(repository.hasCachedSchedules, isTrue);
      expect((await repository.listSchedules()).single.id, _scheduleId);
      expect(
        (await repository.listAssetCategories()).single.code,
        'fire-extinguisher',
      );
      expect((await repository.listPeriodTypes()).single.code, 'Quarter');
      expect((await repository.listQuarters()).single.code, 'Q1');
      expect((await repository.getByCode('FE-CCMS-001')).id, _assetId);
      expect(
        (await repository.getByQr('UNIPM-LEGACY-CUSTOM-LABEL')).id,
        _assetId,
      );
      await expectLater(
        repository.getByQr('UNIPM-FIRE-EXTINGUISHER-55555555'),
        throwsA(isA<ApiException>()),
      );
      expect(repository.hasNetwork, isFalse);
    },
  );

  test(
    'automatically syncs an offline Draft when connectivity returns',
    () async {
      await repository.activate(_userA);
      await repository.listSchedules();
      connectivity.setConnected(false);
      remote.connected = false;

      final form = await repository.createForm(_createInput());
      expect(repository.pendingCount, 1);
      expect(repository.syncState, OfflineSyncState.offline);
      expect(remote.forms, isEmpty);

      final synced = Completer<void>();
      void completeWhenSynced() {
        if (repository.pendingCount == 0 &&
            repository.syncState == OfflineSyncState.ready &&
            !synced.isCompleted) {
          synced.complete();
        }
      }

      repository.addListener(completeWhenSynced);
      remote.connected = true;
      connectivity.setConnected(true);
      try {
        await synced.future.timeout(const Duration(seconds: 2));
      } finally {
        repository.removeListener(completeWhenSynced);
      }

      expect(repository.syncState, OfflineSyncState.ready);
      expect(remote.forms.map((saved) => saved.id), [form.id]);
    },
  );

  test(
    'a server conflict preserves the local row and blocks submission',
    () async {
      remote.forms = [
        _form(inspections: [_inspection()]),
      ];
      await repository.activate(_userA);
      await repository.listForms();
      await repository.listSchedules();
      connectivity.setConnected(false);
      remote.connected = false;

      await repository.updateInspection(
        _formId,
        _inspectionId,
        _updateInput(remarks: 'Locally recorded observation.'),
      );
      remote.connected = true;
      remote.conflictNextUpdate = true;
      connectivity.setConnected(true);
      await Future<void>.delayed(Duration.zero);
      await repository.syncNow();

      expect(repository.hasConflictFor(_formId), isTrue);
      expect(repository.syncState, OfflineSyncState.attention);
      expect(
        (await repository.getForm(_formId)).inspections.single.remarks,
        'Locally recorded observation.',
      );
      expect(repository.pendingCountFor(_formId), 1);
      await _expectApiException(() => repository.submitForm(_formId));
      expect(remote.submitCalls, 0);
    },
  );

  test(
    'a conflict in one form does not stop unrelated forms from syncing',
    () async {
      remote.forms = [
        _form(inspections: [_inspection()]),
        _form(
          id: _formBId,
          inspections: [_inspection(id: _inspectionBId)],
        ),
      ];
      await repository.activate(_userA);
      await repository.listForms();
      connectivity.setConnected(false);
      remote.connected = false;
      await repository.updateInspection(
        _formId,
        _inspectionId,
        _updateInput(remarks: 'Local conflict.'),
      );
      await repository.updateInspection(
        _formBId,
        _inspectionBId,
        _updateInput(remarks: 'Independent update.'),
      );

      remote.conflictNextUpdate = true;
      remote.connected = true;
      connectivity.setConnected(true);
      await Future<void>.delayed(Duration.zero);
      await repository.syncNow();

      expect(repository.pendingCountFor(_formId), 1);
      expect(repository.hasConflictFor(_formId), isTrue);
      expect(repository.pendingCountFor(_formBId), 0);
      expect(
        remote.forms
            .singleWhere((form) => form.id == _formBId)
            .inspections
            .single
            .remarks,
        'Independent update.',
      );
    },
  );

  test('retry uses the reviewed current server version', () async {
    remote.forms = [
      _form(inspections: [_inspection()]),
    ];
    await repository.activate(_userA);
    await repository.listForms();
    connectivity.setConnected(false);
    remote.connected = false;
    await repository.updateInspection(
      _formId,
      _inspectionId,
      _updateInput(remarks: 'Keep my local observation.'),
    );

    remote.conflictNextUpdate = true;
    remote.connected = true;
    connectivity.setConnected(true);
    await Future<void>.delayed(Duration.zero);
    await repository.syncNow();
    final conflict = repository.conflictsForForm(_formId).single;
    expect(conflict.localInspection?.remarks, 'Keep my local observation.');
    expect(conflict.canRetry, isTrue);

    remote.forms = [
      _form(
        inspections: [
          _inspection(
            remarks: 'Server changed this row.',
            updatedAt: DateTime.utc(2026, 2, 21),
          ),
        ],
      ),
    ];
    await repository.retryConflict(conflict.writeId);

    expect(repository.pendingCount, 0);
    expect(
      remote.forms.single.inspections.single.remarks,
      'Keep my local observation.',
    );
    expect(remote.updateExpectedVersions.last, DateTime.utc(2026, 2, 21));
  });

  test(
    'discard restores the latest server row and removes the local write',
    () async {
      remote.forms = [
        _form(inspections: [_inspection()]),
      ];
      await repository.activate(_userA);
      await repository.listForms();
      connectivity.setConnected(false);
      remote.connected = false;
      await repository.updateInspection(
        _formId,
        _inspectionId,
        _updateInput(remarks: 'Discard this local change.'),
      );

      remote.conflictNextUpdate = true;
      remote.connected = true;
      connectivity.setConnected(true);
      await Future<void>.delayed(Duration.zero);
      await repository.syncNow();
      final conflict = repository.conflictsForForm(_formId).single;
      remote.forms = [
        _form(
          inspections: [
            _inspection(
              remarks: 'Keep the newer server version.',
              updatedAt: DateTime.utc(2026, 2, 21),
            ),
          ],
        ),
      ];

      await repository.discardConflict(conflict.writeId);

      expect(repository.pendingCount, 0);
      expect(
        (await repository.getForm(_formId)).inspections.single.remarks,
        'Keep the newer server version.',
      );
    },
  );

  test(
    'supplied optimistic versions are sent for updates and deletions',
    () async {
      final staleVersion = DateTime.utc(2026, 2, 19);
      remote.forms = [
        _form(inspections: [_inspection()]),
      ];
      await repository.activate(_userA);
      await repository.listForms();
      connectivity.setConnected(false);
      remote.connected = false;
      await repository.updateInspection(
        _formId,
        _inspectionId,
        _updateInput(remarks: 'Stale update.', expectedUpdatedAt: staleVersion),
      );
      remote.connected = true;
      connectivity.setConnected(true);
      await Future<void>.delayed(Duration.zero);
      await repository.syncNow();
      expect(remote.updateExpectedVersions.last, staleVersion);
      expect(repository.hasConflictFor(_formId), isTrue);
    },
  );

  test('supplied optimistic version is sent for deletion', () async {
    final staleVersion = DateTime.utc(2026, 2, 19);
    remote.forms = [
      _form(inspections: [_inspection()]),
    ];
    await repository.activate(_userA);
    await repository.listForms();
    connectivity.setConnected(false);
    remote.connected = false;
    await repository.deleteInspectionIfUnchanged(
      _formId,
      _inspectionId,
      staleVersion,
    );
    remote.connected = true;
    connectivity.setConnected(true);
    await Future<void>.delayed(Duration.zero);
    await repository.syncNow();
    expect(remote.deleteExpectedVersions.last, staleVersion);
    expect(repository.hasConflictFor(_formId), isTrue);
  });

  testWidgets(
    'conflict details and actions remain visible while schedules load',
    (tester) async {
      await prepareConflict(tester);
      expect(repository.hasConflictFor(_formId), isTrue);
      expect(repository.pendingCountFor(_formId), 1);

      final pendingSchedules = Completer<List<ScheduleOption>>();
      remote.pendingScheduleResponse = pendingSchedules;
      final controller = PreventiveMaintenanceController(
        repository: repository,
        user: const AuthUser(
          id: _userA,
          email: 'inspector@example.test',
          displayName: 'Synthetic Inspector',
          roles: ['Inspector'],
        ),
      );
      addTearDown(controller.dispose);
      final selectedForm = await tester.runAsync(
        () => repository.getForm(_formId).timeout(const Duration(seconds: 5)),
      );
      controller.selectForm(selectedForm!);

      await tester.pumpWidget(
        MaterialApp(
          home: PreventiveMaintenanceDraftPage(
            controller: controller,
            formId: _formId,
          ),
        ),
      );
      await tester.pump();
      expect(find.text('This draft needs sync review'), findsOneWidget);
      expect(find.byKey(const Key('schedules-loading')), findsOneWidget);
      await tester.tap(find.text('Update inspection needs review'));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 300));
      expect(
        find.textContaining('Local observation to review.'),
        findsOneWidget,
      );
      expect(find.text('Retry my saved change'), findsOneWidget);
      expect(find.text('Discard local change'), findsOneWidget);

      await tester.pumpWidget(const MaterialApp(home: SizedBox.shrink()));
      pendingSchedules.complete(remote.schedules);
      await tester.pump();
      await tester.pump();
    },
  );

  testWidgets('conflict details can be reviewed and safely discarded', (
    tester,
  ) async {
    repository.dispose();
    repository = _ImmediateScheduleOfflineInspectionRepository(
      remote: remote,
      remoteAssets: remote,
      store: store,
      connectivity: connectivity,
    );
    await prepareConflict(tester);
    expect(repository.hasConflictFor(_formId), isTrue);
    expect(repository.pendingCountFor(_formId), 1);
    final conflict = repository.conflictsForForm(_formId).single;

    final controller = PreventiveMaintenanceController(
      repository: repository,
      user: const AuthUser(
        id: _userA,
        email: 'inspector@example.test',
        displayName: 'Synthetic Inspector',
        roles: ['Inspector'],
      ),
    );
    addTearDown(controller.dispose);
    final selectedForm = await tester.runAsync(
      () => repository.getForm(_formId).timeout(const Duration(seconds: 5)),
    );
    controller.selectForm(selectedForm!);
    await tester.pumpWidget(
      MaterialApp(
        home: PreventiveMaintenanceDraftPage(
          controller: controller,
          formId: _formId,
        ),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('This draft needs sync review'), findsOneWidget);
    expect(find.byKey(const Key('schedules-loading')), findsNothing);
    await tester.tap(find.text('Update inspection needs review'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));
    expect(find.textContaining('Local observation to review.'), findsOneWidget);
    expect(find.text('Retry my saved change'), findsOneWidget);
    expect(find.text('Discard local change'), findsOneWidget);

    await tester.tap(
      find.byKey(Key('offline-conflict-discard-${conflict.writeId}')),
    );
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));
    expect(find.text('Discard this saved change?'), findsOneWidget);
    await tester.tap(
      find.descendant(
        of: find.byType(AlertDialog),
        matching: find.text('Discard local change'),
      ),
    );
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 300));
    await tester.pumpAndSettle();
    final restoredForm = await tester.runAsync(
      () => repository.getForm(_formId).timeout(const Duration(seconds: 5)),
    );
    expect(repository.pendingCount, 0);
    expect(
      restoredForm!.inspections.single.remarks,
      'Existing server observation.',
    );
  });

  test(
    'deleting a queued row preserves ordered create and delete writes',
    () async {
      remote.schedules[0] = _schedule(
        scheduleDate: DateTime.now().toUtc().add(const Duration(days: 30)),
      );
      await repository.activate(_userA);
      await repository.listSchedules();
      connectivity.setConnected(false);
      remote.connected = false;

      final form = await repository.createForm(_createInput());
      final row = await repository.addInspection(form.id, _addInput());
      await repository.deleteInspection(form.id, row.id);
      expect(repository.pendingCount, 3);
      expect((await repository.getForm(form.id)).inspections, isEmpty);

      remote.connected = true;
      connectivity.setConnected(true);
      await Future<void>.delayed(Duration.zero);
      await repository.syncNow();

      expect(repository.pendingCount, 0);
      expect(remote.forms.single.inspections, isEmpty);
      expect(remote.schedules.single.status, 'Due');
      expect(remote.addedInspectionIds, [row.id]);
      expect(remote.deletedInspectionIds, [row.id]);
    },
  );
}

Future<void> _expectApiException<T>(Future<T> Function() action) async {
  try {
    await action();
  } on ApiException {
    return;
  }
  fail('Expected the operation to be blocked with an ApiException.');
}

CreatePreventiveMaintenanceFormInput _createInput() =>
    const CreatePreventiveMaintenanceFormInput(
      assetCategory: 'fire-extinguisher',
      building: 'CCMS Building',
      department: 'CCMS',
      periodType: 'Quarter',
      quarter: 'Q1',
      semester: null,
      year: 2026,
      academicYear: '2025-2026',
    );

AddInspectionInput _addInput() => AddInspectionInput(
  scheduleId: _scheduleId,
  inspectorUserId: _userA,
  dateInspected: DateTime.utc(2026, 2, 20),
  isOperational: false,
  remarks: 'Initial observation.',
  actionsRecommendations: 'GSD follow-up required.',
);

UpdateInspectionInput _updateInput({
  required String remarks,
  DateTime? expectedUpdatedAt,
}) => UpdateInspectionInput(
  expectedUpdatedAt: expectedUpdatedAt,
  dateInspected: DateTime.utc(2026, 2, 20),
  isOperational: false,
  remarks: remarks,
  actionsRecommendations: 'GSD follow-up required.',
);

PreventiveMaintenanceForm _form({
  String id = _formId,
  required List<PreventiveMaintenanceInspection> inspections,
}) => PreventiveMaintenanceForm(
  id: id,
  fileNumber: null,
  assetCategory: 'fire-extinguisher',
  building: 'CCMS Building',
  department: 'CCMS',
  pmCycle: '2026-02',
  periodType: 'Quarter',
  quarter: 'Q1',
  semester: null,
  year: 2026,
  academicYear: '2025-2026',
  status: 'Draft',
  createdByUserId: _userA,
  submittedByUserId: null,
  submittedAt: null,
  createdAt: DateTime.utc(2026, 2, 20),
  updatedAt: DateTime.utc(2026, 2, 20),
  inspections: inspections,
);

PreventiveMaintenanceInspection _inspection({
  String id = _inspectionId,
  String remarks = 'Existing server observation.',
  DateTime? updatedAt,
}) => PreventiveMaintenanceInspection(
  id: id,
  scheduleId: _scheduleId,
  assetId: _assetId,
  assetCode: 'FE-CCMS-001',
  inspectorUserId: _userA,
  dateInspected: DateTime.utc(2026, 2, 20),
  completedAt: DateTime.utc(2026, 2, 20, 8),
  isOperational: false,
  remarks: remarks,
  actionsRecommendations: 'GSD follow-up required.',
  createdAt: DateTime.utc(2026, 2, 20),
  updatedAt: updatedAt ?? DateTime.utc(2026, 2, 20),
);

ScheduleOption _schedule({String status = 'Due', DateTime? scheduleDate}) =>
    ScheduleOption(
      id: _scheduleId,
      assetId: _assetId,
      scheduleDate: scheduleDate ?? DateTime.utc(2026, 2, 28, 15, 59, 59),
      pmCycle: '2026-02',
      periodType: 'Quarter',
      status: status,
      quarter: 'Q1',
      semester: null,
      year: 2026,
      academicYear: '2025-2026',
      assignedToUserId: _userA,
      asset: const ScheduleAssetOption(
        id: _assetId,
        assetCode: 'FE-CCMS-001',
        assetCategory: 'fire-extinguisher',
        building: 'CCMS Building',
        department: 'CCMS',
        location: 'CL-01',
        status: 'Active',
        qrCodeValue: 'UNIPM-LEGACY-CUSTOM-LABEL',
      ),
    );

class _MemoryStore implements OfflineSnapshotStore {
  final Map<String, String> values = <String, String>{};

  @override
  Future<String?> read(String userId) async => values[userId];

  @override
  Future<void> write(String userId, String json) async => values[userId] = json;

  @override
  Future<void> delete(String userId) async => values.remove(userId);
}

class _ImmediateScheduleOfflineInspectionRepository
    extends OfflineInspectionRepository {
  _ImmediateScheduleOfflineInspectionRepository({
    required super.remote,
    required super.remoteAssets,
    required super.store,
    required super.connectivity,
  });

  @override
  Future<List<ScheduleOption>> listSchedules({String? assetId}) async =>
      const <ScheduleOption>[];
}

class _FakeConnectivity implements OfflineConnectivityMonitor {
  _FakeConnectivity({required this.connected});

  final StreamController<bool> _controller = StreamController<bool>.broadcast(
    sync: true,
  );
  bool connected;

  @override
  Stream<bool> get changes => _controller.stream;

  @override
  Future<bool> hasNetwork() async => connected;

  void setConnected(bool value) {
    connected = value;
    _controller.add(value);
  }

  Future<void> dispose() => _controller.close();
}

class _FakeRemote
    implements
        PreventiveMaintenanceRepository,
        OfflineSafePreventiveMaintenanceRepository,
        AssetRepository {
  bool connected = true;
  bool failAcceptedAddOnce = false;
  bool conflictNextUpdate = false;
  Completer<List<ScheduleOption>>? pendingScheduleResponse;
  int submitCalls = 0;
  int acknowledgementCalls = 0;
  final List<String> addedInspectionIds = <String>[];
  final List<String> deletedInspectionIds = <String>[];
  final List<DateTime> updateExpectedVersions = <DateTime>[];
  final List<DateTime?> deleteExpectedVersions = <DateTime?>[];
  List<PreventiveMaintenanceForm> forms = <PreventiveMaintenanceForm>[];
  final List<ScheduleOption> schedules = <ScheduleOption>[_schedule()];
  final Map<String, Asset> assets = <String, Asset>{};

  void _checkConnected() {
    if (!connected) {
      throw const ApiException(message: 'The mobile service is unavailable.');
    }
  }

  @override
  Future<List<PreventiveMaintenanceForm>> listForms() async {
    _checkConnected();
    return List<PreventiveMaintenanceForm>.of(forms);
  }

  @override
  Future<PreventiveMaintenanceForm> getForm(String id) async {
    _checkConnected();
    return forms.where((form) => form.id == id).firstOrNull ??
        (throw const ApiException(statusCode: 404, message: 'Form missing.'));
  }

  @override
  Future<PreventiveMaintenanceForm> createForm(
    CreatePreventiveMaintenanceFormInput input,
  ) => createFormWithId(input, input.id ?? _formId);

  @override
  Future<PreventiveMaintenanceForm> createFormWithId(
    CreatePreventiveMaintenanceFormInput input,
    String id,
  ) async {
    _checkConnected();
    final existing = forms.where((form) => form.id == id).firstOrNull;
    if (existing != null) return existing;
    final now = DateTime.utc(2026, 2, 20, 9);
    final created = PreventiveMaintenanceForm(
      id: id,
      fileNumber: null,
      assetCategory: input.assetCategory,
      building: input.building,
      department: input.department,
      pmCycle: null,
      periodType: input.periodType,
      quarter: input.quarter,
      semester: input.semester,
      year: input.year,
      academicYear: input.academicYear,
      status: 'Draft',
      createdByUserId: _userA,
      submittedByUserId: null,
      submittedAt: null,
      createdAt: now,
      updatedAt: now,
      inspections: const <PreventiveMaintenanceInspection>[],
    );
    forms = [...forms, created];
    return created;
  }

  @override
  Future<PreventiveMaintenanceForm> submitForm(String formId) async {
    _checkConnected();
    submitCalls++;
    final current = await getForm(formId);
    final submitted = current.copyWith(status: 'Submitted');
    forms = forms.map((form) => form.id == formId ? submitted : form).toList();
    return submitted;
  }

  @override
  Future<PreventiveMaintenanceAcknowledgement> acknowledgeForm(
    String formId,
    AcknowledgePreventiveMaintenanceInput input,
  ) async {
    _checkConnected();
    acknowledgementCalls++;
    throw UnimplementedError();
  }

  @override
  Future<List<ScheduleOption>> listSchedules({String? assetId}) async {
    _checkConnected();
    final pendingResponse = pendingScheduleResponse;
    final scheduleResults = pendingResponse == null
        ? schedules
        : await pendingResponse.future;
    return scheduleResults
        .where((schedule) => assetId == null || schedule.assetId == assetId)
        .toList(growable: false);
  }

  @override
  Future<List<ReferenceOption>> listAssetCategories() async {
    _checkConnected();
    return const <ReferenceOption>[
      ReferenceOption(
        code: 'fire-extinguisher',
        displayName: 'Fire extinguisher',
      ),
    ];
  }

  @override
  Future<List<ReferenceOption>> listPeriodTypes() async {
    _checkConnected();
    return const <ReferenceOption>[
      ReferenceOption(code: 'Quarter', displayName: 'Quarter'),
    ];
  }

  @override
  Future<List<ReferenceOption>> listQuarters() async {
    _checkConnected();
    return const <ReferenceOption>[
      ReferenceOption(code: 'Q1', displayName: 'Q1'),
    ];
  }

  @override
  Future<PreventiveMaintenanceInspection> addInspection(
    String formId,
    AddInspectionInput input,
  ) => addInspectionWithId(formId, input, input.id ?? _inspectionId);

  @override
  Future<PreventiveMaintenanceInspection> addInspectionWithId(
    String formId,
    AddInspectionInput input,
    String id,
  ) async {
    _checkConnected();
    addedInspectionIds.add(id);
    final existing = forms
        .expand((form) => form.inspections)
        .where((inspection) => inspection.id == id)
        .firstOrNull;
    if (existing != null) return existing;
    if (forms.any(
      (form) =>
          form.inspections.any((row) => row.scheduleId == input.scheduleId),
    )) {
      throw const ApiException(statusCode: 409, message: 'Schedule used.');
    }
    final schedule = schedules.singleWhere(
      (item) => item.id == input.scheduleId,
    );
    final now = DateTime.utc(2026, 2, 20, 10);
    final row = PreventiveMaintenanceInspection(
      id: id,
      scheduleId: schedule.id,
      assetId: schedule.assetId,
      assetCode: schedule.asset?.assetCode,
      location: schedule.asset?.location,
      inspectorUserId: input.inspectorUserId,
      dateInspected: input.dateInspected,
      completedAt: now,
      dateAccomplished: input.dateAccomplished,
      isOperational: input.isOperational,
      remarks: input.remarks,
      actionsRecommendations: input.actionsRecommendations,
      waterReplaceCarbonFilter: input.waterReplaceCarbonFilter,
      waterReplaceSedimentFilter: input.waterReplaceSedimentFilter,
      waterCheckUvLight: input.waterCheckUvLight,
      createdAt: now,
      updatedAt: now,
    );
    final form = await getForm(formId);
    forms = forms
        .map(
          (item) => item.id == formId
              ? item.copyWith(inspections: [...form.inspections, row])
              : item,
        )
        .toList(growable: false);
    schedules[0] = schedules[0].copyWith(status: 'Completed');
    if (failAcceptedAddOnce) {
      failAcceptedAddOnce = false;
      throw const ApiException(message: 'Response was lost after save.');
    }
    return row;
  }

  @override
  Future<PreventiveMaintenanceInspection> updateInspection(
    String formId,
    String inspectionId,
    UpdateInspectionInput input,
  ) => updateInspectionIfUnchanged(
    formId,
    inspectionId,
    input,
    input.expectedUpdatedAt ?? DateTime.utc(2026, 2, 20),
  );

  @override
  Future<PreventiveMaintenanceInspection> updateInspectionIfUnchanged(
    String formId,
    String inspectionId,
    UpdateInspectionInput input,
    DateTime expectedUpdatedAt,
  ) async {
    _checkConnected();
    updateExpectedVersions.add(expectedUpdatedAt);
    if (conflictNextUpdate) {
      conflictNextUpdate = false;
      throw const ApiException(statusCode: 409, message: 'Server changed.');
    }
    final form = await getForm(formId);
    final current = form.inspections.singleWhere(
      (row) => row.id == inspectionId,
    );
    if (current.updatedAt != expectedUpdatedAt) {
      if (current.remarks == input.remarks &&
          current.dateInspected == input.dateInspected &&
          current.isOperational == input.isOperational) {
        return current;
      }
      throw const ApiException(statusCode: 409, message: 'Server changed.');
    }
    final updated = PreventiveMaintenanceInspection(
      id: current.id,
      scheduleId: current.scheduleId,
      assetId: current.assetId,
      assetCode: current.assetCode,
      location: current.location,
      inspectorUserId: current.inspectorUserId,
      dateInspected: input.dateInspected,
      startedAt: current.startedAt,
      completedAt: current.completedAt,
      dateAccomplished: input.dateAccomplished,
      isOperational: input.isOperational,
      remarks: input.remarks,
      actionsRecommendations: input.actionsRecommendations,
      waterReplaceCarbonFilter: input.waterReplaceCarbonFilter,
      waterReplaceSedimentFilter: input.waterReplaceSedimentFilter,
      waterCheckUvLight: input.waterCheckUvLight,
      createdAt: current.createdAt,
      updatedAt: DateTime.utc(2026, 2, 20, 11),
      hasPhotoEvidence: current.hasPhotoEvidence,
    );
    forms = forms
        .map(
          (item) => item.id == formId
              ? item.copyWith(
                  inspections: item.inspections
                      .map((row) => row.id == inspectionId ? updated : row)
                      .toList(),
                )
              : item,
        )
        .toList();
    return updated;
  }

  @override
  Future<void> deleteInspection(String formId, String inspectionId) =>
      deleteInspectionIfUnchanged(formId, inspectionId, null);

  @override
  Future<void> deleteInspectionIfUnchanged(
    String formId,
    String inspectionId,
    DateTime? expectedUpdatedAt,
  ) async {
    _checkConnected();
    deleteExpectedVersions.add(expectedUpdatedAt);
    deletedInspectionIds.add(inspectionId);
    final form = await getForm(formId);
    final row = form.inspections
        .where((item) => item.id == inspectionId)
        .firstOrNull;
    if (row == null) {
      if (expectedUpdatedAt != null) return;
      throw const ApiException(statusCode: 404, message: 'Row missing.');
    }
    if (expectedUpdatedAt != null && row.updatedAt != expectedUpdatedAt) {
      throw const ApiException(statusCode: 409, message: 'Server changed.');
    }
    forms = forms
        .map(
          (item) => item.id == formId
              ? item.copyWith(
                  inspections: item.inspections
                      .where((inspection) => inspection.id != inspectionId)
                      .toList(),
                )
              : item,
        )
        .toList();
    schedules[0] = schedules[0].copyWith(status: 'Due');
  }

  @override
  Future<Asset> getByQr(String scannedValue) async {
    _checkConnected();
    return assets.values.firstWhere(
      (asset) => asset.qrCodeValue == scannedValue,
    );
  }

  @override
  Future<Asset> getByCode(String assetCode) async {
    _checkConnected();
    return assets.values.firstWhere((asset) => asset.assetCode == assetCode);
  }

  @override
  Future<List<Asset>> searchAssets({
    String? search,
    String? assetCategory,
    int limit = 25,
  }) async {
    _checkConnected();
    return assets.values.take(limit).toList(growable: false);
  }
}

extension _FirstOrNull<T> on Iterable<T> {
  T? get firstOrNull {
    final iterator = this.iterator;
    return iterator.moveNext() ? iterator.current : null;
  }
}
