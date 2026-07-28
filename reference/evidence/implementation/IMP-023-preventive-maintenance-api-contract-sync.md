---
id: IMP-023
type: implementation
title: Preventive-maintenance API contract synchronization
status: reviewed
recordedAtUtc: 2026-07-28T16:09:24Z
sourceBranch: chore/preventive-maintenance-api-contract-sync
evidenceLevel: source-inspected
testedCommit: 2cd1bf7181b7c68ad6ddf7066a2351e03021b97f
---

# Preventive-Maintenance API Contract Synchronization

## Objective

Synchronize the committed OpenAPI v1 snapshot and generated Orval client with
the preventive-maintenance form workflows merged through PR #45.

## Source Identity

- Tested implementation commit:
  `2cd1bf7181b7c68ad6ddf7066a2351e03021b97f`
- Source branch: `chore/preventive-maintenance-api-contract-sync`
- Starting main commit after PR #45:
  `bc0fe8243ec38a7ab56ba013324838db7dbb4287`
- Merged source PR: `#45`

## Contract Coverage

The live OpenAPI document and generated client include these stable operation
IDs:

- `CreatePreventiveMaintenanceFormDraft`
- `ListPreventiveMaintenanceForms`
- `GetPreventiveMaintenanceForm`
- `SubmitPreventiveMaintenanceForm`
- `AcknowledgePreventiveMaintenanceForm`
- `GetCorrectiveMaintenanceHandoff`
- `AddPreventiveMaintenanceFormDraftInspection`
- `UpdatePreventiveMaintenanceFormDraftInspection`
- `DeletePreventiveMaintenanceFormDraftInspection`

The generated models include draft form, inspection-row, submission,
acknowledgement, and corrective-handoff contracts. `AssetCode` remains a
string, and `AssetDeviceNumber` remains nullable. Signature inputs remain on
the acknowledgement request; corrective-handoff responses do not expose
signature data or checksum fields.

## Scope Boundary

This change updates only `web/openapi/unipm-v1.json` and Orval-generated
TypeScript files. It adds no React screens, routes, forms, queries, or
mutations, and does not change backend behavior, database schema, migrations,
RMRF, WMS, OEM retrieval, embeddings, or generation.

## Related Evidence

- [TEST-029](../test-runs/TEST-029-preventive-maintenance-api-contract-sync.md)
  - live refresh and generated-client verification.
