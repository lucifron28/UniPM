---
id: IMP-018
type: implementation
title: Preventive-maintenance form domain foundation
status: reviewed
recordedAtUtc: 2026-07-27T10:37:10Z
sourceBranch: refactor/preventive-maintenance-form-domain
evidenceLevel: source-inspected
sourceCommit: 5152c04
---

# Preventive-Maintenance Form Domain Foundation

## Objective

Represent the confirmed one-page preventive-maintenance form as a persisted
form header with multiple existing inspection rows, without implementing form
submission or acknowledgement commands.

## Implementation Summary

`PreventiveMaintenanceForm` stores the confirmed form metadata, controlled
status, creator/submission references, timestamps, and an EF concurrency token.
`InspectionRecord.PreventiveMaintenanceFormId` is nullable so existing
inspection history remains valid and can be associated with a form later.
`PreventiveMaintenanceAcknowledgement` is a one-to-one persistence record with
one acknowledgement per form and bounded signature metadata. Signature data is
not projected into retrieval documents or embeddings.

The migration adds only the nullable inspection link and the two new tables.
The existing one-inspection-per-schedule unique constraint is unchanged. A
filtered unique index protects non-null file numbers, while check constraints
protect the three supported form statuses and the configured domain catalogs.

## Important Paths

- `server/Models/PreventiveMaintenanceForm.cs`
- `server/Models/PreventiveMaintenanceAcknowledgement.cs`
- `server/Models/InspectionRecord.cs`
- `server/Data/ApplicationDbContext.cs`
- `server/Migrations/20260727100331_AddPreventiveMaintenanceFormDomain.cs`
- `tests/UniPM.Api.Tests/Forms/PreventiveMaintenanceFormDomainTests.cs`
- `tests/UniPM.Api.Tests/Infrastructure/SqlServerDomainContractTests.cs`

## Verification Status

The final Release build completed with 0 warnings and 0 errors. With the
process-scoped native SQL Server 2019 connection configured, the full solution
test run passed 341 tests with 0 failures and 1 skipped optional provider smoke
test. The form constraint and migration-preservation tests executed and
passed, including preservation of existing inspections with a null form link.

This record does not claim production readiness. SQL Server verification used
the local native development instance and did not include real institutional
data.

## Boundaries

No form submission or acknowledgement commands, file-number generation,
schedule completion behavior, handoff/RMRF workflow, OEM retrieval, retrieval
indexing of acknowledgement/signature fields, frontend work, or final GSD
workflow rules are included.
