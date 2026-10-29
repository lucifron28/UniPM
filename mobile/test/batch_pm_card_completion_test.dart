import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/ui/widgets/batch_pm_card.dart';

void main() {
  for (final status in ['Draft', 'Submitted', 'Acknowledged']) {
    testWidgets(
      'completed $status preserves lifecycle without overdue work warning',
      (tester) async {
        tester.view.physicalSize = const Size(393, 852);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: BatchPmCard(
                department: 'GSD',
                assetCategory: 'fire-extinguisher',
                pmCycle: '2026-08',
                status: status,
                completedCount: 3,
                totalCount: 3,
                daysUntilDue: -3,
              ),
            ),
          ),
        );
        expect(find.text('Overdue by 3 days'), findsNothing);
        expect(find.text('Inspections complete'), findsOneWidget);
        expect(find.text('3 of 3 assets inspected'), findsOneWidget);
        expect(
          find.text(
            status == 'Submitted' ? 'Awaiting acknowledgement' : status,
          ),
          findsOneWidget,
        );
      },
    );
  }

  testWidgets('unfinished work retains countdown and assignment context', (
    tester,
  ) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: BatchPmCard(
            department: 'GSD',
            assetCategory: 'fire-extinguisher',
            pmCycle: '2026-08',
            status: 'Draft',
            completedCount: 2,
            totalCount: 3,
            daysUntilDue: -3,
            assignmentPeriodSummary: 'Inspector assignment: 12 days remaining',
          ),
        ),
      ),
    );
    expect(find.text('Overdue by 3 days'), findsOneWidget);
    expect(find.text('Inspections complete'), findsNothing);
    expect(
      find.text('Inspector assignment: 12 days remaining'),
      findsOneWidget,
    );
  });
}
