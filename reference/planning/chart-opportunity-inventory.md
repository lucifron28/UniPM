# Chart opportunity inventory

Recorded October 26, 2026. This planning inventory lists 51 possible chart
questions: 27 use existing report measures, 17 need new aggregates over stored
records, and seven need additional data capture or policy decisions. It does
not commit the project to building all 51 charts. No chart implementation or
reporting-rule change is recorded here.

The source scan found that the report catalog defines the available measures
and dimensions, while the plan validator enforces the report limits. The
dashboard service returns its own cohort and cycle summaries. The web report
currently offers Table and Summary presentations. A chart should reuse
validated backend results; it must not calculate a new maintenance measure in
the browser.

## Data readiness

| Group | Count | What is needed |
|---|---:|---|
| Existing report measures | 27 | A chart view over compatible, complete bounded results |
| New aggregates over stored records | 17 | A reviewed server aggregate or report subject |
| New capture or policy | 7 | An approved definition and, where needed, reliable event capture |

## Existing report measures: 27 questions

The measures exist, but chart widgets do not. NLA must return the full bounded
group set for a distribution. One visible table page does not represent the
whole distribution. Dashboard trends across cycles or categories need an
aggregate request beyond the existing single-cycle/category response.

| ID | Chart question | Suitable chart | Data condition |
|---|---|---|---|
| C01 | Scheduled versus completed work by department | Grouped bars | Scheduled and completed by department; dashboard batches also contain counts |
| C02 | Completion progress by department | Horizontal bars | Completed divided by scheduled; show the denominator |
| C03 | On-time compliance by department | Bars or dot plot | Closed cycles only; unmeasurable values remain unavailable |
| C04 | On-time, late and unfinished work | Stacked bars | On-time, late and missed for closed scope; remaining for open scope; four exclusive segments for mixed scope |
| C05 | Department completion proportions | 100% stacked bars | Same exclusive counts as C04; display the denominator |
| C06 | Departments with most overdue unfinished work | Ranked bars | Missed count; optionally use validated Top N |
| C07 | Late completions by department or building | Bars | Completed-late count |
| C08 | Non-operational findings by department or building | Bars | Findings per asset-cycle, not unique failed assets |
| C09 | Scheduled workload by asset category | Bars | Scheduled count by category |
| C10 | Category progress or compliance comparison | Small multiples or bars | Keep percentage meanings separate and show denominators |
| C11 | Scheduled and completed work across PM cycles | Clustered columns | Scheduled and completed count by original PM cycle |
| C12 | Progress across PM cycles | Line or dot chart | Preserve gaps for unscheduled or missing cycles |
| C13 | Compliance across closed PM cycles | Line | Open and future cycles are not zero |
| C14 | Missed counts by original PM cycle | Columns | Current missed outcomes, not a historical backlog snapshot |
| C15 | Non-operational findings across cycles | Columns | Asset-cycle findings, not failure probability |
| C16 | Quarterly comparison | Grouped columns | Counts, or one percentage, within the permitted reporting window |
| C17 | Department by PM cycle | Heatmap | One measure and two grouping dimensions |
| C18 | Asset category by PM cycle | Heatmap or small multiples | Distinguish unscheduled category-month cells |
| C19 | Department by asset category | Heatmap or grouped bars | One measure and two groupings |
| C20 | Workload versus progress or compliance | Scatter plot | Scheduled count and percentage by group; this shows association, not cause |
| C21 | Current asset distribution | Bars | Registered assets by category, department, building or location |
| C22 | Current asset registration status | Bars or small donut | Registration status is separate from inspection condition |
| C23 | Department by category inventory | Heatmap | Registered assets using current allocation |
| C24 | Building by category inventory | Grouped bars or heatmap | Current metadata only |
| C25 | Top or bottom groups for a chosen measure | Ranked bars | Use explicit validated ranking and label the subset |
| C26 | Overall progress | Progress bar and number | Completed divided by scheduled; no speedometer is needed |
| C27 | Inspected asset condition mix | Stacked bar | Dashboard operational/non-operational; compatible NLA counts can derive operational as completed minus non-operational |

## New aggregates over stored records: 17 questions

These questions need a bounded backend aggregate or an approved report
subject. The browser should not fetch an unbounded record set and aggregate it
locally.

| ID | Chart question | Suitable chart | Required aggregation or limit |
|---|---|---|---|
| E01 | Forms in Draft, Submitted and Acknowledged | Status bars | Count unique forms, not inspection rows or dashboard batches |
| E02 | Pending acknowledgement age | Age buckets or histogram | SubmittedAt and acknowledgement time; distinguish an open wait from a completed wait |
| E03 | Gap from last row completion to submission | Histogram or box plot | Server-recorded row completion and submission; not physical work duration |
| E04 | Pending acknowledgements by department | Ranked bars | Unique Submitted forms and a defined aging rule |
| E05 | Acknowledged non-operational handoff coverage | Stacked bars | Eligible findings with and without an external WMS reference |
| E06 | Time until WMS reference is recorded | Histogram or median bars | Reference-recording interval, not repair duration |
| E07 | Assigned versus unassigned unfinished schedules | Bars | Separate supervisor and inspector assignment states |
| E08 | Current unfinished workload by inspector or supervisor | Ranked or stacked bars | Scoped assignment subject; workload alone is not performance |
| E09 | Current overdue unfinished schedule age | Age-bucket bars | Server as-of time minus original PM deadline |
| E10 | Days late for completed schedules | Histogram or box plot | Server completion minus original deadline; identify legacy gaps |
| E11 | Schedule reassignments over time or by role | Bars | Reassignment events only; initial-assignment history is incomplete |
| E12 | New asset registrations by month | Columns | Asset.CreatedAt; registration activity, not historical inventory totals |
| E13 | Assets with repeated non-operational findings | Ranked bars | Cross-asset leaderboard needs asset grouping; a single-asset cycle timeline already works with assetId and PM-cycle grouping within 12 months |
| E14 | Structured water-station tasks recorded | Grouped bars | Category-specific nullable flags; unknown is not false |
| E15 | Submission and acknowledgement activity by event date | Columns | Explicit event-date subject, separate from PM-cycle reporting |
| E16 | Recorded location-check outcomes or accuracy | Bars or histogram | Privacy-reviewed technical aggregate only where records exist; not proof of field inspection |
| E17 | Successful photo uploads by date or size | Columns or histogram | Successful stored evidence only; current rows do not represent failed attempts or complete upload history |

