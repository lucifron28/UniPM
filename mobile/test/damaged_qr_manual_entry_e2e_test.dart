import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:mobile/api/api_exception.dart';
import 'package:mobile/features/assets/asset_models.dart';
import 'package:mobile/features/assets/asset_qr_lookup_page.dart';
import 'package:mobile/features/assets/asset_repository.dart';
import 'package:mobile/features/qr_scanner/qr_scan_result.dart';
import 'package:mobile/features/qr_scanner/qr_scanner_page.dart';

class TrackingAssetRepository implements AssetRepository {
  TrackingAssetRepository(this.assets);

  final List<Asset> assets;
  final List<String> getByQrCalls = [];
  final List<String> getByCodeCalls = [];

  @override
  Future<Asset> getByQr(String scannedValue) async {
    getByQrCalls.add(scannedValue);
    return assets.firstWhere(
      (a) => a.qrCodeValue == scannedValue,
      orElse: () => throw const ApiException(
        statusCode: 404,
        message: 'Asset not found by QR.',
      ),
    );
  }

  @override
  Future<Asset> getByCode(String assetCode) async {
    getByCodeCalls.add(assetCode);
    return assets.firstWhere(
      (a) => a.assetCode.toLowerCase() == assetCode.toLowerCase(),
      orElse: () => throw const ApiException(
        statusCode: 404,
        message: 'Asset not found by code.',
      ),
    );
  }

  @override
  Future<List<Asset>> searchAssets({
    String? search,
    String? assetCategory,
    int limit = 25,
  }) async =>
      assets;
}

/// A representative caller workflow matching the application contract.
/// When the user opens the scanner and completes it:
/// - [QrScanSuccess] triggers asset lookup with `isCodeLookup: false`.
/// - [QrManualCodeEntry] triggers asset lookup with `isCodeLookup: true`.
class DamagedQrWorkflowHost extends StatefulWidget {
  const DamagedQrWorkflowHost({
    super.key,
    required this.repository,
    this.previewBuilder,
  });

  final AssetRepository repository;
  final QrPreviewBuilder? previewBuilder;

  @override
  State<DamagedQrWorkflowHost> createState() => _DamagedQrWorkflowHostState();
}

class _DamagedQrWorkflowHostState extends State<DamagedQrWorkflowHost> {
  QrScanResult? lastResult;

  Future<void> _openScanner() async {
    final result = await Navigator.of(context).push<QrScanResult>(
      MaterialPageRoute<QrScanResult>(
        builder: (_) => QrScannerPage(
          previewBuilder: widget.previewBuilder ??
              (context, onDetected) => const ColoredBox(
                    key: Key('mock-camera-preview'),
                    color: Colors.black,
                  ),
        ),
      ),
    );

    if (!mounted || result == null) return;
    setState(() => lastResult = result);

    switch (result) {
      case QrScanSuccess(:final qrCode):
        await Navigator.of(context).push<void>(
          MaterialPageRoute<void>(
            builder: (_) => AssetQrLookupPage(
              repository: widget.repository,
              scannedValue: qrCode,
              isCodeLookup: false,
            ),
          ),
        );
      case QrManualCodeEntry(:final assetCode):
        await Navigator.of(context).push<void>(
          MaterialPageRoute<void>(
            builder: (_) => AssetQrLookupPage(
              repository: widget.repository,
              scannedValue: assetCode,
              isCodeLookup: true,
            ),
          ),
        );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Field Worker Scanner Host')),
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            FilledButton.icon(
              key: const Key('open-scanner-button'),
              onPressed: _openScanner,
              icon: const Icon(Icons.qr_code_scanner),
              label: const Text('Open Scanner'),
            ),
            if (lastResult != null)
              Text(
                'Result: $lastResult',
                key: const Key('last-scan-result'),
              ),
          ],
        ),
      ),
    );
  }
}

