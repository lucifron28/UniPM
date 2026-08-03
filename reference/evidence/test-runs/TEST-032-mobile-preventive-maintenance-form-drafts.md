---
id: TEST-032
type: test-run
title: Flutter preventive-maintenance Draft form workflow verification
status: executed
recordedAtUtc: 2026-07-31T16:43:53Z
testedCommit: ab41d305b49377048e7216d14b3a85ca77ae9317
sourceBranch: feat/mobile-pm-form-drafts
evidenceLevel: locally-executed
buildTestedCommit: ab41d305b49377048e7216d14b3a85ca77ae9317
buildVerificationStatus: passed
---

# Flutter Preventive-Maintenance Draft Form Verification

## Objective

Verify the mobile Draft form workflow with deterministic repository fakes and
fictional users, without calling a live backend.

## Execution Identity

- Tested source commit:
  `ab41d305b49377048e7216d14b3a85ca77ae9317`
- Source branch: `feat/mobile-pm-form-drafts`
- Starting main commit:
  `4489f2b58103e1ec151d9a3f6551b0104d65cb2b`
- Execution date: 2026-07-31 Asia/Manila (`2026-07-31T16:43:53Z`)

## Commands

```powershell
cd mobile
flutter analyze
flutter test test/mobile_foundation_test.dart test/preventive_maintenance_form_draft_test.dart
flutter build apk --debug --dart-define=UNIPM_API_BASE_URL=http://10.0.2.2:5000/
```

## Results

- `flutter analyze`: passed with no issues.
- `flutter test test/mobile_foundation_test.dart test/preventive_maintenance_form_draft_test.dart`:
  passed all 20 focused tests: 8 existing authentication tests and 12 Draft
  workflow tests.
- `flutter build apk --debug --dart-define=UNIPM_API_BASE_URL=http://10.0.2.2:5000/`:
  passed and produced `build/app/outputs/flutter-apk/app-debug.apk`.

| Scope | Passed | Failed | Skipped | Total |
|---|---:|---:|---:|---:|
| Flutter analyzer | 1 | 0 | 0 | 1 |
| Focused mobile tests | 20 | 0 | 0 | 20 |
| Debug Android APK build | 1 | 0 | 0 | 1 |

## Behavior Covered

- Draft registry metadata, row counts, empty state, and Inspector/GSD visibility
  boundaries.
- Header creation through reference-backed fields and immediate API persistence.
- Reference-data failure and retry behavior without invented options.
- Resuming a Draft with multiple rows.
- Authenticated Inspector identity on row creation.
- Duplicate schedule prevention before a second API write.
- Date, condition, remarks, and recommended-action row updates.
- Delete confirmation and immediate row removal persistence.
- Absence of submission, acknowledgement, and signature actions.

## Verification Scope

Tests use deterministic repository fakes and fictional users. No live backend,
emulator connectivity, emulator integration test, mobile offline storage or
synchronization, release build, SQL Server test, full repository suite, or
production deployment verification was run.

## Generated Artifacts

No credentials, secrets, tokens, cookies, live responses, real institutional
records, or generated vectors were recorded. The debug APK is a local build
artifact and is not committed as evidence.

