---
id: TEST-167
type: test-run
title: Chart and assignment-period review corrections
status: executed
recordedAtUtc: 2026-10-26T14:06:47Z
testedCommit: 35ec1c1aa20f88a4c92d9eddac7aa5777300499d
sourceBranch: feature/dashboard-nla-charts
evidenceLevel: locally-executed
---

# Review corrections

The project owner authorized correction of all seven findings from the review
of `9d0742c`. This record supplements TEST-166 without changing its historical
results.

## Corrections

1. New schedules carry `AssignmentHistoryComplete=true`; the additive
   `20261026210000_AddAssignmentHistoryCompleteness` migration sets existing
   rows to false. The event recorder requires complete history before treating
   a null assignee as a newly known initial assignment. Existing first
   timestamps remain intact. No legacy dates are inferred or backfilled.
2. Administrative schedule edits read the persisted inspection completion
   timestamp and synchronize the schedule timestamp from that evidence.
   Marking Completed without a completed inspection timestamp returns a
   conflict. The edit time is not a replacement completion time.
3. The staging initializer includes both new migrations, keeps the exact
   migration-ID comparison and derives the expected count from the list.
   The application-table check remains 24 because neither migration adds a
   table. Two existing SQL checks now expect 26 migrations and the correct
   last migration; no new tests were added.
4. Web `Unavailable` wording no longer asserts that the initial assignment
   date is always the missing evidence.
5. Complete temporal charts enumerate missing quarters, months and years as
   unavailable gaps. Ranked or aggregate-filter subsets use bars rather than
   imply continuity or insert excluded groups.
6. Mobile QR assignment timing/status is explicitly labeled as of the last
   server response, including cached reads.
7. Dedicated web assignment-date formatting shows `HH:mm:ss Asia/Manila` and
   handles invalid dates. General schedule-date formatting is unchanged.

## Verification

| Check                                                                                  | Source identity                                                            | Result                                                                                                                                                                                                               |
| -------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Existing `Admin_schedule_filters_and_form_reversals_preserve_the_documented_lifecycle` | `35ec1c1aa20f88a4c92d9eddac7aa5777300499d`                                 | 1 passed; adjacent lifecycle regression, not direct PUT schedule coverage                                                                                                                                            |
| Existing `schedule-presentation.test.ts`                                               | `35ec1c1`                                                                  | 6 passed; existing civil-month-end checks                                                                                                                                                                            |
| Actual Vite-served client module, manual Chromium check                                | `35ec1c1`                                                                  | Missing Q2 creates an unavailable row and two separate line segments; Top subset uses bars without inventing Q2; precise seconds/timezone, generic unavailable text and invalid-date fallback passed; no page errors |
| TypeScript `tsc -b --pretty false`                                                     | `35ec1c1` plus one removed unused import, committed unchanged as `bf53911` | Passed after one focused correction                                                                                                                                                                                  |
| EF `migrations has-pending-model-changes --no-build`                                   | Server source at `35ec1c1`                                                 | No pending model changes                                                                                                                                                                                             |
| PowerShell parse and literal migration-ID comparison with source files                 | Initializer source committed as `35ec1c1`                                  | All 26 IDs match; no SQL or Azure call                                                                                                                                                                               |
| Changed-file whitespace check                                                          | Final code and documentation                                               | Passed before commit                                                                                                                                                                                                 |

The browser harness imports the actual chart and formatter modules with
synthetic response data. It does not claim a full authenticated live
browser/API/database workflow.

The initial PowerShell AST verification harness could not evaluate an array
statement with SafeGetValue. Reading its literal AST nodes instead passed the
same source check. An additional backend reflection smoke could not load
`Microsoft.AspNetCore.Http.Abstractions` in its host. That attempt did not
invoke the changed backend paths. Further resolver work was stopped. Direct
runtime verification of the new legacy-history gate and administrative
completion correction therefore remains unverified; the source changes,
existing adjacent regression and model check are the available evidence.

Two SQL-test expectations were refreshed in `6564f3b` after the executed
checks. Those optional SQL tests were not run and their expectation-only
changes do not alter the tested application binary. No native database
migration application, Azure initialization, deployment, physical-device
check, full suite, or AI-provider call ran.

Raw local artifacts remain ignored:

- `artifacts/remediation/backend/chart-assignment/admin-update-target-test.log`
- `artifacts/dashboard-charts/client/schedule-presentation-test.log`
- `artifacts/review/chart-assignment-fixes/model-check.log`
- `artifacts/review/chart-assignment-fixes/staging-chain.log`
- `artifacts/review/chart-assignment-fixes/client-smoke.mjs`
- `artifacts/review/chart-assignment-fixes/client-smoke.json`
- `artifacts/review/chart-assignment-fixes/quarter-gap.png`

The new migration still needs application when an application database is
updated. Original PM cycles/deadlines remain separate from assignment periods.
