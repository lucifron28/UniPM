class PreventiveMaintenanceForm {
  const PreventiveMaintenanceForm({
    required this.id,
    required this.fileNumber,
    required this.assetCategory,
    required this.building,
    required this.department,
    required this.periodType,
    required this.quarter,
    required this.semester,
    required this.year,
    required this.academicYear,
    required this.status,
    required this.createdByUserId,
    required this.submittedByUserId,
    required this.submittedAt,
    required this.createdAt,
    required this.updatedAt,
    required this.inspections,
  });

  final String id;
  final String? fileNumber;
  final String assetCategory;
  final String? building;
  final String? department;
  final String periodType;
  final String? quarter;
  final String? semester;
  final int? year;
  final String? academicYear;
  final String status;
  final String createdByUserId;
  final String? submittedByUserId;
  final DateTime? submittedAt;
  final DateTime createdAt;
  final DateTime updatedAt;
  final List<PreventiveMaintenanceInspection> inspections;

  bool get isDraft => status == 'Draft';

  factory PreventiveMaintenanceForm.fromJson(Map<String, dynamic> json) {
    return PreventiveMaintenanceForm(
      id: _requiredUuid(json, 'id'),
      fileNumber: _nullableString(json, 'fileNumber'),
      assetCategory: _requiredText(json, 'assetCategory'),
      building: _nullableString(json, 'building'),
      department: _nullableString(json, 'department'),
      periodType: _requiredText(json, 'periodType'),
      quarter: _nullableString(json, 'quarter'),
      semester: _nullableString(json, 'semester'),
      year: _nullableInt(json, 'year'),
      academicYear: _nullableString(json, 'academicYear'),
      status: _requiredText(json, 'status'),
      createdByUserId: _requiredUuid(json, 'createdByUserId'),
      submittedByUserId: _nullableUuid(json, 'submittedByUserId'),
      submittedAt: _nullableDateTime(json, 'submittedAt'),
      createdAt: _requiredDateTime(json, 'createdAt'),
      updatedAt: _requiredDateTime(json, 'updatedAt'),
      inspections: _requiredList(
        json,
        'inspections',
      ).map(_mapInspection).toList(growable: false),
    );
  }

  PreventiveMaintenanceForm copyWith({
    List<PreventiveMaintenanceInspection>? inspections,
  }) {
    return PreventiveMaintenanceForm(
      id: id,
      fileNumber: fileNumber,
      assetCategory: assetCategory,
      building: building,
      department: department,
      periodType: periodType,
      quarter: quarter,
      semester: semester,
      year: year,
      academicYear: academicYear,
      status: status,
      createdByUserId: createdByUserId,
      submittedByUserId: submittedByUserId,
      submittedAt: submittedAt,
      createdAt: createdAt,
      updatedAt: updatedAt,
      inspections: inspections ?? this.inspections,
    );
  }
}

class PreventiveMaintenanceInspection {
  const PreventiveMaintenanceInspection({
    required this.id,
    required this.scheduleId,
    required this.assetId,
    required this.inspectorUserId,
    required this.dateInspected,
    required this.isOperational,
    required this.remarks,
    required this.actionsRecommendations,
    required this.createdAt,
    required this.updatedAt,
  });

  final String id;
  final String scheduleId;
  final String assetId;
  final String inspectorUserId;
  final DateTime dateInspected;
  final bool isOperational;
  final String? remarks;
  final String? actionsRecommendations;
  final DateTime createdAt;
  final DateTime updatedAt;

  factory PreventiveMaintenanceInspection.fromJson(Map<String, dynamic> json) {
    return PreventiveMaintenanceInspection(
      id: _requiredUuid(json, 'id'),
      scheduleId: _requiredUuid(json, 'scheduleId'),
      assetId: _requiredUuid(json, 'assetId'),
      inspectorUserId: _requiredUuid(json, 'inspectorUserId'),
      dateInspected: _requiredDateTime(json, 'dateInspected'),
      isOperational: _requiredBool(json, 'isOperational'),
      remarks: _nullableString(json, 'remarks'),
      actionsRecommendations: _nullableString(json, 'actionsRecommendations'),
      createdAt: _requiredDateTime(json, 'createdAt'),
      updatedAt: _requiredDateTime(json, 'updatedAt'),
    );
  }
}

class ReferenceOption {
  const ReferenceOption({required this.code, required this.displayName});

  final String code;
  final String displayName;

  factory ReferenceOption.fromJson(Map<String, dynamic> json) {
    return ReferenceOption(
      code: _requiredText(json, 'code'),
      displayName: _requiredText(json, 'displayName'),
    );
  }
}

class ScheduleOption {
  const ScheduleOption({
    required this.id,
    required this.assetId,
    required this.scheduleDate,
    required this.periodType,
    required this.status,
    required this.asset,
  });

