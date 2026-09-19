import 'package:flutter/foundation.dart';

enum QrScannerStatus { scanning, captured }

class QrScannerController extends ChangeNotifier {
  QrScannerStatus status = QrScannerStatus.scanning;
  String? capturedText;

  bool capture(String? rawValue) {
    if (status != QrScannerStatus.scanning) return false;

    final value = rawValue?.trim();
    if (value == null || value.isEmpty) return false;

    capturedText = value;
    status = QrScannerStatus.captured;
    notifyListeners();
    return true;
  }

  void retry() {
    if (status == QrScannerStatus.scanning && capturedText == null) return;

    capturedText = null;
    status = QrScannerStatus.scanning;
    notifyListeners();
  }
}
