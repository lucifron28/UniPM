import 'dart:async';
import 'dart:convert';
import 'dart:math';
import 'dart:typed_data';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

import '../../api/api_exception.dart';
import '../assets/asset_models.dart';
import '../assets/asset_repository.dart';
import 'inspection_photo_evidence_repository.dart';
import 'preventive_maintenance_models.dart';
import 'preventive_maintenance_repository.dart';

abstract interface class OfflineConnectivityMonitor {
  Stream<bool> get changes;
  Future<bool> hasNetwork();
}

class DeviceConnectivityMonitor implements OfflineConnectivityMonitor {
  DeviceConnectivityMonitor([Connectivity? connectivity])
    : _connectivity = connectivity ?? Connectivity();

  final Connectivity _connectivity;

  @override
  Stream<bool> get changes => _connectivity.onConnectivityChanged.map(
    (results) => results.any((result) => result != ConnectivityResult.none),
  );

  @override
  Future<bool> hasNetwork() async {
    final results = await _connectivity.checkConnectivity();
    return results.any((result) => result != ConnectivityResult.none);
  }
}

abstract interface class OfflineSnapshotStore {
  Future<String?> read(String userId);
  Future<void> write(String userId, String json);
  Future<void> delete(String userId);
}

class SecureOfflineSnapshotStore implements OfflineSnapshotStore {
  SecureOfflineSnapshotStore({FlutterSecureStorage? storage})
    : _storage = storage ?? const FlutterSecureStorage();

  final FlutterSecureStorage _storage;

  String _key(String userId) =>
      'unipm.offline.v1.${base64Url.encode(utf8.encode(userId))}';

  @override
  Future<String?> read(String userId) => _storage.read(key: _key(userId));

  @override
  Future<void> write(String userId, String json) =>
      _storage.write(key: _key(userId), value: json);

  @override
  Future<void> delete(String userId) => _storage.delete(key: _key(userId));
}

enum OfflineSyncState { ready, offline, queued, syncing, attention }

class OfflineConflictReview {
  const OfflineConflictReview({
    required this.writeId,
    required this.kind,
    required this.message,
    required this.canRetry,
    required this.canDiscard,
    this.localForm,
    this.serverForm,
    this.localInspection,
    this.serverInspection,
  });

  final String writeId;
  final String kind;
  final String message;
  final bool canRetry;
  final bool canDiscard;
  final PreventiveMaintenanceForm? localForm;
  final PreventiveMaintenanceForm? serverForm;
  final PreventiveMaintenanceInspection? localInspection;
  final PreventiveMaintenanceInspection? serverInspection;
}

