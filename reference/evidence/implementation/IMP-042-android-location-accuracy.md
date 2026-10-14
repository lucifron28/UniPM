---
id: IMP-042
type: implementation
title: Android location accuracy mapping correction
status: implemented
evidenceLevel: source-inspected
---

Source commit: `54c43a3a25550c1a84efb2b6daf3eb3dcbed141d`.

Local diagnostics found saved attempts with Precise permission, approximately
4.3 meters distance, a 100-meter radius, and missing accuracy. The device's
Android location diagnostics reported horizontal accuracy, including a recent
fused reading of 100 meters. These are separate readings, not a matched capture.

Source inspection of the installed geolocator_android 5.0.3 package found that
AndroidPosition.fromMap copies the accuracy number but does not forward the
Position.hasAccuracy flag. The native mapper includes accuracy only when Android
reports it; missing values become zero in the Dart parser.

The mobile adapter now recovers positive finite Android accuracy values when
the flag is missing. Zero without an availability flag remains unmeasured.
Other platforms, backend classification, and continuation behavior are unchanged.
No dependency cache or package version was edited.

Changed-file Dart formatting and git diff --check passed. No tests, analyze,
build, or verification of the corrected application on the physical phone were
run, following the user's demo-time no-tests instruction. The phone needs a
Flutter hot restart or installation of a rebuilt application before retesting.
No merge or push occurred.
