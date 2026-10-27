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
| Form review and acknowledgement | August 2026 for extinguishers/water stations; June 2026 for alarms/emergency lights | Food Services and Laboratory Services have Submitted forms awaiting the concerned Department Head's acknowledgement through the Inspector mobile session |
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
| List PM schedule records for all supported categories with schedule status Ongoing in scheduled PM-cycle year 2026. | PM execution, all four supported categories, Ongoing schedule status; one row per asset-cycle schedule |
| Show emergency-light progress by department for scheduled PM cycle June 2026. | PM execution, emergency lights; Administration, Campus Facilities and CCMS have partial completion |
| Show missed fire-extinguisher counts by department for scheduled PM cycle August 2026. | PM execution, fire extinguishers; unfinished schedules past their PM-cycle deadlines, including ongoing work |
| Show completed late counts by category for all supported categories in scheduled PM-cycle year 2025. | PM execution, all four supported categories; completions after the original PM-cycle deadlines |
| Show non-operational finding counts by department for all supported categories in scheduled PM-cycle year 2025. | PM execution, all four supported categories; recorded inspection condition. Check form status separately when confirming official history |
| Show overdue Inspector assignment counts by department for all supported categories in scheduled PM-cycle year 2026. | PM execution, all four supported categories; overdue uses the original Inspector assignment period, separate from the PM-cycle deadline |
| Show counts completed within the Inspector assignment period by category for all supported categories in scheduled PM-cycle year 2026. | PM execution, all four supported categories; completed within the original 30-day Inspector period, including examples completed after the scheduled month |
| List fire-alarm schedule records with form status Submitted for scheduled PM cycle June 2026. | PM execution, fire alarms; Food Services and Laboratory Services have Submitted forms |
| Count inactive assets by category in the current asset inventory, across all supported categories. | Current registered asset inventory, all four supported categories; two Inactive assets per category |
| Count retired assets by category in the current asset inventory, across all supported categories. | Current registered asset inventory, all four supported categories; two Retired assets per category |

Do not demonstrate annual on-time compliance for 2026 while that cycle year includes active or future cycles. Use progress for an open period, or use a closed cycle year such as 2025 for an annual compliance report.

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
The Inspector records the concerned Department Head's acknowledgement and
signatory details in the authenticated mobile session.

Location data includes a mocked, NotConfigured attempt, not a successful real
GPS check. Real location capture and physical-device acceptance still require
device testing. Cancellation and enrollment deferrals are not populated.

Reset protection compares full row content, not just IDs. After any hands-on
change the reset deliberately stops. Review and back up changed records before
adopting a new baseline; do not delete the manifest to bypass protection.
For a repeatable return to the captured demo fixture, follow [Restore the starting point after practice](README.md#restore-the-starting-point-after-practice) and use the baseline script from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File reference/demo/demo-baseline.ps1 -Mode Capture
powershell -NoProfile -ExecutionPolicy Bypass -File reference/demo/demo-baseline.ps1 -Mode Verify
powershell -NoProfile -ExecutionPolicy Bypass -File reference/demo/demo-baseline.ps1 -Mode Restore -TargetDatabase UniPMDb
```
