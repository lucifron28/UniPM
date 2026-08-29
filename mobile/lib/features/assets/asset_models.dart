class Asset {
  const Asset({
    required this.id,
    required this.assetCode,
    required this.assetCategory,
    required this.building,
    required this.department,
    required this.location,
    required this.qrCodeValue,
    required this.status,
  });

  final String id;
  final String assetCode;
  final String assetCategory;
  final String? building;
  final String? department;
  final String? location;
  final String qrCodeValue;
  final String status;

  factory Asset.fromJson(Map<String, dynamic> json) {
    return Asset(
      id: _requiredUuid(json, 'id'),
      assetCode: _requiredText(json, 'assetCode'),
      assetCategory: _requiredText(json, 'assetCategory'),
      building: _nullableString(json, 'building'),
      department: _nullableString(json, 'department'),
      location: _nullableString(json, 'location'),
      qrCodeValue: _requiredText(json, 'qrCodeValue'),
      status: _requiredText(json, 'status'),
    );
  }
}

String _requiredText(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value is! String || value.trim().isEmpty) {
    throw FormatException('Invalid asset response field: $key.');
  }
  return value;
}

String? _nullableString(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value == null) return null;
  if (value is String) return value;
  throw FormatException('Invalid asset response field: $key.');
}

String _requiredUuid(Map<String, dynamic> json, String key) {
  final value = _requiredText(json, key);
  final uuid = RegExp(
    r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$',
  );
  if (!uuid.hasMatch(value)) {
    throw FormatException('Invalid asset response field: $key.');
  }
  return value;
}