## New capture or policy required: seven questions

| ID | Proposed chart | Missing requirement |
|---|---|---|
| P01 | Days of assignment notice before original PM deadline | Complete initial supervisor and inspector assignment timestamps, plus reassignment policy |
| P02 | Assignments with at least 30 days' notice | P01 plus an approved calendar, day-counting and timezone rule |
| P03 | Inspector completion within 30 days of assignment | Approved allowance, inspector baseline, reassignment rule and offline/server-clock treatment |
| P04 | Supervisor delay before inspector assignment | Both initial timestamps and a rule for later assignment changes |
| P05 | Maintenance compliance versus inspector turnaround | Separate approved clocks and denominators; preserve original PmCycle and PM deadline |
| P06 | Historical backlog as it stood on each past day | Snapshots or enough event history to reconstruct each past state; current missed counts are not that series |
| P07 | Historical inventory or department allocation | Historical status/location snapshots or complete change events |

Current schedules have assignee IDs and CreatedAt/UpdatedAt values, but not
complete initial-assignment clocks. Reassignment audit ChangedAt is not an
initial assignment timestamp. Inspection StartedAt exists but reviewed create
paths do not populate it. Do not infer notice periods from CreatedAt or
UpdatedAt, backfill missing assignment history, or assume a reliable work
start time.

## Calculation and display rules

- PM measures have asset-cycle grain. Inventory has asset grain. Forms and
  audit events have their own grains. Do not combine them into one funnel.
- Distinct assets are non-additive across cycles. Do not sum group distinct
  counts or average compliance percentages. Keep backend totals and ratios.
- Non-operational findings overlap completed work. Do not stack those measures
  as exclusive segments. Use on-time, late, missed and remaining instead.
- Dashboard condition, timeliness and search filters affect asset rows. Its
  cards and department batches describe the broader department-scoped cohort.
  Label chart scope so filtered rows are not mistaken for card totals.
- Current NLA limits are four measures, two groupings, twelve calendar months,
  Top/Bottom 20 and 50,000 input rows per execution. The API permits page size
  up to 100; the web UI requests 25. A chart needs the complete bounded result,
  not only the current page. Repeated page executions are not a guaranteed
  database snapshot. Keep preview, filters, sorting and as-of semantics.
- PM condition, timeliness and form status are filters, not general grouping
  dimensions. Under the current validator, percentage measures cannot use
  matching filters. Chart selection must not bypass those rules.
- Missing months, unknown groups and unmeasurable compliance are not zero.
  Department and building groupings use current asset metadata, even for older
  cycles. Do not imply that current location is historical location.
- A client-side chart may select a suitable display from a validated plan and
  numeric/time column types. The API currently supports Table and Summary
  presentations. Keep that contract unless a reviewed requirement needs a
  bounded chart-data response. LLM output must not supply chart values.
- Keep exact values, units and denominators available in a table. Support
  keyboard access, print and source-record drilldown. Colour or hover must not
  be the only way to read a value. Chart clicks still follow preview and
  confirmation.

## Initial implementation direction

For the dashboard, retain the KPI cards. Consider department completion,
department progress and condition mix for the selected scope. A cycle trend and
department-by-cycle heatmap need bounded multi-cycle aggregation. Keep charts
that rely on assignment-notice rules behind the separate policy follow-up.

For NLA, start a Chart/Table view for eligible grouped aggregates. Bars,
grouped or stacked bars, time-series lines/columns and heatmaps cover the
questions above. Use scatter only when the selected measures support it. Keep
single numeric summaries as cards and record results as tables. Saved reports
must chart fresh execution results, not cached values.

The assignment-related chart decisions are tracked separately in the
[assignment-period checklist](assignment-period-decision-checklist.md). The
overall follow-up status and next gates are in the
[charts and assignment follow-up record](chart-and-assignment-followups-2026-10-26.md).

## Source references

- [NLA measure and dimension catalog](../../server/Features/Reports/PmAnalyticsV3Catalog.cs)
- [NLA plan validation and limits](../../server/Features/Reports/PmAnalyticsV3PlanValidator.cs)
- [NLA request and response contracts](../../server/Features/Reports/PmAnalyticsV3Contracts.cs)
- [NLA query and aggregation service](../../server/Features/Reports/PmAnalyticsV3QueryService.cs)
- [Dashboard response contracts](../../server/Features/Reports/PmPeriodDashboardContracts.cs)
- [Dashboard aggregation service](../../server/Features/Reports/PmPeriodDashboardService.cs)
- [NLA report UI](../../web/src/features/reports/pm-analytics-v3.tsx)
- [Dashboard UI](../../web/src/features/reports/pm-period-dashboard.tsx)
- [Dashboard query client](../../web/src/features/reports/pm-period-dashboard-queries.ts)

Chart-family choices follow the [ONS guide to choosing a chart type](https://service-manual.ons.gov.uk/data-visualisation/chart-types/choosing-a-chart-type). This inventory is a proposal, not an implementation or verification record.
