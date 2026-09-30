---
id: IMP-035
type: implementation
title: Pre-acceptance integration hardening
status: reviewed
recordedAtUtc: 2026-09-30T01:49:49Z
sourceBranch: feature/pre-acceptance-integration-hardening
evidenceLevel: source-inspected
---

# Pre-acceptance integration hardening

## Objective

Correct the 12 confirmed issues in the pre-acceptance integration checklist
before physical/manual testing. The user-provided checklist is authoritative
for workflow semantics: saving an inspection completes its schedule;
acknowledgement records receipt/noting only and does not complete schedules.

## Source identity

- Final web/source commit: `e301a91cf4ffaa6bdc13812f52141b4891c657af`.
- Backend API tests, backend build, and mobile tests ran at
  `fabb49a05f8947c6eabb5836b0647e1bf46c2960`.
- Backend implementation commits: `46dbf5bb73df6df593190c4b3525162b37ed7e9c`,
  `2ace3f4bfb1ec8dfff3e2d57d57882aac906ffd8`.
- Mobile implementation commit: `fabb49a05f8947c6eabb5836b0647e1bf46c2960`.
- Relevant web implementation/contract commits included in final verification:
  `2ef669d186238b7514ad80a7d29c517ddec527b3`,
  `ddbd73dfa10f05d6bea56eca870ac3a1fa50925e`,
  `478f3b7b157748f1fd0252c7fb941fc58ef34fd3`, and
  `e301a91cf4ffaa6bdc13812f52141b4891c657af`.
- Mobile format/analyzer checks ran at
  `e301a91cf4ffaa6bdc13812f52141b4891c657af`. Their recorded mobile tree
  `9d94627a519f650cd9e8a2909ffbb2c26fc60ef3` matches the tree at the tested
  mobile commit above.

## Implementation summary

1. Mobile schedule resolution permits a Completed schedule with an existing
   Draft row to resume that row. Completed schedules without a Draft row cannot
   start another inspection, preserving one inspection per schedule. Coverage:
   `mobile/test/batch_progress_calculation_test.dart` and
   `mobile/test/preventive_maintenance_form_draft_test.dart`.
2. Draft update omits `InspectorUserId`; the backend authorizes against the
   stored performer. At create time, an Inspector's supplied
   `InspectorUserId` must match the authenticated ID. GSD follows the existing
   permitted inspector-selection flow. Coverage:
   `tests/UniPM.Api.Tests/Forms/PreventiveMaintenanceFormDraftEndpointsTests.cs`
   and `mobile/test/preventive_maintenance_form_draft_test.dart`.
3. Location-attempt ownership remains enforced, and linked attempt actor
   identity must match the inspection performer. Coverage:
   `tests/UniPM.Api.Tests/Inspections/InspectionLocationVerificationTests.cs`
   and `mobile/test/inspection_location_test.dart`.
4. Mobile submission progress uses the canonical Department, AssetCategory,
   and PmCycle batch. Submission stays disabled until every eligible schedule
   has a completed inspection; the backend remains authoritative. Coverage:
   `mobile/test/batch_progress_calculation_test.dart` and
   `mobile/test/preventive_maintenance_form_draft_test.dart`.
5. Backend asset validation requires Department for PM categories and schedule
   creation rejects assets without a department. Web asset entry aligns with
   this requirement. Existing departmentless rows are not destructively
   migrated and cannot enter new scheduling workflows. Coverage:
   `tests/UniPM.Api.Tests/Assets/AssetReadEndpointsTests.cs`,
   `tests/UniPM.Api.Tests/Schedules/ScheduleQueryEndpointsTests.cs`, and
   `web/src/features/assets/asset-create.test.tsx`.
6. ScheduleDate determines PmCycle, Year, and Quarter. Backend validation
   rejects contradictory temporal values, and web inputs derive the period.
   Coverage: `tests/UniPM.Api.Tests/Schedules/ScheduleQueryEndpointsTests.cs`,
   `web/src/features/schedules/schedule-contract.test.ts`, and
   `web/src/features/schedules/schedule-workflow.test.tsx`.
7. Backend schedule creation rejects non-Active assets. Web selection excludes
   inactive/retired assets; mobile's existing protection remains in place.
   Coverage: `tests/UniPM.Api.Tests/Schedules/ScheduleQueryEndpointsTests.cs`,
   `web/src/features/schedules/schedule-workflow.test.tsx`, and
   `web/e2e/schedules.spec.ts`.
8. Dashboard review navigation restores the generated dashboard scope when
   returning from acknowledgement review. The focused component regression
   follows Dashboard -> Review batch -> Back and checks route search plus
   rendered filters for asset category, year, pmCycle, department, condition,
   timeliness, and search. Coverage: `web/src/features/reports/pm-period-dashboard.test.tsx`
   and `web/src/features/preventive-maintenance-forms/preventive-maintenance-form-review.test.tsx`.
   The test-only change was committed as `8951f44781ad07d3295cfa675376f3ccb2b586fc`;
   its execution identity and the fact it was not rerun post-commit are recorded
   in TEST-042.
9. Form request failures and missing/invalid form states resolve before the
   page waits on dependent dashboard data. Coverage:
   `web/src/features/preventive-maintenance-forms/preventive-maintenance-form-review.test.tsx`.
10. Web acknowledgement requires visible signature stroke data; pointer-down
   alone is invalid and Clear resets signature validity. Backend PNG/base64
   validation remains in place. Coverage:
   `web/src/features/preventive-maintenance-forms/preventive-maintenance-form-review.test.tsx`.
