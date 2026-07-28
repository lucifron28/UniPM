import 'package:flutter/material.dart';

import '../../auth/session_controller.dart';
import 'home_page.dart';

class AuthenticatedShell extends StatelessWidget {
  const AuthenticatedShell({super.key, required this.controller});

  final SessionController controller;

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
      body: HomePage(user: controller.user!),
    );
  }
}
