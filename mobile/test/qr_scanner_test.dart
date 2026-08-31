import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import 'package:mobile/features/qr_scanner/qr_camera_preview.dart';
import 'package:mobile/features/qr_scanner/qr_scanner_controller.dart';
import 'package:mobile/features/qr_scanner/qr_scanner_page.dart';

void main() {
  test('ignores empty scans and captures the first usable value', () {
    final controller = QrScannerController();
    var notifications = 0;
    controller.addListener(() => notifications++);

    expect(controller.capture(null), isFalse);
    expect(controller.capture('   '), isFalse);
    expect(controller.status, QrScannerStatus.scanning);
    expect(notifications, 0);

    expect(controller.capture('  UNIPM-ASSET-001  '), isTrue);
    expect(controller.status, QrScannerStatus.captured);
    expect(controller.capturedText, 'UNIPM-ASSET-001');
    expect(notifications, 1);

    controller.dispose();
  });

  test('locks after one capture and ignores repeated camera frames', () {
    final controller = QrScannerController();

    expect(controller.capture('UNIPM-FIRST'), isTrue);
    expect(controller.capture('UNIPM-SECOND'), isFalse);
    expect(controller.capturedText, 'UNIPM-FIRST');

    controller.dispose();
  });

  test('retry clears the lock and permits a new capture', () {
    final controller = QrScannerController();

    controller.capture('UNIPM-FIRST');
    controller.retry();

    expect(controller.status, QrScannerStatus.scanning);
    expect(controller.capturedText, isNull);
    expect(controller.capture('UNIPM-SECOND'), isTrue);
    expect(controller.capturedText, 'UNIPM-SECOND');

    controller.dispose();
  });

  testWidgets('scanner pauses, retries, and returns the selected text', (
    tester,
  ) async {
    ValueChanged<String?>? emitScan;
    String? returnedValue;

    await tester.pumpWidget(
      MaterialApp(
        home: Builder(
          builder: (context) => Scaffold(
            body: FilledButton(
              onPressed: () async {
                returnedValue = await Navigator.of(context).push<String>(
                  MaterialPageRoute<String>(
                    builder: (_) => QrScannerPage(
                      previewBuilder: (context, onDetected) {
                        emitScan = onDetected;
                        return const ColoredBox(
                          key: Key('fake-camera-preview'),
                          color: Colors.black,
                        );
                      },
                    ),
                  ),
                );
              },
              child: const Text('Open scanner'),
            ),
          ),
        ),
      ),
    );

    await tester.tap(find.text('Open scanner'));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('fake-camera-preview')), findsOneWidget);
    expect(find.byKey(const Key('cancel-qr-scan')), findsOneWidget);

    emitScan!('UNIPM-FIRST');
    emitScan!('UNIPM-IGNORED');
    await tester.pump();

    expect(find.byKey(const Key('fake-camera-preview')), findsNothing);
    expect(find.text('UNIPM-FIRST'), findsOneWidget);
    expect(find.text('UNIPM-IGNORED'), findsNothing);

    await tester.tap(find.byKey(const Key('retry-qr-scan')));
    await tester.pump();
    expect(find.byKey(const Key('fake-camera-preview')), findsOneWidget);

    emitScan!('UNIPM-SECOND');
    await tester.pump();
    await tester.tap(find.byKey(const Key('use-captured-qr')));
    await tester.pumpAndSettle();

    expect(returnedValue, 'UNIPM-SECOND');
    expect(find.text('Open scanner'), findsOneWidget);
  });

  testWidgets('cancel exits without returning a scanned value', (tester) async {
    String? returnedValue = 'not-returned';

    await tester.pumpWidget(
      MaterialApp(
        home: Builder(
          builder: (context) => Scaffold(
            body: FilledButton(
              onPressed: () async {
                returnedValue = await Navigator.of(context).push<String>(
                  MaterialPageRoute<String>(
                    builder: (_) => QrScannerPage(
                      previewBuilder: (context, onDetected) =>
                          const ColoredBox(color: Colors.black),
                    ),
                  ),
                );
              },
              child: const Text('Open scanner'),
            ),
          ),
        ),
      ),
    );

    await tester.tap(find.text('Open scanner'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('cancel-qr-scan')));
    await tester.pumpAndSettle();

    expect(returnedValue, isNull);
  });

  testWidgets('camera permission denial is clear and remains escapable', (
    tester,
  ) async {
    String? returnedValue = 'not-returned';

    await tester.pumpWidget(
      MaterialApp(
        home: Builder(
          builder: (context) => Scaffold(
            body: FilledButton(
              onPressed: () async {
                returnedValue = await Navigator.of(context).push<String>(
                  MaterialPageRoute<String>(
                    builder: (_) => QrScannerPage(
                      previewBuilder: (context, onDetected) =>
                          const QrCameraErrorView(
                            error: MobileScannerException(
                              errorCode:
                                  MobileScannerErrorCode.permissionDenied,
                            ),
                          ),
                    ),
                  ),
                );
              },
              child: const Text('Open scanner'),
            ),
          ),
        ),
      ),
    );

    await tester.tap(find.text('Open scanner'));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('qr-camera-error')), findsOneWidget);
    expect(
      find.textContaining('Camera permission is required'),
      findsOneWidget,
    );
    expect(find.byKey(const Key('cancel-qr-scan')), findsOneWidget);

    await tester.tap(find.byKey(const Key('cancel-qr-scan')));
    await tester.pumpAndSettle();
    expect(returnedValue, isNull);
  });

  testWidgets('system back exits the scanner without returning a value', (
    tester,
  ) async {
    String? returnedValue = 'not-returned';

    await tester.pumpWidget(
      MaterialApp(
        home: Builder(
          builder: (context) => Scaffold(
            body: FilledButton(
              onPressed: () async {
                returnedValue = await Navigator.of(context).push<String>(
                  MaterialPageRoute<String>(
                    builder: (_) => QrScannerPage(
                      previewBuilder: (context, onDetected) =>
                          const ColoredBox(color: Colors.black),
                    ),
                  ),
                );
              },
              child: const Text('Open scanner'),
            ),
          ),
        ),
      ),
    );

    await tester.tap(find.text('Open scanner'));
    await tester.pumpAndSettle();
    await tester.pageBack();
    await tester.pumpAndSettle();

    expect(returnedValue, isNull);
  });
}
