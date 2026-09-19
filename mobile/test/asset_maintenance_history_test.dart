import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:mobile/api/api_client.dart';
import 'package:mobile/api/api_exception.dart';
import 'package:mobile/features/assets/asset_models.dart';
import 'package:mobile/features/assets/asset_qr_lookup_page.dart';
import 'package:mobile/features/assets/asset_repository.dart';
import 'package:mobile/features/maintenance_history/asset_maintenance_history_controller.dart';
import 'package:mobile/features/maintenance_history/asset_maintenance_history_models.dart';
import 'package:mobile/features/maintenance_history/asset_maintenance_history_page.dart';
import 'package:mobile/features/maintenance_history/asset_maintenance_history_repository.dart';

const assetId = '11111111-1111-4111-8111-111111111111';
const historyId = '22222222-2222-4222-8222-222222222222';
const qrCodeValue = 'UNIPM-FIREALARM-11111111';

Asset testAsset() => const Asset(
  id: assetId,
  assetCode: 'FA-001',
  assetCategory: 'fire-alarm',
  building: 'Main Building',
  department: 'GSD',
  location: 'First floor',
  qrCodeValue: qrCodeValue,
  status: 'Active',
);

AssetMaintenanceHistoryRecord testRecord({
  bool isOperational = false,
  String? remarks = 'Panel fault recorded',
  String? actionsRecommendations = 'Review the panel indication',
}) => AssetMaintenanceHistoryRecord(
  id: historyId,
  dateInspected: DateTime.parse('2026-08-15T08:00:00+08:00'),
  isOperational: isOperational,
  remarks: remarks,
  actionsRecommendations: actionsRecommendations,
);

Map<String, dynamic> historyJson() => <String, dynamic>{
  'id': historyId,
  'dateInspected': '2026-08-15T08:00:00+08:00',
  'isOperational': false,
  'remarks': 'Panel fault recorded',
  'actionsRecommendations': 'Review the panel indication',
};

class FakeAssetMaintenanceHistoryRepository
    implements AssetMaintenanceHistoryRepository {
  FakeAssetMaintenanceHistoryRepository({
    this.records = const [],
    this.failuresRemaining = 0,
    this.failure,
    this.pending,
  });

  List<AssetMaintenanceHistoryRecord> records;
  int failuresRemaining;
  Object? failure;
  Future<List<AssetMaintenanceHistoryRecord>>? pending;
  final requestedAssetIds = <String>[];

  @override
  Future<List<AssetMaintenanceHistoryRecord>> getForAsset(
    String assetId,
  ) async {
    requestedAssetIds.add(assetId);
    if (pending != null) return await pending!;
    if (failuresRemaining > 0) {
      failuresRemaining--;
      throw failure ??
          const ApiException(
            message: 'The mobile service is unavailable. Please try again.',
          );
    }
    return records;
  }
}

class FakeAssetRepository implements AssetRepository {
  @override
  Future<Asset> getByQr(String scannedValue) async => testAsset();
}