  final String id;
  final String assetId;
  final DateTime scheduleDate;
  final String periodType;
  final String status;
  final ScheduleAssetOption? asset;

  factory ScheduleOption.fromJson(Map<String, dynamic> json) {
    final assetJson = json['asset'];
    if (assetJson is! Map) {
      throw const FormatException('Invalid schedule response.');
    }
    return ScheduleOption(
      id: _requiredUuid(json, 'id'),
      assetId: _requiredUuid(json, 'assetId'),
      scheduleDate: _requiredDateTime(json, 'scheduleDate'),
      periodType: _requiredText(json, 'periodType'),
      status: _requiredText(json, 'status'),
      asset: ScheduleAssetOption.fromJson(assetJson.cast<String, dynamic>()),
    );
  }
}

class ScheduleAssetOption {
  const ScheduleAssetOption({
    required this.id,
    required this.assetCode,
    required this.assetCategory,
    required this.building,
    required this.department,
    required this.location,
  });

  final String id;
  final String assetCode;
  final String assetCategory;
  final String? building;
  final String? department;
  final String? location;

  factory ScheduleAssetOption.fromJson(Map<String, dynamic> json) {
    return ScheduleAssetOption(
      id: _requiredUuid(json, 'id'),
      assetCode: _requiredText(json, 'assetCode'),
      assetCategory: _requiredText(json, 'assetCategory'),
      building: _nullableString(json, 'building'),
      department: _nullableString(json, 'department'),
      location: _nullableString(json, 'location'),
    );
  }
}

class CreatePreventiveMaintenanceFormInput {
  const CreatePreventiveMaintenanceFormInput({
    required this.assetCategory,
    required this.building,
    required this.department,
    required this.periodType,
    required this.quarter,
    required this.semester,
    required this.year,
    required this.academicYear,
  });

  final String assetCategory;
  final String? building;
  final String? department;
  final String periodType;
  final String? quarter;
  final String? semester;
  final int? year;
  final String? academicYear;
}

class AddInspectionInput {
  const AddInspectionInput({
    required this.scheduleId,
    required this.inspectorUserId,
    required this.dateInspected,
    required this.isOperational,
    required this.remarks,
    required this.actionsRecommendations,
  });

  final String scheduleId;
  final String inspectorUserId;
  final DateTime dateInspected;
  final bool isOperational;
  final String? remarks;
  final String? actionsRecommendations;
}

class UpdateInspectionInput {
  const UpdateInspectionInput({
    required this.inspectorUserId,
    required this.dateInspected,
    required this.isOperational,
    required this.remarks,
    required this.actionsRecommendations,
  });

  final String inspectorUserId;
  final DateTime dateInspected;
  final bool isOperational;
  final String? remarks;
  final String? actionsRecommendations;
}

PreventiveMaintenanceInspection _mapInspection(dynamic value) {
  if (value is! Map) {
    throw const FormatException('Invalid inspection response.');
  }
  return PreventiveMaintenanceInspection.fromJson(
    value.cast<String, dynamic>(),
  );
}

String _requiredText(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value is! String || value.trim().isEmpty) {
    throw FormatException('Invalid response field: $key.');
  }
  return value;
}

String? _nullableString(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value == null) return null;
  if (value is String) return value;
  throw FormatException('Invalid response field: $key.');
}

String _requiredUuid(Map<String, dynamic> json, String key) {
  final value = _requiredText(json, key);
  final uuid = RegExp(
    r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$',
  );
  if (!uuid.hasMatch(value)) {
    throw FormatException('Invalid response field: $key.');
  }
  return value;
}

String? _nullableUuid(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value == null) return null;
  if (value is! String) throw FormatException('Invalid response field: $key.');
  final copy = <String, dynamic>{key: value};
  return _requiredUuid(copy, key);
}

DateTime _requiredDateTime(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value is! String) throw FormatException('Invalid response field: $key.');
  final parsed = DateTime.tryParse(value);
  if (parsed == null) throw FormatException('Invalid response field: $key.');
  return parsed;
}

DateTime? _nullableDateTime(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value == null) return null;
  final copy = <String, dynamic>{key: value};
  return _requiredDateTime(copy, key);
}

int? _nullableInt(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value == null) return null;
  if (value is int) return value;
  if (value is String && RegExp(r'^-?\d+$').hasMatch(value)) {
    return int.parse(value);
  }
  throw FormatException('Invalid response field: $key.');
}

bool _requiredBool(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value is! bool) throw FormatException('Invalid response field: $key.');
  return value;
}

List<dynamic> _requiredList(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value is! List<dynamic>) {
    throw FormatException('Invalid response field: $key.');
  }
  return value;
}
