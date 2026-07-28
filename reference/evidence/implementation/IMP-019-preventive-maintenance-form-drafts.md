---
id: IMP-019
type: implementation
title: Preventive-maintenance form draft workflows
status: reviewed
recordedAtUtc: 2026-07-28T10:48:56Z
sourceBranch: feat/preventive-maintenance-form-drafts
evidenceLevel: source-inspected
---

# Preventive-Maintenance Form Draft Workflows

## Objective

Provide the backend-only draft lifecycle for the confirmed one-page
preventive-maintenance form: create/list/get form headers and add, update, or
remove their inspection rows while the form remains `Draft`.

## Source Identity

- Relevant commits:
  - `463d38c235d9b74833fcd9f0b679453b62d5fe9a`
  - `01e3370508496eeb7cd3963c5ba2ee5aa24b7fd5`
  - `51ac33ac9019c5a8b8f657e860aae5c865dc1fa8`
- Implementation date: 2026-07-28 UTC
- Source paths:
  - `server/Features/PreventiveMaintenanceForms/PreventiveMaintenanceFormEndpoints.cs`
  - `server/Features/Inspections/InspectionsEndpoints.cs`
  - `server/Retrieval/MaintenanceSearchDocumentProjector.cs`
  - `tests/UniPM.Api.Tests/Forms/PreventiveMaintenanceFormDraftEndpointsTests.cs`

## Implementation Summary

- Adds create, list, and detail routes for preventive-maintenance forms.
- Adds draft-only add, update, and delete routes for inspection rows.
- Requires a schedule to exist, match the form asset category, and remain
  unused by any other inspection record.
- Keeps the existing one-inspection-per-schedule database contract intact.
- Blocks row mutations after the form leaves `Draft`.

## Architecture And Contracts

- Draft rows use the existing `InspectionRecord` entity through its nullable
  `PreventiveMaintenanceFormId` link.
- Form creation and draft-row mutations require the dedicated GSD/Inspector
  policy; form reads require authentication. Inspector callers may only create,
  update, or delete rows assigned to their authenticated identity.
- Draft edits do not complete schedules and do not create maintenance search
  documents.
- Official inspection history/list/detail reads include only legacy rows or
  rows whose linked form is `Acknowledged`; `Draft` and `Submitted` rows remain
  hidden.
- The maintenance-search projection rebuild follows the same rule, so a
  rebuild cannot turn Draft or Submitted findings into RAG evidence.
- The routes do not implement submission, file-number generation,
  acknowledgement, signature validation, schedule completion, handoff, RMRF,
  OEM retrieval, or frontend behavior.

## Database Changes

No migration was added. The workflow uses the form-to-inspection relationship
and one-inspection-per-schedule constraint introduced by IMP-018.

## Tests Present

The focused endpoint tests cover multiple draft rows, duplicate and category
mismatch rejection, submitted/acknowledged immutability, GSD/Inspector access
and Inspector ownership, and official-history plus search-projection exclusion.

## Verification Status

TEST-025 records the Release build and focused local endpoint execution for
the implementation commit.

## Known Limitations

This is a draft-only foundation. Form submission, form file numbering,
acknowledgement capture, schedule completion, corrective-action handoff, and
final institutional authority rules remain deferred.

## Related Evidence

- [IMP-018](IMP-018-preventive-maintenance-form-domain.md) - form domain
  foundation.
- [TEST-024](../test-runs/TEST-024-preventive-maintenance-form-domain-verification.md) -
  domain and migration verification.
- [TEST-025](../test-runs/TEST-025-preventive-maintenance-form-drafts.md) -
  focused draft workflow verification.
