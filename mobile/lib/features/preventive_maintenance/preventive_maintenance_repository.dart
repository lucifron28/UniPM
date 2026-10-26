import 'dart:typed_data';

import '../../api/api_client.dart';
import 'inspection_photo_evidence_repository.dart';
import 'preventive_maintenance_models.dart';

abstract interface class PreventiveMaintenanceRepository {
  Future<List<PreventiveMaintenanceForm>> listForms();
  Future<PreventiveMaintenanceForm> getForm(String id);
  Future<PreventiveMaintenanceForm> createForm(
    CreatePreventiveMaintenanceFormInput input,
  );
  Future<PreventiveMaintenanceForm> submitForm(String formId);
  Future<PreventiveMaintenanceAcknowledgement> acknowledgeForm(
    String formId,
    AcknowledgePreventiveMaintenanceInput input,
  );
  Future<List<ScheduleOption>> listSchedules({String? assetId});
  Future<List<ReferenceOption>> listAssetCategories();
  Future<List<ReferenceOption>> listPeriodTypes();
  Future<List<ReferenceOption>> listQuarters();
  Future<PreventiveMaintenanceInspection> addInspection(
    String formId,
    AddInspectionInput input,
  );
  Future<PreventiveMaintenanceInspection> updateInspection(
    String formId,
    String inspectionId,
    UpdateInspectionInput input,
  );
  Future<void> deleteInspection(String formId, String inspectionId);
}

abstract interface class OfflineSafePreventiveMaintenanceRepository {
  Future<PreventiveMaintenanceForm> createFormWithId(
    CreatePreventiveMaintenanceFormInput input,
    String id,
  );
  Future<PreventiveMaintenanceInspection> addInspectionWithId(
    String formId,
    AddInspectionInput input,
    String id,
  );
  Future<PreventiveMaintenanceInspection> updateInspectionIfUnchanged(
    String formId,
    String inspectionId,
    UpdateInspectionInput input,
    DateTime expectedUpdatedAt,
  );
  Future<void> deleteInspectionIfUnchanged(
    String formId,
    String inspectionId,
    DateTime? expectedUpdatedAt,
  );
}

abstract interface class ScheduleInspectionStartRepository {
  /// Returns true when the in-progress state was recorded by the API.
  /// Offline implementations may return false so field work can continue.
  Future<bool> startInspection(String scheduleId);
}

