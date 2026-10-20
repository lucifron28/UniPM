---
id: TEST-097
type: test-run
title: Staging workflow follow-up focused verification
status: executed
recordedAtUtc: 2026-10-18T09:45:05Z
testedCommit: d474cb4725ffce031a7c903cf2201d7afe2427ab
preEvidenceWorkingTreeManifestSha256: 9c02be22045b95ec496ac9154641783ee28ba1dc44e589ecf1035c90fbebc121
latestScheduleTestSourceManifestSha256: d65757bcff81ba873214f4dc5dacd7f8b0826f3a4f2d4975402cb2adef80c3f4
sourceBranch: feature/staging-workflow-followups
evidenceLevel: locally-executed
---

# Staging workflow follow-up focused verification

## Objective

Record focused verification for the local staging workflow follow-up changes:
CPMP schedule recovery configuration and presentation, the exact annual
non-operational NLA wording regression, and the schedule registry's removal of
the routine per-asset Add schedule action.

## Execution identity

The repository was on branch `feature/staging-workflow-followups` at HEAD
`d474cb4725ffce031a7c903cf2201d7afe2427ab`. The working tree was modified and
uncommitted. The manifest SHA-256 above fingerprints the 64 changed, added, or
deleted non-ignored paths immediately before this record and its index entry
were added. Each command below ran against the evolving working tree after its
related change; this record does not claim every command ran against one
identical snapshot or against the pristine HEAD commit. No commit or push was
made.

## Environment

- Windows PowerShell workspace.
- Backend verification used the repository's .NET Release test configuration
  with `--no-restore --maxcpucount:1`.
- Web checks used the repository's npm scripts.
- No live Azure SQL, Azure Blob, or deployed application connection was used.

## Commands and results

| Command | Result |
| --- | --- |
| `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore --configuration Release --filter "FullyQualifiedName~PmAnalyticsV2QuestionGrammarTests.Parses_all_departments_wording_as_a_grouped_annual_non_operational_report" --maxcpucount:1` | Passed, 1/1 focused regression. The supplied annual Water Drinking Station question maps to NonOperational grouped by Department. |
| `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore --configuration Release --filter "FullyQualifiedName~PreventiveMaintenanceScheduleGenerationTests.Worker_retries_failed_startup_check_and_recovers_each_institutional_year_only" --maxcpucount:1` | Passed, 1/1 focused worker recovery test. Existing package advisory, nullable, and SQL analyzer warnings were reported; the test passed. |
| `npm run test:run -- src/features/schedules/schedule-workflow.test.tsx -t "orders the schedule page sections and exposes management navigation"` | Passed, 1 test; 20 unrelated tests skipped. Confirms the ordinary registry view does not expose the per-asset Add schedule link and retains management navigation. |
| `npm run test:run -- src/features/schedules/schedule-workflow.test.tsx -t "lets GSD assign the Supervisor stage for the full department, category, and cycle batch"` | Passed on the latest source worktree, 1 test; 20 unrelated tests skipped. Covers the batch selection/review/confirmation flow after the queue URL-filter change. The source manifest above excludes `reference/evidence/` records. |
| `npm run typecheck` | Passed after the latest URL-backed assignment filter, keyboard result scrolling, and schedule registry action changes. |
| `npm run build` | Passed after the latest Dashboard, Quick Search, assignment filter, keyboard scrolling, and schedule registry action changes. |
| `git diff --check` | Passed. Git emitted a line-ending conversion notice for generated `web/src/routeTree.gen.ts`; no whitespace error was reported. |

## Browser and visual verification

Five exploratory desktop captures of the current local worktree used mocked
API responses and fictional session data. They were not connected to staging.
The compact 1280x720 question view had no horizontal overflow. The Quick Search
capture predates the final assignment URL-filter and keyboard scrolling edits.
No screenshot covers the final Schedule registry after the Add schedule action
was removed.

A focused Playwright attempt returned no test output after 60 seconds while
starting the local Vite server under Node 24.15.0, outside the web package's
declared Node 22 range. A localhost probe was blocked by the workspace sandbox's
socket-access restriction. The attempt was stopped and the child process was
confirmed stopped. It produced no assertion result and does not verify browser
behavior.

## Skipped verification and limitations

- Source inspection confirmed that `ScheduleAssignmentQueue` currently fetches
  the unpaginated schedule list for its selected year and groups those rows in
  the browser. A 1,000-asset catalog with four annual cycles could yield about
  4,000 rows. No representative performance measurement was made, so this is a
  scale risk to measure, not a confirmed user-visible performance defect.
- Full backend suite, full web suite, and successful Playwright workflow run:
  not executed or not completed.
- Browser acceptance of Quick Search keyboard behavior, assignment destinations,
  and the final Schedule registry: not verified.
- Fresh visual capture of the final Schedule registry: not verified.
- Live schedule generation behavior and effective-date impact: not verified.
- Azure configuration, SQL data, Blob persistence, deployed acceptance, and
  physical-device acceptance: not verified; Azure was not accessed or changed.
- GSD approval of historical effective date and schedule-adjustment authority
  remain pending.

The focused results do not establish full-suite or deployment readiness.
