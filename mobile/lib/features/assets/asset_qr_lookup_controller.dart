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

  AssetQrLookupStatus status = AssetQrLookupStatus.idle;
  Asset? asset;
  String? scannedValue;
  String? errorMessage;

  Future<void> lookup(String value) async {
    status = AssetQrLookupStatus.loading;
    asset = null;
    scannedValue = value;
    errorMessage = null;
    notifyListeners();

    try {
      asset = await repository.getByQr(value);
      status = AssetQrLookupStatus.success;
    } on InvalidUniPmQrException catch (error) {
      status = AssetQrLookupStatus.invalidQr;
      errorMessage = error.message;
    } on ApiException catch (error) {
      if (error.statusCode == 404) {
        status = AssetQrLookupStatus.notFound;
        errorMessage = 'No asset matches this UniPM QR code.';
      } else {
        status = AssetQrLookupStatus.failure;
        errorMessage = error.message;
      }
    } on FormatException {
      status = AssetQrLookupStatus.failure;
      errorMessage = 'The server returned an invalid asset response.';
    } catch (_) {
      status = AssetQrLookupStatus.failure;
      errorMessage = 'The mobile service is unavailable. Please try again.';
    }
    notifyListeners();
  }
}
