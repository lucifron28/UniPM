import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
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

  test('encrypted snapshots are isolated by backend and user', () async {
    final legacyKey =
        'unipm.offline.v1.${base64Url.encode(utf8.encode(_userB))}';
    FlutterSecureStorage.setMockInitialValues({legacyKey: 'legacy queue'});
    final cloud = SecureOfflineSnapshotStore(
      apiBaseUrl: Uri.parse('https://cloud.example.test/'),
    );
    final cloudAlias = SecureOfflineSnapshotStore(
      apiBaseUrl: Uri.parse('https://CLOUD.example.test:443'),
    );
    final lan = SecureOfflineSnapshotStore(
      apiBaseUrl: Uri.parse('http://192.168.1.10:5254/'),
    );
    final otherPath = SecureOfflineSnapshotStore(
      apiBaseUrl: Uri.parse('https://cloud.example.test/other/'),
    );
    await cloud.write(_userA, 'cloud queue');
    expect(await cloudAlias.read(_userA), 'cloud queue');
    expect(await lan.read(_userA), isNull);
    expect(await otherPath.read(_userA), isNull);
    expect(await cloud.read(_userB), isNull);
    expect(await lan.read(_userB), isNull);
    await lan.write(_userA, 'local queue');
    await lan.delete(_userA);
    expect(await cloud.read(_userA), 'cloud queue');
    expect(
      await const FlutterSecureStorage().read(key: legacyKey),
      'legacy queue',
    );
  });

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

  testWidgets('offline draft copy names only online-required features', (
    tester,
  ) async {
    await awaitRepositoryAction(
      tester,
      'activate user',
      () => repository.activate(_userA),
    );
    await awaitRepositoryAction(tester, 'cache schedules', () async {
      await repository.listSchedules();
    });
    connectivity.setConnected(false);
    remote.connected = false;

    final form = await tester.runAsync(
      () => repository
          .createForm(_createInput())
          .timeout(const Duration(seconds: 5)),
    );
    expect(form, isNotNull);
    expect(repository.pendingCountFor(form!.id), 1);

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
    controller.selectForm(form);

    await tester.pumpWidget(
      MaterialApp(
        home: PreventiveMaintenanceDraftPage(
          controller: controller,
          formId: form.id,
        ),
      ),
    );
    await tester.pump();

    expect(
      find.textContaining(
        'Photo evidence and form submission need a connection.',
      ),
      findsOneWidget,
    );
    expect(find.textContaining('Location verification'), findsNothing);

    await tester.pumpWidget(const MaterialApp(home: SizedBox.shrink()));
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
      final deviceSavedAt = row.completedAt;
      expect(deviceSavedAt, isNotNull);
      final queuedSnapshot =
          jsonDecode(store.values[_userA]!) as Map<String, dynamic>;
      final queuedWrites = (queuedSnapshot['writes'] as List<dynamic>)
          .cast<Map<String, dynamic>>();
      final queuedInspection = queuedWrites.singleWhere(
        (write) => write['kind'] == 'addInspection',
      );
      expect(
        (queuedInspection['data'] as Map<String, dynamic>).containsKey(
          'completedAt',
        ),
        isFalse,
      );
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
      final serverCompletionAt = DateTime.utc(2026, 2, 20, 10);
      expect(
        remote.forms.single.inspections.single.completedAt,
        serverCompletionAt,
      );
      expect(
        remote.forms.single.inspections.single.completedAt,
        isNot(deviceSavedAt),
      );
      expect(
        (await repository.getForm(form.id)).inspections.single.completedAt,
        serverCompletionAt,
      );
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
    'failed explicit logout keeps the owner and queued work active for retry',
    () async {
      await repository.activate(_userA);
      await repository.listSchedules();
      connectivity.setConnected(false);
      remote.connected = false;
      final form = await repository.createForm(_createInput());
      expect(repository.pendingCount, 1);

      store.failNextWrite = true;
      await expectLater(
        repository.addInspection(form.id, _addInput()),
        throwsA(isA<StateError>()),
      );
      final unsavedRow = (await repository.getForm(form.id)).inspections.single;
      expect(repository.pendingCount, 2);
      final storedSnapshot =
          jsonDecode(store.values[_userA]!) as Map<String, dynamic>;
      expect(storedSnapshot['writes'], hasLength(1));

      store.failNextWrite = true;
      await expectLater(repository.deactivate(), throwsA(isA<StateError>()));

      expect(repository.isActiveFor(_userA), isTrue);
      expect(repository.pendingCount, 2);
      expect((await repository.getForm(form.id)).id, form.id);
      expect(
        (await repository.getForm(form.id)).inspections.single.id,
        unsavedRow.id,
      );

      await repository.deactivate();
      expect(repository.activeUserId, isNull);
      expect(store.values[_userA], contains(form.id));

      repository.dispose();
      repository = createRepository();
      await repository.activate(_userA);
      expect(repository.pendingCount, 2);
      expect((await repository.getForm(form.id)).id, form.id);
      expect(
        (await repository.getForm(form.id)).inspections.single.id,
        unsavedRow.id,
      );
    },
  );

  test(
    'failed account switch locks retained work until its owner signs in again',
    () async {
      connectivity.setConnected(false);
      remote.connected = false;
      await repository.activate(_userA);
      final form = await repository.createForm(_createInput());
      expect(repository.pendingCount, 1);

      store.failNextWrite = true;
      await expectLater(
        repository.deactivate(lockOnFailure: true),
        throwsA(isA<StateError>()),
      );
      expect(repository.activeUserId, isNull);
      expect(repository.isActiveFor(_userA), isFalse);

      await repository.activate(_userB);
      expect(repository.isActiveFor(_userB), isFalse);
      await expectLater(repository.listForms(), throwsA(isA<StateError>()));
      expect(store.values[_userA], contains(form.id));

      await repository.activate(_userA);
      expect(repository.isActiveFor(_userA), isTrue);
      expect(repository.pendingCount, 1);
      expect((await repository.getForm(form.id)).id, form.id);

      await repository.deactivate();
      remote.connected = true;
      await repository.activate(_userB);
      expect(repository.isActiveFor(_userB), isTrue);
      expect(repository.pendingCount, 0);
      expect(await repository.listForms(), isEmpty);
    },
  );

  test('forced scope exit stops sync before the next queued write', () async {
    await repository.activate(_userA);
    await repository.listSchedules();
    connectivity.setConnected(false);
    remote.connected = false;

    final form = await repository.createForm(_createInput());
    final row = await repository.addInspection(form.id, _addInput());
    expect(repository.pendingCount, 2);

    final createStarted = Completer<void>();
    final heldCreate = Completer<PreventiveMaintenanceForm>();
    remote.createFormStarted = createStarted;
    remote.heldCreateForm = heldCreate;
    remote.connected = true;
    connectivity.connected = true;

    final sync = repository.syncNow();
    await createStarted.future.timeout(const Duration(seconds: 3));
    final scopeExit = repository.deactivate(lockOnFailure: true);
    expect(repository.isActiveFor(_userA), isFalse);

    final acceptedForm = _form(
      id: form.id,
      inspections: const <PreventiveMaintenanceInspection>[],
    );
    remote.forms = [acceptedForm];
    heldCreate.complete(acceptedForm);
    await sync.timeout(const Duration(seconds: 3));
    await scopeExit.timeout(const Duration(seconds: 3));

    expect(remote.createFormCalls, 1);
    expect(remote.addedInspectionIds, isEmpty);
    final snapshot = jsonDecode(store.values[_userA]!) as Map<String, dynamic>;
    expect(snapshot['writes'], hasLength(2));

    remote.connected = false;
    connectivity.connected = false;
    await repository.activate(_userA);
    expect(repository.pendingCount, 2);
    expect((await repository.getForm(form.id)).inspections.single.id, row.id);
  });

  test(
    'forced exit releases an unreadable empty scope without changing its snapshot',
    () async {
      const unreadableSnapshot = 'preserve these unreadable snapshot bytes';
      store.values[_userA] = unreadableSnapshot;
      store.failNextRead = true;

      await repository.activate(_userA);
      expect(repository.isActiveFor(_userA), isFalse);
      await repository.deactivate(lockOnFailure: true);

      expect(repository.activeUserId, isNull);
      expect(store.values[_userA], unreadableSnapshot);
      await repository.activate(_userB);
      expect(repository.isActiveFor(_userB), isTrue);
      expect(repository.pendingCount, 0);
      expect(store.values[_userA], unreadableSnapshot);
    },
  );

  for (final statusCode in [403, 409]) {
    test(
      'stale schedule authorization HTTP $statusCode keeps the queued row',
      () async {
        remote.forms = [_form(inspections: const [])];
        await repository.activate(_userA);
        await repository.listForms();
        await repository.listSchedules();
        connectivity.setConnected(false);
        remote.connected = false;

        final localRow = await repository.addInspection(_formId, _addInput());
        expect(repository.isInspectionCreatePending(localRow.id), isTrue);

        remote.failNextAddInspectionStatusCodes.add(statusCode);
        remote.connected = true;
        connectivity.connected = true;
        await repository.syncNow();

        expect(repository.pendingCountFor(_formId), 1);
        final conflict = repository.conflictsForForm(_formId).single;
        expect(conflict.canRetry, statusCode == 409);
        expect(conflict.canDiscard, isTrue);
        expect(
          conflict.message,
          contains(
            statusCode == 403
                ? 'server rejected a saved change'
                : 'server record changed or is no longer eligible',
          ),
        );
        expect(
          (await repository.getForm(_formId)).inspections.single.id,
          localRow.id,
        );
        expect(remote.forms.single.inspections, isEmpty);
        expect(repository.isInspectionCreatePending(localRow.id), isTrue);
      },
    );
  }

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
    'cached forms, schedules, references, and QR lookups survive transient HTTP failures',
    () async {
      remote.forms = [
        _form(inspections: [_inspection()]),
      ];
      await repository.activate(_userA);
      await repository.listForms();
      await repository.listSchedules();
      await repository.listAssetCategories();
      await repository.listPeriodTypes();
      await repository.listQuarters();

      for (final statusCode in [408, 425, 429, 500, 503, 599]) {
        remote.readFailureStatusCode = statusCode;

        expect((await repository.listForms()).single.id, _formId);
        expect((await repository.getForm(_formId)).id, _formId);
        expect((await repository.listSchedules()).single.id, _scheduleId);
        expect(
          (await repository.listAssetCategories()).single.code,
          'fire-extinguisher',
        );
        expect((await repository.listPeriodTypes()).single.code, 'Quarter');
        expect((await repository.listQuarters()).single.code, 'Q1');
        expect(
          (await repository.getByQr('UNIPM-LEGACY-CUSTOM-LABEL')).id,
          _assetId,
        );
      }

      remote.readFailureStatusCode = null;
      remote.connected = false;
      expect((await repository.listForms()).single.id, _formId);
      expect((await repository.listSchedules()).single.id, _scheduleId);
      expect(
        (await repository.listAssetCategories()).single.code,
        'fire-extinguisher',
      );
      expect((await repository.listPeriodTypes()).single.code, 'Quarter');
      expect((await repository.listQuarters()).single.code, 'Q1');
      expect(
        (await repository.getByQr('UNIPM-LEGACY-CUSTOM-LABEL')).id,
        _assetId,
      );
    },
  );

  test(
    'cached reads never bypass authentication or authorization failures',
    () async {
      remote.forms = [
        _form(inspections: [_inspection()]),
      ];
      await repository.activate(_userA);
      await repository.listForms();
      await repository.listSchedules();

      for (final statusCode in [401, 403]) {
        remote.readFailureStatusCode = statusCode;
        await expectLater(repository.listForms(), throwsA(isA<ApiException>()));
        await expectLater(
          repository.getByQr('UNIPM-LEGACY-CUSTOM-LABEL'),
          throwsA(isA<ApiException>()),
        );
      }
    },
  );

  test(
    'retryable HTTP failures preserve queued drafts for the next sync attempt',
    () async {
      await repository.activate(_userA);

      for (final statusCode in [408, 429, 500, 503]) {
        connectivity.connected = false;
        remote.connected = false;
        await repository.syncNow();

        final form = await repository.createForm(_createInput());
        expect(repository.pendingCount, 1);

        remote.connected = true;
        connectivity.connected = true;
        remote.failNextCreateFormStatus = statusCode;
        await repository.syncNow();

        expect(repository.pendingCount, 1, reason: 'HTTP $statusCode');
        expect(repository.conflictCount, 0, reason: 'HTTP $statusCode');
        expect(store.values[_userA], contains(form.id));
        expect(
          remote.forms.any((saved) => saved.id == form.id),
          isFalse,
          reason: 'HTTP $statusCode',
        );

        await repository.syncNow();

        expect(repository.pendingCount, 0, reason: 'HTTP $statusCode retry');
        expect(
          remote.forms
              .where((saved) => saved.id == form.id)
              .map((saved) => saved.id),
          [form.id],
        );
      }
    },
  );

  test(
    'session expiry preserves the queue until the same user signs in again',
    () async {
      await repository.activate(_userA);
      connectivity.connected = false;
      remote.connected = false;
      await repository.syncNow();
      final form = await repository.createForm(_createInput());

      remote.connected = true;
      connectivity.connected = true;
      remote.failNextCreateFormStatus = 401;
      await repository.syncNow();

      expect(repository.pendingCount, 1);
      expect(repository.conflictCount, 0);
      expect(repository.lastIssue, contains('Sign in again'));
      expect(remote.createFormCalls, 1);

      await repository.syncNow();
      expect(repository.pendingCount, 1);
      expect(remote.createFormCalls, 1);

      await repository.deactivate();
      expect(store.values[_userA], contains(form.id));
      repository.dispose();
      repository = createRepository();
      await repository.activate(_userA);
      await repository.syncNow();

      expect(repository.pendingCount, 0);
      expect(remote.forms.map((saved) => saved.id), [form.id]);
      expect(remote.createFormCalls, 2);
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

  testWidgets('conflict discard confirmation calls the repository', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(800, 800);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    repository.dispose();
    repository = _ImmediateScheduleOfflineInspectionRepository(
      tester: tester,
      remote: remote,
      fakeRemote: remote,
      remoteAssets: remote,
      store: store,
      connectivity: connectivity,
    );
    final immediateRepository =
        repository as _ImmediateScheduleOfflineInspectionRepository;
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
    expect(find.text('Discard this saved change?'), findsNothing);
    expect(immediateRepository.discardCalls, 1);
    await immediateRepository.waitForDiscard();
    final restoredForm = await immediateRepository.readThroughRepository(
      _formId,
    );
    expect(repository.pendingCount, 0);
    expect(
      restoredForm.inspections.single.remarks,
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

AddInspectionInput _addInput({String scheduleId = _scheduleId}) =>
    AddInspectionInput(
      scheduleId: scheduleId,
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

ScheduleOption _schedule({
  String id = _scheduleId,
  String assetId = _assetId,
  String assetCode = 'FE-CCMS-001',
  String status = 'Due',
  DateTime? scheduleDate,
}) => ScheduleOption(
  id: id,
  assetId: assetId,
  scheduleDate: scheduleDate ?? DateTime.utc(2026, 2, 28, 15, 59, 59),
  pmCycle: '2026-02',
  periodType: 'Quarter',
  status: status,
  quarter: 'Q1',
  semester: null,
  year: 2026,
  academicYear: '2025-2026',
  assignedToUserId: _userA,
  asset: ScheduleAssetOption(
    id: assetId,
    assetCode: assetCode,
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
  bool failNextWrite = false;
  bool failNextRead = false;

  @override
  Future<String?> read(String userId) async {
    if (failNextRead) {
      failNextRead = false;
      throw StateError('Synthetic secure-storage read failure.');
    }
    return values[userId];
  }

  @override
  Future<void> write(String userId, String json) async {
    if (failNextWrite) {
      failNextWrite = false;
      throw StateError('Synthetic secure-storage write failure.');
    }
    values[userId] = json;
  }

  @override
  Future<void> delete(String userId) async => values.remove(userId);
}

class _ImmediateScheduleOfflineInspectionRepository
    extends OfflineInspectionRepository {
  _ImmediateScheduleOfflineInspectionRepository({
    required this.tester,
    required super.remote,
    required this.fakeRemote,
    required super.remoteAssets,
    required super.store,
    required super.connectivity,
  });

  final WidgetTester tester;
  final PreventiveMaintenanceRepository fakeRemote;
  int discardCalls = 0;
  Future<bool?>? _discardOperation;

  @override
  Future<List<ScheduleOption>> listSchedules({String? assetId}) async =>
      const <ScheduleOption>[];

  @override
  Future<void> discardConflict(String writeId) async {
    discardCalls++;
    final operation = tester.runAsync<bool>(() async {
      await _discardWithBaseRepository(
        writeId,
      ).timeout(const Duration(seconds: 5));
      return true;
    });
    _discardOperation = operation;
    final completed = await operation;
    if (completed != true) {
      throw StateError('Conflict discard did not finish.');
    }
  }

  Future<void> waitForDiscard() async {
    final operation = _discardOperation;
    if (operation == null) {
      throw StateError('Conflict discard was not started.');
    }
    if (await operation != true) {
      throw StateError('Conflict discard did not finish.');
    }
  }

  @override
  Future<PreventiveMaintenanceForm> getForm(String id) =>
      fakeRemote.getForm(id);

  Future<PreventiveMaintenanceForm> readThroughRepository(String id) async {
    final form = await tester.runAsync(
      () => _getFromBaseRepository(id).timeout(const Duration(seconds: 5)),
    );
    if (form == null) throw StateError('The repository read did not finish.');
    return form;
  }

  Future<void> _discardWithBaseRepository(String writeId) =>
      super.discardConflict(writeId);

  Future<PreventiveMaintenanceForm> _getFromBaseRepository(String id) =>
      super.getForm(id);
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
  final List<int> failNextAddInspectionStatusCodes = <int>[];
  int? readFailureStatusCode;
  int? failNextCreateFormStatus;
  bool conflictNextUpdate = false;
  Completer<List<ScheduleOption>>? pendingScheduleResponse;
  Completer<void>? createFormStarted;
  Completer<PreventiveMaintenanceForm>? heldCreateForm;
  int submitCalls = 0;
  int acknowledgementCalls = 0;
  int createFormCalls = 0;
  final List<String> addedInspectionIds = <String>[];
  final List<String> deletedInspectionIds = <String>[];
  final List<DateTime> updateExpectedVersions = <DateTime>[];
  final List<DateTime?> deleteExpectedVersions = <DateTime?>[];
  List<PreventiveMaintenanceForm> forms = <PreventiveMaintenanceForm>[];
  final List<ScheduleOption> schedules = <ScheduleOption>[_schedule()];
  final Map<String, Asset> assets = <String, Asset>{};

  void _checkConnected() {
    final failureStatusCode = readFailureStatusCode;
    if (failureStatusCode != null) {
      throw ApiException(
        statusCode: failureStatusCode,
        message: 'Synthetic HTTP $failureStatusCode response.',
      );
    }
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
    createFormCalls++;
    final failureStatus = failNextCreateFormStatus;
    if (failureStatus != null) {
      failNextCreateFormStatus = null;
      throw ApiException(
        statusCode: failureStatus,
        message: 'Synthetic HTTP $failureStatus response.',
      );
    }
    final heldResponse = heldCreateForm;
    if (heldResponse != null) {
      heldCreateForm = null;
      createFormStarted?.complete();
      return heldResponse.future;
    }
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
    if (failNextAddInspectionStatusCodes.isNotEmpty) {
      final statusCode = failNextAddInspectionStatusCodes.removeAt(0);
      throw ApiException(
        statusCode: statusCode,
        message: 'Synthetic HTTP $statusCode add-inspection rejection.',
      );
    }
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
