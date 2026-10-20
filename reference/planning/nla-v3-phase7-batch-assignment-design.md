# NLA V3 Phase 7: PM batch and assignment reporting plan

Status: planning only. No Phase 7 reporting subject or implementation is part
of this change.

## Existing source and operational grouping

The current assignment queue is `GET /api/v1/schedules/assignment-batches` in
`SchedulesEndpoints`. Its batch identity is the normalized tuple:

```text
current Asset.Department + Asset.AssetCategory + PreventiveMaintenanceSchedule.PmCycle
```

`ScheduleBatchIdentity` trims and uppercases department and category; cycle
remains the canonical `yyyy-MM` value. Building is not part of the identity.
The route queries a year of persisted schedules, optionally filters category,
department, and PM cycle, then groups the rows. It returns schedule counts,
schedule status counts, Supervisor and Inspector assignment states, an
assignment lock flag, ownership consistency, and one representative schedule.
The queue's current query skips rows that cannot form the identity. The new
report must make that exclusion visible or resolve it by an approved rule; it
must not silently imply that all stored schedules were grouped.

Assignment state is aggregated across every schedule in a batch:

- `Assigned`: every schedule has the same non-null assignee.
- `Unassigned`: every schedule has no assignee.
- `Partially assigned`: the batch contains both null and non-null assignees.
- `Mixed`: all rows are assigned, but to more than one user.

The operational queue sets `AssignmentLocked` when any row has inspection
evidence, a completion timestamp, or a schedule status that is not assignable.
These definitions should be reused or explicitly versioned; a V3 report must
not reinterpret “assigned” as “work started” or “completed.”

The assignment queue is GSD-readable and Supervisor-readable under the existing
schedule-read policy. A Supervisor sees only batches with at least one schedule
assigned to that authenticated Supervisor, but each returned batch summarizes
its complete schedule membership. Assignment mutation endpoints are separately
authorized. A first V3 subject should remain GSD-only, matching current V3
authorization, until record-level assignment privacy and mixed-owner scope are
reviewed. It should return assignment states and counts, not user IDs, names,
staff workload scores, or mutation links.

## Proposed V3 subject boundary

Use a new typed subject such as `pmAssignmentBatch`, separate from `pmExecution`
and `assetInventory`. The subject grain is one operational batch keyed by
current normalized department, category, and PM cycle. A building never splits
a batch. Do not join to inspections, forms, users, or enrollment-deferral rows
in a way that multiplies a batch; aggregate schedule facts first and join only
bounded, unique references if a reviewed requirement needs them.

Initial dimensions and filters to review:

- PM cycle/year and asset category;
- current department, with an explicit policy for missing or whitespace-only
  department values;
- Supervisor assignment state and Inspector assignment state;
- assignment lock state;
- schedule status counts, with cancelled schedules explicitly represented;
- optional current building as a displayed batch attribute only if one value can
  be stated safely; it is never part of batch identity.

Candidate measures are `batchCount`, `scheduleCount`, `supervisorAssignedBatchCount`,
`inspectorAssignedBatchCount`, and explicit schedule-status counts. Do not add
workload percentages or employee comparisons in the first milestone. If
assignment coverage percentages are later approved, state the denominator as
eligible keyed batches and define treatment of `Partially assigned`, `Mixed`,
locked batches, and unkeyed schedules before implementation.

Do not sum the representative schedule's status as a batch status. A batch can
contain due, overdue, ongoing, completed, and cancelled schedule rows at once;
report those as separate counts or with a separately approved batch-state
calculation.

## Questions that need review before coding

1. Does the V3 report use the exact operational assignment queue year filter,
   including its `Schedule.Year` behavior, or should year scope be derived from
   canonical PM cycles? Resolve rows where these fields disagree.
2. Should a blank stored PM cycle use the existing fallback from schedule date,
   be counted as unkeyed, or be excluded with a visible exclusion count? The
   assignment queue currently attempts to group on the persisted cycle.
3. How should schedules with missing department be represented? The operational
   batch identity currently requires a department, while other V3 subjects can
   group unknown current values.
4. Should cancelled schedules remain in `scheduleCount` and status counts? The
   current assignment queue includes them in its source scope and reports a
   lock; other PM measures may exclude them.
5. Are current department and Supervisor/Inspector assignments the intended
   facts, or is historical responsibility required? The schema stores current
   assignment fields, not assignment snapshots.
6. Is an assignment state sufficient for reporting, or does GSD need counts of
   distinct assignees? Avoid returning identities until access is separately
   reviewed.
7. Do reports include locked batches? If included, document the lock and state;
   if excluded, expose their count and reason.
8. Which existing workflow test and exact source query define “eligible batch”
   for operational parity?

No question labels for this subject should be approved by implementation
authors alone. Keep all first-draft natural-language cases pending human review.
The current 18 PM execution labels remain pending, and V3 scoring remains
blocked until those labels are independently reviewed and the corpus is frozen.

## Focused verification proposal

Before implementing Phase 7, specify a synthetic oracle with stable expected
results and add API tests that execute full preview and confirmation:

- same department/category/cycle across multiple buildings is one batch;
- another category, cycle, or normalized department creates another batch;
- the four assignment states are distinguishable for Supervisor and Inspector;
- one-to-many inspection, form, or audit records do not multiply schedules or
  batches;
- complete, partial, mixed, locked, cancelled, and inconsistent batches have
  explicit expected inclusion and counts;
- unkeyed schedule counts are visible under the chosen missing-data policy;
- grouped ranking, stable ordering, and pagination use batch grain;
- GSD authorization follows the existing V3 policy; no Supervisor sees a V3
  batch until record-scope behavior is approved;
- signed preview expiry, tampering, and cross-user access remain covered;
- parity tests compare V3 eligibility/counts with the existing assignment queue
  for the approved scope and show any intentional differences.

Run the same subject against a disposable SQL Server 2019 database to verify
translation, grouping, and deterministic pagination. Never use Azure staging.
No schedule write, assignment mutation, database migration, or employee scoring
belongs in Phase 7.

## Phase 8 dependency notes

Form reporting should remain one form per row and explicitly distinguish Draft,
Submitted, and Acknowledged. Select an event timestamp and fixed as-of instant
for each ageing measure. An acknowledgement is not inspection completion.
Follow-up reporting must choose one inspection or one referral as its grain and
deduplicate referral audit history. Phase 8 depends on the same count-fanout and
authorization review; signatures and signatory details stay outside question
interpretation.
