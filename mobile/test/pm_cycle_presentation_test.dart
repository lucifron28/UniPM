import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/preventive_maintenance/pm_cycle_presentation.dart';

void main() {
  group('PM cycle presentation', () {
    test('formats canonical cycles as month and year', () {
      expect(formatPmCycle('2026-11'), 'November 2026');
      expect(formatPmCycle('2028-02'), 'February 2028');
    });

    test('derives month-end for leap February and 30- and 31-day months', () {
      expect(formatPmCycleDueDate('2028-02'), 'February 29, 2028');
      expect(formatPmCycleDueDate('2026-06'), 'June 30, 2026');
      expect(formatPmCycleDueDate('2026-11'), 'November 30, 2026');
      expect(formatPmCycleDueDate('2026-12'), 'December 31, 2026');
    });

    test(
      'keeps the civil due date at UTC midnight independent of local time',
      () {
        final dueDate = getPmCycleDueDate('2028-02');

        expect(dueDate, isNotNull);
        expect(dueDate!.isUtc, isTrue);
        expect(dueDate.toIso8601String(), '2028-02-29T00:00:00.000Z');
      },
    );

    test('fails closed for an invalid or missing cycle', () {
      expect(formatPmCycle('2026-13'), 'Not recorded');
      expect(formatPmCycleDueDate(null), 'Not recorded');
      expect(getPmCycleDueDate('2026-00'), isNull);
      expect(formatPmCycle('0000-01'), 'Not recorded');
      expect(getPmCycleDueDate('0000-01'), isNull);
    });
  });
}
