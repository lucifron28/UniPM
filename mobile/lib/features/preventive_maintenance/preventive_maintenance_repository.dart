import '../../api/api_client.dart';
import 'preventive_maintenance_models.dart';

abstract interface class PreventiveMaintenanceRepository {
  Future<List<PreventiveMaintenanceForm>> listForms();
  Future<PreventiveMaintenanceForm> getForm(String id);
  Future<PreventiveMaintenanceForm> createForm(
    CreatePreventiveMaintenanceFormInput input,
  );
  Future<List<ScheduleOption>> listSchedules();
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

class ApiPreventiveMaintenanceRepository
    implements PreventiveMaintenanceRepository {
  const ApiPreventiveMaintenanceRepository(this._client);

  final ApiClient _client;

  @override
  Future<List<PreventiveMaintenanceForm>> listForms() async {
    final values = await _client.getJsonList(
      '/api/v1/preventive-maintenance-forms',
    );
    return values.map(_formFromValue).toList(growable: false);
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
  ) async {
    return PreventiveMaintenanceForm.fromJson(
      await _client.postJson('/api/v1/preventive-maintenance-forms', {
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
  Future<List<ScheduleOption>> listSchedules() async {
    final values = await _client.getJsonList('/api/v1/schedules');
    return values.map(_scheduleFromValue).toList(growable: false);
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
  ) async {
    final json = await _client.postJson(
      '/api/v1/preventive-maintenance-forms/$formId/inspections',
      _inspectionBody(
        scheduleId: input.scheduleId,
        inspectorUserId: input.inspectorUserId,
        dateInspected: input.dateInspected,
        isOperational: input.isOperational,
        remarks: input.remarks,
        actionsRecommendations: input.actionsRecommendations,
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
        inspectorUserId: input.inspectorUserId,
        dateInspected: input.dateInspected,
        isOperational: input.isOperational,
        remarks: input.remarks,
        actionsRecommendations: input.actionsRecommendations,
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
  String? scheduleId,
  required String inspectorUserId,
  required DateTime dateInspected,
  required bool isOperational,
  required String? remarks,
  required String? actionsRecommendations,
}) {
  return <String, dynamic>{
    ...?scheduleId == null ? null : <String, dynamic>{'scheduleId': scheduleId},
    'inspectorUserId': inspectorUserId,
    'dateInspected': dateInspected.toUtc().toIso8601String(),
    'isOperational': isOperational,
    'remarks': _blankToNull(remarks),
    'actionsRecommendations': _blankToNull(actionsRecommendations),
  };
}

String? _blankToNull(String? value) {
  final normalized = value?.trim();
  return normalized?.isEmpty ?? true ? null : normalized;
}
