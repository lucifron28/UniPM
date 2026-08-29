import 'package:flutter/material.dart';

import 'qr_camera_preview.dart';
import 'qr_scanner_controller.dart';

typedef QrPreviewBuilder = Widget Function(
  BuildContext context,
  ValueChanged<String?> onDetected,
);

class QrScannerPage extends StatefulWidget {
  const QrScannerPage({
    super.key,
    this.controller,
    this.previewBuilder,
  });

  final QrScannerController? controller;
  final QrPreviewBuilder? previewBuilder;

  @override
  State<QrScannerPage> createState() => _QrScannerPageState();
}

class _QrScannerPageState extends State<QrScannerPage> {
  late final QrScannerController controller =
      widget.controller ?? QrScannerController();
  late final bool ownsController = widget.controller == null;

  @override
  void dispose() {
    if (ownsController) controller.dispose();
    super.dispose();
  }

  void _cancel() => Navigator.of(context).pop();

  void _useCapturedValue() {
    final value = controller.capturedText;
    if (value != null) Navigator.of(context).pop(value);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Scan asset QR')),
      body: SafeArea(
        child: AnimatedBuilder(
          animation: controller,
          builder: (context, _) => controller.status == QrScannerStatus.scanning
              ? _buildScanner(context)
              : _buildCaptured(context),
        ),
      ),
    );
  }

  Widget _buildScanner(BuildContext context) {
    final preview = widget.previewBuilder?.call(context, controller.capture) ??
        QrCameraPreview(onDetected: controller.capture);

    return Column(
      children: [
        const Padding(
          padding: EdgeInsets.fromLTRB(24, 20, 24, 16),
          child: Text(
            'Place one UniPM QR code inside the camera view.',
            textAlign: TextAlign.center,
          ),
        ),
        Expanded(
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: ClipRRect(
              borderRadius: BorderRadius.circular(16),
              child: preview,
            ),
          ),
        ),
        Padding(
          padding: const EdgeInsets.all(24),
          child: SizedBox(
            width: double.infinity,
            child: OutlinedButton(
              key: const Key('cancel-qr-scan'),
              onPressed: _cancel,
              child: const Text('Cancel'),
            ),
          ),
        ),
      ],
    );
  }

  Widget _buildCaptured(BuildContext context) {
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.qr_code_2, size: 64),
            const SizedBox(height: 16),
            Text(
              'QR captured',
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            const SizedBox(height: 8),
            const Text(
              'Scanning is paused. Confirm this value or scan again.',
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 16),
            SelectableText(
              controller.capturedText!,
              key: const Key('captured-qr-text'),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 24),
            FilledButton(
              key: const Key('use-captured-qr'),
              onPressed: _useCapturedValue,
              child: const Text('Use scanned value'),
            ),
            const SizedBox(height: 8),
            TextButton(
              key: const Key('retry-qr-scan'),
              onPressed: controller.retry,
              child: const Text('Scan again'),
            ),
          ],
        ),
      ),
    );
  }
}
