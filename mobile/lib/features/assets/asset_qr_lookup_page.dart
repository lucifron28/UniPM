import 'dart:async';

import 'package:flutter/material.dart';

import '../../auth/auth_models.dart';
import '../maintenance_history/asset_maintenance_history_page.dart';
import '../maintenance_history/asset_maintenance_history_repository.dart';
import '../preventive_maintenance/preventive_maintenance_repository.dart';
import '../preventive_maintenance/scanned_asset_pm_entry.dart';
import '../qr_scanner/qr_scanner_page.dart';
import 'asset_models.dart';
import 'asset_qr_lookup_controller.dart';
import 'asset_repository.dart';

typedef QrScannerLauncher = Future<String?> Function(BuildContext context);

class AssetQrLookupPage extends StatefulWidget {
  const AssetQrLookupPage({
    super.key,
    required this.repository,
    required this.scannedValue,
    this.controller,
    this.scannerLauncher,
    this.preventiveMaintenanceRepository,
    this.assetMaintenanceHistoryRepository,
    this.user,
  });

  final AssetRepository repository;
  final String scannedValue;
  final AssetQrLookupController? controller;
  final QrScannerLauncher? scannerLauncher;
  final PreventiveMaintenanceRepository? preventiveMaintenanceRepository;
  final AssetMaintenanceHistoryRepository? assetMaintenanceHistoryRepository;
  final AuthUser? user;

  @override
  State<AssetQrLookupPage> createState() => _AssetQrLookupPageState();
}

class _AssetQrLookupPageState extends State<AssetQrLookupPage> {
  late final AssetQrLookupController controller =
      widget.controller ?? AssetQrLookupController(widget.repository);
  late final bool ownsController = widget.controller == null;

  @override
  void initState() {
    super.initState();
    unawaited(controller.lookup(widget.scannedValue));
  }

  @override
  void dispose() {
    if (ownsController) controller.dispose();
    super.dispose();
  }

  Future<void> _scanAnother() async {
    final scannedValue =
        await (widget.scannerLauncher?.call(context) ??
            Navigator.of(context).push<String>(
              MaterialPageRoute<String>(builder: (_) => const QrScannerPage()),
            ));
    if (!mounted || scannedValue == null) return;
    await controller.lookup(scannedValue);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Asset lookup')),
      body: SafeArea(
        child: AnimatedBuilder(
          animation: controller,
          builder: (context, _) {
            return switch (controller.status) {
              AssetQrLookupStatus.idle ||
              AssetQrLookupStatus.loading => const _LookupLoading(),
              AssetQrLookupStatus.success => _AssetDetails(
                asset: controller.asset!,
                onScanAnother: _scanAnother,
                preventiveMaintenanceRepository:
                    widget.preventiveMaintenanceRepository,
                assetMaintenanceHistoryRepository:
                    widget.assetMaintenanceHistoryRepository,
                user: widget.user,
              ),
              AssetQrLookupStatus.invalidQr ||
              AssetQrLookupStatus.notFound ||
              AssetQrLookupStatus.failure => _LookupError(
                title: switch (controller.status) {
                  AssetQrLookupStatus.invalidQr => 'Invalid QR',
                  AssetQrLookupStatus.notFound => 'Asset not found',
                  _ => 'Unable to look up asset',
                },
                message: controller.errorMessage!,
                canRetry: controller.status == AssetQrLookupStatus.failure,
                onRetry: () => controller.lookup(controller.scannedValue!),
                onScanAnother: _scanAnother,
              ),
            };
          },
        ),
      ),
    );
  }
}

class _LookupLoading extends StatelessWidget {
  const _LookupLoading();

  @override
  Widget build(BuildContext context) {
    return const Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          CircularProgressIndicator(),
          SizedBox(height: 16),
          Text('Finding asset...'),
        ],
      ),
    );
  }
}

