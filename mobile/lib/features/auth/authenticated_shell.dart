import 'package:flutter/material.dart';

import '../../auth/session_controller.dart';
import '../preventive_maintenance/preventive_maintenance_page.dart';
import '../preventive_maintenance/preventive_maintenance_repository.dart';
import 'home_page.dart';

class AuthenticatedShell extends StatelessWidget {
  const AuthenticatedShell({
    super.key,
    required this.controller,
    this.preventiveMaintenanceRepository,
  });

  final SessionController controller;
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
