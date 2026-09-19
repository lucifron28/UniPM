---
id: TEST-037
type: test-run
title: Flutter Android release-boundary hardening verification
status: executed
recordedAtUtc: 2026-09-04T06:49:40Z
testedCommit: 4a78936731de5b341d1608f039b1b6ce5db60638
sourceBranch: chore/mobile-release-hardening
evidenceLevel: locally-executed
buildTestedCommit: 4a78936731de5b341d1608f039b1b6ce5db60638
buildVerificationStatus: passed
---

# Flutter Android Release-Boundary Hardening Verification

## Objective

Verify the final mobile regression and the Android release packaging and
security boundaries without contacting a live backend.

## Execution Identity

- Tested source commit:
  `4a78936731de5b341d1608f039b1b6ce5db60638`
- Source branch: `chore/mobile-release-hardening`
- Execution date: 2026-09-04 Asia/Manila
- The working tree retained unrelated pre-existing local edits in
  `mobile/analysis_options.yaml` and `mobile/test/mobile_foundation_test.dart`;
  those files were not staged or committed.

## Commands

```powershell
cd mobile
dart format --output=none --set-exit-if-changed --suppress-analytics lib test
flutter analyze --no-pub
flutter test --no-pub
flutter build apk --release --dart-define=UNIPM_API_BASE_URL=https://api.example.test/
```

The generated release manifest was inspected at the ignored Gradle build
output and contained `android:usesCleartextTraffic="false"`. The debug merged
manifest retained `android:usesCleartextTraffic="true"` only through the
debug overlay.

The generated APK was checked with the local Android `apksigner` tool. It did
not verify because it has no signing metadata, which is the expected result
when no untracked project-owned keystore is present. This confirms the release
configuration does not silently use the debug keystore; it does not establish
distributable signing.

## Results

- `dart format --output=none --set-exit-if-changed --suppress-analytics lib
  test`: passed; 39 files inspected, 0 changed.
- `flutter analyze --no-pub`: passed with no issues.
- `flutter test --no-pub`: passed all 86 mobile tests.
- `flutter build apk --release --dart-define=UNIPM_API_BASE_URL=https://api.example.test/`:
  passed and produced `build/app/outputs/flutter-apk/app-release.apk`.
- Release manifest inspection: passed; cleartext traffic is explicitly false.
- APK certificate verification: intentionally not verified because the
  artifact is unsigned without project-owned release signing material.
- The build emitted the existing `mobile_scanner` Kotlin Gradle Plugin
  migration warning; it did not fail the build.

| Scope | Passed | Failed | Skipped | Total |
|---|---:|---:|---:|---:|
| Dart formatter check | 1 | 0 | 0 | 1 |
| Flutter analyzer | 1 | 0 | 0 | 1 |
| Complete mobile tests | 86 | 0 | 0 | 86 |
| Release Android APK packaging | 1 | 0 | 0 | 1 |
| Release cleartext manifest inspection | 1 | 0 | 0 | 1 |
| Distributable signing verification | 0 | 0 | 1 | 1 |

## Verification Scope

The API URL used for packaging was a synthetic HTTPS placeholder and no API
request was made. No physical Android device, emulator, live backend, SQL
Server, AAB, production keystore, signed distributable, crash test, restart
test, or network-transition test was executed. Production API identity,
application ID, signing ownership, and device acceptance remain project-owner
decisions.

No credentials, secrets, cookies, authorization headers, live responses, real
institutional records, prompts, vectors, keystore contents, or generated
provider artifacts were recorded.
