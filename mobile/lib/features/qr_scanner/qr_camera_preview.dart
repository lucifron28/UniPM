import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

class QrCameraPreview extends StatelessWidget {
  const QrCameraPreview({super.key, required this.onDetected});

  final ValueChanged<String?> onDetected;

  @override
  Widget build(BuildContext context) {
    return MobileScanner(
      errorBuilder: (context, error) => QrCameraErrorView(error: error),
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

class QrCameraErrorView extends StatelessWidget {
  const QrCameraErrorView({super.key, required this.error});

  final MobileScannerException error;

  @override
  Widget build(BuildContext context) {
    final permissionDenied =
        error.errorCode == MobileScannerErrorCode.permissionDenied;
    return ColoredBox(
      key: const Key('qr-camera-error'),
      color: Colors.black,
      child: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.no_photography_outlined, color: Colors.white),
              const SizedBox(height: 12),
              Text(
                permissionDenied
                    ? 'Camera permission is required to scan a QR code. '
                          'You can go back or cancel, then enable camera access '
                          'in Android settings.'
                    : 'The camera could not be started. Go back or cancel and '
                          'try again.',
                textAlign: TextAlign.center,
                style: const TextStyle(color: Colors.white),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
