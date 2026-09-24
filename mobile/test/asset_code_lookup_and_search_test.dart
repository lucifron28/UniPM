import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:mobile/api/api_client.dart';
import 'package:mobile/api/api_exception.dart';
import 'package:mobile/features/assets/asset_models.dart';
import 'package:mobile/features/assets/asset_qr_lookup_controller.dart';
import 'package:mobile/features/assets/asset_repository.dart';
import 'package:mobile/features/assets/asset_search_page.dart';
import 'package:mobile/features/qr_scanner/qr_scanner_controller.dart';
import 'package:mobile/features/qr_scanner/qr_scanner_page.dart';
import 'package:mobile/ui/widgets/status_badge.dart';

const testAssetId = '22222222-2222-4222-8222-222222222222';
const testQrValue = 'UNIPM-FIREEXTINGUISHER-22222222';

Asset createTestAsset({
  String code = 'FE-CS-001',
  String category = 'fire-extinguisher',
  String building = 'Science Hall',
  String department = 'College of Science',
  String location = 'Room 201',
}) => Asset(
  id: testAssetId,
  assetCode: code,
  assetCategory: category,
  building: building,
  department: department,
  location: location,
  qrCodeValue: testQrValue,
  status: 'Active',
);

Map<String, dynamic> assetToMap(Asset asset) => <String, dynamic>{
  'id': asset.id,
  'assetCode': asset.assetCode,
  'assetCategory': asset.assetCategory,
  'building': asset.building,
  'department': asset.department,
  'location': asset.location,
  'qrCodeValue': asset.qrCodeValue,
  'status': asset.status,
  'createdAt': '2026-08-01T00:00:00Z',
  'updatedAt': '2026-08-01T00:00:00Z',
};

ApiAssetRepository createRepositoryWith(
  Future<http.Response> Function(http.Request request) handler,
) {
  final client = ApiClient(
    baseUrl: Uri.parse('http://localhost:5000/'),
    httpClient: MockClient(handler),
  );
  client.configureSession(
    accessTokenProvider: () => 'bearer-token-field-worker',
    terminalAuthFailureHandler: () async {},
  );
  return ApiAssetRepository(client);
}

class FakeSearchAssetRepository implements AssetRepository {
  FakeSearchAssetRepository({this.assets = const [], this.throwOnCode = false});

  final List<Asset> assets;
  final bool throwOnCode;

  @override
  Future<Asset> getByQr(String scannedValue) async {
    return assets.firstWhere(
      (a) => a.qrCodeValue == scannedValue,
      orElse: () =>
          throw const ApiException(statusCode: 404, message: 'QR not found.'),
    );
  }

  @override
  Future<Asset> getByCode(String assetCode) async {
    if (throwOnCode) {
      throw const ApiException(
        statusCode: 404,
        message: 'No asset matches this asset code.',
      );
    }
    return assets.firstWhere(
      (a) => a.assetCode.toLowerCase() == assetCode.toLowerCase(),
      orElse: () => throw const ApiException(
        statusCode: 404,
        message: 'Asset code not found.',
      ),
    );
  }

  @override
  Future<List<Asset>> searchAssets({
    String? search,
    String? assetCategory,
    int limit = 25,
  }) async {
    return assets.where((a) {
      if (assetCategory != null && a.assetCategory != assetCategory) {
        return false;
      }
      if (search == null || search.trim().isEmpty) return true;
      final q = search.toLowerCase();
      return a.assetCode.toLowerCase().contains(q) ||
          (a.building?.toLowerCase().contains(q) ?? false) ||
          (a.department?.toLowerCase().contains(q) ?? false) ||
          (a.location?.toLowerCase().contains(q) ?? false);
    }).toList();
  }
}

