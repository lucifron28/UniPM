---
id: IMP-033
type: implementation
title: Flutter mobile confirmed GSD preventive-maintenance forms
status: reviewed
recordedAtUtc: 2026-09-07T15:15:50Z
sourceBranch: feature/mobile-confirmed-pm-forms
evidenceLevel: locally-executed
---

# Flutter mobile confirmed GSD preventive-maintenance forms

## Objective

Implement the confirmed core mobile preventive-maintenance workflow for the
four supplied General Services Department forms without making document-control
Page 2 notation a blocker and without adding unapproved downstream features.

The preserved workflow is:

`QR -> asset -> schedule -> category form -> multi-row Draft -> review -> submit -> existing web acknowledgement -> schedule completion -> official history`

## Source Identity

- Relevant commit: `5a1c5b8b5c5ed68a270c7b2178aca96c5963d5f6`
- Implementation date: 2026-09-07
- Source branch: `feature/mobile-confirmed-pm-forms`
- Source paths:
  - `mobile/lib/features/preventive_maintenance/preventive_maintenance_form_specs.dart`
  - `mobile/lib/features/preventive_maintenance/preventive_maintenance_models.dart`
  - `mobile/lib/features/preventive_maintenance/preventive_maintenance_repository.dart`
  - `mobile/lib/features/preventive_maintenance/preventive_maintenance_page.dart`
  - `mobile/lib/features/maintenance_history/asset_maintenance_history_models.dart`
  - `mobile/lib/features/maintenance_history/asset_maintenance_history_page.dart`
  - `server/Models/InspectionRecord.cs`
  - `server/Features/PreventiveMaintenanceForms/PreventiveMaintenanceFormEndpoints.cs`
  - `server/Features/Inspections/InspectionsEndpoints.cs`
  - `server/Data/ApplicationDbContext.cs`
  - `server/Migrations/20260907150158_AddWaterStationInspectionWorkItems.cs`

The four user-supplied GSD form images were inspected as authoritative source
material. The photos are not committed and no personal or institutional source
records were copied into this evidence record.

## Implementation Summary

- Added explicit mobile form specifications for Fire Extinguishers, Fire
  Alarms, Emergency Lights, and Water Drinking Stations, including the visible
  form title, revision, effectivity date, and Water work items.
- Kept asset number, location, building/department, and category authoritative
  from the selected UniPM asset/schedule instead of asking the worker to
  re-enter them.
- Added conservative inspection-time fields: operational status, inspection
  date, remarks, actions/recommendations through the existing contract, and
  Water Drinking Station accomplishment date/work checks.
- Preserved multi-row Draft editing, review, submission, existing web
  acknowledgement, schedule completion, and acknowledged-only official
  history.
- Displayed the new optional Water fields in official asset history.

## Architecture And Contracts

- Obvious operational fields remain boolean; obvious dates remain date values;
  remarks and recommendations remain text.
- No new requiredness, measurement range, controlled vocabulary, or category
  business rule was invented.
- Water-only fields are normalized to `null` for non-Water forms.
- Water `RMRF No.` is not part of this mobile PM boundary; UniPM does not issue
  or process RMRFs.
- `Inspected by` continues to be represented by the authenticated Inspector/GSD
  worker. Existing acknowledgement/signatory behavior is preserved; no new
  mobile acknowledgement implementation was added in this branch.
- `Noted by` is not a new required mobile input.
- The `Page 1 of 2` notation on the supplied forms was treated as
  document-control context, not as a missing-content blocker.

## Important Files

- Mobile form specifications, row models, repository serialization, and UI:
  `mobile/lib/features/preventive_maintenance/`
- Official history parsing and display:
  `mobile/lib/features/maintenance_history/`
- Preventive-maintenance form and official-history API response extensions:
  `server/Features/PreventiveMaintenanceForms/PreventiveMaintenanceFormEndpoints.cs`
  and `server/Features/Inspections/InspectionsEndpoints.cs`
- Persistence model and migration:
  `server/Models/InspectionRecord.cs`,
  `server/Data/ApplicationDbContext.cs`, and
  `server/Migrations/20260907150158_AddWaterStationInspectionWorkItems.cs`

## Database Changes

Added nullable inspection columns for the visible Water form data:

- `DateAccomplished`
- `WaterReplaceCarbonFilter`
- `WaterReplaceSedimentFilter`
- `WaterCheckUvLight`

All are nullable so existing records and non-Water categories remain compatible.

## Tests Present

- Category specification and Water field widget coverage in
  `mobile/test/preventive_maintenance_form_draft_test.dart`.
- Four-category API contract and Water-only normalization coverage in
  `tests/UniPM.Api.Tests/Forms/PreventiveMaintenanceFormDraftEndpointsTests.cs`.

## Verification Status

Post-commit verification was run against commit
`5a1c5b8b5c5ed68a270c7b2178aca96c5963d5f6`:

- Flutter formatter check: 40 files checked, no changes.
- Flutter analyzer: no issues found.
- Focused mobile PM tests: 36 passed.
- Full mobile tests: 88 passed.
- Focused PM endpoint tests: 18 passed.
- Full backend tests: 309 passed, 41 skipped, 0 failed.
- Solution build: 0 errors; one pre-existing nullable-navigation warning in
  corrective-action handoff code remains.

## Known Limitations

- Physical-device, live-backend, release-signing, and production deployment
  verification remain separate validation work.
- Offline synchronization remains deferred and no offline persistence or sync
  was added.
- No attachments, alerts, AI/RAG, WMS/RPA, or RMRF processing was added.
- Existing mobile acknowledgement from the earlier workflow remains available;
  this branch did not add or change it.
- Asset metadata such as installation date, type, capacity, or expiration date
  is shown only when the existing backend asset contract provides it; no new
  worker re-entry fields were invented for unavailable metadata.
- Historical form-version preservation and stronger versioned-form architecture
  remain future work.

## Related Evidence

- `IMP-026` / `TEST-032`: initial mobile preventive-maintenance Draft workflow.
- `IMP-028` / `TEST-034`: official asset maintenance history.
- `IMP-029` / `TEST-035`: existing mobile acknowledgement workflow.
- `IMP-032` / `TEST-038`: Android debug cleartext overlay correction.
- `TEST-039`: verification record for this implementation.
