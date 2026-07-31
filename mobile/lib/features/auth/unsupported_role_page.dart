import 'package:flutter/material.dart';

import '../../auth/session_controller.dart';

class UnsupportedRolePage extends StatelessWidget {
  const UnsupportedRolePage({super.key, required this.controller});

  final SessionController controller;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.lock_outline, size: 48),
              const SizedBox(height: 16),
              const Text(
                'Mobile field access is not available for this role.',
                textAlign: TextAlign.center,
                style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 8),
              const Text(
                'This foundation supports Inspector and GSD users. Contact an administrator if this is unexpected.',
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 24),
              OutlinedButton(
                onPressed: controller.logout,
                child: const Text('Log out'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
