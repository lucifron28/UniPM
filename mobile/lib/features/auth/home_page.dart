import 'package:flutter/material.dart';

import '../../auth/auth_models.dart';

class HomePage extends StatelessWidget {
  const HomePage({super.key, required this.user});

  final AuthUser user;

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
