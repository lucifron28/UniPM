---
id: TEST-033
type: test-run
title: Flutter preventive-maintenance Draft workflow hardening verification
status: executed
recordedAtUtc: 2026-08-12T10:10:52Z
testedCommit: 85ee12aef8d7f9b9ce384d1170cf827685c1af4d
sourceBranch: fix/mobile-pm-draft-hardening
evidenceLevel: locally-executed
buildTestedCommit: 85ee12aef8d7f9b9ce384d1170cf827685c1af4d
buildVerificationStatus: passed
---

# Flutter Preventive-Maintenance Draft Workflow Hardening Verification

## Objective

Verify the mobile Draft hardening behavior with deterministic fakes and
fictional users, without calling a live backend.

## Execution Identity

- Tested source commit:
  `85ee12aef8d7f9b9ce384d1170cf827685c1af4d`
- Source branch: `fix/mobile-pm-draft-hardening`
- Starting main commit:
  `362c0f4ecdd92dab0be08693b77ccb887b2edf85`
- Execution date: 2026-08-12 Asia/Manila (`2026-08-12T10:10:52Z`)

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
  passed all 25 focused tests.
- `flutter build apk --debug --dart-define=UNIPM_API_BASE_URL=http://10.0.2.2:5000/`:
  passed and produced `build/app/outputs/flutter-apk/app-debug.apk`.

| Scope | Passed | Failed | Skipped | Total |
|---|---:|---:|---:|---:|
| Flutter analyzer | 1 | 0 | 0 | 1 |
| Focused mobile tests | 25 | 0 | 0 | 25 |
| Debug Android APK build | 1 | 0 | 0 | 1 |

## Behavior Covered

- Terminal authentication failure clears the memory-only session and removes
  pushed feature routes before showing sign-in.
- Draft schedule loading, successful empty results, and retryable schedule
  failures remain distinct UI states.
- Inspection timestamps and loosely formatted dates are rejected before an API
  write; valid date-only values remain accepted.
- The Draft API boundary sends the authenticated Inspector ID, includes
  `scheduleId` only when adding a row, omits it when updating, handles a 409,
  and invokes terminal handling for a protected 401.
- Existing authentication, logout, role, Draft registry, multi-row, duplicate,
  edit, delete, and exclusion-of-submission/acknowledgement coverage remains
  passing.

## Verification Scope

Tests use deterministic HTTP/repository fakes and fictional users. No live
backend, emulator connectivity, emulator integration test, mobile offline
storage or synchronization, release build, SQL Server test, full repository
suite, or production deployment verification was run.

## Generated Artifacts

No credentials, secrets, tokens, cookies, live responses, real institutional
records, or generated vectors were recorded. The debug APK is a local build
artifact and is not committed as evidence.
