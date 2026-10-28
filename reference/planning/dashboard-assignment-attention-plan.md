# Dashboard assignment attention plan

Recorded October 28, 2026.

Status: implemented on `feature/assignment-attention-plan`, verified in CI on
`dd5c01737a996c7814852432ece2285c901120fc`. The warning thresholds below are UI
defaults, not an institutional assignment deadline. This document retains the
original design and pre-implementation source audit. Execution results and
remaining verification limits are recorded in
[TEST-178](../evidence/test-runs/TEST-178-assignment-attention.md).

## Purpose

Help the GSD Head identify PM batches needing a Supervisor and help Supervisors
identify their batches needing an Inspector before the scheduled month ends.
Put a visible action near the top of the dashboard and show matching urgency
badges in Planning & assignment.

Preserve the recorded PM month and original PM deadline. Preserve the separate
30-calendar-day Inspector period from the first saved Inspector assignment,
including its original due date after reassignment. An assignment warning must
never mark an Inspector late merely because the scheduled month ended.

## Dashboard layout

Use this order at desktop width:

1. Dashboard title and Ask a question action.
2. Reporting-period selector and department filter.
3. One compact Assignment attention banner when eligible assignments remain.
4. Existing summary cards: Scheduled, Completed, Outstanding, Overdue assignment,
   Unassigned, and Non-operational.
5. Completion by department and Inspector assignment turnaround charts.
6. More comparisons, access to full records, and corrective follow-up.

The source audit confirmed that Outstanding already exists and there is no
separate Progress card to replace. Retain the current cards and their record
links. Keep the completion percentage in its existing supporting text or chart.

Example GSD banner:

```text
Warning icon  Assignment attention
3 batches need a Supervisor. 2 are urgent.
Fire extinguishers · August 2026 · All departments
                                      Review assignments →
```

Example Supervisor message:
"4 batches need an Inspector. 1 is urgent."

Use correct singular/plural labels. The banner counts distinct batches; the
Unassigned card counts individual schedules. Display those units explicitly.

## Role and scope

| Role | Work requiring attention | Destination |
| --- | --- | --- |
| GSD Head using the GSD role | Eligible unfinished batches without a Supervisor | Supervisor Assignments |
| Supervisor | Eligible unfinished batches owned by the signed-in Supervisor without an Inspector | Inspector Assignments |

Respect existing assignment locks, ownership checks and role authorization.
The current queue excludes locked batches and uses explicit Supervisor and
Inspector assignment states. Reuse that eligibility contract. Mixed ownership,
partially assigned batches and inconsistent states must be handled through the
existing review/reassignment flow, not counted as immediately assignable work.

The banner follows the selected asset category, PM cycle and department.
Label this scope visibly. It is not a global backlog count. Preserve these
filters when Review assignments opens Planning & assignment. If urgency filters
are added, include them in the URL so reload and back navigation retain scope.
Show all pending batches by default, with urgent ones first; an urgent count
can provide a separate filtered shortcut.

Confirm the existing dashboard access rules for Supervisors before wiring the
banner. Put their warning on the authorized Supervisor landing view if the PM
dashboard is not available to that role. Do not widen access to GSD analytics
just to display assignment counts. For accounts with both roles, show clearly
labeled role actions without combining their counts.

The source audit below confirms that Supervisors already have dashboard access.
For accounts with both roles, the existing schedules page gives GSD precedence.
Use that same precedence in the initial banner so its action opens the matching
queue. Separate role actions require an explicit role selector on the destination;
do not link a Supervisor count to a page that will render the GSD queue.

## Proposed urgency rules

Use the batch's scheduled PM month and calendar dates in Asia/Manila. Determine
month-end from the schedule, including February and leap years. The thresholds
apply only while the required assignment is missing and eligible work remains.

| Timing | Tone | Batch label |
| --- | --- | --- |
| Before the scheduled month | Neutral | Assignment pending |
| In the scheduled month, more than 14 days before month-end | Neutral | Supervisor needed / Inspector needed |
| 8–14 days before month-end | Amber | Assign soon |
| 0–7 days before month-end | Red | Assignment urgent |
| After the scheduled month | Red | Scheduled month ended · Assignment pending |

Use calendar-date subtraction, with month-end itself equal to zero days.
Display "Month ends today" for zero, and "5 days until month-end" for five.
After month-end, retain the scheduled month in the message. Do not call this
an overdue Inspector assignment period.

Hide the banner after a successful load confirms zero eligible pending batches.
Use a neutral banner for ordinary pending work, amber if any batch needs action
soon, and red if any is urgent or remains pending after month-end. The urgent
subtotal includes both red states and is a subset of the pending total.

After a Supervisor is assigned, remove that batch from the GSD missing-Supervisor
count. It becomes visible to its Supervisor if an Inspector is still needed.
After an Inspector is assigned, remove the missing-Inspector warning and use
the existing Inspector assignment-period status. Recalculate after subsequent
assignment changes rather than preserving stale warnings.

## Visual treatment and interaction

- Put the banner below the reporting-period controls and above summary cards.
- Use a lightly tinted background, a thin coloured left border, an icon and
  plain text. Avoid a solid red row, flashing, or a blocking dialog.
- Make Review assignments a visible action with pointer cursor, hover
  highlight and keyboard focus styling.
- Use matching badges next to each batch heading in Planning & assignment.
  Include the missing role and remaining days so urgency has a clear cause.
- Sort pending batches by urgency, then scheduled month, department and category.
- Refresh counts after successful assignments and when the relevant calendar
  day changes. Show loading and unavailable states without presenting errors
  as zero pending assignments. Clear stale messages when scope changes.