void main() {
  const testAsset = Asset(
    id: '22222222-2222-4222-8222-222222222222',
    assetCode: 'FE-CS-002',
    assetCategory: 'fire-extinguisher',
    building: 'Science Hall',
    department: 'College of Science',
    location: 'Room 201',
    qrCodeValue: 'UNIPM-FE-CS-002',
    status: 'Active',
  );

  group('Damaged QR Manual Entry End-to-End Workflow', () {
    testWidgets(
      'User opens scanner -> taps "Damaged QR? Enter asset code" -> '
      'enters asset code (FE-CS-002) -> dialog confirms -> '
      'scanner returns QrManualCodeEntry -> caller invokes asset lookup with isCodeLookup: true -> '
      'repository.getByCode is called (NOT getByQr) -> asset detail page renders with asset code and metadata',
      (tester) async {
        final repository = TrackingAssetRepository([testAsset]);

        await tester.pumpWidget(
          MaterialApp(
            home: DamagedQrWorkflowHost(repository: repository),
          ),
        );

        // 1. User opens scanner
        expect(find.byKey(const Key('open-scanner-button')), findsOneWidget);
        await tester.tap(find.byKey(const Key('open-scanner-button')));
        await tester.pumpAndSettle();

        expect(find.byType(QrScannerPage), findsOneWidget);
        expect(find.byKey(const Key('mock-camera-preview')), findsOneWidget);

        // 2. User taps "Damaged QR? Enter asset code"
        final damagedQrButton = find.byKey(const Key('enter-code-manually-button'));
        expect(damagedQrButton, findsOneWidget);
        expect(find.text('Damaged QR? Enter asset code'), findsOneWidget);
        await tester.tap(damagedQrButton);
        await tester.pumpAndSettle();

        // 3. Dialog opens and user enters asset code (FE-CS-002)
        expect(find.text('Enter Asset Code'), findsOneWidget);
        final inputField = find.byKey(const Key('manual-asset-code-input'));
        expect(inputField, findsOneWidget);
        await tester.enterText(inputField, 'FE-CS-002');
        await tester.pump();

        // 4. Dialog confirms via "Find Asset" button
        final submitButton = find.byKey(const Key('submit-manual-asset-code'));
        expect(submitButton, findsOneWidget);
        await tester.tap(submitButton);
        await tester.pumpAndSettle();

        // 5 & 6. Scanner returned QrManualCodeEntry, caller invoked asset lookup with isCodeLookup: true
        // 7. repository.getByCode is called with 'FE-CS-002' and getByQr is NOT called
        expect(repository.getByCodeCalls, equals(['FE-CS-002']));
        expect(repository.getByQrCalls, isEmpty);

        // 8. Asset detail page renders with asset code and metadata!
        expect(find.text('Asset found'), findsOneWidget);
        expect(find.byKey(const Key('asset-code')), findsOneWidget);
        expect(find.text('FE-CS-002'), findsOneWidget);
        expect(find.text('Fire Extinguisher'), findsOneWidget);
        expect(find.text('Science Hall'), findsOneWidget);
        expect(find.text('College of Science'), findsOneWidget);
        expect(find.text('Room 201'), findsOneWidget);
        expect(find.text('Active'), findsOneWidget);
      },
    );

    testWidgets(
      'Cancelling the manual entry dialog keeps the scanner active without triggering any lookup',
      (tester) async {
        final repository = TrackingAssetRepository([testAsset]);

        await tester.pumpWidget(
          MaterialApp(
            home: DamagedQrWorkflowHost(repository: repository),
          ),
        );

        await tester.tap(find.byKey(const Key('open-scanner-button')));
        await tester.pumpAndSettle();

        await tester.tap(find.byKey(const Key('enter-code-manually-button')));
        await tester.pumpAndSettle();

        expect(find.text('Enter Asset Code'), findsOneWidget);
        final dialogCancelButton = find.descendant(
          of: find.byType(AlertDialog),
          matching: find.text('Cancel'),
        );
        await tester.tap(dialogCancelButton);
        await tester.pumpAndSettle();

        // Scanner remains open
        expect(find.byType(QrScannerPage), findsOneWidget);
        expect(find.text('Damaged QR? Enter asset code'), findsOneWidget);
        expect(repository.getByCodeCalls, isEmpty);
        expect(repository.getByQrCalls, isEmpty);
      },
    );

    testWidgets(
      'Submitting empty manual code does not close dialog or trigger lookup',
      (tester) async {
        final repository = TrackingAssetRepository([testAsset]);

        await tester.pumpWidget(
          MaterialApp(
            home: DamagedQrWorkflowHost(repository: repository),
          ),
        );

        await tester.tap(find.byKey(const Key('open-scanner-button')));
        await tester.pumpAndSettle();

        await tester.tap(find.byKey(const Key('enter-code-manually-button')));
        await tester.pumpAndSettle();

        // Click find asset with empty text
        await tester.tap(find.byKey(const Key('submit-manual-asset-code')));
        await tester.pumpAndSettle();

        // Dialog is still open
        expect(find.text('Enter Asset Code'), findsOneWidget);
        expect(repository.getByCodeCalls, isEmpty);
        expect(repository.getByQrCalls, isEmpty);
      },
    );
  });
}