11. Saving an inspection marks its schedule Completed. Deleting its Draft row
   recalculates schedule status from the institutional current date and
   ScheduleDate: past dates become Overdue; current/future dates become Due.
   Acknowledgement does not change schedule status. No prior Ongoing state is
   persisted for restoration, so deletion does not invent one or add a
   scheduler/status engine. Coverage:
   `tests/UniPM.Api.Tests/Forms/PreventiveMaintenanceFormDraftEndpointsTests.cs`.
12. Resume opens the existing row without creating another location attempt.
   Start continues through the existing verification path and advisory,
   non-blocking geolocation policy. Coverage:
   `mobile/test/batch_progress_calculation_test.dart` and
   `mobile/test/inspection_location_test.dart`.

## Architecture and contracts

The update DTO no longer accepts performer identity. The OpenAPI snapshot and
generated web DTO were synchronized to remove that update field. The create
contract still includes `InspectorUserId`; the backend applies role-aware
identity validation, including matching an Inspector's submitted ID to the
authenticated user. Schedule temporal and asset/department rules are enforced
by the API, with corresponding web validation. No duplicate
AssetId + PmCycle rule, recurrence behavior, broad RBAC change, or migration
was introduced.

## Important files

- Backend: `server/Features/Assets/AssetsEndpoints.cs`,
  `server/Features/Schedules/SchedulesEndpoints.cs`,
  `server/Features/PreventiveMaintenanceForms/PreventiveMaintenanceFormEndpoints.cs`.
- Backend regression tests:
  `tests/UniPM.Api.Tests/Assets/AssetReadEndpointsTests.cs`,
  `tests/UniPM.Api.Tests/Schedules/ScheduleQueryEndpointsTests.cs`,
  `tests/UniPM.Api.Tests/Forms/PreventiveMaintenanceFormDraftEndpointsTests.cs`,
  `tests/UniPM.Api.Tests/Inspections/InspectionLocationVerificationTests.cs`.
- Mobile: `mobile/lib/features/preventive_maintenance/preventive_maintenance_controller.dart`,
  `mobile/lib/features/preventive_maintenance/preventive_maintenance_models.dart`,
  `mobile/lib/features/preventive_maintenance/preventive_maintenance_repository.dart`,
  `mobile/lib/features/preventive_maintenance/preventive_maintenance_page.dart`,
  `mobile/lib/features/preventive_maintenance/scanned_asset_pm_entry.dart`.
- Mobile tests: `mobile/test/batch_progress_calculation_test.dart`,
  `mobile/test/inspection_location_test.dart`,
  `mobile/test/preventive_maintenance_form_draft_test.dart`.
- Web: `web/src/features/assets/asset-create.tsx`,
  `web/src/features/assets/asset-contract.ts`,
  `web/src/features/schedules/schedule-create.tsx`,
  `web/src/features/schedules/schedule-contract.ts`,
  `web/src/features/preventive-maintenance-forms/pm-acknowledgement-review.tsx`,
  `web/src/features/preventive-maintenance-forms/form-detail.tsx`,
  `web/src/features/reports/pm-period-dashboard.tsx`,
  `web/src/routes/app/preventive-maintenance-forms/$formId.review.tsx`.
- Web tests: `web/src/features/assets/asset-contract.test.ts`,
  `web/src/features/assets/asset-create.test.tsx`,
  `web/src/features/schedules/schedule-contract.test.ts`,
  `web/src/features/schedules/schedule-workflow.test.tsx`,
  `web/src/features/preventive-maintenance-forms/preventive-maintenance-form-review.test.tsx`,
  `web/src/features/reports/pm-period-dashboard.test.tsx`,
  `web/e2e/assets.spec.ts`, `web/e2e/schedules.spec.ts`.
- API contract: `web/openapi/unipm-v1.json`,
  `web/src/api/generated/models/updateDraftInspectionRowDto.ts`.

## Database changes

No migration or destructive data change. Legacy departmentless assets remain
stored; the API prevents new schedules for them until their records meet the
department requirement.

## Tests present

Focused backend, mobile, web component, and Playwright regression tests cover
the listed changes. Execution claims are recorded separately in TEST-042 and
remain bounded by that record's commit identities and verification limits.

## Verification status

Source changes are mapped to the 12-item checklist. Backend/mobile and final
web executions are recorded separately by tested SHA in TEST-042. The final
web checks ran at `e301a91cf4ffaa6bdc13812f52141b4891c657af`.

## Known limitations

- Native SQL tests were skipped because `UNIPM_SQLSERVER_TEST_CONNECTION` was
  not configured during backend verification.
- The full web `format:check` failed on 39 paths outside the 19 web paths
  changed from `fabb49a` through `e301a91`; formatting checks for changed files
  passed.
- The date-based delete rule restores Due or Overdue. Ongoing cannot be
  reconstructed because no previous execution status is persisted.
- Physical-device and iOS verification are NOT VERIFIED.
- Geolocation remains advisory evidence and does not prove physical presence.
- TEST-029 and TEST-041 remain immutable historical records at their own
  commits. They do not replace this task's source identity or workflow
  semantics.

## Related evidence

- [TEST-042](../test-runs/TEST-042-pre-acceptance-integration-hardening.md)
- [IMP-034](IMP-034-pmis-gsd-demo-readiness.md)
- [TEST-041](../test-runs/TEST-041-pmis-gsd-demo-readiness.md)
- [TEST-029](../test-runs/TEST-029-preventive-maintenance-api-contract-sync.md)