class OfflineInspectionRepository extends ChangeNotifier
    with WidgetsBindingObserver
    implements
        PreventiveMaintenanceRepository,
        OfflineSafePreventiveMaintenanceRepository,
        AssetRepository,
        InspectionPhotoEvidenceRepository {
  OfflineInspectionRepository({
    required this._remote,
    required this._remoteAssets,
    required this._store,
    required this._connectivity,
  }) {
    WidgetsBinding.instance.addObserver(this);
    _connectivitySubscription = _connectivity.changes.listen((available) {
      _hasNetwork = available;
      notifyListeners();
      if (available && _activeUserId != null) unawaited(syncNow());
    });
  }

  final PreventiveMaintenanceRepository _remote;
  final AssetRepository _remoteAssets;
  final OfflineSnapshotStore _store;
  final OfflineConnectivityMonitor _connectivity;
  late final StreamSubscription<bool> _connectivitySubscription;
  Future<void> _tail = Future<void>.value();
  _OfflineUserData _data = _OfflineUserData();
  String? _activeUserId;
  String? _loadedUserId;
  bool _hasNetwork = false;
  bool _isSyncing = false;
  bool _reauthenticationRequired = false;
  String? _lastIssue;
  bool _disposed = false;

  String? get activeUserId => _activeUserId;
  int get pendingCount => _data.writes.length;
  int get conflictCount =>
      _data.writes.where((write) => write.state != 'pending').length;
  bool get hasNetwork => _hasNetwork;
  bool get hasCachedSchedules => _data.schedules.isNotEmpty;
  String? get lastIssue => _lastIssue;

  OfflineSyncState get syncState {
    if (_isSyncing) return OfflineSyncState.syncing;
    if (conflictCount > 0) return OfflineSyncState.attention;
    if (pendingCount > 0) {
      return _hasNetwork ? OfflineSyncState.queued : OfflineSyncState.offline;
    }
    return _hasNetwork ? OfflineSyncState.ready : OfflineSyncState.offline;
  }

  bool isFormPending(String formId) =>
      _data.writes.any((write) => write.formId == formId);

  int pendingCountFor(String formId) =>
      _data.writes.where((write) => write.formId == formId).length;

  bool hasConflictFor(String formId) => _data.writes.any(
    (write) => write.formId == formId && write.state != 'pending',
  );

  List<OfflineConflictReview> conflictsForForm(String formId) => _data.writes
      .where((write) => write.formId == formId && write.state != 'pending')
      .map((write) {
        final serverForm = _data.serverForms[formId];
        final localInspection = write.inspectionId == null
            ? null
            : _findInspection(formId, write.inspectionId!);
        final serverInspection = write.inspectionId == null
            ? null
            : serverForm?.inspections
                      .where((row) => row.id == write.inspectionId)
                      .firstOrNull ??
                  (write.kind == 'addInspection'
                      ? serverForm?.inspections
                            .where(
                              (row) =>
                                  row.scheduleId == write.data['scheduleId'],
                            )
                            .firstOrNull
                      : null);
        return OfflineConflictReview(
          writeId: write.id,
          kind: write.kind,
          message:
              write.message ?? 'Review the saved local and server versions.',
          canRetry: write.state == 'conflict',
          canDiscard: serverForm != null || write.kind == 'createForm',
          localForm: _data.forms[formId],
          serverForm: serverForm,
          localInspection: localInspection,
          serverInspection: serverInspection,
        );
      })
      .toList(growable: false);

  bool canSubmitForm(String formId) => _hasNetwork && !isFormPending(formId);

  bool isInspectionPending(String inspectionId) =>
      _data.writes.any((write) => write.inspectionId == inspectionId);

  bool canEditPhoto(String inspectionId) =>
      _hasNetwork && !isInspectionPending(inspectionId);

  Future<void> activate(String userId) async {
    try {
      await _exclusive(() async {
        _activeUserId = userId;
        await _ensureLoaded();
        _reauthenticationRequired = false;
        _hasNetwork = await _connectivity.hasNetwork();
        _notifyListeners();
      });
      if (_hasNetwork) unawaited(syncNow());
    } catch (_) {
      _lastIssue = 'Offline storage could not be opened for this account.';
      _hasNetwork = false;
      _notifyListeners();
    }
  }

  /// Locks and clears the in-memory scope. An empty synced cache is removed;
  /// queued work remains encrypted for the same account's next sign-in.
  Future<void> deactivate() async {
    await _exclusive(() async {
      final userId = _activeUserId;
      if (userId == null) return;
      try {
        await _ensureLoaded();
        if (_data.writes.isEmpty) {
          await _store.delete(userId);
        } else {
          await _persist();
        }
      } finally {
        _activeUserId = null;
        _loadedUserId = null;
        _data = _OfflineUserData();
        _reauthenticationRequired = false;
        _lastIssue = null;
        _notifyListeners();
      }
    });
  }

  Future<void> syncNow() => _exclusive(() async {
    try {
      _hasNetwork = await _connectivity.hasNetwork();
    } catch (_) {
      _hasNetwork = false;
    }
    _notifyListeners();
    if (!_hasNetwork) {
      _lastIssue = 'Still offline. Saved changes remain on this device.';
      _notifyListeners();
      return;
    }
    try {
      await _syncQueue();
    } catch (_) {
      _lastIssue =
          'Offline data could not be read. Your saved copy is preserved.';
      _notifyListeners();
    }
  });

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed && _activeUserId != null) {
      unawaited(_resumeSync());
    }
  }

  Future<void> _resumeSync() async {
    try {
      _hasNetwork = await _connectivity.hasNetwork();
      _notifyListeners();
      if (_hasNetwork) await syncNow();
    } catch (_) {
      _hasNetwork = false;
      _notifyListeners();
    }
  }

  @override
  Future<List<PreventiveMaintenanceForm>> listForms() => _exclusive(() async {
    await _ensureLoaded();
    try {
      final remoteForms = await _remote.listForms();
      _rememberRemoteForms(remoteForms);
      await _persist();
      _hasNetwork = true;
      return _orderedForms();
    } on ApiException catch (error) {
      if (!_canUseCacheFor(error) || _data.forms.isEmpty) rethrow;
      _hasNetwork = false;
      return _orderedForms();
    }
  });

  @override
  Future<PreventiveMaintenanceForm> getForm(String id) => _exclusive(() async {
    await _ensureLoaded();
    try {
      final remoteForm = await _remote.getForm(id);
      _rememberRemoteForm(remoteForm);
      await _persist();
      _hasNetwork = true;
      if (isFormPending(id)) return _data.forms[id]!;
      return remoteForm;
    } on ApiException catch (error) {
      if (!_canUseCacheFor(error) || _data.forms[id] == null) rethrow;
      _hasNetwork = false;
      return _data.forms[id]!;
    }
  });

  @override
  Future<PreventiveMaintenanceForm> createForm(
    CreatePreventiveMaintenanceFormInput input,
  ) => _exclusive(() async {
    await _ensureLoaded();
    final id = input.id ?? _newUuid();
    final now = DateTime.now().toUtc();
    final form = PreventiveMaintenanceForm(
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
      createdByUserId: _activeUserId!,
      submittedByUserId: null,
      submittedAt: null,
      fieldWorkCompletedAt: null,
      createdAt: now,
      updatedAt: now,
      inspections: const [],
    );
    _data.forms[id] = form;
    _data.writes.add(
      _PendingWrite(
        id: _newUuid(),
        kind: 'createForm',
        formId: id,
        data: _createFormData(input),
      ),
    );
    await _persist();
    await _syncIfConnected();
    return _data.forms[id] ?? form;
  });

  @override
  Future<PreventiveMaintenanceForm> createFormWithId(
    CreatePreventiveMaintenanceFormInput input,
    String id,
  ) => createForm(_withFormId(input, id));

  @override
  Future<PreventiveMaintenanceForm> submitForm(String formId) =>
      _exclusive(() async {
        await _ensureLoaded();
        await _requireSynchronizedForm(formId);
        final result = await _remote.submitForm(formId);
        _rememberRemoteForm(result);
        await _persist();
        _hasNetwork = true;
        return result;
      });

  @override
  Future<PreventiveMaintenanceAcknowledgement> acknowledgeForm(
    String formId,
    AcknowledgePreventiveMaintenanceInput input,
  ) => _exclusive(() async {
    await _ensureLoaded();
    await _requireSynchronizedForm(formId);
    final result = await _remote.acknowledgeForm(formId, input);
    final form = _data.forms[formId];
    if (form != null) {
      final acknowledged = form.copyWith(status: 'Acknowledged');
      _rememberRemoteForm(acknowledged);
    }
    await _persist();
    _hasNetwork = true;
    return result;
  });

  @override
  Future<List<ScheduleOption>> listSchedules({String? assetId}) =>
      _exclusive(() async {
        await _ensureLoaded();
        try {
          final schedules = await _remote.listSchedules(assetId: assetId);
          for (final schedule in schedules) {
            _data.schedules[schedule.id] = schedule;
            _cacheScheduleAsset(schedule);
          }
          await _persist();
          _hasNetwork = true;
          return _schedulesFor(assetId);
        } on ApiException catch (error) {
          if (!_canUseCacheFor(error) || _data.schedules.isEmpty) rethrow;
          _hasNetwork = false;
          return _schedulesFor(assetId);
        }
      });

  @override
  Future<List<ReferenceOption>> listAssetCategories() => _loadReferenceList(
    _remote.listAssetCategories,
    () => _data.assetCategories,
    (values) => _data.assetCategories = values,
  );

  @override
  Future<List<ReferenceOption>> listPeriodTypes() => _loadReferenceList(
    _remote.listPeriodTypes,
    () => _data.periodTypes,
    (values) => _data.periodTypes = values,
  );

  @override
  Future<List<ReferenceOption>> listQuarters() => _loadReferenceList(
    _remote.listQuarters,
    () => _data.quarters,
    (values) => _data.quarters = values,
  );

  Future<List<ReferenceOption>> _loadReferenceList(
    Future<List<ReferenceOption>> Function() fetch,
    List<ReferenceOption> Function() getCached,
    void Function(List<ReferenceOption>) setCached,
  ) => _exclusive(() async {
    await _ensureLoaded();
    try {
      final values = await fetch();
      setCached(values);
      await _persist();
      _hasNetwork = true;
      return List<ReferenceOption>.unmodifiable(values);
    } on ApiException catch (error) {
      if (!_canUseCacheFor(error) || getCached().isEmpty) rethrow;
      _hasNetwork = false;
      return List<ReferenceOption>.unmodifiable(getCached());
    }
  });

  @override
  Future<PreventiveMaintenanceInspection> addInspection(
    String formId,
    AddInspectionInput input,
  ) => _exclusive(() async {
    await _ensureLoaded();
    final form = _data.forms[formId];
    if (form == null || !form.isDraft) {
      throw const ApiException(
        statusCode: 409,
        message: 'Only a cached Draft form can be edited offline.',
      );
    }
    if (form.inspections.any((row) => row.scheduleId == input.scheduleId)) {
      throw const ApiException(
        statusCode: 409,
        message: 'This schedule is already included in the draft.',
      );
    }
    final schedule = _data.schedules[input.scheduleId];
    if (schedule == null || schedule.asset == null) {
      throw const ApiException(
        message:
            'This assigned schedule is not cached. Connect and refresh before recording it.',
      );
    }
    if (!schedule.isEligibleForFieldWork) {
      throw const ApiException(
        statusCode: 409,
        message: 'This schedule is no longer eligible for field work.',
      );
    }
    final id = input.id ?? _newUuid();
    final localInput = _withInspectionId(input, id);
    final now = DateTime.now().toUtc();
    final row = _inspectionFromInput(localInput, schedule, now);
    _data.forms[formId] = form.copyWith(
      inspections: [...form.inspections, row],
    );
    _setScheduleStatus(schedule.id, 'Completed');
    _data.writes.add(
      _PendingWrite(
        id: _newUuid(),
        kind: 'addInspection',
        formId: formId,
        inspectionId: id,
        data: _addInspectionData(localInput),
      ),
    );
    await _persist();
    await _syncIfConnected();
    return _findInspection(formId, id) ?? row;
  });

  @override
  Future<PreventiveMaintenanceInspection> addInspectionWithId(
    String formId,
    AddInspectionInput input,
    String id,
  ) => addInspection(formId, _withInspectionId(input, id));

  @override
  Future<PreventiveMaintenanceInspection> updateInspection(
    String formId,
    String inspectionId,
    UpdateInspectionInput input,
  ) => _exclusive(() async {
    await _ensureLoaded();
    final form = _data.forms[formId];
    if (form == null || !form.isDraft) {
      throw const ApiException(
        statusCode: 409,
        message: 'Only a cached Draft form can be edited offline.',
      );
    }
    final current = _findInspection(formId, inspectionId);
    if (current == null) {
      throw const ApiException(
        statusCode: 404,
        message:
            'The draft inspection row is not available in the local cache.',
      );
    }
    final pendingAdd = _data.writes.cast<_PendingWrite?>().firstWhere(
      (write) =>
          write?.kind == 'addInspection' && write?.inspectionId == inspectionId,
      orElse: () => null,
    );
    if (pendingAdd != null) {
      pendingAdd.data = <String, dynamic>{
        ...pendingAdd.data,
        ..._updateInspectionData(input),
      };
      if (pendingAdd.state == 'blocked') {
        pendingAdd.state = 'pending';
        pendingAdd.message = null;
      }
    } else {
      final pendingUpdate = _data.writes.cast<_PendingWrite?>().firstWhere(
        (write) =>
            write?.kind == 'updateInspection' &&
            write?.inspectionId == inspectionId,
        orElse: () => null,
      );
      if (pendingUpdate != null) {
        pendingUpdate.data = _updateInspectionData(input);
        if (pendingUpdate.state == 'blocked') {
          pendingUpdate.state = 'pending';
          pendingUpdate.message = null;
        }
      } else {
        _data.writes.add(
          _PendingWrite(
            id: _newUuid(),
            kind: 'updateInspection',
            formId: formId,
            inspectionId: inspectionId,
            expectedUpdatedAt: input.expectedUpdatedAt ?? current.updatedAt,
            data: _updateInspectionData(input),
          ),
        );
      }
    }
    final updated = _inspectionWithInput(
      current,
      input,
      DateTime.now().toUtc(),
    );
    _replaceInspection(formId, updated);
    await _persist();
    await _syncIfConnected();
    return _findInspection(formId, inspectionId) ?? updated;
  });

  @override
  Future<PreventiveMaintenanceInspection> updateInspectionIfUnchanged(
    String formId,
    String inspectionId,
    UpdateInspectionInput input,
    DateTime expectedUpdatedAt,
  ) => updateInspection(
    formId,
    inspectionId,
    _withExpectedVersion(input, expectedUpdatedAt),
  );

  @override
  Future<void> deleteInspection(String formId, String inspectionId) =>
      _deleteInspection(formId, inspectionId, null);

  Future<void> _deleteInspection(
    String formId,
    String inspectionId,
    DateTime? expectedUpdatedAt,
  ) => _exclusive(() async {
    await _ensureLoaded();
    final form = _data.forms[formId];
    if (form == null || !form.isDraft) {
      throw const ApiException(
        statusCode: 409,
        message: 'Only a cached Draft form can be edited offline.',
      );
    }
    final row = _findInspection(formId, inspectionId);
    if (row == null) return;
    final addWrite = _data.writes.cast<_PendingWrite?>().firstWhere(
      (write) =>
          write?.kind == 'addInspection' && write?.inspectionId == inspectionId,
      orElse: () => null,
    );
    final updateWrite = _data.writes.cast<_PendingWrite?>().firstWhere(
      (write) =>
          write?.kind == 'updateInspection' &&
          write?.inspectionId == inspectionId,
      orElse: () => null,
    );
    final inspectionWrites = _data.writes
        .where((write) => write.inspectionId == inspectionId)
        .toList(growable: false);
    if (inspectionWrites.any((write) => write.state != 'pending')) {
      throw const ApiException(
        statusCode: 409,
        message: 'Resolve the saved sync conflict before deleting this row.',
      );
    }
    final hasQueuedMutation = addWrite != null || updateWrite != null;
    if (_data.writes.any(
      (write) =>
          write.kind == 'deleteInspection' &&
          write.inspectionId == inspectionId,
    )) {
      return;
    }
    _data.writes.add(
      _PendingWrite(
        id: _newUuid(),
        kind: 'deleteInspection',
        formId: formId,
        inspectionId: inspectionId,
        expectedUpdatedAt: hasQueuedMutation
            ? null
            : expectedUpdatedAt ?? row.updatedAt,
        data: const <String, dynamic>{},
      ),
    );
    _data.forms[formId] = form.copyWith(
      inspections: form.inspections
          .where((inspection) => inspection.id != inspectionId)
          .toList(growable: false),
    );
    _restoreScheduleStatus(row.scheduleId);
    await _persist();
    await _syncIfConnected();
  });

  @override
  Future<void> deleteInspectionIfUnchanged(
    String formId,
    String inspectionId,
    DateTime? expectedUpdatedAt,
  ) => _deleteInspection(formId, inspectionId, expectedUpdatedAt);

  Future<void> retryConflict(String writeId) => _exclusive(() async {
    await _ensureLoaded();
    final write = _data.writes.where((item) => item.id == writeId).firstOrNull;
    if (write == null || write.state != 'conflict') {
      throw const ApiException(
        statusCode: 409,
        message: 'This saved change is no longer available to retry.',
      );
    }
    if (!_hasNetwork) {
      throw const ApiException(
        message: 'Reconnect before retrying a saved conflict.',
      );
    }

    final serverForm = await _remote.getForm(write.formId);
    _data.serverForms[write.formId] = serverForm;
    switch (write.kind) {
      case 'createForm':
        if (!_matchesCreateForm(serverForm, write)) {
          throw const ApiException(
            statusCode: 409,
            message:
                'The server form differs from this saved draft. Review it before discarding the local copy.',
          );
        }
        break;
      case 'addInspection':
        final existingScheduleRow = serverForm.inspections
            .where((row) => row.scheduleId == write.data['scheduleId'])
            .firstOrNull;
        if (existingScheduleRow != null &&
            existingScheduleRow.id != write.inspectionId) {
          throw const ApiException(
            statusCode: 409,
            message:
                'The server already has an inspection for this schedule. Compare both rows before discarding the local copy.',
          );
        }
        break;
      case 'updateInspection':
        final serverInspection = serverForm.inspections
            .where((row) => row.id == write.inspectionId)
            .firstOrNull;
        if (serverInspection == null) {
          throw const ApiException(
            statusCode: 409,
            message:
                'The server inspection no longer exists. Review the local copy before discarding it.',
          );
        }
        write.expectedUpdatedAt = serverInspection.updatedAt;
        break;
      case 'deleteInspection':
        final serverInspection = serverForm.inspections
            .where((row) => row.id == write.inspectionId)
            .firstOrNull;
        if (serverInspection != null) {
          write.expectedUpdatedAt = serverInspection.updatedAt;
        }
        break;
      default:
        throw const ApiException(
          statusCode: 409,
          message: 'This saved change cannot be retried safely.',
        );
    }

    write.state = 'pending';
    write.message = null;
    await _persist();
    await _syncIfConnected();
  });

  Future<void> discardConflict(String writeId) => _exclusive(() async {
    await _ensureLoaded();
    final write = _data.writes.where((item) => item.id == writeId).firstOrNull;
    if (write == null || write.state == 'pending') {
      throw const ApiException(
        statusCode: 409,
        message: 'This saved change is no longer available to discard.',
      );
    }

    if (write.kind == 'createForm') {
      if (_hasNetwork) {
        final latest = await _tryGetServerForm(write.formId);
        if (latest != null) _data.serverForms[write.formId] = latest;
      }
      final serverForm = _data.serverForms[write.formId];
      _data.writes.removeWhere((item) => item.formId == write.formId);
      if (serverForm == null) {
        final local = _data.forms.remove(write.formId);
        for (final row in local?.inspections ?? const []) {
          _restoreScheduleStatus(row.scheduleId);
        }
      } else {
        _rememberRemoteForm(serverForm);
      }
    } else if (write.inspectionId != null) {
      if (_hasNetwork) {
        final latest = await _tryGetServerForm(write.formId);
        if (latest != null) _data.serverForms[write.formId] = latest;
      }
      final serverForm = _data.serverForms[write.formId];
      if (serverForm == null) {
        throw const ApiException(
          message:
              'The latest server version is unavailable. Reconnect before discarding this conflict.',
        );
      }
      final inspectionId = write.inspectionId!;
      final localInspection = _findInspection(write.formId, inspectionId);
      _data.writes.removeWhere(
        (item) =>
            item.formId == write.formId && item.inspectionId == inspectionId,
      );
      _rememberRemoteForm(serverForm);
      final serverInspection = serverForm.inspections
          .where((row) => row.id == inspectionId)
          .firstOrNull;
      if (serverInspection == null) {
        if (localInspection != null) {
          _restoreScheduleStatus(localInspection.scheduleId);
        }
      } else {
        _setScheduleStatus(serverInspection.scheduleId, 'Completed');
      }
    } else {
      _data.writes.remove(write);
    }

    await _persist();
  });

  @override
  Future<Asset> getByQr(String scannedValue) async {
    final normalized = validateUniPmQrValue(scannedValue).toUpperCase();
    try {
      final asset = await _remoteAssets.getByQr(scannedValue);
      await _rememberAsset(asset);
      _hasNetwork = true;
      _notifyListeners();
      return asset;
    } on ApiException catch (error) {
      if (!_canUseCacheFor(error)) rethrow;
      final cached = await _exclusive(() async {
        await _ensureLoaded();
        return _data.assets.values
            .where((asset) => asset.qrCodeValue.toUpperCase() == normalized)
            .firstOrNull;
      });
      if (cached != null) {
        _hasNetwork = false;
        _notifyListeners();
        return cached;
      }
      rethrow;
    }
  }

  @override
  Future<Asset> getByCode(String assetCode) async {
    try {
      final asset = await _remoteAssets.getByCode(assetCode);
      await _rememberAsset(asset);
      _hasNetwork = true;
      _notifyListeners();
      return asset;
    } on ApiException catch (error) {
      if (!_canUseCacheFor(error)) rethrow;
      final normalized = assetCode.trim().toUpperCase();
      final cached = await _exclusive(() async {
        await _ensureLoaded();
        return _data.assets.values
            .where((asset) => asset.assetCode.toUpperCase() == normalized)
            .firstOrNull;
      });
      if (cached != null) {
        _hasNetwork = false;
        _notifyListeners();
        return cached;
      }
      rethrow;
    }
  }

  @override
  Future<List<Asset>> searchAssets({
    String? search,
    String? assetCategory,
    int limit = 25,
  }) async {
    try {
      final assets = await _remoteAssets.searchAssets(
        search: search,
        assetCategory: assetCategory,
        limit: limit,
      );
      await _exclusive(() async {
        await _ensureLoaded();
        for (final asset in assets) {
          _data.assets[asset.id] = asset;
        }
        await _persist();
      });
      _hasNetwork = true;
      _notifyListeners();
      return assets;
    } on ApiException catch (error) {
      if (!_canUseCacheFor(error)) rethrow;
      final normalizedSearch = search?.trim().toUpperCase() ?? '';
      final normalizedCategory = assetCategory?.trim().toLowerCase();
      final assets = await _exclusive(() async {
        await _ensureLoaded();
        return _data.assets.values
            .where(
              (asset) =>
                  normalizedSearch.isEmpty ||
                  asset.assetCode.toUpperCase().contains(normalizedSearch) ||
                  (asset.location?.toUpperCase().contains(normalizedSearch) ??
                      false) ||
                  (asset.building?.toUpperCase().contains(normalizedSearch) ??
                      false) ||
                  (asset.department?.toUpperCase().contains(normalizedSearch) ??
                      false),
            )
            .where(
              (asset) =>
                  normalizedCategory == null ||
                  asset.assetCategory.toLowerCase() == normalizedCategory,
            )
            .take(limit.clamp(1, 50))
            .toList(growable: false);
      });
      if (assets.isEmpty) rethrow;
      _hasNetwork = false;
      _notifyListeners();
      return assets;
    }
  }

  @override
  Future<Uint8List> getInspectionPhoto(String inspectionId) {
    final remote = _photoRepository;
    if (remote == null) throw StateError('Photo evidence is unavailable.');
    return remote.getInspectionPhoto(inspectionId);
  }

  @override
  Future<void> saveInspectionPhoto(
    String inspectionId,
    Uint8List jpegBytes,
  ) async {
    if (!canEditPhoto(inspectionId)) {
      throw const ApiException(
        message:
            'Sync the inspection and reconnect before saving photo evidence.',
      );
    }
    final remote = _photoRepository;
    if (remote == null) throw StateError('Photo evidence is unavailable.');
    await remote.saveInspectionPhoto(inspectionId, jpegBytes);
  }

  @override
  Future<void> deleteInspectionPhoto(String inspectionId) async {
    final remote = _photoRepository;
    if (remote == null) throw StateError('Photo evidence is unavailable.');
    await remote.deleteInspectionPhoto(inspectionId);
  }

  InspectionPhotoEvidenceRepository? get _photoRepository =>
      _remote is InspectionPhotoEvidenceRepository
      ? _remote as InspectionPhotoEvidenceRepository
      : null;

  Future<void> _rememberAsset(Asset asset) => _exclusive(() async {
    await _ensureLoaded();
    _data.assets[asset.id] = asset;
    await _persist();
  });

  Future<void> _syncIfConnected() async {
    if (_hasNetwork) await _syncQueue();
  }

  Future<void> _syncQueue() async {
    if (_activeUserId == null) return;
    await _ensureLoaded();
    if (_reauthenticationRequired) return;
    if (_data.writes.isEmpty) {
      _lastIssue = null;
      _notifyListeners();
      return;
    }
    _isSyncing = true;
    _notifyListeners();
    try {
      final blockedFormIds = _data.writes
          .where((write) => write.state != 'pending')
          .map((write) => write.formId)
          .toSet();
      var index = 0;
      while (index < _data.writes.length) {
        final write = _data.writes[index];
        if (write.state != 'pending') {
          blockedFormIds.add(write.formId);
          index++;
          continue;
        }
        if (blockedFormIds.contains(write.formId)) {
          index++;
          continue;
        }
        try {
          await _applyWrite(write);
          _data.writes.removeAt(index);
          _hasNetwork = true;
          _lastIssue = null;
          await _persist();
        } on ApiException catch (error) {
          if (error.statusCode == 409 || error.statusCode == 412) {
            write.state = 'conflict';
            write.message =
                'The server record changed or is no longer eligible. Your saved draft remains on this device.';
            await _refreshConflictedForm(write.formId);
            await _persist();
            _lastIssue = write.message;
            blockedFormIds.add(write.formId);
            index++;
          } else if (error.statusCode == 401) {
            _reauthenticationRequired = true;
            _lastIssue =
                'Your session expired. Sign in again to sync saved changes.';
            break;
          } else if (_isRetryableSyncFailure(error)) {
            _lastIssue = error.statusCode == null
                ? 'Connection was lost. Saved changes are waiting to sync.'
                : 'The service could not sync changes just now. Saved changes remain queued.';
            break;
          } else if (error.statusCode != null) {
            write.state = 'blocked';
            write.message =
                'The server rejected a saved change. Review the draft before retrying.';
            await _persist();
            _lastIssue = write.message;
            blockedFormIds.add(write.formId);
            index++;
          } else {
            _hasNetwork = false;
            _lastIssue =
                'Connection was lost. Saved changes are waiting to sync.';
            break;
          }
        } catch (_) {
          _hasNetwork = false;
          _lastIssue =
              'Connection was lost. Saved changes are waiting to sync.';
          break;
        }
      }
    } finally {
      _isSyncing = false;
      _notifyListeners();
    }
  }

  Future<void> _applyWrite(_PendingWrite write) async {
    if (_remote is! OfflineSafePreventiveMaintenanceRepository) {
      throw const ApiException(
        message: 'The service does not support safe offline synchronization.',
      );
    }
    final safeRemote = _remote as OfflineSafePreventiveMaintenanceRepository;
    switch (write.kind) {
      case 'createForm':
        final result = await safeRemote.createFormWithId(
          _formInputFromData(write.data, id: write.formId),
          write.formId,
        );
        _rememberRemoteForm(result);
        break;
      case 'addInspection':
        final result = await safeRemote.addInspectionWithId(
          write.formId,
          _addInputFromData(write.data, id: write.inspectionId!),
          write.inspectionId!,
        );
        _rememberRemoteInspection(write.formId, result);
        _setFollowingDeleteVersion(write.inspectionId!, result.updatedAt);
        break;
      case 'updateInspection':
        final result = await safeRemote.updateInspectionIfUnchanged(
          write.formId,
          write.inspectionId!,
          _updateInputFromData(write.data),
          write.expectedUpdatedAt!,
        );
        _rememberRemoteInspection(write.formId, result);
        _setFollowingDeleteVersion(write.inspectionId!, result.updatedAt);
        break;
      case 'deleteInspection':
        await safeRemote.deleteInspectionIfUnchanged(
          write.formId,
          write.inspectionId!,
          write.expectedUpdatedAt,
        );
        _removeInspection(write.formId, write.inspectionId!);
        break;
      default:
        throw StateError('Unknown offline write type.');
    }
  }

  void _setFollowingDeleteVersion(String inspectionId, DateTime updatedAt) {
    final deletion = _data.writes
        .where(
          (write) =>
              write.kind == 'deleteInspection' &&
              write.inspectionId == inspectionId,
        )
        .firstOrNull;
    if (deletion != null && deletion.expectedUpdatedAt == null) {
      deletion.expectedUpdatedAt = updatedAt;
    }
  }

  Future<void> _refreshConflictedForm(String formId) async {
    try {
      final serverForm = await _remote.getForm(formId);
      _data.serverForms[formId] = serverForm;
    } catch (_) {
      // Keep the last cached server form; local edits remain the active draft.
    }
  }

  Future<PreventiveMaintenanceForm?> _tryGetServerForm(String formId) async {
    try {
      return await _remote.getForm(formId);
    } catch (_) {
      return null;
    }
  }

  bool _matchesCreateForm(PreventiveMaintenanceForm form, _PendingWrite write) {
    final data = write.data;
    return form.createdByUserId == _activeUserId &&
        form.assetCategory == data['assetCategory'] &&
        form.building == data['building'] &&
        form.department == data['department'] &&
        form.periodType == data['periodType'] &&
        form.quarter == data['quarter'] &&
        form.semester == data['semester'] &&
        form.year == data['year'] &&
        form.academicYear == data['academicYear'];
  }

  void _rememberRemoteForms(List<PreventiveMaintenanceForm> forms) {
    final remoteIds = forms.map((form) => form.id).toSet();
    for (final form in forms) {
      _rememberRemoteForm(form);
    }
    for (final id in _data.forms.keys.toList(growable: false)) {
      if (!remoteIds.contains(id) && !isFormPending(id)) {
        _data.forms.remove(id);
      }
    }
  }

  void _rememberRemoteForm(PreventiveMaintenanceForm form) {
    _data.serverForms[form.id] = form;
    if (isFormPending(form.id)) {
      final local = _data.forms[form.id];
      if (local != null) {
        _data.forms[form.id] = _mergeRemoteMetadataKeepingPendingRows(
          form,
          local,
        );
      }
    } else {
      _data.forms[form.id] = form;
    }
  }

  PreventiveMaintenanceForm _mergeRemoteMetadataKeepingPendingRows(
    PreventiveMaintenanceForm remote,
    PreventiveMaintenanceForm local,
  ) {
    final pendingInspectionWrites = _data.writes
        .where(
          (write) => write.formId == local.id && write.inspectionId != null,
        )
        .toList(growable: false);
    final merged = <String, PreventiveMaintenanceInspection>{
      for (final row in remote.inspections) row.id: row,
    };
    for (final write in pendingInspectionWrites) {
      final inspectionId = write.inspectionId!;
      if (write.kind == 'deleteInspection') {
        merged.remove(inspectionId);
        continue;
      }
      final localRow = local.inspections
          .where((row) => row.id == inspectionId)
          .firstOrNull;
      if (localRow != null) merged[inspectionId] = localRow;
    }
    return remote.copyWith(inspections: merged.values.toList(growable: false));
  }

  void _rememberRemoteInspection(
    String formId,
    PreventiveMaintenanceInspection inspection,
  ) {
    final form = _data.forms[formId];
    if (form == null) return;
    final rows = <String, PreventiveMaintenanceInspection>{
      for (final row in form.inspections) row.id: row,
    };
    rows[inspection.id] = inspection;
    _data.forms[formId] = form.copyWith(
      inspections: rows.values.toList(growable: false),
    );
    _data.serverForms[formId] = _data.forms[formId]!;
    _setScheduleStatus(inspection.scheduleId, 'Completed');
  }

  void _replaceInspection(
    String formId,
    PreventiveMaintenanceInspection inspection,
  ) {
    final form = _data.forms[formId];
    if (form == null) return;
    _data.forms[formId] = form.copyWith(
      inspections: form.inspections
          .map((row) => row.id == inspection.id ? inspection : row)
          .toList(growable: false),
    );
  }

  void _removeInspection(String formId, String inspectionId) {
    final form = _data.forms[formId];
    if (form == null) return;
    final row = form.inspections
        .where((item) => item.id == inspectionId)
        .firstOrNull;
    _data.forms[formId] = form.copyWith(
      inspections: form.inspections
          .where((inspection) => inspection.id != inspectionId)
          .toList(growable: false),
    );
    if (row != null) _restoreScheduleStatus(row.scheduleId);
  }

  PreventiveMaintenanceInspection? _findInspection(String formId, String id) =>
      _data.forms[formId]?.inspections
          .where((inspection) => inspection.id == id)
          .firstOrNull;

  List<PreventiveMaintenanceForm> _orderedForms() =>
      _data.forms.values.toList(growable: false)
        ..sort((a, b) => b.updatedAt.compareTo(a.updatedAt));

  List<ScheduleOption> _schedulesFor(String? assetId) {
    final pendingAddedScheduleIds = _data.writes
        .where((write) => write.kind == 'addInspection')
        .map((write) => write.data['scheduleId'] as String?)
        .whereType<String>()
        .toSet();
    return _data.schedules.values
        .where((schedule) => assetId == null || schedule.assetId == assetId)
        .map(
          (schedule) => pendingAddedScheduleIds.contains(schedule.id)
              ? schedule.copyWith(status: 'Completed')
              : schedule,
        )
        .toList(growable: false);
  }

  void _cacheScheduleAsset(ScheduleOption schedule) {
    final asset = schedule.asset;
    final qrCodeValue = asset?.qrCodeValue?.trim();
    if (asset == null ||
        asset.status == null ||
        qrCodeValue == null ||
        qrCodeValue.isEmpty) {
      return;
    }
    try {
      validateUniPmQrValue(qrCodeValue);
    } on InvalidUniPmQrException {
      return;
    }
    final id = asset.id;
    _data.assets[id] = Asset(
      id: id,
      assetCode: asset.assetCode,
      assetCategory: asset.assetCategory,
      building: asset.building,
      department: asset.department,
      location: asset.location,
      qrCodeValue: qrCodeValue,
      status: asset.status!,
    );
  }

  void _setScheduleStatus(String scheduleId, String status) {
    final schedule = _data.schedules[scheduleId];
    if (schedule != null) {
      _data.schedules[scheduleId] = schedule.copyWith(status: status);
    }
  }

  void _restoreScheduleStatus(String scheduleId) {
    final schedule = _data.schedules[scheduleId];
    if (schedule == null) return;
    final today = DateTime.now().toUtc().add(const Duration(hours: 8));
    final scheduledDate = schedule.scheduleDate.toUtc().add(
      const Duration(hours: 8),
    );
    final status =
        DateTime(
          scheduledDate.year,
          scheduledDate.month,
          scheduledDate.day,
        ).isBefore(DateTime(today.year, today.month, today.day))
        ? 'Overdue'
        : 'Due';
    _data.schedules[scheduleId] = schedule.copyWith(status: status);
  }

  Future<void> _requireSynchronizedForm(String formId) async {
    if (_data.forms[formId] == null) {
      throw const ApiException(
        statusCode: 404,
        message: 'Form is unavailable.',
      );
    }
    if (_data.writes.any((write) => write.formId == formId)) {
      throw const ApiException(
        statusCode: 409,
        message: 'Sync this draft before submitting or acknowledging it.',
      );
    }
    if (!_hasNetwork) {
      throw const ApiException(
        message: 'Reconnect before submitting or acknowledging this form.',
      );
    }
  }

  bool _canUseCacheFor(ApiException error) {
    final statusCode = error.statusCode;
    final canUseCache = statusCode == null ||
        statusCode == 408 ||
        statusCode == 425 ||
        statusCode == 429 ||
        (statusCode >= 500 && statusCode < 600);
    if (!canUseCache) return false;

    _hasNetwork = false;
    _notifyListeners();
    return true;
  }

  bool _isRetryableSyncFailure(ApiException error) {
    final statusCode = error.statusCode;
    return statusCode == null ||
        statusCode == 408 ||
        statusCode == 425 ||
        statusCode == 429 ||
        (statusCode >= 500 && statusCode < 600);
  }

  Future<void> _ensureLoaded() async {
    final userId = _activeUserId;
    if (userId == null) {
      throw StateError('Sign in to access offline inspection data.');
    }
    if (_loadedUserId == userId) return;
    final stored = await _store.read(userId);
    _data = stored == null || stored.isEmpty
        ? _OfflineUserData()
        : _OfflineUserData.fromJson(jsonDecode(stored) as Map<String, dynamic>);
    _loadedUserId = userId;
    _notifyListeners();
  }

  Future<void> _persist() async {
    final userId = _activeUserId;
    if (userId == null) throw StateError('Offline account is locked.');
    await _store.write(userId, jsonEncode(_data.toJson()));
    _notifyListeners();
  }

  Future<T> _exclusive<T>(Future<T> Function() action) async {
    final previous = _tail;
    final released = Completer<void>();
    _tail = released.future;
    await previous;
    try {
      return await action();
    } finally {
      released.complete();
    }
  }

  void _notifyListeners() {
    if (!_disposed) notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    WidgetsBinding.instance.removeObserver(this);
    unawaited(_connectivitySubscription.cancel());
    super.dispose();
  }
}

