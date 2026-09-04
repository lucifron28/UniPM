---
id: TEST-038
type: test-run
title: Flutter Android debug cleartext overlay correction verification
status: executed
recordedAtUtc: 2026-09-04T07:29:34Z
testedCommit: e3f10673eb07af08ce47bbf5b26c04b7eb082fca
sourceBranch: fix/mobile-debug-cleartext-overlay
evidenceLevel: locally-executed
buildTestedCommit: e3f10673eb07af08ce47bbf5b26c04b7eb082fca
buildVerificationStatus: passed
---

# Flutter Android Debug Cleartext Overlay Correction Verification

## Objective

Verify that the debug-only HTTP manifest overlay merges successfully after
release hardening explicitly denies cleartext traffic in the main manifest.

## Execution Identity

- Tested source commit:
  `e3f10673eb07af08ce47bbf5b26c04b7eb082fca`
- Source branch: `fix/mobile-debug-cleartext-overlay`
- Execution date: 2026-09-04 Asia/Manila
- The working tree retained unrelated pre-existing local edits in
  `mobile/analysis_options.yaml` and `mobile/test/mobile_foundation_test.dart`;
  those files were not staged or committed.

## Commands

```powershell
cd mobile
flutter build apk --debug --dart-define=UNIPM_API_BASE_URL=http://10.0.2.2:5000/
flutter devices --device-timeout 60
adb -s emulator-5554 install -r --no-streaming build/app/outputs/flutter-apk/app-debug.apk
```

The first debug build against the parent release-hardening state failed at
manifest merge because the main `false` and debug `true` values lacked an
explicit replacement directive. After the focused XML correction, the same
debug build command passed and produced
`build/app/outputs/flutter-apk/app-debug.apk`.

The emulator was detected as Android API 37. Two ADB installation attempts
then hung without producing an install result; the client processes were
stopped after inspection. No app launch, UI, or emulator runtime behavior is
claimed from that attempt.

## Results

- Corrected `flutter build apk --debug --dart-define=UNIPM_API_BASE_URL=http://10.0.2.2:5000/`:
  passed.
- Debug manifest merge: passed after the `tools:replace` correction.
- Android emulator discovery: passed; `emulator-5554`, API 37.
- Emulator APK installation: not verified; ADB client hung on both attempts.
- The build emitted the existing `mobile_scanner` Kotlin Gradle Plugin
  migration warning; it did not fail the corrected build.

| Scope | Passed | Failed | Skipped | Total |
|---|---:|---:|---:|---:|
| Corrected debug APK packaging | 1 | 0 | 0 | 1 |
| Debug manifest merge | 1 | 0 | 0 | 1 |
| Emulator discovery | 1 | 0 | 0 | 1 |
| Emulator APK installation | 0 | 0 | 1 | 1 |

## Verification Scope

No physical Android device, live backend, SQL Server, release signing, AAB,
or emulator application startup was verified. The HTTP URL was a documented
local development route; no API request was made.
