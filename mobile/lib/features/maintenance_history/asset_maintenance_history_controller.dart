import 'package:flutter/foundation.dart';

import '../../api/api_exception.dart';
import 'asset_maintenance_history_models.dart';
import 'asset_maintenance_history_repository.dart';

enum AssetMaintenanceHistoryStatus { idle, loading, success, failure }

class AssetMaintenanceHistoryController extends ChangeNotifier {
  AssetMaintenanceHistoryController({
    required this.repository,
    required this.assetId,
  });

  final AssetMaintenanceHistoryRepository repository;
  final String assetId;
  bool _disposed = false;

  AssetMaintenanceHistoryStatus status = AssetMaintenanceHistoryStatus.idle;
  List<AssetMaintenanceHistoryRecord> records = const [];
  String? errorMessage;

  @override
  void dispose() {
    _disposed = true;
    super.dispose();
  }

  Future<void> load() async {
    if (_disposed) return;

    status = AssetMaintenanceHistoryStatus.loading;
    records = const [];
    errorMessage = null;
    notifyListeners();

    try {
      final nextRecords = await repository.getForAsset(assetId);
      if (_disposed) return;
      records = nextRecords;
      status = AssetMaintenanceHistoryStatus.success;
    } on ApiException catch (error) {
      if (_disposed) return;
      status = AssetMaintenanceHistoryStatus.failure;
      errorMessage = _apiErrorMessage(error);
    } on FormatException {
      if (_disposed) return;
      status = AssetMaintenanceHistoryStatus.failure;
      errorMessage = 'The server returned an invalid history response.';
    } catch (_) {
      if (_disposed) return;
      status = AssetMaintenanceHistoryStatus.failure;
      errorMessage = 'The mobile service is unavailable. Please try again.';
    }

    if (_disposed) return;
    notifyListeners();
  }

  String _apiErrorMessage(ApiException error) {
    switch (error.statusCode) {
      case 401:
        return 'Your session expired. Please sign in again.';
      case 403:
        return 'This account cannot view official maintenance history.';
      default:
        return error.message;
    }
  }
}