class _OfflineUserData {
  _OfflineUserData();

  final Map<String, PreventiveMaintenanceForm> forms = {};
  final Map<String, PreventiveMaintenanceForm> serverForms = {};
  final Map<String, ScheduleOption> schedules = {};
  final Map<String, Asset> assets = {};
  List<ReferenceOption> assetCategories = const [];
  List<ReferenceOption> periodTypes = const [];
  List<ReferenceOption> quarters = const [];
  final List<_PendingWrite> writes = [];

  Map<String, dynamic> toJson() => <String, dynamic>{
    'version': 1,
    'forms': forms.values.map((form) => form.toJson()).toList(),
    'serverForms': serverForms.values.map((form) => form.toJson()).toList(),
    'schedules': schedules.values.map((schedule) => schedule.toJson()).toList(),
    'assets': assets.values.map((asset) => asset.toJson()).toList(),
    'assetCategories': assetCategories.map((item) => item.toJson()).toList(),
    'periodTypes': periodTypes.map((item) => item.toJson()).toList(),
    'quarters': quarters.map((item) => item.toJson()).toList(),
    'writes': writes.map((write) => write.toJson()).toList(),
  };

  factory _OfflineUserData.fromJson(Map<String, dynamic> json) {
    if (json['version'] != 1) {
      throw const FormatException('Unsupported offline cache version.');
    }
    final data = _OfflineUserData();
    for (final value in _list(json, 'forms')) {
      final form = PreventiveMaintenanceForm.fromJson(_map(value));
      data.forms[form.id] = form;
    }
    for (final value in _list(json, 'serverForms')) {
      final form = PreventiveMaintenanceForm.fromJson(_map(value));
      data.serverForms[form.id] = form;
    }
    for (final value in _list(json, 'schedules')) {
      final schedule = ScheduleOption.fromJson(_map(value));
      data.schedules[schedule.id] = schedule;
    }
    for (final value in _list(json, 'assets')) {
      final asset = Asset.fromJson(_map(value));
      data.assets[asset.id] = asset;
    }
    data.assetCategories = _list(json, 'assetCategories')
        .map((value) => ReferenceOption.fromJson(_map(value)))
        .toList(growable: false);
    data.periodTypes = _list(json, 'periodTypes')
        .map((value) => ReferenceOption.fromJson(_map(value)))
        .toList(growable: false);
    data.quarters = _list(json, 'quarters')
        .map((value) => ReferenceOption.fromJson(_map(value)))
        .toList(growable: false);
    for (final value in _list(json, 'writes')) {
      data.writes.add(_PendingWrite.fromJson(_map(value)));
    }
    return data;
  }
}

