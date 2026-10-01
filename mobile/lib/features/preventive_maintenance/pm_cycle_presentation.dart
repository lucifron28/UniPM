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

String formatPmCycleDueDate(String? value) {
  final dueDate = getPmCycleDueDate(value);
  if (dueDate == null) return 'Not recorded';

  return '${_monthNames[dueDate.month - 1]} ${dueDate.day}, ${dueDate.year}';
}
