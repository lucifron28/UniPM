class AssetMaintenanceHistoryRecord {
  const AssetMaintenanceHistoryRecord({
    required this.id,
    required this.dateInspected,
    this.dateAccomplished,
    required this.isOperational,
    required this.remarks,
    required this.actionsRecommendations,
    this.waterReplaceCarbonFilter,
    this.waterReplaceSedimentFilter,
    this.waterCheckUvLight,
  });

  final String id;
  final DateTime dateInspected;
  final DateTime? dateAccomplished;
  final bool isOperational;
  final String? remarks;
  final String? actionsRecommendations;
  final bool? waterReplaceCarbonFilter;
  final bool? waterReplaceSedimentFilter;
  final bool? waterCheckUvLight;

  factory AssetMaintenanceHistoryRecord.fromJson(Map<String, dynamic> json) {
    return AssetMaintenanceHistoryRecord(
      id: _requiredUuid(json, 'id'),
      dateInspected: _requiredDateTime(json, 'dateInspected'),
      dateAccomplished: _nullableDateTime(json, 'dateAccomplished'),
      isOperational: _requiredBool(json, 'isOperational'),
      remarks: _nullableString(json, 'remarks'),
      actionsRecommendations: _nullableString(json, 'actionsRecommendations'),
      waterReplaceCarbonFilter: _nullableBool(json, 'waterReplaceCarbonFilter'),
      waterReplaceSedimentFilter: _nullableBool(
        json,
        'waterReplaceSedimentFilter',
      ),
      waterCheckUvLight: _nullableBool(json, 'waterCheckUvLight'),
    );
  }
}

String _requiredText(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value is! String || value.trim().isEmpty) {
    throw FormatException('Invalid history response field: $key.');
  }
  return value;
}

String _requiredUuid(Map<String, dynamic> json, String key) {
  final value = _requiredText(json, key);
  final uuid = RegExp(
    r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$',
  );
  if (!uuid.hasMatch(value)) {
    throw FormatException('Invalid history response field: $key.');
  }
  return value;
}

DateTime _requiredDateTime(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value is! String) {
    throw FormatException('Invalid history response field: $key.');
  }
  final parsed = DateTime.tryParse(value);
  if (parsed == null) {
    throw FormatException('Invalid history response field: $key.');
  }
  return parsed;
}

DateTime? _nullableDateTime(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value == null) return null;
  final copy = <String, dynamic>{key: value};
  return _requiredDateTime(copy, key);
}

bool _requiredBool(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value is! bool) {
    throw FormatException('Invalid history response field: $key.');
  }
  return value;
}

String? _nullableString(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value == null) return null;
  if (value is String) return value;
  throw FormatException('Invalid history response field: $key.');
}

bool? _nullableBool(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value == null) return null;
  if (value is bool) return value;
  throw FormatException('Invalid history response field: $key.');
}
