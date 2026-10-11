---
id: TEST-075
type: test-run
title: Inspection photo lifecycle and regression verification
status: executed
recordedAtUtc: 2026-10-11T02:36:00Z
testedCommit: d9299fca0803cf82412c41776a7166ed1764a59d
sourceBranch: feat/inspection-photo-evidence
evidenceLevel: locally-executed
---

# Inspection photo lifecycle and regression verification

## Objective

Verify the upload/submission race fix, SQL-backed photo lifecycle, camera lifecycle, keyboard-accessible preview, and regressions for draft PR #89.

## Execution identity

- Web checks and EF model check: `d9299fca0803cf82412c41776a7166ed1764a59d`.
- Backend Release/SQL and Flutter checks: `5bbfbce3488a8cc783e252add2bd3829778a5d87`.
- Focused SQL race/lifecycle run: `acd0a5e90622acb58ab1f29547f8b4890eca0d3a`.
- PR #89 is draft and stacked on PR #88 at `eb45d5d9edf51f55a1c9264f390849cd6d2d9fe7`.
- Backend and Flutter files did not change after the `5bbfbce` checks; later commits changed web focus handling.

## Environment

- Windows 10, .NET SDK 10.0.300, SQL Server 2019, Flutter/Dart, Node.js 22, Vite, Vitest, and Playwright Chromium.
- SQL checks confirmed SQL Server major version 15 and compatibility level 150.
- Backend connection settings were process-scoped and were not copied into output or evidence.
- Raw backend logs and TRX reports are in ignored `artifacts/evidence/20261011-022220Z-5bbfbce3488a/`.

## Commands

- `.\scripts\evidence\Invoke-BackendVerification.ps1 -Configuration Release -RunSqlServerTests`
- `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~SqlServerPhotoSubmissionRaceTests' --verbosity minimal`
- `dart format --output=none --set-exit-if-changed mobile/lib/features/preventive_maintenance/inspection_photo_evidence_card.dart`; `flutter analyze`; `flutter test`; `flutter build apk --debug`.
- In `web/`: `npm run format:check`, `npm run lint`, `npm run typecheck`, `npm run api:contract:check`, `npm run api:contract:test`, `npm run api:check`, `npm run test:run`, `npm run build`, and `npm run e2e -- --workers=1 --reporter=line`.
- `dotnet ef migrations has-pending-model-changes --project .\server\server.csproj --startup-project .\server\server.csproj --configuration Release`.
- `git diff --cached --check` and `git diff-tree --check` for staged and committed changes.

## Results

- Backend Release build and full suite passed: 402 passed, 0 failed, 1 optional provider test skipped.
- SQL Server-only suite: 43 passed, 0 failed, 0 skipped. Focused photo race/lifecycle suite: 4 passed, 0 failed, 0 skipped.
- Migration Up -> Down -> Up passed on SQL Server 2019 at compatibility 150. Inspection and location-attempt data remained intact; the schema was recreated and the migration reapplied.
- SQL photo lifecycle, one-row uniqueness, GSD upload denial, post-submission mutation rejection, and file cleanup after forced database failure passed.
- Both race orderings passed. If submission wins, the delayed upload receives Conflict and leaves no evidence row or file. If upload wins, submission waits for the row lock and completes with the saved photo retained.
- Flutter formatting and analysis passed; all 159 tests passed; debug APK build passed. The build printed the existing `mobile_scanner` Kotlin Gradle Plugin compatibility warning.
- Web at `d9299fc`: Prettier, ESLint, TypeScript, OpenAPI contract sanity, generated-client drift check, Vite production build, and all 207 Vitest tests passed. All 7 negative OpenAPI checks passed.
- Playwright: 47 passed, 1 skipped out of 48, including desktop and mobile preview keyboard checks.
- EF model check passed: no pending changes since the last migration.
- The backend Release build emitted one existing nullable warning at `PreventiveMaintenanceFormEndpoints.cs:559`, outside this change.

## Failures and corrections

The pre-fix native SQL race test reproduced a real defect: a storage-delayed upload could persist after the form became Submitted. Shared parent-row locking and the Draft recheck fixed the race. Both SQL race directions then passed.

The first web focus E2E run found that Tab could move focus to the document body. The bounded keyboard cycle fixed it; desktop and mobile E2E checks passed.

## Not verified

One optional backend provider test and one existing Playwright case were skipped. Exact-head GitHub CI is checked after the final push. Physical Android camera behavior, iOS, staging acceptance, deployment, production file permissions, restart persistence, backup, retention, and full WCAG/screen-reader review were not performed. No merge or deployment was performed.
