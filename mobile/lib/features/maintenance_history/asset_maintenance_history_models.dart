class AssetMaintenanceHistoryRecord {
  const AssetMaintenanceHistoryRecord({
    required this.id,
    required this.dateInspected,
    required this.isOperational,
    required this.remarks,
    required this.actionsRecommendations,
  });

  final String id;
  final DateTime dateInspected;
  final bool isOperational;
  final String? remarks;
  final String? actionsRecommendations;

  factory AssetMaintenanceHistoryRecord.fromJson(Map<String, dynamic> json) {
    return AssetMaintenanceHistoryRecord(
      id: _requiredUuid(json, 'id'),
      dateInspected: _requiredDateTime(json, 'dateInspected'),
      isOperational: _requiredBool(json, 'isOperational'),
      remarks: _nullableString(json, 'remarks'),
      actionsRecommendations: _nullableString(json, 'actionsRecommendations'),
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
