---
id: TEST-170
type: test-run
title: Guarded local rebuilt demo reset
status: executed
recordedAtUtc: 2026-10-27T07:23:04Z
testedCommit: 107306519328bd66de42f6fa969f23ecfb5d6568
sourceBranch: feature/dashboard-chart-overview
evidenceLevel: locally-executed
---

# Guarded local rebuilt demo reset

The owner requested a reset workflow for the rebuilt synthetic local dataset.
The legacy demo reset uses different fixture identities and remains separate.
The new local workflow captures a manifest of fixture IDs and refuses a reset
if those IDs change or protected attachments and workflow records exist. Its
reset SQL runs in one transaction and holds table locks during the ID checks.
The generated dates, inspections, and assignment events are fictional.

The final script was exercised on the working source later committed as
`107306519328bd66de42f6fa969f23ecfb5d6568`, using Windows PowerShell and the
native local SQL Server database. Existing unrelated dashboard edits were
present in the working tree but were not used by the reset script.

- Manifest initialization succeeded for the current synthetic fixture. Its
  ignored local manifest contains record IDs and is not committed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File reference/demo/reset-rebuilt-demo.ps1 -CheckOnly`
  passed, exit 0. It ran the guarded rebuild inside a transaction, captured
  the rebuilt record IDs, and rolled back. The command was run again after an
  in-scope correction to capture new IDs before the transaction ends; it passed.
- Read-only counts after the check were 135 assets, 828 schedules, and 422
  inspections. The check did not commit a replacement dataset.
- The initial manifest attempt identified an array-handling defect and the
  following attempt exposed tables with non-Id primary keys. Both were fixed;
  sensitive attachment tables are now required to be empty, not inferred as
  fixture-owned. The final initialization succeeded.

The guard against unrelated IDs and attachments was source-inspected; an
actual contamination or committed reset was not executed. The legacy reset
command was not changed. The manifest is local-only and the workflow targets
only the specified Windows-authenticated local database. No full test suite,
API, physical device or live institutional workflow was run.
