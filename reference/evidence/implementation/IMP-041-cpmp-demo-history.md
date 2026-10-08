---
id: IMP-041
type: implementation
title: CPMP-aligned fictional demo history and legacy completion repair
status: implemented
recordedOn: 2026-10-08
sourceBranch: feature/cpmp-demo-history
sourceCommit: 2283e1736d9ddf7dee8f396199fc5e437b285467
evidenceLevel: locally-executed
---

# CPMP demo history

The owner requested immediate demo-data correction and explicitly prohibited
tests. No tests, full suites, provider calls, or CI runs were performed.

Changes:

- Synthetic seeding writes inspection completion/start timestamps, rather than
  only schedule completion timestamps.
- `--seed-demo` adds a distinct fictional CPMP history dataset for 2025/2026,
  covering all four categories and two clearly labelled demo departments.
- Every new schedule uses a CPMP-valid cycle and Manila month-end deadline.
  Past batches include completed/on-time/late, Submitted, Acknowledged, and
  partially completed Draft scenarios. Future cycles remain uninspected.
- Existing immutable fixture IDs are repaired in the local database: missing
  inspection completion timestamps, month-end deadline representation, and the
  two invalid FE months moved to the previous eligible month. Inspection dates
  remain unchanged, so those examples become late inspections.
- The original JSON, frozen benchmark datasets, runtime calculation rules,
  domain models, and migrations were not edited. Unrelated records are outside
  the legacy repair's ID set.
- Signatory capture uses the Inspector ID, without creating department-head
  accounts. Signatures are synthetic placeholders, not real signatures.

Execution:

1. Release application compilation succeeded with one existing nullable warning
   at `PreventiveMaintenanceFormEndpoints.cs:442`.
2. The first seed attempt at `b275c59185799c009e667dc85216f300ab88beb7`
   failed on canonical form-batch uniqueness for Library/FE/2026-08. Its data
   transaction rolled back. A focused correction isolates the new history in
   Demo CCMS/Demo Library, preserving the existing submitted demo batch.
3. Release compilation and the seed command succeeded at the source commit
   above. The command reported 25 assets, 105 schedules, 77 inspections,
   42 forms, and 29 acknowledgements for the combined demo datasets. These
   counts exclude existing legacy-fixture rows and other local records.
4. The existing API was still listening on port 5254. It was not restarted.

Raw logs are ignored under `artifacts/cpmp-demo-history/`. The command read only
the required backend settings from the existing ignored environment file.
Credentials and provider keys were not printed or committed.

Not verified: browser/mobile walkthrough, database-wide count reconciliation,
repeat seed/reset behavior, physical GPS/QR, user acceptance, remote CI, or
deployment. User verification follows. No merge or remote publication occurred.
