---
id: IMP-022
type: implementation
title: Corrective-action handoff preparation read model
status: reviewed
recordedAtUtc: 2026-07-28T15:44:36Z
sourceBranch: feat/corrective-action-handoff-read-model
evidenceLevel: source-inspected
testedCommit: fb26a0b23b9b8ec1d6f2f3fe4d42353be25605cd
---

# Corrective-Action Handoff Preparation Read Model

## Objective

Provide a GSD-only, read-only preparation view for corrective follow-up from
an acknowledged preventive-maintenance form. This phase does not create or
track a handoff record.

## Source Identity

- Tested implementation commit:
  `fb26a0b23b9b8ec1d6f2f3fe4d42353be25605cd`
- Source branch: `feat/corrective-action-handoff-read-model`
- Source paths:
  - `server/Features/Auth/AuthPolicyCatalog.cs`
  - `server/Features/Auth/AuthServiceCollectionExtensions.cs`
  - `server/Features/PreventiveMaintenanceForms/PreventiveMaintenanceFormEndpoints.cs`
  - `tests/UniPM.Api.Tests/Forms/PreventiveMaintenanceFormDraftEndpointsTests.cs`

## Implementation Summary

- Adds `GET /api/v1/preventive-maintenance-forms/{id}/corrective-handoff`.
- Requires the dedicated GSD-only corrective-handoff policy.
- Returns data only for an `Acknowledged` form with acknowledgement metadata.
- Builds the response from the form, acknowledged inspection rows, assets, and
  inspector user records without adding a persistence model.
- Includes only rows with a recommended corrective action.
- Includes the form file number, form metadata, acknowledgement date, source
  inspection details, operational status, recommendation, and skilled-worker
  identity.
- Reports `HasCorrectiveActionRows` for the acknowledged form.
- Reads acknowledgement date only; signature data and checksum are never
  returned.
- Keeps `AssetCode` unchanged and returns a nullable `AssetDeviceNumber`.
  `AssetDeviceNumber` is `null` until an actual institutional device-number
  source or approved mapping is confirmed.

## Boundary

No table, migration, persisted handoff status, export file, WMS integration,
RMRF processing, approval workflow, OEM retrieval, frontend work, embedding,
or summary generation was added. The response is not an RMRF and does not
create, process, approve, or monitor one.

## Verification Status

TEST-028 records the two focused endpoint tests passing on the tested commit
and verifies the unresolved device-number value is nullable.
Native SQL Server 2019 verification was not run for this phase.

## Known Limitations

The read model prepares data for later human-led corrective follow-up. It does
not establish final institutional handoff tracking, WMS lifecycle behavior,
RMRF rules, export requirements, or a separate asset device-number contract.

## Related Evidence

- [IMP-021](IMP-021-preventive-maintenance-form-acknowledgement.md) - form
  acknowledgement workflow.
- [IMP-019](IMP-019-preventive-maintenance-form-drafts.md) - draft form
  workflows and publication boundary.
- [TEST-028](../test-runs/TEST-028-corrective-action-handoff-read-model.md) -
  focused verification.
