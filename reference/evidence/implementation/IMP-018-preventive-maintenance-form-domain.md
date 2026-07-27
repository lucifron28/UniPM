---
id: IMP-018
type: implementation
title: Preventive-maintenance form domain foundation
status: reviewed
recordedAtUtc: 2026-07-27T10:10:24Z
sourceBranch: refactor/preventive-maintenance-form-domain
evidenceLevel: source-inspected
sourceCommit: b6ddd87dd5f2ed6cc0d977220430be76e8f47aed
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

The Release build completed with 0 warnings and 0 errors. The full solution
test run passed 302 tests with 0 failures and skipped 40 optional SQL Server,
provider, and integration tests because the required process-scoped test
connections/provider configuration were unavailable.

The SQL Server 2019 tests for form constraints and migration preservation are
present but were not executed in this run. This record therefore does not
claim SQL migration execution or production readiness.

## Boundaries

No form submission or acknowledgement commands, file-number generation,
schedule completion behavior, handoff/RMRF workflow, OEM retrieval, retrieval
indexing of acknowledgement/signature fields, frontend work, or final GSD
workflow rules are included.
