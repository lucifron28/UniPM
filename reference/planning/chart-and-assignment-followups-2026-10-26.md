# Charts and assignment-period follow-ups

Recorded October 26, 2026, from the project owner's discussion. Both items are
follow-up work. This record does not change the application, compliance
formula, schedule dates or manuscript.

| Item | Recorded outcome | Next gate |
|---|---|---|
| FU-01 | Dashboard and NLA chart scope, priorities and data constraints | Select the first charts and verify their complete result scope before implementation |
| FU-02 | Late-assignment problem, proposed working period and preserved schedule identity | Confirm the allowance rules before implementing deadline or compliance changes |

Supporting records:

- [Full chart inventory and data readiness](chart-opportunity-inventory.md)
- [Assignment-period decisions and acceptance scenarios](assignment-period-decision-checklist.md)

## FU-01: Dashboard and NLA charts

**Status:** Requested follow-up; implementation pending.

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

These are implementation completion criteria. Recording the follow-up does not
mark the charts implemented or tested.

## FU-02: Proposed 30-day working period after assignment

**Status:** Late assignment is a confirmed operational occurrence, as reported
by the project owner from the GSD Head's explanation. The 30-day allowance and
its precise calculation remain a proposal pending policy confirmation.

**Problem:** A person assigned on June 29 may have only one day before a June
30 PM deadline. Scheduled maintenance lateness alone cannot explain whether
the delay preceded or followed assignment to the person doing the inspection.

Preserve the original `PmCycle`, recorded scheduled month and original PM
deadline. A June schedule remains June if assignment or completion occurs in
July. Show any assignment-based due date separately; do not silently move the
PM deadline or rewrite historical schedule identities.

Proposed direction for confirmation:

- Start the inspector's working-period clock when the inspector receives the
  assignment, rather than when a supervisor is assigned.
- Track supervisor assignment and subsequent inspector assignment separately
  so distribution delay remains visible.
- Distinguish original PM schedule compliance, assignment notice and inspector
  turnaround in dashboards and NLA. Meeting an assignment allowance must not
  silently make overdue scheduled maintenance appear on time.
- Preserve assignment events and reassignment history. Reassignment must not
  silently restart an allowance or erase an earlier delay.

Decisions required before implementation:

1. Whether the allowance is **30 calendar days or 30 working days**, including
   weekend/holiday handling. "Working period" does not settle this distinction.
2. The effective assignment event: persisted assignment, notification delivery
   or another explicitly defined event. Define timezone and inclusive counting.
3. Whether the 30 days are an allowance after assignment, minimum advance notice
   before the original deadline, or two separate rules. These are different
   measures and must not share an ambiguous label.
4. Reassignment, cancellation, unassignment and retrospective-correction rules.
5. The treatment of late offline synchronization. The current completion clock
   is the first server-accepted save; this follow-up does not replace it.
6. How legacy schedules without complete assignment timestamps will appear.

Current schedules contain assignee IDs and general CreatedAt/UpdatedAt values.
Recorded reassignment events do not provide complete initial-assignment history.
Add reliable assignment-event capture before calculating notice or turnaround.
Do not infer missing assignment dates from general update timestamps or backfill
unrecorded history. Mark unavailable historical calculations explicitly.

**Completion criteria:** Approved rules and separate reporting definitions are
documented; assignment events are captured; late-assignment and reassignment
cases are verified; original PM months/deadlines remain intact; dashboard/NLA
labels distinguish maintenance compliance from the assignment allowance.

These are implementation completion criteria. The GSD Head's explanation
confirms late assignments occur; it does not confirm a 30-day allowance or
which assignment event should start it. Unanswered decisions remain open in
the linked checklist.

## Suggested sequence

Confirm FU-02's day-counting and assignment rules first. Implement its data
capture and separate measures before assignment-related charts. FU-01's
existing PM and inventory charts can proceed independently once prioritized.

Source references:

- [Confirmed maintenance workflow](confirmed-gsd-workflow.md)
- [NLA report catalog](../../server/Features/Reports/PmAnalyticsV3Catalog.cs)
- [Report limits and validation](../../server/Features/Reports/PmAnalyticsV3PlanValidator.cs)
- [Schedule model](../../server/Models/PreventiveMaintenanceSchedule.cs)
- [Reassignment audit model](../../server/Models/ScheduleAssignmentReassignmentAudit.cs)