class _PendingWrite {
  _PendingWrite({
    required this.id,
    required this.kind,
    required this.formId,
    this.inspectionId,
    this.expectedUpdatedAt,
    required this.data,
    this.state = 'pending',
    this.message,
  });

  final String id;
  final String kind;
  final String formId;
  final String? inspectionId;
  DateTime? expectedUpdatedAt;
  Map<String, dynamic> data;
  String state;
  String? message;

  Map<String, dynamic> toJson() => <String, dynamic>{
    'id': id,
    'kind': kind,
    'formId': formId,
    'inspectionId': inspectionId,
    'expectedUpdatedAt': expectedUpdatedAt?.toUtc().toIso8601String(),
    'data': data,
    'state': state,
    'message': message,
  };

  factory _PendingWrite.fromJson(Map<String, dynamic> json) => _PendingWrite(
    id: json['id'] as String,
    kind: json['kind'] as String,
    formId: json['formId'] as String,
    inspectionId: json['inspectionId'] as String?,
    expectedUpdatedAt: json['expectedUpdatedAt'] == null
        ? null
        : DateTime.parse(json['expectedUpdatedAt'] as String),
    data: (json['data'] as Map).cast<String, dynamic>(),
    state: json['state'] as String? ?? 'pending',
    message: json['message'] as String?,
  );
}

