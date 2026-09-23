import 'package:flutter/material.dart';

import '../../auth/session_controller.dart';
import '../../ui/widgets/unipm_brand_mark.dart';
import '../assets/asset_qr_lookup_page.dart';
import '../assets/asset_repository.dart';
import '../assets/asset_search_page.dart';
import '../maintenance_history/asset_maintenance_history_repository.dart';
import '../preventive_maintenance/preventive_maintenance_acknowledgement_page.dart';
import '../preventive_maintenance/preventive_maintenance_controller.dart';
import '../preventive_maintenance/preventive_maintenance_models.dart';
import '../preventive_maintenance/preventive_maintenance_page.dart';
import '../preventive_maintenance/preventive_maintenance_repository.dart';
import '../qr_scanner/qr_scanner_page.dart';
import '../qr_scanner/qr_scan_result.dart';
import 'home_page.dart';

class AuthenticatedShell extends StatelessWidget {
  const AuthenticatedShell({
    super.key,
    required this.controller,
    this.assetRepository,
    this.preventiveMaintenanceRepository,
    this.assetMaintenanceHistoryRepository,
  });

  final SessionController controller;
  final AssetRepository? assetRepository;
  final PreventiveMaintenanceRepository? preventiveMaintenanceRepository;
  final AssetMaintenanceHistoryRepository? assetMaintenanceHistoryRepository;

  Future<void> _openAssetLookup(
    BuildContext context,
    String codeOrQr, {
    bool isCodeLookup = false,
    PmBatchScope? batchScope,
  }) async {
    final repository = assetRepository;
    if (repository == null) return;
    await Navigator.of(context).push<void>(
      MaterialPageRoute<void>(
        builder: (_) => AssetQrLookupPage(
          repository: repository,
          scannedValue: codeOrQr,
          isCodeLookup: isCodeLookup,
          batchScope: batchScope,
          preventiveMaintenanceRepository: preventiveMaintenanceRepository,
          assetMaintenanceHistoryRepository: assetMaintenanceHistoryRepository,
          user: controller.user,
        ),
      ),
    );
  }

  Future<void> _handleStartBatch(
    BuildContext context,
    PmBatchScope scope,
  ) async {
    final result = await Navigator.of(context).push<QrScanResult>(
      MaterialPageRoute<QrScanResult>(builder: (_) => const QrScannerPage()),
    );
    if (!context.mounted || result == null) return;
    switch (result) {
      case QrScanSuccess(:final qrCode):
        await _openAssetLookup(context, qrCode, batchScope: scope);
      case QrManualCodeEntry(:final assetCode):
        await _openAssetLookup(
          context,
          assetCode,
          isCodeLookup: true,
          batchScope: scope,
        );
    }
  }

  Future<void> _handleEnterAssetCode(BuildContext context) async {
    final codeController = TextEditingController();
    final code = await showDialog<String>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Enter Asset Code'),
        content: TextField(
          key: const Key('manual-code-input'),
          controller: codeController,
          autofocus: true,
          textCapitalization: TextCapitalization.characters,
          decoration: const InputDecoration(
            hintText: 'e.g. FE-CS-001',
            labelText: 'Asset Code',
          ),
          onSubmitted: (val) {
            if (val.trim().isNotEmpty) {
              Navigator.of(dialogContext).pop(val.trim());
            }
          },
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('Cancel'),
          ),
          FilledButton(
            key: const Key('submit-asset-code'),
            onPressed: () {
              final val = codeController.text.trim();
              if (val.isNotEmpty) {
                Navigator.of(dialogContext).pop(val);
              }
            },
            child: const Text('Find Asset'),
          ),
        ],
      ),
    );
    if (!context.mounted || code == null || code.isEmpty) return;
    await _openAssetLookup(context, code, isCodeLookup: true);
  }

  Future<void> _handleSearchAssets(BuildContext context) async {
    final repository = assetRepository;
    if (repository == null) return;
    await Navigator.of(context).push<void>(
      MaterialPageRoute<void>(
        builder: (_) => AssetSearchPage(
          repository: repository,
          preventiveMaintenanceRepository: preventiveMaintenanceRepository,
          assetMaintenanceHistoryRepository: assetMaintenanceHistoryRepository,
          user: controller.user,
        ),
      ),
    );
  }

  Future<void> _handleOpenBatch(BuildContext context, String formId) async {
    final repo = preventiveMaintenanceRepository;
    final user = controller.user;
    if (repo == null || user == null) return;
    final pmController = PreventiveMaintenanceController(
      repository: repo,
      user: user,
    );
    await Navigator.of(context).push<void>(
      MaterialPageRoute<void>(
        builder: (_) => PreventiveMaintenanceDraftPage(
          controller: pmController,
          formId: formId,
        ),
      ),
    );
  }

  Future<void> _handleOpenAcknowledgement(
    BuildContext context,
    PreventiveMaintenanceForm form,
  ) async {
    final repo = preventiveMaintenanceRepository;
    final user = controller.user;
    if (repo == null || user == null) return;
    final pmController = PreventiveMaintenanceController(
      repository: repo,
      user: user,
    );
    pmController.selectForm(form);
    await Navigator.of(context).push<void>(
      MaterialPageRoute<void>(
        builder: (_) => PreventiveMaintenanceAcknowledgementPage(
          controller: pmController,
          form: form,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const UniPmBrandMark(
          size: BrandMarkSize.compact,
          showSubtitle: false,
        ),
        actions: [
          IconButton(
            onPressed: controller.logout,
            tooltip: 'Log out',
            icon: const Icon(Icons.logout),
          ),
        ],
      ),
      body: HomePage(
        user: controller.user!,
        onScanQr: () async {
          final result = await Navigator.of(context).push<QrScanResult>(
            MaterialPageRoute<QrScanResult>(
              builder: (_) => const QrScannerPage(),
            ),
          );
          if (!context.mounted || result == null) return;
          switch (result) {
            case QrScanSuccess(:final qrCode):
              await _openAssetLookup(context, qrCode, isCodeLookup: false);
            case QrManualCodeEntry(:final assetCode):
              await _openAssetLookup(context, assetCode, isCodeLookup: true);
          }
        },
        onEnterAssetCode: () => _handleEnterAssetCode(context),
        onSearchAssets: () => _handleSearchAssets(context),
        onStartBatch: (scope) => _handleStartBatch(context, scope),
        preventiveMaintenanceRepository: preventiveMaintenanceRepository,
        onOpenForm: (formId) => _handleOpenBatch(context, formId),
        onOpenAcknowledgement: (form) =>
            _handleOpenAcknowledgement(context, form),
        onOpenPreventiveMaintenance:
            (controller.user?.roles.contains('GSD') == true &&
                preventiveMaintenanceRepository != null)
            ? () {
                Navigator.of(context).push(
                  MaterialPageRoute<void>(
                    builder: (_) => PreventiveMaintenancePage(
                      repository: preventiveMaintenanceRepository!,
                      user: controller.user!,
                    ),
                  ),
                );
              }
            : null,
      ),
    );
  }
}
