String displayAssetCategory(String value) {
  final normalized = value.trim();
  if (normalized.isEmpty) return value;

  return normalized
      .split(RegExp(r'[-_]+'))
      .where((part) => part.isNotEmpty)
      .map(
        (part) => part.length == 1
            ? part.toUpperCase()
            : '${part[0].toUpperCase()}${part.substring(1).toLowerCase()}',
      )
      .join(' ');
}
