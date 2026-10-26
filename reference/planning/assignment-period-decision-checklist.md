# Assignment-period decisions and acceptance checklist

**Status:** Planning aid for FU-02. The GSD Head's explanation, as reported by the project owner, confirms that late assignment occurs. It does not approve a 30-day allowance, define its calculation, or select the event that starts it. This checklist records questions for GSD confirmation; it does not change application behavior or PM compliance.

Related record: [Dashboard and assignment-period follow-ups](chart-and-assignment-followups-2026-10-26.md).

## Current facts

- `PmCycle` and its original month-end deadline remain the scheduled PM identity. The deadline uses the Asia/Manila calendar. Assignment after the cycle begins must not move the cycle or overwrite that deadline.
- Existing PM compliance uses the first server-accepted inspection `CompletedAt`. `DateInspected` is a separate entered date. The assignment allowance, if approved, must be a separate measure.
- Schedules store current Inspector and Supervisor IDs plus general `CreatedAt` and `UpdatedAt` values. Initial assignment routes update the assignee and `UpdatedAt`, but do not create an assignment audit event (`SchedulesEndpoints.cs:630-752, 762-898`). `UpdatedAt` also changes for other operations and is not a reliable assignment timestamp.
- `ScheduleAssignmentReassignmentAudit.ChangedAt` records reassignment events, not complete initial assignment history (`ScheduleAssignmentReassignmentAudit.cs:3-14`; `SchedulesEndpoints.cs:2399-2412`).
- The server does not persist the queued device-local completion timestamp. Offline synchronization continues to use the first server-accepted `CompletedAt` for PM compliance.

## Proposed direction for review

The current recommendation is to start a proposed Inspector turnaround allowance at the persisted Inspector assignment event. Keep Supervisor assignment as a separate event so the time spent distributing a batch remains visible. This is a recommendation only.

If GSD approves an allowance, report it separately from:

- the original PM deadline and schedule-compliance result;
- the elapsed time before Inspector assignment; and
- any notification or advance-notice measure.

Do not label an allowance due date as a notice deadline. Do not claim that a notification was delivered or received unless that event is explicitly captured and approved as the clock trigger.

## Decisions required before implementation

| Decision | Options or question | Required definition |
|---|---|---|
| Day basis | 30 calendar days or 30 working days | Weekend and holiday calendar, if applicable |
| Clock trigger | Persisted Inspector assignment, notification delivery, or another event | Authoritative timestamp, timezone, start boundary and inclusive/exclusive counting |
| Relationship to PM deadline | Allowance after assignment, minimum advance notice before the PM deadline, or both | Separate names, formulas and chart labels if both apply |
| Supervisor delay | Measure time between Supervisor assignment and Inspector assignment? | Whether this is a separate operational metric and its event boundaries |
| Reassignment | Keep original Inspector due date, restart, pause, or another rule? | Preserve every prior assignment event and define any new allowance explicitly |
| Unassignment | What happens while no Inspector is assigned? | Pause, continue, close, or another rule, including reassignment behavior |
| Cancellation and completion | What ends or excludes an allowance? | Eligible schedule states and treatment of completed work |
| Offline synchronization | Which timestamp applies to the assignment allowance? | Whether the existing server-accepted completion clock remains authoritative; do not promote device time implicitly |
| Legacy missing events | How should incomplete assignment history appear? | Explicit unavailable/unknown treatment and denominator behavior; no inferred backfill |

The 30-day duration itself also needs confirmation. “Working period” does not establish calendar versus working days. Do not calculate or promise a July 29 assignment due date until GSD selects the day basis, event, timezone, and counting boundary.

## Example to resolve counting rules

A June-cycle schedule keeps its original June 30 PM deadline even if assignment occurs later. If the Inspector is assigned on June 29, that is one calendar date before the original deadline; the remaining elapsed time depends on the event timestamp. The schedule must not be relabeled as a July cycle, and the assignment-based due date must remain unset until the counting policy is selected.

The same schedule assigned on June 30 still retains its June 30 PM deadline. The policy decision must define whether assignment on the deadline date starts the allowance that day or the next day. The system must report the original PM result and any approved assignment-period result separately.

## Data capture required after decisions

Before calculating an assignment allowance, capture an immutable initial-assignment event for each relevant role. It should identify the schedule or batch, role, prior and new assignee where applicable, actor, event time, and event type. Keep reassignment events instead of overwriting them. If GSD chooses a notice event as the trigger, capture that notice event separately; do not assume notification receipt from assignment persistence.

Use an explicit assignment-event timestamp. Do not derive it from schedule `CreatedAt`, general `UpdatedAt`, PM deadline, or current assignee ID. Preserve unknown values for legacy schedules and display their assignment calculation as unavailable until the chosen rule says otherwise.

## Acceptance scenarios after policy approval

- A June 29 Inspector assignment for a June-cycle schedule preserves the June cycle and original June 30 deadline. The assignment allowance is calculated only using the approved rule.
- A June 30 assignment exercises the approved same-day counting boundary. No July 29 date is asserted before day-counting is decided.
- Supervisor assignment followed by Inspector assignment records both events and reports any approved distribution interval separately from Inspector turnaround.
- Reassignment retains the first assignment event and exposes the approved rule for the new assignee. It does not silently erase an earlier delay or restart a clock without an explicit rule.
- Unassignment, reassignment after unassignment, cancellation, and schedule completion follow the selected behavior and denominator rules.
- A completion entered offline before the PM deadline but first accepted by the server after the deadline remains late under existing PM compliance. Assignment-period reporting follows the separately approved offline rule and never silently trusts a device timestamp.
- A legacy schedule with no initial assignment event displays an unavailable assignment calculation. The system does not substitute `UpdatedAt` or manufacture history.
- Weekend and holiday boundary examples distinguish the approved calendar-day or working-day calculation and timezone.

## Stop conditions

Keep FU-02 in planning until GSD confirms the decisions above and the required initial-assignment history is available. Leave original `PmCycle`, original PM deadline, and existing server-accepted completion semantics unchanged. Do not add notification delivery/receipt claims or treat assignment allowance as PM compliance.
