---
id: TEST-061
type: test-run
title: CPMP batch-lock deferral and pre-evaluation UX verification
status: executed
recordedAtUtc: 2026-10-09T19:34:33Z
testedCommit: bd41690ef58796c4ad678397156b19a266352ee4
sourceBranch: feat/pre-evaluation-ux-motion
evidenceLevel: locally-executed
---

# CPMP batch-lock deferral and pre-evaluation UX verification

## Objective

Record verification for schedule batch enrollment deferral, current-year generation safeguards, and focused web interaction and accessibility refinements. The change preserves existing PM workflow behavior and does not alter CPMP category/month rules.

## Execution Identity

The backend, generated-client, lint, typecheck, unit, build, and full Playwright checks were run on exact source HEAD bd41690ef58796c4ad678397156b19a266352ee4. The only later source change was ec7a7d693ec121c1282493fad3d0ca039a04effa, which adds endOfLine: auto to the web Prettier configuration. The standard format check and git diff --check passed on ec7a7d6; no application or test code changed after the full suites.

Branch: feat/pre-evaluation-ux-motion. The feature branch is stacked on feat/pre-evaluation-remediation at 48676738498eb6fe6a2341a697b2611f80a87b20.

## Environment

- Local Windows development environment.
- UNIPM_SQLSERVER_TEST_CONNECTION was not configured, so SQL Server-specific integration facts were skipped.
- No AI provider was called.
- No Flutter source changed.
- Full Playwright used the repository's local test configuration.
- No manual screenshots were retained for this exact revision.

## Commands

| Command | Tested source | Result |
| --- | --- | --- |
| dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore | bd41690ef58796c4ad678397156b19a266352ee4 | 338 passed, 0 failed, 37 skipped. |
| dotnet build server/server.csproj --configuration Release --no-restore | bd41690ef58796c4ad678397156b19a266352ee4 | Succeeded, 0 errors; one existing CS8602 nullable warning in PreventiveMaintenanceFormEndpoints.cs:540. |
| dotnet ef migrations has-pending-model-changes --project server/server.csproj --no-build | bd41690ef58796c4ad678397156b19a266352ee4 | No pending model changes. |
| npm run api:check | bd41690ef58796c4ad678397156b19a266352ee4 | Passed OpenAPI contract and generated-client drift checks. |
| npm run lint | bd41690ef58796c4ad678397156b19a266352ee4 | Passed. |
| npm run typecheck | bd41690ef58796c4ad678397156b19a266352ee4 | Passed. |
| npm run test:run | bd41690ef58796c4ad678397156b19a266352ee4 | 27 files, 207 tests passed. |
| npm run build | bd41690ef58796c4ad678397156b19a266352ee4 | Passed; Vite transformed 2,207 modules. |
| npm run e2e -- --reporter=line | bd41690ef58796c4ad678397156b19a266352ee4 | 44 passed, 1 skipped. |
| npm run format:check | ec7a7d693ec121c1282493fad3d0ca039a04effa | Passed; all files matched Prettier. |
| git diff --check | ec7a7d693ec121c1282493fad3d0ca039a04effa | Passed. |

## Results

The backend suite and web checks passed on the implementation source commit. The release build has one pre-existing nullable warning and no errors. The EF model check found no pending changes.

The full browser suite passed with one existing skip. Automated Playwright coverage ran; a separate manual visual review and screenshot capture were not performed for this exact revision.

## Test Counts

- Backend: 338 passed, 0 failed, 37 skipped.
- Web unit: 207 passed across 27 files.
- Playwright: 44 passed, 1 skipped.
- Release build: passed with one nullable warning.
- Generated API contract check: passed.
- Formatter and diff check: passed on the final formatter-config commit.

## SQL Server Verification

The focused SQL Server tests Manual_schedule_creation_waits_for_assignment_and_rejects_locked_batch and Manual_schedule_creation_commits_the_schedule_with_its_batch_lock compiled and were discovered, but did not execute because UNIPM_SQLSERVER_TEST_CONNECTION was unset. The full suite's SQL Server-specific facts were skipped for the same environment limitation.

Consequently, SQL Server 2019 application-lock ordering, transaction persistence, migration application, and concurrent batch-enrollment behavior are NOT VERIFIED against a live database in this run.

## AI-Provider Verification

No AI provider calls were made. The system behavior covered here is deterministic PMIS scheduling and web presentation.

## Generated Artifacts

No raw command logs or screenshots are included in this record. Test totals and outcomes above reflect the executed local commands.

## Failures And Corrections

The implementation review found that manual schedule creation opened an outer transaction but returned without committing it. That could produce a successful response while the row was later rolled back. The implementation now commits after the schedule save, and a focused SQL Server regression test covers row persistence. The test compiled but could not run without the SQL Server test connection.

A Windows checkout line-ending difference caused the standard formatter check to report many unchanged files. The web Prettier configuration now uses endOfLine: auto; the standard npm run format:check passed afterward without rewriting unrelated files.

## Skipped Verification

- 37 backend facts were skipped: 36 SQL Server-dependent facts and one optional embedding-provider smoke fact.
- The two focused manual-schedule SQL Server cases did not execute because no SQL Server test connection was configured.
- Flutter tests were not run because no Flutter source changed.
- No physical device, native SQL Server, or institutional GSD acceptance was exercised.
- Exact-head GitHub CI is checked separately after pushing the final branch head.

## Limitations

Automated tests establish the behavior of the local source and mocks. They do not establish SQL Server 2019 concurrency behavior, migration execution on a disposable SQL Server instance, deployment behavior, physical-device experience, or GSD acceptance. The branch remains unmerged.