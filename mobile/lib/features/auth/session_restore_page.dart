import 'package:flutter/material.dart';

class SessionRestorePage extends StatelessWidget {
  const SessionRestorePage({super.key, this.configurationError});

  final String? configurationError;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: configurationError == null
              ? const Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    CircularProgressIndicator(),
                    SizedBox(height: 16),
                    Text('Restoring your UniPM session...'),
                  ],
                )
              : Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.settings_outlined, size: 48),
                    const SizedBox(height: 16),
                    const Text(
                      'Mobile setup required',
                      style: TextStyle(
                        fontSize: 20,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(configurationError!, textAlign: TextAlign.center),
                  ],
                ),
        ),
      ),
    );
  }
}
