---
id: TEST-129
type: test-run
title: Library Complex and synthetic history extension through 2024
status: executed
recordedAtUtc: 2026-10-21T03:14:40Z
testedCommit: f0e31fc44d43982e4573a80f0b8ff52dbc192920
sourceBranch: feature/gsd-library-history
evidenceLevel: locally-executed
---

# Library Complex and synthetic history extension through 2024

The owner corrected the Library building name to Library Complex, identified
General Services Department, Procurement, and ICTD within it, and requested
history back to 2024. These are owner-provided campus labels. Equipment,
placements, findings, signatories, signatures, and WMS references remain
synthetic. No new campus floor plan or official inventory is claimed.

## Source change and focused checks

The fixture adds four assets per category to each new office, 48 assets total.
The original 200 asset IDs/codes and original 2026 schedule, inspection, form,
acknowledgement, and referral identities remain stable. The generator processes
the original completed batches first so ordinal-based outcomes and statuses do
not change. History follows the unchanged approved CPMP months for 2024, 2025,
and 2026. Synthetic asset registration dates now start in January 2024 so the
older cycles fall after registration; this is fixture metadata, not a runtime
backdating or schedule-generation workaround.

Executed this focused Release filter outside the sandbox:

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~GsdValidation -m:1 -nodeReuse:false
```

Two tests passed, zero failed or skipped. The executor ran the same filter
again after completing additional assertions; that run also passed 2/2. Logs
are in ignored `artifacts/gsd-validation-focused-runs/attempt-06.txt` and
`attempt-07.txt`. No full local suite, Web, Flutter, or Playwright suite ran.
The test includes counts, years, CPMP months, office mapping, unique IDs/QRs,
original live groups, and existing synthetic referral scenarios.

An ignored .NET operator helper compiled successfully and compared every
original fixture's scalar values with the exact deployed baseline assembly
from `f78d7216f869fb27507f57d2089e3ff5823990fa`. Comparison passed with only
the intended Library building labels and asset registration-date changes
excluded. Original update timestamps, findings, statuses, codes, and referrals
remained unchanged. Checks ran with unrelated local Web/NLA edits present;
those edits were excluded from the source commit. This is not clean-tree or
exact-head CI evidence.

## Live additive update

The active Azure validation database passed exact-target, TLS, engine edition
5, compatibility 150, 23-migration, original-identity, and population checks.
The first read-only preflight found a cross-assembly cast error in the operator
helper and performed no writes. A helper-only correction used reflection for
the baseline objects; the corrected preflight passed.

The operator applied the following additions in one serializable transaction:

| Record | Added | Final |
| --- | ---: | ---: |
| Assets | 48 | 248 |
| Schedules | 1,732 | 2,232 |
| Inspections | 1,684 | 1,932 |
| Forms | 200 | 220 |
| Acknowledgements | 100 | 110 |
| WMS referrals | 88 | 103 |
| WMS referral audits | 88 | 103 |

Only owned Library asset/form building labels and the original synthetic asset
registration dates were updated. All other original business scalar values
were protected by before/after hashes inside the transaction. The normalized
preservation digest matched before commit:
`0FCC16A172BC859D1D73C1CA7EA2EB8740AA47E4998F7400C289A71A751F4801`.
The normalization excludes only the approved fields and the rowversion on
Library forms changed by that update. The helper returned a `COMMITTED`
receipt. It did not delete or reset records, change credentials, apply
migrations, or rerun the fresh-target initializer.

Independent read-only postflight confirmed all final counts, four accounts,
23 migrations, zero remaining `University Library` building labels, and these
Library Complex populations:

| Department | Assets |
| --- | ---: |
| Library | 50 |
| General Services Department | 16 |
| Procurement | 16 |
| ICTD | 16 |

There are 744 schedules for each year, 2024, 2025, and 2026. All four original
live groups still contain 13 Due schedules with no Supervisor/Inspector
assignment and no inspection: FE/CCMS/August 2026, WDS/Administration/August
2026, FA/Library/June 2026, and EL/CNAHS/June 2026.

The existing least-privilege runtime connection was handled in process memory
only. The task's temporary exact-IP SQL firewall rule was removed. No SQL
administrator password, new account, secret file, provider call, database
replacement, Blob modification, or application redeployment was required.
The running application reads the updated data directly. Raw helper source,
comparison, preflight, commit receipt, and postflight are retained in ignored
`artifacts/library-history-update/`.

These checks establish synthetic data consistency and the live SQL update.
They do not establish browser UX, physical-device or GSD acceptance, AI
interpretation quality, or correction of the earlier Web CI failures.
