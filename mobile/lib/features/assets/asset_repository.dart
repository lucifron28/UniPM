import '../../api/api_client.dart';
import 'asset_models.dart';

abstract interface class AssetRepository {
  Future<Asset> getByQr(String scannedValue);
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
}

String validateUniPmQrValue(String scannedValue) {
  final value = scannedValue.trim();
  if (value.isEmpty) {
    throw const InvalidUniPmQrException(
      'The scanned QR code is empty. Scan a UniPM asset QR code.',
    );
  }
  if (!value.toUpperCase().startsWith('UNIPM-')) {
    throw const InvalidUniPmQrException(
      'This is not a UniPM asset QR code.',
    );
  }
  return value;
}

class InvalidUniPmQrException implements Exception {
  const InvalidUniPmQrException(this.message);

  final String message;

  @override
  String toString() => message;
}