class ApiPreventiveMaintenanceRepository
    implements
        PreventiveMaintenanceRepository,
        OfflineSafePreventiveMaintenanceRepository,
        ScheduleInspectionStartRepository,
        InspectionPhotoEvidenceRepository {
  const ApiPreventiveMaintenanceRepository(this._client);

  final ApiClient _client;

  @override
  Future<List<PreventiveMaintenanceForm>> listForms() async {
    const pageSize = 100;
    final forms = <PreventiveMaintenanceForm>[];
    final formIds = <String>{};
    int? totalCount;
    var page = 1;

    while (totalCount == null || (page - 1) * pageSize < totalCount) {
      final response = await _client.getJson(
        '/api/v1/preventive-maintenance-forms?page=$page&pageSize=$pageSize',
      );
      final responsePage = response['page'];
      final responsePageSize = response['pageSize'];
      final responseTotalCount = response['totalCount'];
      final values = response['items'];

      if (responsePage is! int || responsePage != page) {
        throw const FormatException('Invalid form page metadata.');
      }
      if (responsePageSize is! int || responsePageSize != pageSize) {
        throw const FormatException('Invalid form page size metadata.');
      }
      if (responseTotalCount is! int || responseTotalCount < 0) {
        throw const FormatException('Invalid form count metadata.');
      }
      if (values is! List) {
        throw const FormatException('Invalid form page items.');
      }
      if (totalCount != null && responseTotalCount != totalCount) {
        throw const FormatException('Form count changed while loading pages.');
      }
      totalCount ??= responseTotalCount;

      final remainingCount = totalCount - ((page - 1) * pageSize);
      final expectedPageCount = remainingCount > pageSize
          ? pageSize
          : remainingCount;
      if (values.length != expectedPageCount) {
        throw const FormatException('Incomplete form page response.');
      }

      for (final value in values) {
        final form = _formFromValue(value);
        if (formIds.add(form.id.toLowerCase())) forms.add(form);
      }

      if (page * pageSize >= totalCount) break;
      if (values.isEmpty) {
        throw const FormatException('Form pages ended before all rows loaded.');
      }
      page++;
    }

    if (forms.length != totalCount) {
      throw const FormatException('Form pages ended before all rows loaded.');
    }
    return List<PreventiveMaintenanceForm>.unmodifiable(forms);
  }

  @override
  Future<PreventiveMaintenanceForm> getForm(String id) async {
    return PreventiveMaintenanceForm.fromJson(
      await _client.getJson('/api/v1/preventive-maintenance-forms/$id'),
    );
  }

  @override
  Future<PreventiveMaintenanceForm> createForm(
    CreatePreventiveMaintenanceFormInput input,
  ) => createFormWithId(input, input.id ?? '');

  @override
  Future<PreventiveMaintenanceForm> createFormWithId(
    CreatePreventiveMaintenanceFormInput input,
    String id,
  ) async {
    return PreventiveMaintenanceForm.fromJson(
      await _client.postJson('/api/v1/preventive-maintenance-forms', {
        if (id.isNotEmpty) 'id': id,
        'assetCategory': input.assetCategory,
        'building': input.building,
        'department': input.department,
        'periodType': input.periodType,
        'quarter': input.quarter,
        'semester': input.semester,
        'year': input.year,
        'academicYear': input.academicYear,
      }),
    );
  }

  @override
  Future<PreventiveMaintenanceForm> submitForm(String formId) async {
    return PreventiveMaintenanceForm.fromJson(
      await _client.postJson(
        '/api/v1/preventive-maintenance-forms/$formId/submit',
      ),
    );
  }

  @override
  Future<PreventiveMaintenanceAcknowledgement> acknowledgeForm(
    String formId,
    AcknowledgePreventiveMaintenanceInput input,
  ) async {
    final json = await _client.postJson(
      '/api/v1/preventive-maintenance-forms/$formId/acknowledge',
      <String, dynamic>{
        'signatoryName': input.signatoryName.trim(),
        'signatoryPosition': input.signatoryPosition.trim(),
        'signatureData': input.signatureData,
        'signatureContentType': input.signatureContentType,
      },
    );
    return PreventiveMaintenanceAcknowledgement.fromJson(json);
  }

  @override
  Future<List<ScheduleOption>> listSchedules({String? assetId}) async {
    final path = assetId == null
        ? '/api/v1/schedules'
        : '/api/v1/schedules?assetId=${Uri.encodeQueryComponent(assetId)}';
    final values = await _client.getJsonList(path);
    return values.map(_scheduleFromValue).toList(growable: false);
  }

  @override
  Future<bool> startInspection(String scheduleId) async {
    await _client.postEmpty('/api/v1/schedules/$scheduleId/start-inspection');
    return true;
  }

  @override
  Future<List<ReferenceOption>> listAssetCategories() =>
      _getReferences('/api/v1/reference-data/asset-categories');

  @override
  Future<List<ReferenceOption>> listPeriodTypes() =>
      _getReferences('/api/v1/reference-data/schedule-period-types');

  @override
  Future<List<ReferenceOption>> listQuarters() =>
      _getReferences('/api/v1/reference-data/schedule-quarters');

  @override
  Future<PreventiveMaintenanceInspection> addInspection(
    String formId,
    AddInspectionInput input,
  ) => addInspectionWithId(formId, input, input.id ?? '');

  @override
  Future<PreventiveMaintenanceInspection> addInspectionWithId(
    String formId,
    AddInspectionInput input,
    String id,
  ) async {
    final json = await _client.postJson(
      '/api/v1/preventive-maintenance-forms/$formId/inspections',
      _inspectionBody(
        id: id.isEmpty ? null : id,
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
      ),
    );
    return PreventiveMaintenanceInspection.fromJson(json);
  }

  @override
  Future<PreventiveMaintenanceInspection> updateInspection(
    String formId,
    String inspectionId,
    UpdateInspectionInput input,
  ) async {
    final json = await _client.putJson(
      '/api/v1/preventive-maintenance-forms/$formId/inspections/$inspectionId',
      _inspectionBody(
        expectedUpdatedAt: input.expectedUpdatedAt,
        dateInspected: input.dateInspected,
        dateAccomplished: input.dateAccomplished,
        isOperational: input.isOperational,
        remarks: input.remarks,
        actionsRecommendations: input.actionsRecommendations,
        waterReplaceCarbonFilter: input.waterReplaceCarbonFilter,
        waterReplaceSedimentFilter: input.waterReplaceSedimentFilter,
        waterCheckUvLight: input.waterCheckUvLight,
      ),
    );
    return PreventiveMaintenanceInspection.fromJson(json);
  }

  @override
  Future<void> deleteInspection(
    String formId,
    String inspectionId,
  ) => _client.deleteEmpty(
    '/api/v1/preventive-maintenance-forms/$formId/inspections/$inspectionId',
  );

  @override
  Future<PreventiveMaintenanceInspection> updateInspectionIfUnchanged(
    String formId,
    String inspectionId,
    UpdateInspectionInput input,
    DateTime expectedUpdatedAt,
  ) async {
    final json = await _client.putJson(
      '/api/v1/preventive-maintenance-forms/$formId/inspections/$inspectionId',
      _inspectionBody(
        expectedUpdatedAt: expectedUpdatedAt,
        dateInspected: input.dateInspected,
        dateAccomplished: input.dateAccomplished,
        isOperational: input.isOperational,
        remarks: input.remarks,
        actionsRecommendations: input.actionsRecommendations,
        waterReplaceCarbonFilter: input.waterReplaceCarbonFilter,
        waterReplaceSedimentFilter: input.waterReplaceSedimentFilter,
        waterCheckUvLight: input.waterCheckUvLight,
      ),
    );
    return PreventiveMaintenanceInspection.fromJson(json);
  }

  @override
  Future<void> deleteInspectionIfUnchanged(
    String formId,
    String inspectionId,
    DateTime? expectedUpdatedAt,
  ) => _client.deleteEmpty(
    '/api/v1/preventive-maintenance-forms/$formId/inspections/$inspectionId'
    '${expectedUpdatedAt == null ? '' : '?expectedUpdatedAt=${Uri.encodeQueryComponent(expectedUpdatedAt.toUtc().toIso8601String())}'}',
  );

  @override
  Future<Uint8List> getInspectionPhoto(String inspectionId) =>
      _client.getBytes('/api/v1/inspections/$inspectionId/photo');

  @override
  Future<void> saveInspectionPhoto(String inspectionId, Uint8List jpegBytes) =>
      _client.putBytes(
        '/api/v1/inspections/$inspectionId/photo',
        jpegBytes,
        contentType: 'image/jpeg',
      );

  @override
  Future<void> deleteInspectionPhoto(String inspectionId) =>
      _client.deleteEmpty('/api/v1/inspections/$inspectionId/photo');

  Future<List<ReferenceOption>> _getReferences(String path) async {
    final values = await _client.getJsonList(path);
    return values.map(_referenceFromValue).toList(growable: false);
  }
}