void main() {
  group('Manual Asset-Code Lookup & API Integration', () {
    test('getByCode calls the by-code endpoint with normalized code', () async {
      http.Request? capturedRequest;
      final asset = createTestAsset();
      final repository = createRepositoryWith((request) async {
        capturedRequest = request;
        return http.Response(jsonEncode(assetToMap(asset)), 200);
      });

      final result = await repository.getByCode('FE-CS-001');

      expect(capturedRequest?.method, 'GET');
      expect(capturedRequest?.url.path, '/api/v1/assets/by-code/FE-CS-001');
      expect(
        capturedRequest?.headers['authorization'],
        'Bearer bearer-token-field-worker',
      );
      expect(result.assetCode, 'FE-CS-001');
      expect(result.assetCategory, 'fire-extinguisher');
    });

    test('getByCode rejects empty code before making HTTP call', () async {
      final repository = createRepositoryWith(
        (_) async => http.Response('{}', 200),
      );
      expect(
        () => repository.getByCode('   '),
        throwsA(isA<FormatException>()),
      );
    });

    test(
      'searchAssets builds query parameters with limit and search term',
      () async {
        http.Request? capturedRequest;
        final asset = createTestAsset();
        final repository = createRepositoryWith((request) async {
          capturedRequest = request;
          return http.Response(jsonEncode([assetToMap(asset)]), 200);
        });

        final results = await repository.searchAssets(
          search: 'Science',
          assetCategory: 'fire-extinguisher',
          limit: 15,
        );

        expect(capturedRequest?.method, 'GET');
        expect(capturedRequest?.url.path, '/api/v1/assets');
        expect(capturedRequest?.url.queryParameters['search'], 'Science');
        expect(
          capturedRequest?.url.queryParameters['assetCategory'],
          'fire-extinguisher',
        );
        expect(capturedRequest?.url.queryParameters['limit'], '15');
        expect(results.length, 1);
        expect(results.first.assetCode, 'FE-CS-001');
      },
    );

    test('controller handles successful code lookup', () async {
      final asset = createTestAsset();
      final repository = FakeSearchAssetRepository(assets: [asset]);
      final controller = AssetQrLookupController(repository);

      await controller.lookupCode('FE-CS-001');

      expect(controller.status, AssetQrLookupStatus.success);
      expect(controller.asset?.assetCode, 'FE-CS-001');
      expect(controller.errorMessage, isNull);
    });

    test('controller handles 404 not-found for unknown asset code', () async {
      final repository = FakeSearchAssetRepository(throwOnCode: true);
      final controller = AssetQrLookupController(repository);

      await controller.lookupCode('UNKNOWN-999');

      expect(controller.status, AssetQrLookupStatus.notFound);
      expect(controller.asset, isNull);
      expect(controller.errorMessage, 'No asset matches this asset code.');
    });
  });

  group('Asset Search Page & Multi-Field Search UX', () {
    testWidgets(
      'search page displays results and allows filtering by category',
      (tester) async {
        final asset1 = createTestAsset(
          code: 'FE-001',
          category: 'fire-extinguisher',
          department: 'Science',
        );
        final asset2 = createTestAsset(
          code: 'FA-002',
          category: 'fire-alarm',
          department: 'Science',
        );
        final repository = FakeSearchAssetRepository(assets: [asset1, asset2]);

        await tester.pumpWidget(
          MaterialApp(home: AssetSearchPage(repository: repository)),
        );
        await tester.pumpAndSettle();

        // Both assets initially visible
        expect(find.text('FE-001'), findsOneWidget);
        expect(find.text('FA-002'), findsOneWidget);

        // Filter by category: tap 'Fire Alarm' chip
        await tester.tap(find.widgetWithText(FilterChip, 'Fire Alarm'));
        await tester.pumpAndSettle();

        // Only FA-002 matches
        expect(find.text('FA-002'), findsOneWidget);
        expect(find.text('FE-001'), findsNothing);

        // Search by text: enter '001'
        await tester.enterText(
          find.byKey(const Key('asset-search-input')),
          '001',
        );
        // Wait for 300ms debounce
        await tester.pump(const Duration(milliseconds: 350));
        await tester.pumpAndSettle();

        // No fire alarm with '001'
        expect(find.text('No assets match your search'), findsOneWidget);

        // Deselect chip
        await tester.tap(find.widgetWithText(FilterChip, 'Fire Alarm'));
        await tester.pumpAndSettle();

        // Now FE-001 shows up
        expect(find.text('FE-001'), findsOneWidget);
      },
    );

    testWidgets('search page explains when results reach the 30-item cap', (
      tester,
    ) async {
      final assets = List.generate(
        30,
        (index) =>
            createTestAsset(code: 'FE-${index.toString().padLeft(3, '0')}'),
      );
      final repository = FakeSearchAssetRepository(assets: assets);

      await tester.pumpWidget(
        MaterialApp(home: AssetSearchPage(repository: repository)),
      );
      await tester.pumpAndSettle();

      expect(
        find.byKey(const Key('asset-search-result-limit')),
        findsOneWidget,
      );
      expect(
        find.text(
          'Showing up to 30 assets. Refine your search if the asset is not listed.',
        ),
        findsOneWidget,
      );
    });

    testWidgets('tapping an asset in search page opens asset lookup detail', (
      tester,
    ) async {
      final asset = createTestAsset(code: 'FE-TAP-001');
      final repository = FakeSearchAssetRepository(assets: [asset]);

      await tester.pumpWidget(
        MaterialApp(home: AssetSearchPage(repository: repository)),
      );
      await tester.pumpAndSettle();

      await tester.tap(find.text('FE-TAP-001'));
      await tester.pumpAndSettle();

      expect(find.text('Asset found'), findsOneWidget);
      expect(find.byKey(const Key('asset-code')), findsOneWidget);
      expect(find.text('FE-TAP-001'), findsOneWidget);
    });
  });

  group('Damaged QR Fallback & Scanner Integration', () {
    testWidgets('scanner offers manual asset-code dialog when QR is damaged', (
      tester,
    ) async {
      final controller = QrScannerController();

      await tester.pumpWidget(
        MaterialApp(
          home: QrScannerPage(
            controller: controller,
            previewBuilder: (context, onDetected) =>
                const SizedBox(key: Key('mock-camera-preview'), height: 200),
          ),
        ),
      );
      await tester.pumpAndSettle();

      // Fallback button is visible
      expect(
        find.byKey(const Key('enter-code-manually-button')),
        findsOneWidget,
      );
      expect(find.text('Damaged QR? Enter asset code'), findsOneWidget);

      // Tap fallback button
      await tester.tap(find.byKey(const Key('enter-code-manually-button')));
      await tester.pumpAndSettle();

      // Dialog opens
      expect(find.text('Enter Asset Code'), findsOneWidget);
      expect(find.byKey(const Key('manual-asset-code-input')), findsOneWidget);
      expect(find.byKey(const Key('submit-manual-asset-code')), findsOneWidget);

      // Enter code and submit
      await tester.enterText(
        find.byKey(const Key('manual-asset-code-input')),
        'FE-DAMAGED-01',
      );
      await tester.tap(find.byKey(const Key('submit-manual-asset-code')));
      await tester.pumpAndSettle();

      // Dialog and scanner popped, returning entered code
      expect(find.text('Enter Asset Code'), findsNothing);
    });
  });

  test('asset status badges use semantic variants', () {
    expect(
      StatusBadge.fromAssetStatus('Active').variant,
      StatusBadgeVariant.active,
    );
    expect(
      StatusBadge.fromAssetStatus('Inactive').variant,
      StatusBadgeVariant.inactive,
    );
    expect(
      StatusBadge.fromAssetStatus('Retired').variant,
      StatusBadgeVariant.retired,
    );
  });
}
