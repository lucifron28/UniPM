---
id: TEST-119
type: test-run
title: Offline-first mobile Draft synchronization verification
status: executed
recordedAtUtc: 2026-10-19T16:46:32Z
testedCommit: 75c005da4f09d0b2b1f2b3a031c30bf03225d329
sourceBranch: feature/offline-inspection-workflow
evidenceLevel: locally-executed
---

# Offline-first mobile Draft synchronization verification

## Objective

Verify encrypted per-user local Draft persistence and queued synchronization,
the idempotent/concurrency-aware server contracts, generated API-client
consistency, and Flutter compilation. This is a feature-branch prototype; it
does not establish GSD acceptance or production readiness.

## Execution Identity

- Tested source commit: `75c005da4f09d0b2b1f2b3a031c30bf03225d329`
- Branch: `feature/offline-inspection-workflow`
- Working tree source was committed unchanged after the verification commands;
  the later documentation/evidence commit is not part of this tested source
  identity.
- Platform: Windows PowerShell.

## Environment

- .NET SDK: `10.0.300`.
- Node.js: `v24.15.0`; the Web package declares Node `>=22 <23`. API generation
  passed in this environment, but the supported Node version should be used in
  CI or a clean local reproduction.
- A dedicated SQL Server 2019 test connection was not configured.
- No Azure or production connection was used.

## Commands

```powershell
# From the repository root
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --filter FullyQualifiedName~PreventiveMaintenanceFormDraftEndpointsTests
git diff --check

# From web/
npm run api:generate

# From mobile/; these arguments were run with the locally installed Flutter
# SDK. Its absolute user-profile path is omitted from committed evidence.
flutter analyze
flutter test --no-pub
flutter build apk --debug --no-pub
```

## Results

- Focused backend draft-endpoint tests passed: **36 passed, 0 failed, 0 skipped**.
- The focused backend run reported existing NuGet vulnerability advisories
  (NU1902/NU1903) and a nullable warning in existing test code (CS8602); these
  were outside this change and were not suppressed.
- OpenAPI sanity checking and Orval client generation completed successfully.
- Flutter static analysis passed with no issues.
- Full Flutter test suite passed: **166 passed, 0 failed**.
- Android debug APK build succeeded. This verifies compilation only; the APK
  was not distributed or accepted on a physical device.
- The Android build emitted the existing `mobile_scanner` Kotlin Gradle plugin
  migration warning; no Gradle or plugin migration was part of this change.
- `git diff --check` passed. Git reported a line-ending conversion notice for
  the OpenAPI snapshot; no whitespace errors were reported.

## Test Counts

| Check | Result |
|---|---:|
| Focused backend draft API tests | 36 passed |
| Flutter analyzer | 0 issues |
| Full Flutter tests | 166 passed |
| OpenAPI and generated-client check | Passed |
| Android debug APK build | Passed |
| Diff whitespace check | Passed |

The Flutter repository tests cover restart persistence and retry without
duplicate forms, account-scoped cache isolation, server-update conflict
preservation with submission blocked, and queued add-then-delete behavior.
These are automated local checks, not physical-device acceptance.

## SQL Server Verification

SQL Server 2019 integration tests were **not run** because
`UNIPM_SQLSERVER_TEST_CONNECTION` was not configured. No SQL Server connection,
migration, or database write was attempted as part of this verification.

## AI-Provider Verification

No AI provider was called. Core PM behavior remains provider-independent.

## Generated Artifacts

- Android debug APK: `mobile/build/app/outputs/flutter-apk/app-debug.apk`;
  local build output, not committed or released.
- Command logs were not retained as raw files. This reviewed record contains
  sanitized results only.

## Failures And Corrections

- The first focused backend test invocation failed at compilation because a
  retry-local variable reused a name in the same method scope. The variable was
  renamed, then the focused test command was rerun once and passed 36/36.
- A clean Web dependency restore did not complete in this environment. API
  generation was then run with the existing local Orval 8.22.0 and required
  formatter/plugin packages; it completed successfully. Node 24 is outside the
  package's declared Node 22 range, as noted above.

## Skipped Verification

- SQL Server 2019 tagged integration tests: skipped; no dedicated test
  connection was configured.
- Physical Android device, iOS, release APK, network interruption/recovery on a
  device, and deployed-backend verification: not performed.
- Full backend suite, full Web suite, and Playwright: not run because the
  requested scope was focused backend and full Flutter verification.
- GSD/adviser review and acceptance: pending.

## Limitations

This branch prototype encrypts local per-user cached data and Draft queues, but
authentication remains memory-only. A user must sign in again after an app
restart before queued work can synchronize. Location verification, photo
evidence, submission, and acknowledgement remain online-only. Conflicts
preserve local and server versions and block submission pending user review.
Automated tests and a debug build do not verify Android keystore behavior,
connectivity callbacks, crash recovery, or UX on physical devices. The feature
is not accepted into the GSD validation baseline and must not be described as
an evaluated capability.
