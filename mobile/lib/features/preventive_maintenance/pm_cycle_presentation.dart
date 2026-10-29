const _monthNames = <String>[
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December',
];

final _pmCyclePattern = RegExp(r'^(\d{4})-(0[1-9]|1[0-2])$');

({int year, int month})? _parsePmCycle(String? value) {
  final match = _pmCyclePattern.firstMatch(value?.trim() ?? '');
  if (match == null) return null;

  final year = int.parse(match.group(1)!);
  if (year < 1) return null;

  return (year: year, month: int.parse(match.group(2)!));
}

String formatPmCycle(String? value) {
  final cycle = _parsePmCycle(value);
  if (cycle == null) return 'Not recorded';

  return '${_monthNames[cycle.month - 1]} ${cycle.year}';
}

DateTime? getPmCycleDueDate(String? value) {
  final cycle = _parsePmCycle(value);
  if (cycle == null) return null;

  // UTC represents the Asia/Manila civil month-end, not a deadline instant.
  return DateTime.utc(cycle.year, cycle.month + 1, 0);
}

int? getPmCycleDaysUntilDue(String? value, {DateTime? now}) {
  final dueDate = getPmCycleDueDate(value);
  if (dueDate == null) return null;

  final manilaNow = (now ?? DateTime.now()).toUtc().add(
    const Duration(hours: 8),
  );
  final manilaToday = DateTime.utc(
    manilaNow.year,
    manilaNow.month,
    manilaNow.day,
  );
  return dueDate.difference(manilaToday).inDays;
}

String formatPmCycleDueDate(String? value) {
  final dueDate = getPmCycleDueDate(value);
  if (dueDate == null) return 'Not recorded';

  return '${_monthNames[dueDate.month - 1]} ${dueDate.day}, ${dueDate.year}';
}

String formatAssignmentPeriodDate(DateTime? value) {
  if (value == null) return 'Not available';

  final manila = value.toUtc().add(const Duration(hours: 8));
  final hour = manila.hour.toString().padLeft(2, '0');
  final minute = manila.minute.toString().padLeft(2, '0');
  final second = manila.second.toString().padLeft(2, '0');
  return '${_monthNames[manila.month - 1]} ${manila.day}, ${manila.year} '
      '$hour:$minute:$second Asia/Manila';
}

String formatAssignmentPeriodStatus(String? value) => switch (value) {
  'Unavailable' => 'Assignment period unavailable',
  'WithinPeriod' => 'Within 30 calendar days',
  'Overdue' => 'Overdue and unfinished',
  'CompletedWithinPeriod' => 'Completed within 30 calendar days',
  'CompletedLate' => 'Completed after 30 calendar days',
  'Cancelled' => 'Schedule cancelled',
  null || '' => 'Not available',
  _ => value,
};
