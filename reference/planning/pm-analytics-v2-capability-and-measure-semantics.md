---
id: PLAN-NLA-V2-001
type: planning
status: draft
contractVersion: 2
sourceCommit: ae64da0b2b7d96415435b7e24f82fe29e47eab98
---

# PM analytics V2 capability and measure semantics

This document freezes the plan shape and calculation meaning for the V2
interpretation work on this branch. It records the contracts and validator at
`ae64da0b2b7d96415435b7e24f82fe29e47eab98`. It does not claim GSD, professor,
or adviser approval, and it does not report language evaluation results.

The interpreter returns a typed plan for existing PM analytics. It does not
execute a query, generate SQL, calculate a result, update maintenance records,
or create official history. A plan is executable only after the backend
validates it. A `Valid` result must carry a schema version 2 plan that passes
`PmAnalyticsV2PlanValidator.TryNormalize`. A failed parse or failed validation
must never return a plan.

## Plan fields

The V2 plan has these fields. Model output must include every field, including
fields whose value is `null`:

| Field | Required value | Meaning |
| --- | --- | --- |
| `schemaVersion` | `2` | Plan schema version. |
| `metric` | One of six metric names below | The row predicate being counted. |
| `assetCategory` | One canonical category slug | Exactly one supported category per plan. |
| `period` | Period object or `null` | A month, quarter, year, or inclusive range. |
| `comparison` | Comparison object or `null` | Exactly two ordered cycles. |
| `presentation` | `Count`, `Percent`, or `Records` | How the matching result is presented. |
| `countBasis` | `AssetCycles` or `DistinctAssets` | The unit used for numerator and denominator. |
| `department` | String or `null` | Exact current Asset department filter. |
| `building` | String or `null` | Exact current Asset building filter. |
| `assetCode` | String or `null` | Exact asset code filter. |
| `condition` | Condition name or `null` | Optional inspection condition filter. |
| `timeliness` | Timeliness name or `null` | Optional deadline filter. |
| `groupBy` | `None`, `Department`, `Building`, or `PmCycle` | Optional grouping. |
| `ranking` | Ranking object or `null` | Optional department ranking. |

Exactly one of `period` and `comparison` must be non-null. Missing required
intent should produce `NeedsClarification` with no plan. Unsupported or
contradictory requests should produce `Unsupported` with no plan. These status
shapes are mandatory:

| Status | `plan` | `clarificationFields` |
| --- | --- | --- |
| `Valid` | Complete normalized candidate plan | Empty list |
| `NeedsClarification` | `null` | Nonempty list of known field names |
| `Unsupported` | `null` | Empty list |

The model does not supply an error code or detail text. The backend owns those
values.

## Supported categories and cycles

The category is one scalar string. The canonical values and scheduled months
are:

| Category slug | Scheduled months |
| --- | --- |
| `fire-extinguisher` | February, May, August, November |
| `fire-alarm` | June, December |
| `emergency-light` | June, December |
| `water-drinking-station` | February, May, August, November |

The plan validator accepts these period forms:

| Kind | Required fields | Selected cycles |
| --- | --- | --- |
| `Month` | `year`, `month` | The category's cycle in that month. |
| `Quarter` | `year`, `quarter` from 1 through 4 | Scheduled category cycles within the quarter. |
| `Year` | `year` | Scheduled category cycles within the year. |
| `Range` | Canonical `startCycle` and `endCycle` in `yyyy-MM` form | Scheduled category cycles within the inclusive range. |

Year values are bounded to 2000 through 9998. A range must be ordered and span
no more than 12 calendar months. A period must contain at least one scheduled
cycle for the chosen category. A comparison requires two different, ordered,
canonical cycles, each scheduled for the chosen category. Comparison cannot be
combined with grouping or ranking.

## Metrics

The eligible population consists of schedule rows for the selected category
and cycles, excluding cancelled schedules. The backend uses the current Asset
department and building values. For a blank stored PM cycle, it uses
`ScheduleDate` as a fallback. It does not count a cycle for which no schedule
exists as missed.

| Metric | Matching rows |
| --- | --- |
| `Progress` | Rows with an inspection `CompletedAt` value. |
| `OnTimeCompliance` | Completed rows with `CompletedAt` at or before the cycle deadline. |
| `CompletedLate` | Completed rows with `CompletedAt` after the cycle deadline. |
| `NonOperational` | Completed rows whose inspection condition is non-operational. |
| `Remaining` | Incomplete rows whose cycle has started and whose deadline has not passed. |
| `Missed` | Incomplete rows whose deadline has passed. |

Completion uses `CompletedAt`, including Draft and Submitted forms. This is a
live PM analytics view. Official history remains acknowledged-only.

`AssetCycles` counts schedule rows across selected cycles. `DistinctAssets`
counts unique assets in that same scope. For a comparison, each side gets its
own cycle denominator. A period percentage uses the ratio of total matching
asset-cycles to total eligible asset-cycles. It does not average per-cycle
percentages. The percentage is rounded to two decimal places, with midpoint
values rounded away from zero.

The percentage denominator must use `AssetCycles`. Percent plans cannot include
`condition` or `timeliness` filters. `OnTimeCompliance` percentages are
unmeasurable while any selected cycle remains open or future. A zero denominator
makes a measure unmeasurable. Other valid measures with a nonzero denominator
may return zero when no rows match. `Progress` percentages can be measured while
cycles are open.

`Count` returns a count. `Records` uses the same count basis and presents the
matching source records. `OnTimeCompliance`, `CompletedLate`, and
`NonOperational` do not force a presentation; the requested count, rate, or
record view determines it, subject to validation.

## Filters, grouping, and ranking

Conditions are `Operational`, `NonOperational`, and `NotInspected`.
Timeliness values are `OnTime`, `Late`, `Remaining`, `Missed`, and `Future`.
The validator rejects combinations that cannot match the selected metric.
`Future` is present in the timeliness enum and query predicate, but the current
validator rejects it with every metric. It is not a valid-plan option in this
revision.

Department and building refer to current Asset fields, not historical
snapshots. The query service requires an optional department, building, or
asset code to resolve to one exact current catalog value. A missing or
ambiguous value is a clarification, not a reason to broaden the query.

Grouping supports `Department`, `Building`, and `PmCycle`. Ranking is available
only for `Department`, uses `Highest` or `Lowest`, and accepts a limit from 1
through 10. Ranking cannot be combined with a comparison or `Records`. In the
frozen query implementation, every `Percent` ranking excludes departments
while any relevant cycle remains open, including `Progress` rankings even
though a standalone `Progress` percentage can be measured over open cycles.
This broader ranking restriction is recorded as a limitation for later review;
this scope-freeze does not change query behavior. Ranking limits returned
groups; it does not change population totals or source pages.

## Evaluation boundary

The model parser will require exact property names, all required properties,
known string enum values, no duplicate properties, and no unknown properties.
It will reject numeric enum values and schema versions other than 2. The V2
plan validator remains the source of structural and cross-field plan
validation. The model parser will not require every natural-language question
to fit the existing deterministic grammar. That leaves semantic scope drift as
an evaluation risk. The new protocol records how to measure that risk without
claiming that the current dataset has human-reviewed labels.
