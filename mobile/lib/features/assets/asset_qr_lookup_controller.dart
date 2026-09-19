import 'package:flutter/foundation.dart';

import '../../api/api_exception.dart';
import 'asset_models.dart';
import 'asset_repository.dart';

enum AssetQrLookupStatus {
  idle,
  loading,
  success,
  invalidQr,
  notFound,
  failure,
}

class AssetQrLookupController extends ChangeNotifier {
  AssetQrLookupController(this.repository);

  final AssetRepository repository;
  bool _disposed = false;

  AssetQrLookupStatus status = AssetQrLookupStatus.idle;
  Asset? asset;
  String? scannedValue;
  String? errorMessage;

  @override
  void dispose() {
    _disposed = true;
    super.dispose();
  }

  Future<void> lookup(String value) async {
    if (_disposed) return;

    status = AssetQrLookupStatus.loading;
    asset = null;
    scannedValue = value;
    errorMessage = null;
    notifyListeners();

    try {
      final nextAsset = await repository.getByQr(value);
      if (_disposed) return;
      asset = nextAsset;
      status = AssetQrLookupStatus.success;
    } on InvalidUniPmQrException catch (error) {
      if (_disposed) return;
      status = AssetQrLookupStatus.invalidQr;
      errorMessage = error.message;
    } on ApiException catch (error) {
      if (_disposed) return;
      if (error.statusCode == 404) {
        status = AssetQrLookupStatus.notFound;
        errorMessage = 'No asset matches this UniPM QR code.';
      } else {
        status = AssetQrLookupStatus.failure;
        errorMessage = error.message;
      }
    } on FormatException {
      if (_disposed) return;
      status = AssetQrLookupStatus.failure;
      errorMessage = 'The server returned an invalid asset response.';
    } catch (_) {
      if (_disposed) return;
      status = AssetQrLookupStatus.failure;
      errorMessage = 'The mobile service is unavailable. Please try again.';
    }
    if (_disposed) return;
    notifyListeners();
  }
}
