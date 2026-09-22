sealed class QrScanResult {
  const QrScanResult();
}

final class QrScanSuccess extends QrScanResult {
  const QrScanSuccess(this.qrCode);

  final String qrCode;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is QrScanSuccess &&
          runtimeType == other.runtimeType &&
          qrCode == other.qrCode;

  @override
  int get hashCode => qrCode.hashCode;

  @override
  String toString() => 'QrScanSuccess(qrCode: $qrCode)';
}

final class QrManualCodeEntry extends QrScanResult {
  const QrManualCodeEntry(this.assetCode);

  final String assetCode;

  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      other is QrManualCodeEntry &&
          runtimeType == other.runtimeType &&
          assetCode == other.assetCode;

  @override
  int get hashCode => assetCode.hashCode;

  @override
  String toString() => 'QrManualCodeEntry(assetCode: $assetCode)';
}
