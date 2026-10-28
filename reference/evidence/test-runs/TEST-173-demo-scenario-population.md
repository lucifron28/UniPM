---
id: TEST-173
type: test-run
title: Local synthetic scenario population and integrity checks
status: executed
recordedAtUtc: 2026-10-27
testedCommit: 75614cd78be61496199c8359b1d4ae79b533034c
evidenceLevel: executed-local-synthetic
---

# Local synthetic scenario population

The original checkout was used on branch feature/demo-scenario-coverage.
The fixture was applied to the local native SQL Server database with a pinned
seed date of 2026-10-27. This record establishes synthetic data coverage and
SQL integrity checks. It is not user acceptance, live DeepSeek interpretation,
browser verification or physical-device evaluation.

## Execution and correction

The first manifest initialization exposed an incorrect assumed Id column on
ScheduleEnrollmentDeferrals. Initialization rolled back without modifying
records. Its ordering was corrected to the composite AssetId/PmCycle key.
Initialization then captured the inspected synthetic database.

The applying population command created and checksum-verified a copy-only
backup before each applying run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File reference/demo/reset-rebuilt-demo.ps1 -Initialize
powershell -NoProfile -ExecutionPolicy Bypass -File reference/demo/reset-rebuilt-demo.ps1 -SeedDate 2026-10-27
sqlcmd -S localhost -E -d UniPMDb -b -W -s '|' -i artifacts/demo-rebuild/scenario-v2-verification.sql -o artifacts/demo-rebuild/scenario-v2-verification.log
```

The initial targeted verification failed referral chronology. A finding's own
completion could precede completion and acknowledgement of its whole form.
The generator was corrected to timestamp referral two days after the whole
form's latest completion. It was reapplied, and the same targeted verification
was rerun once. The rerun exited 0. No full test suite was run.

## Final observations

| Entity or scenario | Count |
| --- | ---: |
| Assets | 274 |
| Active / Inactive / Retired assets | 258 / 8 / 8 |
| Schedules | 1,584 |
| Completed / Due / Ongoing schedules | 1,286 / 258 / 40 |
| Inspection records | 1,286 |
| Draft / Submitted / Acknowledged forms | 20 / 8 / 152 |
| Acknowledged official-history inspection rows | 1,137 |
| Recorded synthetic WMS referrals | 178 |
| Acknowledged non-operational rows awaiting referral | 201 |
| Entire batches available for GSD assignment | 8 |
| Entire supervisor-only batches for Inspector assignment | 8 |
| Ongoing departments per category | 3 |
| Inspector unassignment / reassignment events | 12 / 12 |
| Mocked NotConfigured location attempt | 1 |
| Labeled synthetic photo fixture | 1 |

Ongoing schedules exist in all four categories, with 9 for emergency lights,
9 for alarms, 13 for extinguishers and 9 for water stations. Official history
contains 135, 135, 492 and 375 inspection rows respectively. Each category has
two Submitted forms, two fully completed Draft forms, and partial ongoing
Draft batches. Existing codes and department/building names were retained.
Synthetic registration dates were adjusted to precede generated history.

The assignment-period groups were 848 CompletedWithinPeriod, 438 CompletedLate,
87 Overdue, 117 Unavailable and 94 WithinPeriod at execution. There were 437
completions after the original scheduled-month deadline but within the
Inspector period. Unavailable includes unassigned and supervisor-only work.

The targeted SQL assertions passed minimum ongoing/submitted coverage,
saved-inspection/schedule completion agreement, submission and acknowledgement
chronology, referral eligibility, original registration chronology, absence
of CPMP/DEMO asset or department naming, and preservation of first assignment
timing through reassignment. The private stored photo's SHA-256 hash matched
the committed synthetic JPEG. No real GPS success was seeded.

## Raw local records and limits

Ignored artifacts under artifacts/demo-rebuild include scenario-v2-initialize.log,
scenario-v2-populate.log, scenario-v2-populate-corrected.log,
scenario-v2-verification-initial.log, scenario-v2-verification.sql and
scenario-v2-verification.log. The ignored scenario-fixture-v2.json stores full-row
fingerprints for reset ownership checks. Raw records were not committed.

The wrapper's CheckOnly mode and mutation-rejection behavior were inspected,
not separately executed in this run. No API/browser rendering, print preview,
new phrasing interpretation or actual device capture was tested. Only one
Inspector account exists, so reassignment returns work to that account after
temporary unassignment. Cancellation and enrollment deferrals remain absent.
No schema migration, credentials or manuscript evaluation results changed.
