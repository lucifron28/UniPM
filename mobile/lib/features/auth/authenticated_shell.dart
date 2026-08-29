import 'package:flutter/material.dart';

import '../../auth/session_controller.dart';
import '../assets/asset_qr_lookup_page.dart';
import '../assets/asset_repository.dart';
import '../preventive_maintenance/preventive_maintenance_page.dart';
import '../preventive_maintenance/preventive_maintenance_repository.dart';
import '../qr_scanner/qr_scanner_page.dart';
import 'home_page.dart';

class AuthenticatedShell extends StatelessWidget {
  const AuthenticatedShell({
    super.key,
    required this.controller,
    this.assetRepository,
    this.preventiveMaintenanceRepository,
  });

  final SessionController controller;
  final AssetRepository? assetRepository;
  final PreventiveMaintenanceRepository? preventiveMaintenanceRepository;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('UniPM Mobile'),
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
          final scannedText = await Navigator.of(context).push<String>(
            MaterialPageRoute<String>(
              builder: (_) => const QrScannerPage(),
            ),
          );
          if (!context.mounted || scannedText == null) return;
          final repository = assetRepository;
          if (repository == null) return;
          await Navigator.of(context).push<void>(
            MaterialPageRoute<void>(
              builder: (_) => AssetQrLookupPage(
                repository: repository,
                scannedValue: scannedText,
              ),
            ),
          );
        },
        onOpenPreventiveMaintenance: preventiveMaintenanceRepository == null
            ? null
            : () {
                Navigator.of(context).push(
                  MaterialPageRoute<void>(
                    builder: (_) => PreventiveMaintenancePage(
                      repository: preventiveMaintenanceRepository!,
                      user: controller.user!,
                    ),
                  ),
                );
              },
      ),
    );
  }
}
