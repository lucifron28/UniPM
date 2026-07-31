import 'package:flutter/material.dart';

class ConfigurationErrorPage extends StatelessWidget {
  const ConfigurationErrorPage({super.key, required this.configurationError});

  final String? configurationError;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.settings_outlined, size: 48),
              const SizedBox(height: 16),
              const Text(
                'Mobile setup required',
                style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
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