Map<String, dynamic> _createFormData(
  CreatePreventiveMaintenanceFormInput input,
) => <String, dynamic>{
  'assetCategory': input.assetCategory,
  'building': input.building,
  'department': input.department,
  'periodType': input.periodType,
  'quarter': input.quarter,
  'semester': input.semester,
  'year': input.year,
  'academicYear': input.academicYear,
};

CreatePreventiveMaintenanceFormInput _formInputFromData(
  Map<String, dynamic> data, {
  String? id,
}) => CreatePreventiveMaintenanceFormInput(
  id: id,
  assetCategory: data['assetCategory'] as String,
  building: data['building'] as String?,
  department: data['department'] as String?,
  periodType: data['periodType'] as String,
  quarter: data['quarter'] as String?,
  semester: data['semester'] as String?,
  year: data['year'] as int?,
  academicYear: data['academicYear'] as String?,
);

CreatePreventiveMaintenanceFormInput _withFormId(
  CreatePreventiveMaintenanceFormInput input,
  String id,
) => CreatePreventiveMaintenanceFormInput(
  id: id,
  assetCategory: input.assetCategory,
  building: input.building,
  department: input.department,
  periodType: input.periodType,
  quarter: input.quarter,
  semester: input.semester,
  year: input.year,
  academicYear: input.academicYear,
);