class _AssetDetails extends StatelessWidget {
  const _AssetDetails({
    required this.asset,
    required this.onScanAnother,
    required this.preventiveMaintenanceRepository,
    required this.assetMaintenanceHistoryRepository,
    required this.user,
  });

  final Asset asset;
  final VoidCallback onScanAnother;
  final PreventiveMaintenanceRepository? preventiveMaintenanceRepository;
  final AssetMaintenanceHistoryRepository? assetMaintenanceHistoryRepository;
  final AuthUser? user;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Icon(
          Icons.check_circle,
          size: 64,
          color: Theme.of(context).colorScheme.primary,
        ),
        const SizedBox(height: 16),
        Text(
          'Asset found',
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.headlineSmall,
        ),
        const SizedBox(height: 24),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  asset.assetCode,
                  key: const Key('asset-code'),
                  style: Theme.of(context).textTheme.headlineMedium,
                ),
                const SizedBox(height: 20),
                _AssetField(label: 'Category', value: asset.assetCategory),
                _AssetField(label: 'Status', value: asset.status),
                if (_hasValue(asset.building))
                  _AssetField(label: 'Building', value: asset.building!),
                if (_hasValue(asset.department))
                  _AssetField(label: 'Department', value: asset.department!),
                if (_hasValue(asset.location))
                  _AssetField(label: 'Location', value: asset.location!),
              ],
            ),
          ),
        ),
        const SizedBox(height: 24),
        if (assetMaintenanceHistoryRepository != null) ...[
          OutlinedButton.icon(
            key: const Key('view-asset-history'),
            onPressed: () {
              Navigator.of(context).push<void>(
                MaterialPageRoute<void>(
                  builder: (_) => AssetMaintenanceHistoryPage(
                    asset: asset,
                    repository: assetMaintenanceHistoryRepository!,
                  ),
                ),
              );
            },
            icon: const Icon(Icons.history),
            label: const Text('View maintenance history'),
          ),
          const SizedBox(height: 24),
        ],
        if (preventiveMaintenanceRepository != null && user != null) ...[
          ScannedAssetPmEntry(
            key: ValueKey('pm-entry-${asset.id}'),
            asset: asset,
            repository: preventiveMaintenanceRepository!,
            user: user!,
          ),
          const SizedBox(height: 24),
        ],
        FilledButton.icon(
          key: const Key('scan-another-asset-qr'),
          onPressed: onScanAnother,
          icon: const Icon(Icons.qr_code_scanner),
          label: const Text('Scan another QR'),
        ),
      ],
    );
  }
}

class _AssetField extends StatelessWidget {
  const _AssetField({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: Theme.of(context).textTheme.labelMedium),
          const SizedBox(height: 2),
          Text(value),
        ],
      ),
    );
  }
}

class _LookupError extends StatelessWidget {
  const _LookupError({
    required this.title,
    required this.message,
    required this.canRetry,
    required this.onRetry,
    required this.onScanAnother,
  });

  final String title;
  final String message;
  final bool canRetry;
  final VoidCallback onRetry;
  final VoidCallback onScanAnother;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              Icons.error_outline,
              size: 64,
              color: Theme.of(context).colorScheme.error,
            ),
            const SizedBox(height: 16),
            Text(
              title,
              key: const Key('asset-lookup-error-title'),
              style: Theme.of(context).textTheme.headlineSmall,
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 8),
            Text(
              message,
              key: const Key('asset-lookup-error'),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 24),
            if (canRetry) ...[
              FilledButton(
                key: const Key('retry-asset-lookup'),
                onPressed: onRetry,
                child: const Text('Try again'),
              ),
              const SizedBox(height: 8),
            ],
            OutlinedButton.icon(
              key: const Key('scan-another-asset-qr'),
              onPressed: onScanAnother,
              icon: const Icon(Icons.qr_code_scanner),
              label: const Text('Scan another QR'),
            ),
          ],
        ),
      ),
    );
  }
}

bool _hasValue(String? value) => value?.trim().isNotEmpty ?? false;
