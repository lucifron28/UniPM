# Charts and assignment-period follow-ups

Recorded October 26, 2026, from the project owner's discussion. The selected
charts and owner-confirmed 30-calendar-day assignment period are implemented
on `feature/dashboard-nla-charts`. Original PM compliance, schedule identities
and deadlines remain separate. No manuscript edits are included.

Verification and remaining acceptance limits are recorded in
[TEST-166](../evidence/test-runs/TEST-166-charts-and-assignment-period.md) and the
[review corrections in TEST-167](../evidence/test-runs/TEST-167-chart-assignment-review-corrections.md).

| Item  | Recorded outcome                                                                                     | Next gate                                                                                     |
| ----- | ---------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------- |
| FU-01 | Dashboard bars/stacked outcomes, NLA Chart/Table, cycle lines and two-dimension heatmaps implemented | Review the interface with GSD; additional inventory candidates remain optional                |
| FU-02 | Owner-selected 30-calendar-day period and initial/event history implemented                          | Apply the additive migration when updating an application database; legacy dates stay unknown |

Supporting records:

- [Full chart inventory and data readiness](chart-opportunity-inventory.md)
- [Assignment-period decisions and acceptance scenarios](assignment-period-decision-checklist.md)

## FU-01: Dashboard and NLA charts

**Status:** Selected charts implemented. Dashboard charts cover department workload, progress, PM outcomes, compliance, condition mix and assignment outcomes. NLA charts use complete same-execution grouped results, with bars, cycle lines and heatmaps. All 51 inventory candidates were not requested for implementation.

Add charts to help GSD compare maintenance progress, findings and workload.
Keep the existing tables, report-scope review, confirmation and source records.
Use backend-calculated values from the same validated report definition.

Initial candidates:

- Department progress and scheduled-versus-completed comparisons.
- On-time, late and unfinished PM breakdowns using mutually exclusive counts.
- Progress and closed-cycle compliance trends by scheduled PM cycle.
- Non-operational findings by department or asset category.
- Current asset distributions by category, department and building.
- Department-by-cycle or category-by-cycle heatmaps.

Start with bars, grouped/stacked bars and time-series lines/columns. Add a
Chart/Table view for compatible NLA aggregates. Use heatmaps for two grouping
dimensions. Single-value summaries can remain cards; individual records remain
tables. Saved-report charts must use fresh execution results.

Implementation requirements:

- Retrieve a complete bounded group set, or explicitly label a selected Top N.
  Do not chart the visible paginated table as the entire population.
- Keep units, denominators, current-metadata labels and unavailable values.
  Do not sum distinct assets across cycles or average group percentages.
- Distinguish dashboard cohort totals from condition/timeliness/search-filtered
  asset rows. Do not silently apply one scope's values to another scope's chart.
- Preserve the reporting bounds: four measures, two groupings, twelve calendar
  months, Top/Bottom 20 and the 50,000-input-row execution limit unless a
  separately reviewed requirement changes them.
- Provide readable labels, accessible exact-value tables, printable output and
  source-record drilldown. Colour and hover must not be the only explanations.
- New form, assignment, aging and event-date charts need explicit backend
  aggregates. They must not be inferred from unrelated fields in the browser.

The source scan identified 51 candidate questions: 27 using existing report
measures, 17 requiring aggregates over stored records, and seven requiring
additional capture or policy decisions. These are possibilities for prioritizing
work, not a commitment to implement 51 charts.

**Completion criteria:** Selected charts agree with their tables and source
scope, handle empty/null/paged results correctly, preserve backend calculations,
and work on mobile-width screens and printed reports.

Charts retain exact-value tables and source review. NLA charts include at most
200 complete groups; larger unranked results require narrower scope or the
existing Top/Bottom limit. The inventory remains a list of possible later work.

## FU-02: Proposed 30-day working period after assignment

**Status:** Implementation authorized by the project owner on October 26, 2026.
Use 30 calendar days from the first saved Inspector assignment, preserve that
due date after reassignment, and measure completion at the first server-accepted
save. Keep the original PM month and deadline separate. The linked checklist
records the selected rules and preserves the earlier decision questions.

**Problem:** A person assigned on June 29 may have only one day before a June
30 PM deadline. Scheduled maintenance lateness alone cannot explain whether
the delay preceded or followed assignment to the person doing the inspection.

Preserve the original `PmCycle`, recorded scheduled month and original PM
deadline. A June schedule remains June if assignment or completion occurs in
July. Show any assignment-based due date separately; do not silently move the
PM deadline or rewrite historical schedule identities.

Selected implementation rules:

- Start at the first saved Inspector assignment and add 30 calendar days to
  that instant. Completion at or before the due instant is within the period.
- Capture Supervisor and Inspector assignments separately. Preserve initial
  timestamps through reassignment and unassignment; the clock continues.
- Keep original PM compliance and assignment turnaround separate in dashboard,
  NLA, web schedule detail and mobile schedule displays.
- Record initial assignment, reassignment and unassignment events. Legacy
  schedules without reliable initial history remain unavailable.
- Keep server-accepted completion authoritative, including offline uploads.
  Do not assert notification delivery or physical work time.

The [assignment checklist](assignment-period-decision-checklist.md) defines the
selected rules, June/July example, data boundary and acceptance scenarios.

**Completion criteria:** Approved rules and separate reporting definitions are
documented; assignment events are captured; late-assignment and reassignment
cases are verified; original PM months/deadlines remain intact; dashboard/NLA
labels distinguish maintenance compliance from the assignment allowance.

The owner confirmed the calculation during this implementation. The GSD Head's
explanation confirms the occurrence of late assignments, not separate approval
of the new allowance. Native database migration execution and GSD/device
acceptance are not established by the focused checks recorded in TEST-166.

Source references:

- [Confirmed maintenance workflow](confirmed-gsd-workflow.md)
- [NLA report catalog](../../server/Features/Reports/PmAnalyticsV3Catalog.cs)
- [Report limits and validation](../../server/Features/Reports/PmAnalyticsV3PlanValidator.cs)
- [Schedule model](../../server/Models/PreventiveMaintenanceSchedule.cs)
- [Reassignment audit model](../../server/Models/ScheduleAssignmentReassignmentAudit.cs)
