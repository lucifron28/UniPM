import 'package:flutter/material.dart';

import '../../auth/auth_models.dart';

class HomePage extends StatelessWidget {
  const HomePage({
    super.key,
    required this.user,
    this.onScanQr,
    this.onOpenPreventiveMaintenance,
  });

  final AuthUser user;
  final VoidCallback? onScanQr;
  final VoidCallback? onOpenPreventiveMaintenance;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(24),
      children: [
        Text(
          'Welcome, ${user.displayName}',
          style: Theme.of(context).textTheme.headlineSmall,
        ),
        const SizedBox(height: 8),
        const Text('Your field-work session is ready.'),
        const SizedBox(height: 24),
        if (onScanQr != null)
          Card(
            child: ListTile(
              key: const Key('scan-asset-qr'),
              leading: const Icon(Icons.qr_code_scanner),
              title: const Text('Scan asset QR'),
              subtitle: const Text('Capture a UniPM asset QR code.'),
              trailing: const Icon(Icons.chevron_right),
              onTap: onScanQr,
            ),
          ),
        if (onScanQr != null) const SizedBox(height: 16),
        if (onOpenPreventiveMaintenance != null)
          Card(
            child: ListTile(
              leading: const Icon(Icons.assignment_outlined),
              title: const Text('Preventive-maintenance drafts'),
              subtitle: const Text('Create, resume, and update field drafts.'),
              trailing: const Icon(Icons.chevron_right),
              onTap: onOpenPreventiveMaintenance,
            ),
          ),
        if (onOpenPreventiveMaintenance != null) const SizedBox(height: 16),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'Current roles',
                  style: TextStyle(fontWeight: FontWeight.bold),
                ),
                const SizedBox(height: 8),
                Text(user.roles.join(', ')),
              ],
            ),
          ),
        ),
      ],
    );
  }
}
