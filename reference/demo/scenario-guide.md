# Local scenario fixture v2

All records are synthetic. The pinned fixture date is October 27, 2026.
The existing Inspector, Supervisor and GSD accounts own the corresponding work.
No accounts or passwords are added by the population script.

## Where to demonstrate each workflow

| Screen | Select | Available scenario |
| --- | --- | --- |
| GSD Planning and assignment | November 2026 for extinguishers/water stations; December 2026 for alarms/emergency lights; Administration or Campus Facilities | Entire batches awaiting Supervisor assignment |
| Supervisor assignment | Same future cycles; CCMS or Food Services | Supervisor assigned, Inspector not yet assigned |
| Inspector assigned work | Same future cycles; Laboratory Services, Library, Registrar, Sports Office or Student Affairs Office | Assigned Due work ready to start |
| Work tracking | August 2026 for extinguishers/water stations; June 2026 for alarms/emergency lights | Administration, Campus Facilities and CCMS have partial completion and Ongoing work |
| Draft forms ready to submit | February 2025 for extinguishers/water stations; June 2025 for alarms/emergency lights | Administration and Campus Facilities have completed rows in Draft forms |
| Form review and acknowledgement | August 2026 for extinguishers/water stations; June 2026 for alarms/emergency lights | Food Services and Laboratory Services have Submitted forms |
| Official inspection history | Completed 2025 and 2026 cycles; each category | Acknowledged operational and non-operational inspections |
| Corrective follow-up | An acknowledged cycle, such as June 2025 alarms or August 2025 extinguishers | Non-operational findings awaiting referral and findings with synthetic external WMS references |
| Asset registry | Registration status Inactive or Retired | Two assets of each status per category, with no schedules |
| Draft inspection photo | Administration, emergency lights, June 2025; first asset by code | A labeled synthetic JPEG; no real inspection photograph |

The GSD assignment action consumes a reserved batch. Supervisor assignment,
starting work, submitting a form and acknowledging it likewise change the
fixture. Leave a second batch untouched until the next demonstration step.

## NLA query examples

These are examples for the supported PM execution and inventory subjects.
Review the interpreted scope before execution. This guide does not claim that
every wording has been tested against DeepSeek or the deterministic parser.

| Question | Intended scope and result |
| --- | --- |
| Which assets have ongoing inspections in 2026? | PM execution, schedule status Ongoing; source rows in all four categories |
| Show emergency-light inspection progress by department in June 2026. | PM execution, progress by department; Administration, Campus Facilities and CCMS have partial completion |
| Show missed fire-extinguisher inspections by department in August 2026. | Unfinished schedules after the scheduled month; includes ongoing work |
| Show inspections completed late in 2025 by category. | Completion after the original cycle deadline; source rows in each category |
| Show non-operational findings in 2025 by department. | Recorded inspection condition; verify form state separately for official-history use |
| Show overdue Inspector assignments in 2026. | Assignment-period Overdue, separate from original PM month compliance |
| Show inspections completed within the Inspector assignment period in 2026. | CompletedWithinPeriod; includes examples completed after the scheduled month |
| Show Submitted fire-alarm forms in June 2026. | PM execution rows filtered to form status Submitted; Food Services and Laboratory Services |
| List inactive assets by category. | Asset inventory, registration status Inactive; two per category |
| List retired assets by category. | Asset inventory, registration status Retired; two per category |

For exact results use the category and cycle above rather than an unspecified
date range. Current inventory uses one asset per row. PM execution uses one
asset-cycle schedule per row. Repeated assets across cycles are intentional.
Source rows and printable reports must use the same scope as the totals.

## Timing and lifecycle rules

Each assigned Inspector has 30 calendar days from the first saved assignment.
Recent unfinished work uses assignments ten days before the fixture date;
overdue unfinished work uses assignments forty days before it. Completed
history mixes original-month on-time completion, original-month late completion
within the assignment period, and completion after both deadlines.

There is only one existing Inspector account. Reassignment history therefore
demonstrates temporary unassignment followed by reassignment to that account.
The first assignment timestamp and due date remain unchanged. It does not
simulate handoff between two different people.

Starting work leaves the schedule Ongoing. Saving its completed inspection row
marks the schedule Completed while the form can remain Draft. Only fully
completed batches are Submitted or Acknowledged in this fixture. Official
inspection history excludes Draft and Submitted forms. Referral records only
refer to acknowledged non-operational rows and do not claim external repairs.

Location data includes a mocked, NotConfigured attempt, not a successful real
GPS check. Real location capture and physical-device acceptance still require
device testing. Cancellation and enrollment deferrals are not populated.

Reset protection compares full row content, not just IDs. After any hands-on
change the reset deliberately stops. Review and back up changed records before
adopting a new baseline; do not delete the manifest to bypass protection.
