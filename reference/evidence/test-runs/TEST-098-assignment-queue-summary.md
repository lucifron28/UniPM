---
id: TEST-098
type: test-run
title: Schedule assignment batch summary verification
status: executed
recordedAtUtc: 2026-10-18T09:57:03Z
testedCommit: d474cb4725ffce031a7c903cf2201d7afe2427ab
testedWorkingTreeSourceManifestSha256: 6c84cfd89b4b745be34ea66ffa747feb203bf2b19ea173e16a43485e58e05490
sourceBranch: feature/staging-workflow-followups
evidenceLevel: locally-executed
---

# Schedule assignment batch summary verification

## Execution identity

The tests ran on branch `feature/staging-workflow-followups`, based on HEAD
`d474cb4725ffce031a7c903cf2201d7afe2427ab`, with uncommitted changes. The
source-manifest hash fingerprints the 13 source, contract, generated-client,
and focused test files below.
The tested source was that worktree overlay, not the pristine commit. No
commit, push, deployment, Azure SQL connection, or database write occurred.

Fingerprint input paths:

- `server/Features/Schedules/SchedulesEndpoints.cs`
- `tests/UniPM.Api.Tests/Schedules/ScheduleBatchAssignmentEndpointsTests.cs`
- `web/openapi/unipm-v1.json`
- `web/src/api/generated/endpoints.ts`
- `web/src/api/generated/models/index.ts`
- `web/src/api/generated/models/listScheduleAssignmentBatchesParams.ts`
- `web/src/api/generated/models/scheduleAssignmentQueueBatchResponse.ts`
- `web/src/api/generated/models/scheduleAssignmentQueueResponse.ts`
- `web/src/components/layout/app-shell-access.test.tsx`
- `web/src/features/schedules/schedule-assignment-queue.tsx`
- `web/src/features/schedules/schedule-batch-assignment.tsx`
- `web/src/features/schedules/schedule-contract.ts`
- `web/src/features/schedules/schedule-queries.ts`

## Change under verification

The GSD/Supervisor assignment queue now reads grouped batch summaries from
`GET /api/v1/schedules/assignment-batches` instead of downloading the full
schedule list and grouping it in the browser. The endpoint limits its role
scope to GSD and Supervisor. It returns complete department/category/cycle
batches; Supervisor results include only batches with at least one schedule
assigned to the authenticated Supervisor. This preserves mixed-owner visibility
and uses the same whole-batch ownership check as the assignment mutation.
The summary carries counts, assignment states, a representative schedule, and
an assignment lock that includes linked inspection evidence and completed or
nonassignable schedules.

The endpoint projects summary inputs for matching schedules and groups them in
application memory. It avoids sending every full schedule DTO to the browser,
but is not a SQL aggregate and does not establish large-catalog performance.
No schema or migration changed.

## Commands and results

| Command | Result |
| --- | --- |
| `npm run api:generate` | Passed OpenAPI contract sanity; Orval v8.22.0 generated the endpoint and response models from the updated snapshot. |
| `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore --configuration Release --filter "FullyQualifiedName~ScheduleBatchAssignmentEndpointsTests.Assignment_queue_returns_complete_grouped_batches_only_for_the_authenticated_supervisor_scope" --maxcpucount:1` | First run failed because the fixture's year-only query included automatically generated CPMP schedule rows (expected 5, actual 10). The fixture was narrowed to `pmCycle=2026-08`; the single permitted rerun passed 1/1. It verifies case-insensitive batch grouping, GSD visibility, complete mixed-owner batch visibility for the assigned Supervisor, counts, assignment state, and inspection lock. |
| `npm run typecheck` | First run reported an unused type and stale type reference in the queue. After the narrow correction, the single rerun passed. |
| `npx prettier --write src/features/schedules/schedule-assignment-queue.tsx src/features/schedules/schedule-batch-assignment.tsx src/features/schedules/schedule-contract.ts src/features/schedules/schedule-queries.ts src/components/layout/app-shell-access.test.tsx` | Completed successfully. |
| `git diff --check` | Passed; Git emitted line-ending conversion notices for the OpenAPI snapshot and a pre-existing generated route tree, with no whitespace errors. |

The focused backend run emitted existing package vulnerability warnings for
`System.Security.Cryptography.Pkcs` 9.0.4. The first typecheck failure and
fixture failure are retained here rather than represented as passing attempts.

## Not verified

- Web unit tests, full backend tests, and Playwright were not run for this
  continuation. The access test was updated to expect the new summary route,
  but that browser-facing test was not executed.
- No performance benchmark with 1,000+ assets was run. The API still
  materializes one projected row per matching schedule before grouping.
- Full CI, staging acceptance, and GSD workflow acceptance were not run.
- Azure and the deployed service were not accessed or modified.
