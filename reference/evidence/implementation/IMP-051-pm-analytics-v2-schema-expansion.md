---
id: IMP-051
type: implementation
title: PM analytics V2 schema expansion
status: draft
recordedAtUtc: 2026-10-14T19:47:08Z
sourceCommit: 1625d88e68797f9f92ea26646b955a4f8515f85a
sourceBranch: feature/nla-schema-expansion
evidenceLevel: source-inspected
---

# PM analytics V2 schema expansion

This record describes the separately authorized V2 expansion of the GSD-only
preventive-maintenance analytics flow. It is not GSD, professor, or adviser
acceptance, and it does not establish language quality or production readiness.

## Data audit scope

The source-code baseline inspected for the audit was main commit
7179227639b29913451ec81617e39e9e2ca9bd91. A read-only aggregate audit observed
a local SQL Server database named UniPMDb; whether that database is the active
API target is unconfirmed. These values describe that database snapshot only
and must not be presented as current institutional or production counts.

The audited snapshot contained 45 assets, 162 schedules, 107 inspections, 44
forms, and 29 acknowledgements. All inspected category-cycle combinations were
CPMP-valid, with no duplicate asset-cycle schedules. Schedule/inspection
counts by year were 2024: 8/8, 2025: 70/70, and 2026: 84/29. The 107
inspections had CompletedAt values; DateAccomplished was null for all 107.
There were 53 non-operational inspections, 32 late inspections, and 31 in both
sets. The snapshot had 11 past unfinished and 44 future unfinished schedules.
Forms were 29 Acknowledged, 10 Draft, and 5 Submitted; no WMS referral rows
were observed.

The report labels department and building from current asset metadata. They
are not historical snapshots. Inspection completion uses CompletedAt; the
audit's null DateAccomplished values do not establish that other databases
share the same condition.

## V2 contract and display semantics

V2 adds GSD-only deterministic interpretation and typed-plan execution at
/api/v1/analytics/pm/v2/interpret and /api/v1/analytics/pm/v2/query. The web
client sends the returned plan directly to the query operation. Requests are
revalidated by the API; the interpreter does not generate SQL or call a model.

The bounded English query scope includes Progress, On-time Compliance,
Completed Late, Non-Operational, Remaining, and Missed measures; one of the
four supported asset categories; a single month, quarter, year, or at most
twelve-month range; and a separate comparison of exactly two CPMP-valid
cycles. Department, building, and specific-asset context can narrow the
scope. Results can be grouped by department, building, or PM cycle, with
department rankings available for eligible measures.

Remaining counts unfinished schedules in active cycles only. Missed counts
unfinished schedules in closed cycles only. Future cycles and missing schedule
rows are not treated as missed work. Cancelled schedules are excluded.
CompletedAt is the authoritative completion timestamp. On-time compliance
uses the eligible population and remains unavailable until every required
period cycle is closed. Progress percentages remain available for open cycles.
A zero matching count with an eligible denominator remains zero;
an empty eligible denominator is unmeasurable. Draft and Submitted inspection
work remains visible in operational measures and source rows; results do not
publish official maintenance history.

The query response separates eligible and matching asset-cycle counts from
eligible and matching distinct-asset counts. Matching source rows and
calculation-source rows are independently paged on the server. Changing source
scope or page does not change the calculation denominator. Distinct-asset
counts can overlap across groups or cycles. Ranking uses the full eligible
department population and reports groups excluded from rate rankings; its
source list is still scoped to the entire report, not only the top-ranked
departments.

The UI shows current department/building labels, PM cycle, deadline, completion
time in Asia/Manila, schedule status, form status, condition, timeliness, and
legacy-date fallback where present. It avoids displaying raw database IDs as
record labels. Print invokes the browser's print action; no PDF layout or
physical-print verification is claimed.

## Provisional query cases

New synthetic development cases are kept separately in
reference/evaluation/pm-analytics-v2-expansion/. They are provisional examples
for V2 contract work, not a benchmark, quality score, language coverage claim,
or held-out set. Existing evaluation datasets and historical scores are
unchanged and were not opened for this task.

## Verification status

Backend verification remains unproven. The focused backend invocation exited
with code 1 during dependency restore, before test discovery; it produced no
test counts and is not a pass. The native SQL Server test did not execute.

The focused web test command
`npm run test:run -- src/features/reports/pm-analytics.test.tsx` ran against
base commit `1625d88e68797f9f92ea26646b955a4f8515f85a` plus the uncommitted
web/API patch, not an exact committed source head. Its first run had 11 passing
tests and one stale label expectation for Remaining + DistinctAssets. After
updating that expectation, the single rerun passed all 12 tests. The rerun
preceded the later button-variant mapping correction, so it does not verify
that final panel source. Logs are
`artifacts/web-qa-20261015/nla-v2/web/pm-analytics-vitest.log` and
`artifacts/web-qa-20261015/nla-v2/web/pm-analytics-vitest-rerun.log`.
The first `npm run typecheck` invocation found Button variant names that were
not supported by the shared component. The panel mapped those values to the
existing `primary` and `secondary` variants; the authorized typecheck rerun
passed. Logs are `artifacts/web-qa-20261015/nla-v2/web/typecheck.log` and
`artifacts/web-qa-20261015/nla-v2/web/typecheck-rerun.log`. Changed web source
files were formatted with the installed Prettier binary.

No full web suite, browser run, web production build, lint, exact-head CI, or
native SQL verification is recorded here. The local aggregate audit and source
inspection are not execution evidence. Exact-source verification must name
the final tested commit and distinguish native SQL, unit, browser, and CI
evidence.
