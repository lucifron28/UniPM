---
id: IMP-037
type: implementation
title: CPMP month scheduling and GSD compliance terminology
status: reviewed
recordedAtUtc: 2026-09-30T18:25:00Z
sourceCommit: ae424403be4dcf7885bd73929184cf6d228a843b
sourceBranch: fix/cpmp-month-end-deadlines-compliance
evidenceLevel: source-inspected
---

# CPMP month scheduling and GSD compliance terminology

New schedules use the prescribed PM month rather than an exact inspection
appointment. The backend is authoritative for the four category frequencies.

| Category | Allowed months |
|---|---|
| fire-extinguisher | February, May, August, November |
| fire-alarm | June, December |
| emergency-light | June, December |
| water-drinking-station | February, May, August, November |

`CpmpScheduleFrequency` supplies the map to creation validation and the existing
asset-category reference endpoint through `scheduledMonths`. Web creation reads
that endpoint, resets the selected month on asset changes, requires a year and
allowed month, and displays a read-only derived due date.

The creation DTO accepts `PmCycle` as `yyyy-MM`. Legacy `ScheduleDate` input
still supplies a cycle in institutional fixed +08:00 time. If both are supplied,
their cycles must match. Newly created `ScheduleDate` values use the final tick
of the PM month, including leap February. Year and quarter metadata derive from
the same cycle. Existing database rows and historical reporting scope remain
unchanged. No migration or recurrence generator was added.

Schedule, inspection, dashboard and print presentation distinguish scheduled
month, due date, and actual inspection date. GSD dashboard and batch-review
labels say Compliance rate. The backend `OnTimeCompliancePercent` field and
calculation remain unchanged: on-time completion divided by all eligible,
non-cancelled schedules in the official scope. Progress remains inspected divided
by scheduled. Before the deadline compliance is null and not measurable.
Acknowledgement timestamps do not determine completion or timeliness.

The synthetic demo uses November extinguishers before their deadline, August
extinguishers with two on-time and one late completion, and June emergency
lights with one on-time, one late and one unfinished schedule. The unfinished
June asset belongs to a separate fictional department so the acknowledged
Student Affairs form contains its complete two-schedule batch. The QR sheet
reflects that department. Authentication, assignment, geolocation and form
lifecycle production behavior are unchanged.

The mobile follow-up remains a separate continuation on this branch. This
record does not claim physical-device verification or institutional validation.
The paused RAG-retirement worktree was not part of this implementation.

## Source identity

- `4705f36`: backend catalog, validation, canonical deadlines and API tests.
- `22ba2ec`: cancelled-schedule compliance denominator regression.
- `a2fc816`, `d4b1815`: CPMP demo fixture and acknowledgement consistency.
- `f8fdcd6`: live OpenAPI snapshot and generated-client synchronization.
- `8140c7e`, `80fa716`: web month selection and validated creation DTO.
- `a3eea0e`, `bdd7bcc`: reporting and inspection presentation.
- `d23f3c1`, `ae42440`: asynchronous web assertions and full dashboard coverage.
- `b6a7ca1`, `370fb85`: current workflow/demo documentation and QR sheet.

## Verification boundary

Execution results are recorded separately. Tests present cover accepted and
rejected category months, date/cycle consistency, legacy input, February leap
year, 30- and 31-day deadlines, cancelled denominators, and acknowledgement
independence. Web tests cover reference-month normalization, category changes,
read-only deadline display, separate Progress and Compliance rate, period
states, generated report rows, and export wording. No AI provider was enabled.
