---
id: IMP-032
type: implementation
title: Flutter Android debug cleartext overlay correction
status: reviewed
recordedAtUtc: 2026-09-04T07:29:34Z
sourceBranch: fix/mobile-debug-cleartext-overlay
evidenceLevel: locally-executed
testedCommit: e3f10673eb07af08ce47bbf5b26c04b7eb082fca
buildVerificationCommit: e3f10673eb07af08ce47bbf5b26c04b7eb082fca
buildVerificationStatus: passed
---

# Flutter Android Debug Cleartext Overlay Correction

## Objective

Restore the documented debug-only HTTP development build after release
hardening made the main manifest explicitly deny cleartext traffic.

## Source Identity

- Tested implementation commit:
  `e3f10673eb07af08ce47bbf5b26c04b7eb082fca`
- Source branch: `fix/mobile-debug-cleartext-overlay`
- Integration base: `validation/pmis-only-gsd` at `4af2468`

## Implementation Summary

- Added the Android tools namespace to the debug manifest.
- Added `tools:replace="android:usesCleartextTraffic"` so the debug-only
  `true` overlay can intentionally override the main release-safe `false`
  value.
- Release configuration remains cleartext-disabled; no production endpoint or
  signing material was changed.

## Verification Scope

The corrected debug APK build passed with the documented local HTTP
configuration. Two attempts to install the APK through ADB on the available
Android API 37 emulator hung in the ADB client and were stopped; no emulator
application startup or physical-device verification is claimed. The complete
86-test regression and release APK evidence remain recorded in `TEST-037` for
the parent release-hardening implementation.
