import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

class QrCameraPreview extends StatelessWidget {
  const QrCameraPreview({super.key, required this.onDetected});

  final ValueChanged<String?> onDetected;

  @override
  Widget build(BuildContext context) {
    return MobileScanner(
      onDetect: (capture) {
        for (final barcode in capture.barcodes) {
          if (barcode.format == BarcodeFormat.qrCode) {
            onDetected(barcode.rawValue);
            return;
          }
        }
      },
    );
  }
}