void main() {
  test('history response parses the acknowledged-history fields', () {
    final record = AssetMaintenanceHistoryRecord.fromJson(historyJson());

    expect(record.id, historyId);
    expect(record.dateInspected, DateTime.parse('2026-08-15T08:00:00+08:00'));
    expect(record.isOperational, isFalse);
    expect(record.remarks, 'Panel fault recorded');
    expect(record.actionsRecommendations, 'Review the panel indication');
  });

  test(
    'history repository uses the exact asset ID and bearer session',
    () async {
      http.Request? capturedRequest;
      final client = ApiClient(
        baseUrl: Uri.parse('http://localhost:5000/'),
        httpClient: MockClient((request) async {
          capturedRequest = request;
          return http.Response(jsonEncode([historyJson()]), 200);
        }),
      );
      client.configureSession(
        accessTokenProvider: () => 'inspector-access-token',
        terminalAuthFailureHandler: () async {},
      );

      final repository = ApiAssetMaintenanceHistoryRepository(client);
      final records = await repository.getForAsset(assetId);

      expect(records, hasLength(1));
      expect(capturedRequest?.url.path, '/api/v1/inspections/history/$assetId');
      expect(
        capturedRequest?.headers['authorization'],
        'Bearer inspector-access-token',
      );
      client.dispose();
    },
  );

  test('protected history 401 invokes terminal session handling', () async {
    var terminalFailures = 0;
    final client = ApiClient(
      baseUrl: Uri.parse('http://localhost:5000/'),
      httpClient: MockClient((request) async => http.Response('', 401)),
    );
    client.configureSession(
      accessTokenProvider: () => 'expired-access-token',
      terminalAuthFailureHandler: () async => terminalFailures++,
    );

    final repository = ApiAssetMaintenanceHistoryRepository(client);

    await expectLater(
      repository.getForAsset(assetId),
      throwsA(
        isA<ApiException>().having(
          (error) => error.statusCode,
          'status code',
          401,
        ),
      ),
    );
    expect(terminalFailures, 1);
    client.dispose();
  });

  test(
    'controller exposes successful empty and malformed-response states',
    () async {
      final emptyRepository = FakeAssetMaintenanceHistoryRepository();
      final emptyController = AssetMaintenanceHistoryController(
        repository: emptyRepository,
        assetId: assetId,
      );
      await emptyController.load();
      expect(emptyController.status, AssetMaintenanceHistoryStatus.success);
      expect(emptyController.records, isEmpty);
      emptyController.dispose();

      final malformedRepository = FakeAssetMaintenanceHistoryRepository(
        failure: const FormatException('malformed'),
        failuresRemaining: 1,
      );
      final malformedController = AssetMaintenanceHistoryController(
        repository: malformedRepository,
        assetId: assetId,
      );
      await malformedController.load();
      expect(malformedController.status, AssetMaintenanceHistoryStatus.failure);
      expect(
        malformedController.errorMessage,
        'The server returned an invalid history response.',
      );
      malformedController.dispose();
    },
  );

  test('controller maps session expiry to a bounded retryable error', () async {
    final repository = FakeAssetMaintenanceHistoryRepository(
      failuresRemaining: 1,
      failure: const ApiException(
        statusCode: 401,
        message: 'internal auth detail',
      ),
    );
    final controller = AssetMaintenanceHistoryController(
      repository: repository,
      assetId: assetId,
    );

    await controller.load();

    expect(controller.status, AssetMaintenanceHistoryStatus.failure);
    expect(
      controller.errorMessage,
      'Your session expired. Please sign in again.',
    );
    expect(controller.errorMessage, isNot(contains('internal auth detail')));
    controller.dispose();
  });

  testWidgets(
    'history page shows official record details and no draft states',
    (tester) async {
      final repository = FakeAssetMaintenanceHistoryRepository(
        records: [testRecord()],
      );

      await tester.pumpWidget(
        MaterialApp(
          home: AssetMaintenanceHistoryPage(
            asset: testAsset(),
            repository: repository,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Maintenance history'), findsOneWidget);
      expect(find.text('FA-001'), findsOneWidget);
      expect(find.text('Fire Alarm'), findsOneWidget);
      expect(
        find.text('Official history contains acknowledged records only.'),
        findsOneWidget,
      );
      expect(
        find.byKey(Key('asset-history-record-$historyId')),
        findsOneWidget,
      );
      expect(find.text('2026-08-15'), findsOneWidget);
      expect(find.text('Non-operational'), findsOneWidget);
      expect(find.text('Panel fault recorded'), findsOneWidget);
      expect(find.text('Review the panel indication'), findsOneWidget);
      expect(find.text('Acknowledged official record'), findsOneWidget);
      expect(find.text('Draft'), findsNothing);
      expect(find.text('Submitted'), findsNothing);
    },
  );

  testWidgets('history page provides an empty state', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: AssetMaintenanceHistoryPage(
          asset: testAsset(),
          repository: FakeAssetMaintenanceHistoryRepository(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('asset-history-empty')), findsOneWidget);
    expect(
      find.text(
        'No acknowledged maintenance history has been recorded for this asset.',
      ),
      findsOneWidget,
    );
  });

  testWidgets('history network failure can retry without rescanning', (
    tester,
  ) async {
    final repository = FakeAssetMaintenanceHistoryRepository(
      failuresRemaining: 1,
      records: [testRecord(isOperational: true)],
    );

    await tester.pumpWidget(
      MaterialApp(
        home: AssetMaintenanceHistoryPage(
          asset: testAsset(),
          repository: repository,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('asset-history-error')), findsOneWidget);
    await tester.tap(find.byKey(const Key('retry-asset-history')));
    await tester.pumpAndSettle();

    expect(repository.requestedAssetIds, [assetId, assetId]);
    expect(find.text('Operational'), findsOneWidget);
  });

  testWidgets('scanned asset details open history with the backend asset ID', (
    tester,
  ) async {
    final historyRepository = FakeAssetMaintenanceHistoryRepository();

    await tester.pumpWidget(
      MaterialApp(
        home: AssetQrLookupPage(
          repository: FakeAssetRepository(),
          scannedValue: qrCodeValue,
          assetMaintenanceHistoryRepository: historyRepository,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('view-asset-history')), findsOneWidget);
    await tester.drag(find.byType(ListView), const Offset(0, -300));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('view-asset-history')));
    await tester.pumpAndSettle();

    expect(find.text('Maintenance history'), findsOneWidget);
    expect(historyRepository.requestedAssetIds, [assetId]);
  });

  testWidgets('history page shows loading until the backend responds', (
    tester,
  ) async {
    final pending = Completer<List<AssetMaintenanceHistoryRecord>>();
    final repository = FakeAssetMaintenanceHistoryRepository(
      pending: pending.future,
    );

    await tester.pumpWidget(
      MaterialApp(
        home: AssetMaintenanceHistoryPage(
          asset: testAsset(),
          repository: repository,
        ),
      ),
    );

    expect(find.text('Loading maintenance history...'), findsOneWidget);
    pending.complete([testRecord()]);
    await tester.pumpAndSettle();
    expect(find.byKey(Key('asset-history-record-$historyId')), findsOneWidget);
  });
}
