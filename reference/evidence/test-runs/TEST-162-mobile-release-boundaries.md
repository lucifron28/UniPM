---
id: TEST-162
type: test-run
title: Mobile identity and release boundary review
status: executed
recordedAtUtc: 2026-10-25
testedCommit: 8cea97af614bdcb9c2bfd9016261016694a5d5c4
sourceBranch: feature/capstone-remediation
evidenceLevel: locally-executed
---

# Mobile identity and release boundaries

Commit `f42a537` replaces the Android placeholder with
`io.github.lucifron28.unipm` in Gradle namespace, applicationId and the moved
MainActivity package/path. Release signing and cleartext settings remain
unchanged. The README records separate installation, no automatic encrypted
queue migration, and synchronization/verification before removing the old app.

Commits `8a9d2e2` and `8cea97a` remove the stale mobile location-verification
claim and identify QR lookup as asset identification. The new text-only test
assertions were removed in `c0036ce`; existing workflow tests remain intact.
TEST-158 separately records the earlier storage-failure/account isolation fix.

At the exact tested commit above, from `mobile/`, Flutter 3.44.5 / Dart 3.12.2:

| Command | Result |
|---|---|
| `flutter test --no-pub --concurrency=2 test/qr_scanner_test.dart` | Exit zero, eight passed |
| `flutter test --no-pub --concurrency=2 test/app_config_test.dart` | Exit zero, four passed |
| `flutter analyze --no-pub` | Exit zero, no issues, analyzer duration 83.8 seconds |

The URL tests accept debug HTTP and release HTTPS, and reject release HTTP
and unsupported schemes. The QR tests cover capture locking, retry, cancel,
permission denial, Back and manual entry. No physical camera test is claimed.
Raw output is ignored under `artifacts/remediation/clients/phase8-*`.
The source/command/result identity is retained; exact UTC command intervals
were not retained in these individual logs. They ran on October 25, 2026.

## Source inspection, separate from executed tests

- The main Android manifest disables backups and cleartext traffic. Debug
  permits local HTTP. Release signing reads external `android/key.properties`
  and does not fall back to the debug key. That file is absent in this worktree.
  An unsigned release artifact would not be distributable.
- No Android device is attached. Signed distribution, installation/upgrade,
  OS restart, real disk exhaustion and physical acceptance remain unverified.
- `server/Features/Assets/AssetQrCodeValue.cs` constructs lookup identifiers;
  scanning or typing one does not prove physical presence.
- The current mobile app captures no coordinates. The retained
  `server/Features/Inspections/InspectionLocationVerificationEndpoints.cs`
  records client-supplied telemetry, an advisory distance classification and
  a server capture timestamp. Optional form attachment checks actor, asset,
  schedule and reuse, not independently established presence or an Inside-only
  rule. No backend behavior was changed by this review.

The main chat reviewed the actual identity, UI and README diffs. No iOS
identifier change, new location feature, deployment or signing credential was
introduced. Final APK compilation and the complete available regression are
recorded separately.
