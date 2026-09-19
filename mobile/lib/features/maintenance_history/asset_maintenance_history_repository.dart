import '../../api/api_client.dart';
import 'asset_maintenance_history_models.dart';

abstract interface class AssetMaintenanceHistoryRepository {
  Future<List<AssetMaintenanceHistoryRecord>> getForAsset(String assetId);
}

class ApiAssetMaintenanceHistoryRepository
    implements AssetMaintenanceHistoryRepository {
  const ApiAssetMaintenanceHistoryRepository(this._client);

  final ApiClient _client;

  @override
  Future<List<AssetMaintenanceHistoryRecord>> getForAsset(
    String assetId,
  ) async {
    final values = await _client.getJsonList(
      '/api/v1/inspections/history/${Uri.encodeComponent(assetId)}',
    );
    return values.map(_recordFromValue).toList(growable: false);
  }
}

AssetMaintenanceHistoryRecord _recordFromValue(dynamic value) {
  if (value is! Map) {
    throw const FormatException('Invalid history response.');
  }
  return AssetMaintenanceHistoryRecord.fromJson(value.cast<String, dynamic>());
}