Map<String, dynamic> _addInspectionData(AddInspectionInput input) =>
    <String, dynamic>{
      'scheduleId': input.scheduleId,
      'inspectorUserId': input.inspectorUserId,
      'dateInspected': input.dateInspected.toUtc().toIso8601String(),
      'dateAccomplished': input.dateAccomplished?.toUtc().toIso8601String(),
      'isOperational': input.isOperational,
      'remarks': input.remarks,
      'actionsRecommendations': input.actionsRecommendations,
      'waterReplaceCarbonFilter': input.waterReplaceCarbonFilter,
      'waterReplaceSedimentFilter': input.waterReplaceSedimentFilter,
      'waterCheckUvLight': input.waterCheckUvLight,
    };

AddInspectionInput _addInputFromData(Map<String, dynamic> data, {String? id}) =>
    AddInspectionInput(
      id: id,
      scheduleId: data['scheduleId'] as String,
      inspectorUserId: data['inspectorUserId'] as String,
      dateInspected: DateTime.parse(data['dateInspected'] as String),
      dateAccomplished: data['dateAccomplished'] == null
          ? null
          : DateTime.parse(data['dateAccomplished'] as String),
      isOperational: data['isOperational'] as bool,
      remarks: data['remarks'] as String?,
      actionsRecommendations: data['actionsRecommendations'] as String?,
      waterReplaceCarbonFilter: data['waterReplaceCarbonFilter'] as bool?,
      waterReplaceSedimentFilter: data['waterReplaceSedimentFilter'] as bool?,
      waterCheckUvLight: data['waterCheckUvLight'] as bool?,
    );

