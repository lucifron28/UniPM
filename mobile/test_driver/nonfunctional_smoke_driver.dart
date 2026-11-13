import 'dart:io';

import 'package:integration_test/integration_test_driver_extended.dart';

Future<void> main() async {
  final artifactDirectory = Platform.environment['UNIPM_ANDROID_ARTIFACTS_DIR'];
  if (artifactDirectory == null || artifactDirectory.isEmpty) {
    throw StateError('Android evidence output directory was not configured.');
  }

  await integrationDriver(
    onScreenshot: (screenshotName, screenshotBytes, [args]) async {
      if (!RegExp(r'^[A-Za-z0-9_-]+$').hasMatch(screenshotName)) {
        throw StateError('Screenshot name contains unsupported characters.');
      }
      final image = File('$artifactDirectory/$screenshotName.png');
      await image.writeAsBytes(screenshotBytes, flush: true);
      if (await image.length() < 1024) {
        throw StateError('Integration screenshot was empty or incomplete.');
      }
      return true;
    },
    writeResponseOnFailure: true,
  );
}
