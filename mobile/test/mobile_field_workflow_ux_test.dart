import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:mobile/auth/auth_models.dart';
import 'package:mobile/features/auth/home_page.dart';
import 'package:mobile/ui/display_labels.dart';

void main() {
  test('formats backend category codes for display without changing codes', () {
    expect(displayAssetCategory('fire-alarm'), 'Fire Alarm');
    expect(
      displayAssetCategory('water_drinking_station'),
      'Water Drinking Station',
    );
    expect(displayAssetCategory(' fire-extinguisher '), 'Fire Extinguisher');
  });

  testWidgets('home entry names the complete preventive-maintenance journey', (
    tester,
  ) async {
    const user = AuthUser(
      id: '11111111-1111-4111-8111-111111111111',
      email: 'inspector@example.test',
      displayName: 'Synthetic Inspector',
      roles: ['Inspector'],
    );

    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: HomePage(user: user, onOpenPreventiveMaintenance: _noop),
        ),
      ),
    );

    expect(find.text('Preventive-maintenance forms'), findsOneWidget);
    expect(
      find.text('Create, resume, submit, and acknowledge PM forms.'),
      findsOneWidget,
    );
    expect(find.text('Preventive-maintenance drafts'), findsNothing);
  });
}

void _noop() {}