AddInspectionInput _withInspectionId(AddInspectionInput input, String id) =>
    AddInspectionInput(
      id: id,
      scheduleId: input.scheduleId,
      inspectorUserId: input.inspectorUserId,
      dateInspected: input.dateInspected,
      dateAccomplished: input.dateAccomplished,
      isOperational: input.isOperational,
      remarks: input.remarks,
      actionsRecommendations: input.actionsRecommendations,
      waterReplaceCarbonFilter: input.waterReplaceCarbonFilter,
      waterReplaceSedimentFilter: input.waterReplaceSedimentFilter,
      waterCheckUvLight: input.waterCheckUvLight,
    );

Map<String, dynamic> _updateInspectionData(UpdateInspectionInput input) =>
    <String, dynamic>{
      'dateInspected': input.dateInspected.toUtc().toIso8601String(),
      'dateAccomplished': input.dateAccomplished?.toUtc().toIso8601String(),
      'isOperational': input.isOperational,
      'remarks': input.remarks,
      'actionsRecommendations': input.actionsRecommendations,
      'waterReplaceCarbonFilter': input.waterReplaceCarbonFilter,
      'waterReplaceSedimentFilter': input.waterReplaceSedimentFilter,
      'waterCheckUvLight': input.waterCheckUvLight,
    };

