---
id: TEST-071
type: test-run
title: Optional inspection photo evidence and GPS-removal verification
status: executed
recordedAtUtc: 2026-10-10T17:49:10Z
testedCommit: b1be6341e94543174b540d269d063bbafecc6b72
sourceBranch: feat/inspection-photo-evidence
evidenceLevel: locally-executed
---

# Optional inspection photo evidence and GPS-removal verification

## Objective

Verify the optional private photo workflow, removal of active client GPS verification, and regressions across Flutter, backend, web, API generation, and browser workflows.

## Execution Identity

- Source commit: `b1be6341e94543174b540d269d063bbafecc6b72`.
- Branch: `feat/inspection-photo-evidence`.
- Base: `eb45d5d9edf51f55a1c9264f390849cd6d2d9fe7` (`feat/pre-evaluation-ux-motion`, PR #88).
- Repository: `lucifron28/UniPM`.

## Environment

- .NET 10, Flutter/Dart, Node.js 22, Vite, Vitest, Playwright Chromium.
- SQL Server was not available: `MSSQLSERVER` was not installed or unavailable, localhost TCP 1433 was unreachable, and `UNIPM_SQLSERVER_TEST_CONNECTION` was unset. No SQL connection value was read or recorded.
- No physical-device camera session, iOS build, or deployment was performed.

## Commands

- `flutter test --reporter compact`
- `flutter test test/preventive_maintenance_form_draft_test.dart --plain-name "a captured photo can be omitted when submitting the form" --reporter compact`
- `flutter analyze`
- `dart format` on the changed Dart implementation and test files
- `flutter build apk --debug`
- `dotnet test .\UniPM.slnx --configuration Release --no-restore --verbosity minimal`
- Focused photo/API tests: `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --filter "FullyQualifiedName~InspectionPhotoEvidence|FullyQualifiedName~InspectionPhotoJpeg|FullyQualifiedName~InspectionOpenApiContractTests" --no-restore`
- `dotnet ef migrations has-pending-model-changes --project .\server\server.csproj --startup-project .\server\server.csproj --configuration Release`, with a process-scoped dummy connection string for model construction only; the command did not connect to SQL Server.
- Web: `npm run format:check`, `npm run lint`, `npm run typecheck`, `npm run api:contract:check`, `npm run api:contract:test`, `npm run api:check`, `npm run test:run`, `npm run build`, and `npm run e2e`.
- `git diff --check`.

## Results

- Flutter full suite: 159 passed. The explicit submit-without-unsaved-photo regression passed; `flutter analyze` reported no issues. Changed Dart files were formatted. The debug APK built successfully. Flutter emitted the existing `mobile_scanner` Kotlin Gradle Plugin compatibility warning.
- Backend focused photo/API filter: 13 passed, 0 failed.
- Backend Release suite: 359 passed, 0 failed, 40 skipped (399 total). Release compilation succeeded. Skips included SQL Server integration and optional provider tests.
- EF model consistency: passed; no changes since the latest migration.
- Web format, lint, typecheck, OpenAPI contract sanity, and generated-client checks passed. All 7 negative OpenAPI contract tests passed. Full web unit suite: 207 passed.
- Web production build passed.
- Playwright: 45 passed, 1 skipped (46 total).
- `git diff --check` passed. Git emitted only line-ending normalization notices for several mobile files.

## Test Counts

| Area | Result |
|---|---:|
| Flutter full suite | 159 passed |
| Flutter explicit unsaved-photo submission | 1 passed |
| Backend focused photo/API/OpenAPI tests | 13 passed |
| Backend Release suite | 359 passed, 40 skipped, 0 failed |
| Web OpenAPI negative contract tests | 7 passed |
| Web unit suite | 207 passed |
| Playwright | 45 passed, 1 skipped |

## SQL Server Verification

NOT VERIFIED. The native SQL Server service/connection was unavailable. The 40 skipped backend tests include the SQL Server-tagged tests. The photo migration was not applied to SQL Server 2019, and compatibility level 150 was not checked in this run. EF model consistency is a design-time model check, not a database migration execution.

## AI-Provider Verification

No AI provider was used or required for this feature.

## Generated Artifacts

Orval regenerated the web API client from the updated OpenAPI contract. `npm run api:check` passed after the generated files were committed, with no resulting working-tree changes.

## Failures And Corrections

- The initial full Vitest run exposed two response fixtures missing `hasPhotoEvidence`; fixtures and the schema were corrected. The full suite then passed 207/207.
- The first focused browser navigation test exposed the strict PM form schema rejecting `hasPhotoEvidence`; the form schema and OpenAPI response contract were updated. The full Playwright suite passed 45/1 afterward.
- The first formatting check overlapped the negative-contract test's temporary JSON fixture and found the OpenAPI JSON needed formatting. The contract tests completed and cleaned their temporary files; the JSON was formatted and the format check then passed.
- The initial EF model command lacked the required design-time connection-string setting. Rerunning with a process-scoped dummy connection passed without connecting to a database.

## Skipped Verification

- Native SQL Server 2019 migration/integration tests: skipped because the service and test connection were unavailable.
- Physical Android camera permission/capture, iOS build, staging acceptance, deployment, and production file-storage persistence/backup: not run.
- Exact-head GitHub CI is pending for the evidence-updated PR head and will be captured in a separate record.

## Limitations

The Android debug APK build does not prove physical-camera behavior. The feature has not been deployed. Production storage permissions, backup and retention, and file lifecycle remain to be verified before any deployment. Unsaved captures are held in memory and are not queued for offline upload. Photo evidence is supporting documentation and does not prove presence or authenticity.
