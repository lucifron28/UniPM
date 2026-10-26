---
id: TEST-166
type: test-run
title: Dashboard and NLA charts with Inspector assignment period
status: executed
recordedAtUtc: 2026-10-26T13:18:15Z
testedCommit: ae0e5996c4e87fb314edbf09120fa93c308f07f1
sourceBranch: feature/dashboard-nla-charts
evidenceLevel: locally-executed
---

# Charts and assignment-period verification

The owner selected 30 calendar days from the first saved Inspector assignment,
retaining the original due date after reassignment and using the first
server-accepted completion. Original PM months, deadlines and compliance remain
separate. This is local engineering verification, not new institutional or
physical-device acceptance.

## Implemented behavior

- Assignment changes use one event recorder across batch assignment, technical
  administration and account removal. Initial timestamps are nullable and not
  backfilled. Reassignment/unassignment preserve the original clock. The audit
  table adds explicit initial/reassignment/unassignment event types. Schedule
  reads, dashboard and NLA share the assignment-period calculator.
- Dashboard charts use backend cohort counts and batch progress/compliance.
  Row-level condition, timeliness and search filters do not redefine the chart
  population. Assignment outcomes and unavailable outcomes are separate.
- Grouped NLA reports return complete post-filter, post-ranking chart rows from
  the same execution before table pagination, through 200 groups. Larger group
  sets return no chart rows and a narrowing reason. Existing reporting limits
  remain intact. The client provides Chart/Table, bars, cycle lines with gaps,
  two-dimensional heatmaps, exact values and source-plan preview/confirmation.
- Web/mobile show the assignment due instant separately from the original PM
  deadline. Mobile caches optional server fields and labels batch status as the
  last server response, without device-clock reclassification.

## Executed checks and source identity

| Check                                                                                                                             | Source                                                                                                         | Result                                                                                                                               |
| --------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| Existing grouped PM/inventory regression, `Asset_transfer_updates_current_report_metadata_without_rewriting_acknowledged_history` | `e77f687f503aff1b4340c18d0e271fe46759ba4d` plus backend chart patch `208652cab7aef90153c87967cc885a5a4c885e65` | 1 passed                                                                                                                             |
| Existing assignment path, `Supervisor_assigns_worker_only_to_a_batch_owned_by_authenticated_supervisor`                           | `0f6b447b4fe09de84f3db918523ff5cfa3d6d18e`                                                                     | 1 passed                                                                                                                             |
| `dotnet ef migrations has-pending-model-changes --no-build --project server/server.csproj --startup-project server/server.csproj` | `0f6b447`                                                                                                      | No pending model changes                                                                                                             |
| Manual reflection smoke of compiled calculator                                                                                    | `0f6b447`                                                                                                      | 8 boundary cases passed                                                                                                              |
| Manual reflection smoke of both production aggregate builders                                                                     | Binary from `0f6b447`; invocation checkout `19ce91b`, with no intervening server changes                       | 8 cases passed: each builder at 0/200/201 groups and Top 20 with page 2                                                              |
| `node node_modules/typescript/bin/tsc -b --pretty false`                                                                          | Final web working changes later committed unchanged as `b0cf785` and `19ce91b`                                 | Passed after one optional-property correction                                                                                        |
| `node node_modules/vitest/vitest.mjs run src/features/reports/pm-period-dashboard.test.tsx --maxWorkers=2`                        | `19ce91bd249d70844bf1d3f5160f75c80d027ae9`                                                                     | 10 passed                                                                                                                            |
| Existing mobile cache restart test                                                                                                | `ae0e5996c4e87fb314edbf09120fa93c308f07f1`                                                                     | 1 passed                                                                                                                             |
| Manual Chromium component smoke using the actual Vite-served AnalyticsChartView                                                   | `ae0e599`; web identical to `19ce91b`                                                                          | 30th group visible beyond 25-row page, source callback invoked, unavailable Chart disabled, no page errors or page overflow at 390px |

The calculator smoke checked exact-due completion, one tick after due,
unfinished work at/after due, legacy unknown history, cancellation, missing
completion evidence and PM-late/assignment-within behavior. The due instant was
July 29 at 14:00 Asia/Manila for June 29 at 14:00 assignment.

The production DLL used by reflection checks had SHA-256
`E1FA5E9547AD232906DF088D37749E1772FE161A9F6041E278508BD03CC58AB4`.
The grouped-builder smoke verified that page slices and chart groups are
independent. It did not use a database or call an AI provider.

The actual OpenAPI document was fetched from an owned loopback Development
host and Orval regenerated the client. Contract checks passed. The combined
`api:generate` wrapper returned nonzero during its Windows formatting step;
standalone Prettier then completed successfully on the generated directory.
The initial browser harness failed because it treated a CommonJS default export
as a named createRoot export. Correcting that harness import produced the
recorded passing run; application source did not change for that correction.
The mobile command was:

```text
flutter test test/offline_inspection_repository_test.dart --plain-name "cached schedules, assets, and references restore after restart offline"
```

## Retained raw artifacts

Raw output stays ignored under:

- `artifacts/remediation/backend/dashboard-nla-charts/grouped-report-targeted-test.log`
- `artifacts/remediation/backend/assignment-period/schedule-assignment-targeted-test.log`
- `artifacts/remediation/backend/assignment-period/model-pending-check.log`
- `artifacts/remediation/backend/assignment-period/calculator-smoke.log`
- `artifacts/remediation/backend/chart-boundary-reflection/chart-boundary-smoke.log`
- `artifacts/dashboard-charts/client/dashboard-test.log`
- `artifacts/dashboard-charts/client/mobile-cache-restart-test.log`
- `artifacts/chart-scan/mobile-chart-smoke.png` and `chart-print-smoke.pdf`

## Limits

The migration was generated and model-checked but was not applied to a native
SQL Server database in this task. Its Down operation rejects a downgrade that
would discard initial/unassignment events. Apply Up when updating an
application database; legacy initial timestamps remain null.

The focused regression tests predate these additions. They verify existing
paths, not every new event, reassignment, offline or cancellation scenario.
Calculator and chart-builder boundary checks are manual compiled-code checks,
not new committed regression tests. Browser verification mounted the actual
component with synthetic response data; it does not establish a full live
browser/API/database workflow or saved-report freshness under concurrent writes.
A print PDF was generated, but full printed-page review was not performed.
No complete test suite, external-provider evaluation, deployment, institutional
acceptance or physical-device verification ran. The 51-chart inventory remains
an opportunity list; only selected charts are implemented.