UpdateInspectionInput _updateInputFromData(Map<String, dynamic> data) =>
    UpdateInspectionInput(
      dateInspected: DateTime.parse(data['dateInspected'] as String),
      dateAccomplished: data['dateAccomplished'] == null
          ? null
          : DateTime.parse(data['dateAccomplished'] as String),
      isOperational: data['isOperational'] as bool,
      remarks: data['remarks'] as String?,
      actionsRecommendations: data['actionsRecommendations'] as String?,
      waterReplaceCarbonFilter: data['waterReplaceCarbonFilter'] as bool?,
      waterReplaceSedimentFilter: data['waterReplaceSedimentFilter'] as bool?,
      waterCheckUvLight: data['waterCheckUvLight'] as bool?,
    );

UpdateInspectionInput _withExpectedVersion(
  UpdateInspectionInput input,
  DateTime expected,
) => UpdateInspectionInput(
  expectedUpdatedAt: expected,
  dateInspected: input.dateInspected,
  dateAccomplished: input.dateAccomplished,
  isOperational: input.isOperational,
  remarks: input.remarks,
  actionsRecommendations: input.actionsRecommendations,
  waterReplaceCarbonFilter: input.waterReplaceCarbonFilter,
  waterReplaceSedimentFilter: input.waterReplaceSedimentFilter,
  waterCheckUvLight: input.waterCheckUvLight,
);

PreventiveMaintenanceInspection _inspectionFromInput(
  AddInspectionInput input,
  ScheduleOption schedule,
  DateTime now,
) => PreventiveMaintenanceInspection(
  id: input.id!,
  scheduleId: schedule.id,
  assetId: schedule.assetId,
  assetCode: schedule.asset?.assetCode,
  location: schedule.asset?.location,
  inspectorUserId: input.inspectorUserId,
  dateInspected: input.dateInspected,
  completedAt: now,
  dateAccomplished: input.dateAccomplished,
  isOperational: input.isOperational,
  remarks: _normalizedOptional(input.remarks),
  actionsRecommendations: _normalizedOptional(input.actionsRecommendations),
  waterReplaceCarbonFilter: input.waterReplaceCarbonFilter,
  waterReplaceSedimentFilter: input.waterReplaceSedimentFilter,
  waterCheckUvLight: input.waterCheckUvLight,
  createdAt: now,
  updatedAt: now,
);

PreventiveMaintenanceInspection _inspectionWithInput(
  PreventiveMaintenanceInspection current,
  UpdateInspectionInput input,
  DateTime now,
) => PreventiveMaintenanceInspection(
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
  remarks: _normalizedOptional(input.remarks),
  actionsRecommendations: _normalizedOptional(input.actionsRecommendations),
  waterReplaceCarbonFilter: input.waterReplaceCarbonFilter,
  waterReplaceSedimentFilter: input.waterReplaceSedimentFilter,
  waterCheckUvLight: input.waterCheckUvLight,
  createdAt: current.createdAt,
  updatedAt: now,
  hasPhotoEvidence: current.hasPhotoEvidence,
);

String? _normalizedOptional(String? value) {
  final trimmed = value?.trim();
  return trimmed == null || trimmed.isEmpty ? null : trimmed;
}

String _newUuid() {
  final random = Random.secure();
  final bytes = List<int>.generate(16, (_) => random.nextInt(256));
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  final hex = bytes
      .map((byte) => byte.toRadixString(16).padLeft(2, '0'))
      .join();
  return '${hex.substring(0, 8)}-${hex.substring(8, 12)}-'
      '${hex.substring(12, 16)}-${hex.substring(16, 20)}-${hex.substring(20)}';
}

List<dynamic> _list(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value is List) return value;
  throw FormatException('Invalid offline cache field: $key.');
}

Map<String, dynamic> _map(dynamic value) {
  if (value is Map) return value.cast<String, dynamic>();
  throw const FormatException('Invalid offline cache record.');
}

extension _FirstOrNull<T> on Iterable<T> {
  T? get firstOrNull => isEmpty ? null : first;
}
