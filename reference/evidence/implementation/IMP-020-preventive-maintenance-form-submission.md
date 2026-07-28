---
id: IMP-020
type: implementation
title: Preventive-maintenance form submission
status: reviewed
recordedAtUtc: 2026-07-28T00:00:00Z
sourceBranch: feat/preventive-maintenance-form-submission
evidenceLevel: source-inspected
---

# Preventive-Maintenance Form Submission

## Objective

Add the bounded backend transition from a populated `Draft` preventive-
maintenance form to `Submitted`, without publishing its inspection rows or
completing schedules.

## Source Identity

- Relevant commits:
  - `dcae3117feaecbdc1b07a04e5a0ce78b4af49161`
  - `62f10e68c4b94d0b3b3e043fc7f1e7ee26e3add8`
  - `8fd4791b08d3ad4d8bc19a0712cdf07ffcc698dd`
- Source paths:
  - `server/Features/PreventiveMaintenanceForms/PreventiveMaintenanceFormEndpoints.cs`
  - `server/Features/PreventiveMaintenanceForms/PreventiveMaintenanceFormSubmissionOptions.cs`
  - `server/Program.cs`
  - `server/appsettings.json`
  - `tests/UniPM.Api.Tests/Forms/PreventiveMaintenanceFormDraftEndpointsTests.cs`
  - `tests/UniPM.Api.Tests/Inspections/SqlServerInspectionSubmissionIntegrityTests.cs`

## Implementation Summary

- Adds `POST /api/v1/preventive-maintenance-forms/{id}/submit`.
- Requires an authenticated GSD or Inspector caller, a `Draft` form, and at
  least one inspection row.
- Allows GSD to submit any eligible draft. An Inspector may submit only a form
  they created when every row is assigned to that Inspector identity.
- Sets the form status, provisional file number, submitter, submission time,
  and update time in one database save operation.
- Uses a serializable relational transaction, the existing filtered unique
  `FileNumber` index, and bounded unique-constraint retries for provisional
  file-number allocation.
- Makes the provisional prefix and sequence width configurable through
  `PreventiveMaintenanceForms:Submission`. The final institutional file-number
  policy remains deferred.
- Caps the yearly provisional sequence at the configured width. For example, a
  four-digit sequence stops at `9999` and returns a controlled conflict rather
  than generating a five-digit value.

## Preserved Boundaries

- Submission does not complete or otherwise change preventive-maintenance
  schedules.
- Submitted forms remain immutable under the existing draft-row mutation
  checks.
- Submitted rows remain excluded from official inspection history and the
  maintenance-search projection; only acknowledgement may change that later.
- No acknowledgement command, signature validation, corrective handoff, RMRF,
  OEM retrieval, or frontend behavior is included.

## Database Changes

No migration was added. Submission uses the form fields and filtered unique
file-number index introduced by IMP-018.

## Tests Present

The focused form endpoint tests cover successful submission metadata and file
number assignment, plus empty-form, repeated-submission, and unauthorized
Inspector rejection. A native SQL Server test covers concurrent eligible form
submissions and distinct provisional file numbers.

## Verification Status

TEST-026 records the final native SQL Server test invocation for commit
`8fd4791b08d3ad4d8bc19a0712cdf07ffcc698dd`; the environment variable required
to execute the test was unavailable, so the native concurrency assertion
remains pending.

## Known Limitations

The file-number format is provisional. Submission does not yet acknowledge a
form, publish rows to official history, complete schedules, or initiate an
external corrective-action workflow.

## Related Evidence

- [IMP-018](IMP-018-preventive-maintenance-form-domain.md) - form domain
  foundation.
- [IMP-019](IMP-019-preventive-maintenance-form-drafts.md) - draft workflows.
- [TEST-026](../test-runs/TEST-026-preventive-maintenance-form-submission.md) -
  focused submission verification.