## Implementation approach

1. Confirm role landing views and share the existing server-side batch identity,
   eligibility, unfinished-work and ownership rules between counts and lists.
2. Calculate role-scoped pending and urgency counts in the backend from the
   complete filtered cohort. Do not count the current browser page or infer
   missing-Supervisor counts from the Unassigned schedules card.
3. Return server-calculated urgency, its calculation date and the selected
   scope through the appropriate authenticated contract. Share the same rules
   with assignment-list badges, filters and sorting. Keep time injectable for
   boundary verification. A new schema migration is not expected for derived
   warnings; confirm this against the implementation before adding storage.
4. Add the banner, scoped navigation and Outstanding card. Keep existing charts,
   records links and printable reports usable.
5. Add assignment-list badges and verify count/list agreement, updates after
   assignment, role permissions and desktop layout.

No automatic assignments, notifications, emails, background escalation, or new
institutional policies are part of this follow-up. The banner is an operational
prompt and must not prevent assignment after month-end.

## Acceptance checklist for implementation

- Verify boundaries at 15, 14, 8, 7, 1 and 0 days before month-end, the next
  day, future months, leap February and the Manila midnight boundary.
- Confirm completed/cancelled or locked work is not offered for assignment.
- Confirm Supervisor A cannot see or act on Supervisor B's assignment counts.
- Confirm counts use distinct batches across all result pages and match the
  destination list for the same scope and missing role.
- Assign an Inspector on June 29 and confirm the full existing 30-calendar-day
  period remains intact. Reassignment retains the original assignment due date.
- Verify assignment success, partial failure, refreshed scope, unavailable
  service and zero-count states without misleading warnings.
- Check desktop widths of 1440 and 1536 pixels, keyboard navigation, text/icon
  meaning without colour, back navigation, and printable report regression.

These are planned acceptance checks, not executed verification. Coordinate the
single most relevant targeted test and desktop browser verification within the
repository's execution limits when implementation is requested.

## Source baseline

Planning is based on main at `f74f014481e2b5ec1b444cade64fcbfe22fdcca4` and the
project owner's dashboard discussion. Relevant existing components:

- `web/src/features/reports/pm-period-dashboard.tsx`
- `web/src/features/schedules/schedule-assignment-queue.tsx`
- [Existing chart and assignment-period follow-up](chart-and-assignment-followups-2026-10-26.md)
- [Inspector assignment-period decisions](assignment-period-decision-checklist.md)

## Codebase feasibility audit, October 28, 2026

Outcome: feasible with additive read-contract and UI changes. This is source
inspection only; no application changes or verification runs were performed.

| Area | Confirmed source behavior | Implementation consequence |
| --- | --- | --- |
| Access | `app-route-access.ts` includes GSD and Supervisor in dashboard roles; dashboard API requires operational read access | Both roles can receive the banner on the existing dashboard |
| Destination | Schedules validates `view=assignments`, category, department, year and PM cycle | Existing navigation can preserve dashboard scope |
| Dual roles | `routes/app/schedules/index.tsx` chooses GSD before Supervisor | Initial banner must follow the same precedence |
| Batch identity | Assignment endpoint groups by `ScheduleBatchIdentity` | Count batches with the existing identity, not assets or buildings |
| Supervisor scope | Assignment endpoint limits Supervisor results to groups containing their schedules; the UI additionally requires consistent ownership | Preserve both checks when deriving actionable counts, including mixed groups |
| Eligibility | Queue excludes locked groups, requires unfinished work without inspection evidence, and requires the role-specific unassigned states | Share this exact predicate with backend attention counts; a null Supervisor on a single record is insufficient |
| Completeness | Non-tracking assignment queries return all grouped results; pagination applies to tracking mode | Reuse the assignment cohort; do not use paginated tracking rows for counts |
| Existing summary | Queue summary exposes progress and overdue-work counts | Add explicit missing-assignment and urgency fields; existing totals are not substitutes |
| Clock | Assignment endpoint already receives TimeProvider and uses institutional-time conversion elsewhere | Derive month-end urgency from one server calculation time, with Manila calendar dates |
| Inspector deadline | ScheduleAssignmentPeriod adds 30 days to first Inspector assignment | Keep month-end warnings outside this calculation |
| Summary cards | DashboardMetrics already renders Outstanding and five other cards | Remove the obsolete Progress-card replacement from the implementation scope |
| Refresh | Assignment hooks invalidate schedule-query families; dashboard uses a different query family | Keep attention queries under the invalidated schedule family, or invalidate their own keys explicitly; add date-change refresh |

The smallest approach is to extend the existing assignment-batches response with
server-derived attention metadata and summary counts, then use that response for
the banner and queue badges. Extract the eligibility predicate from the current
client filter into the server's shared assignment read logic. Preserve existing
queue fields and consumers. Update the OpenAPI snapshot, generated client and
runtime response parser together when the contract changes.

Urgency filtering is not an existing URL option. Initial navigation can show the
complete scoped pending list sorted by urgency. A dedicated urgent-only shortcut
must add an explicit validated URL parameter and matching server behavior before
it is displayed. The warning thresholds remain the proposed 14/7-day defaults.

Review locations include `SchedulesEndpoints.cs` assignment-batches handler and
CreateAssignmentQueueBatch, `schedule-assignment-queue.tsx` eligibleBatches,
`schedule-queries.ts` assignment invalidation, `pm-period-dashboard.tsx`
DashboardMetrics, and `routes/app/schedules/index.tsx` role selection. Existing
ScheduleBatchAssignmentEndpointsTests contain grouped-queue, mixed ownership
and Supervisor authorization cases; their presence is not a new execution claim.
