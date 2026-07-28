---
id: IMP-019
type: implementation
title: Preventive-maintenance form draft workflows
status: reviewed
recordedAtUtc: 2026-07-28T08:07:11Z
sourceBranch: feat/preventive-maintenance-form-drafts
evidenceLevel: source-inspected
---

# Preventive-Maintenance Form Draft Workflows

## Objective

Provide the backend-only draft lifecycle for the confirmed one-page
preventive-maintenance form: create/list/get form headers and add, update, or
remove their inspection rows while the form remains `Draft`.

## Source Identity

- Relevant commit: `463d38c235d9b74833fcd9f0b679453b62d5fe9a`
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
- Draft edits do not complete schedules and do not create maintenance search
  documents.
- Official inspection history/list/detail reads exclude rows whose linked form
  remains `Draft`.
- The maintenance-search projection rebuild likewise excludes draft rows, so a
  rebuild cannot turn a draft finding into RAG evidence.
- The routes do not implement submission, file-number generation,
  acknowledgement, signature validation, schedule completion, handoff, RMRF,
  OEM retrieval, or frontend behavior.

## Database Changes

No migration was added. The workflow uses the form-to-inspection relationship
and one-inspection-per-schedule constraint introduced by IMP-018.

## Tests Present

The focused endpoint tests cover multiple draft rows, duplicate and category
mismatch rejection, submitted/acknowledged immutability, and official-history
plus search-projection exclusion.

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