PreventiveMaintenanceForm _formFromValue(dynamic value) {
  if (value is! Map) throw const FormatException('Invalid form response.');
  return PreventiveMaintenanceForm.fromJson(value.cast<String, dynamic>());
}

ScheduleOption _scheduleFromValue(dynamic value) {
  if (value is! Map) throw const FormatException('Invalid schedule response.');
  return ScheduleOption.fromJson(value.cast<String, dynamic>());
}

ReferenceOption _referenceFromValue(dynamic value) {
  if (value is! Map) throw const FormatException('Invalid reference response.');
  return ReferenceOption.fromJson(value.cast<String, dynamic>());
}

Map<String, dynamic> _inspectionBody({
  String? id,
  DateTime? expectedUpdatedAt,
  String? scheduleId,
  String? inspectorUserId,
  required DateTime dateInspected,
  DateTime? dateAccomplished,
  required bool isOperational,
  required String? remarks,
  required String? actionsRecommendations,
  bool? waterReplaceCarbonFilter,
  bool? waterReplaceSedimentFilter,
  bool? waterCheckUvLight,
}) {
  return <String, dynamic>{
    ...?id == null ? null : <String, dynamic>{'id': id},
    ...?expectedUpdatedAt == null
        ? null
        : <String, dynamic>{
            'expectedUpdatedAt': expectedUpdatedAt.toUtc().toIso8601String(),
          },
    ...?scheduleId == null ? null : <String, dynamic>{'scheduleId': scheduleId},
    ...?inspectorUserId == null
        ? null
        : <String, dynamic>{'inspectorUserId': inspectorUserId},
    'dateInspected': dateInspected.toUtc().toIso8601String(),
    'dateAccomplished': dateAccomplished?.toUtc().toIso8601String(),
    'isOperational': isOperational,
    'remarks': _blankToNull(remarks),
    'actionsRecommendations': _blankToNull(actionsRecommendations),
    ...?waterReplaceCarbonFilter == null
        ? null
        : <String, dynamic>{
            'waterReplaceCarbonFilter': waterReplaceCarbonFilter,
          },
    ...?waterReplaceSedimentFilter == null
        ? null
        : <String, dynamic>{
            'waterReplaceSedimentFilter': waterReplaceSedimentFilter,
          },
    ...?waterCheckUvLight == null
        ? null
        : <String, dynamic>{'waterCheckUvLight': waterCheckUvLight},
  };
}

String? _blankToNull(String? value) {
  final normalized = value?.trim();
  return normalized?.isEmpty ?? true ? null : normalized;
}
