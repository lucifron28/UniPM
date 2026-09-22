import '../../api/api_client.dart';
import 'asset_models.dart';

abstract interface class AssetRepository {
  Future<Asset> getByQr(String scannedValue);
  Future<Asset> getByCode(String assetCode);
  Future<List<Asset>> searchAssets({
    String? search,
    String? assetCategory,
    int limit = 25,
  });
}

class ApiAssetRepository implements AssetRepository {
  const ApiAssetRepository(this._client);

  final ApiClient _client;

  @override
  Future<Asset> getByQr(String scannedValue) async {
    final qrCodeValue = validateUniPmQrValue(scannedValue);
    final encodedValue = Uri.encodeComponent(qrCodeValue);
    final json = await _client.getJson('/api/v1/assets/by-qr/$encodedValue');
    return Asset.fromJson(json);
  }

  @override
  Future<Asset> getByCode(String assetCode) async {
    final code = assetCode.trim();
    if (code.isEmpty) {
      throw const FormatException('Asset code cannot be empty.');
    }
    final encoded = Uri.encodeComponent(code);
    final json = await _client.getJson('/api/v1/assets/by-code/$encoded');
    return Asset.fromJson(json);
  }

  @override
  Future<List<Asset>> searchAssets({
    String? search,
    String? assetCategory,
    int limit = 25,
  }) async {
    final params = <String>[];
    if (search != null && search.trim().isNotEmpty) {
      params.add('search=${Uri.encodeQueryComponent(search.trim())}');
    }
    if (assetCategory != null && assetCategory.trim().isNotEmpty) {
      params.add('assetCategory=${Uri.encodeQueryComponent(assetCategory.trim())}');
    }
    params.add('limit=${limit.clamp(1, 50)}');
    final path = '/api/v1/assets?${params.join('&')}';
    final values = await _client.getJsonList(path);
    return values
        .map((v) => Asset.fromJson(v as Map<String, dynamic>))
        .toList(growable: false);
  }
}

String validateUniPmQrValue(String scannedValue) {
  final value = scannedValue.trim();
  if (value.isEmpty) {
    throw const InvalidUniPmQrException(
      'The scanned QR code is empty. Scan a UniPM asset QR code.',
    );
  }
  if (!value.toUpperCase().startsWith('UNIPM-')) {
    throw const InvalidUniPmQrException('This is not a UniPM asset QR code.');
  }
  return value;
}

class InvalidUniPmQrException implements Exception {
  const InvalidUniPmQrException(this.message);

  final String message;

  @override
  String toString() => message;
}
