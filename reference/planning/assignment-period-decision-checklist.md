# Assignment-period rules and acceptance checklist

The project owner authorized implementation on October 26, 2026 and selected
30 calendar days from the Inspector's saved assignment. The GSD Head's
explanation, as reported by the owner, establishes that late assignment occurs.
The allowance is the owner's implementation decision, not a claim of additional
institutional approval or field acceptance.

Related record: [Charts and assignment follow-ups](chart-and-assignment-followups-2026-10-26.md).

## Selected rules

| Decision               | Selected behavior                                                                                    |
| ---------------------- | ---------------------------------------------------------------------------------------------------- |
| Duration               | 30 calendar days, including weekends and holidays                                                    |
| Start                  | First persisted Inspector assignment timestamp, not notification delivery                            |
| Due instant            | Initial Inspector assignment plus 30 days, displayed in Asia/Manila                                  |
| Completion             | First server-accepted completion at or before the due instant is within the period                   |
| PM identity            | Preserve the original PM month and PM deadline; report PM compliance separately                      |
| Supervisor assignment  | Capture its initial timestamp separately; it does not start Inspector turnaround                     |
| Reassignment           | Preserve the original first-assignment timestamp and due instant                                     |
| Unassignment           | Keep the existing clock running and record the removal; subsequent assignment does not reset it      |
| Cancellation           | Exclude cancelled schedules from aggregate assignment-period measures, as with PM measures           |
| Offline completion     | Keep the first server-accepted save as the completion clock; do not promote a device timestamp       |
| Legacy missing history | Leave first timestamps null and report unavailable outcomes; never substitute CreatedAt or UpdatedAt |
| Notification           | Do not assert delivery or receipt; saved assignment starts the period                                |

The fixed UTC+08:00 institutional timezone has no daylight-saving transition.
The calculation adds 30 days to the saved timestamp. It does not round to the
end of a day or count the assignment date as a completed elapsed day.

## Example

A June-cycle schedule assigned to an Inspector on June 29 at 14:00 Asia/Manila
retains its June 30 PM deadline. Its separate assignment due instant is July 29
at 14:00 Asia/Manila. A July 10 completion is late for the June PM cycle and
within the assignment period. Both results must remain visible.

Reassignment on July 5 keeps July 29 at 14:00 as the assignment due instant.
A server-accepted completion exactly at that instant is within the period; a
later completion is late for the assignment period as well.

## Data and implementation boundaries

- Nullable `FirstSupervisorAssignedAt` and `FirstInspectorAssignedAt` fields
  store the first newly recorded assignment for each role.
- Initial assignment, reassignment and unassignment events capture schedule,
  role, prior/new assignee, actor, timestamp and reason in the existing audit
  table, with an explicit event type. Existing events remain reassignment
  events. No historical first timestamps are backfilled.
- Batch assignment and technical-administration assignment paths use the same
  event recorder. Account removal preserves events and first timestamps.
- Schedule responses, dashboard summaries and NLA records/counts use a shared
  assignment-period calculator. Missing completion evidence for a schedule
  marked Completed also produces an unavailable outcome.
- PM deadline, PM cycle, acknowledgement and offline synchronization rules
  remain separate. The allowance never changes the stored PM deadline.

## Acceptance scenarios

1. June 29 and June 30 assignments preserve the original June cycle and PM
   deadline, while producing their own 30-calendar-day due instants.
2. Completion exactly at the assignment due instant is within the period;
   completion after it is late. Unfinished work becomes overdue only after it.
3. Supervisor assignment and Inspector assignment produce separate events and
   timestamps. Only the Inspector event starts the working period.
4. Reassignment and unassignment preserve the original due instant and retain
   the event history. An idempotent repeated assignment creates no new event.
5. A legacy assigned schedule with no first timestamp remains unavailable after
   reassignment. General update timestamps are not assignment evidence.
6. Offline work accepted after either deadline uses the server acceptance time.
   No claim is made that the recorded completion proves the physical work time.
7. Cancelled schedules are excluded from dashboard/NLA aggregate populations.
8. Dashboard counts and NLA source filters distinguish assignment outcomes from
   original PM timeliness. Missing required history stays visible separately.

These scenarios specify the required behavior. Execution evidence must state
which cases actually ran; an existing regression test or source inspection
alone does not establish that every new boundary case was executed.

## Sources

- [Assignment calculator](../../server/Features/Schedules/ScheduleAssignmentPeriod.cs)
- [Assignment event recorder](../../server/Features/Schedules/ScheduleAssignmentEventRecorder.cs)
- [Schedule commands and responses](../../server/Features/Schedules/SchedulesEndpoints.cs)
- [Report catalog](../../server/Features/Reports/PmAnalyticsV3Catalog.cs)
- [Dashboard calculations](../../server/Features/Reports/PmPeriodDashboardService.cs)
