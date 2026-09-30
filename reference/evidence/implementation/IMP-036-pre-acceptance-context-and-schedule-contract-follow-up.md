---
id: IMP-036
type: implementation
title: Pre-acceptance context and schedule-contract follow-up
status: reviewed
recordedAtUtc: 2026-09-30T09:26:08Z
sourceBranch: feature/pre-acceptance-integration-hardening
evidenceLevel: source-inspected
---

# Pre-acceptance context and schedule-contract follow-up

## Objective

Record the dashboard-context route contract, canonical schedule-date rules,
and the approved lifecycle wording correction with their verification limits.

## Source Identity

- Navigation changes were committed as
  `67cdb44f47ab92e173e292a103aeccc2ade1dc5f`.
- The final focused navigation run used committed HEAD
  `177b5e66f31567d343e8a84c551e9a6e7c6ca7d5` plus navigation-only diff object
  ID `5d08c5393806c99114d840252279cb12f5df59ae`. No test rerun occurred at
  navigation commit `67cdb44`.
- The backend schedule change is commit
  `17847785e6568d17a287460010c4ece8e6d52629`; the web schedule validation
  change is commit `177b5e66f31567d343e8a84c551e9a6e7c6ca7d5`.
- The approved root `AGENTS.md` correction was committed separately as
  `3052bb0`. Schedule tests used a separate base-plus-patch identity,
  documented in TEST-043.

## Implementation Summary

- Dashboard search context includes category, year, PM cycle, department,
  condition, timeliness, and asset search. The review context carries all of
  those fields plus the form ID through the full-form and inspection-detail
  routes; both detail return links restore the context to batch review. The
  review page then preserves available filters when returning to the dashboard.
  Acknowledged-form detail returns directly to its filtered dashboard scope.
- Navigation source and regression-test changes are committed. The final
  focused route run passed 5 tests (11 skipped) after a focused correction;
  its exact execution identity and artifacts are recorded in TEST-043.
- The web schedule form validates a date-only value, derives year and quarter,
  and serializes the chosen day at UTC midnight. The API derives canonical
  `PmCycle` in institutional UTC+08; UTC-midnight serialization preserves the
  selected calendar day. The API enforces a PM-cycle year from 2000 through
  current UTC year + 5, checks any supplied year against that cycle, and checks
  quarter consistency only for quarterly schedules. Non-quarter schedules
  persist no quarter.
- Root `AGENTS.md` now states that saving completed inspection work completes
  its schedule while its form can remain Draft; submission and acknowledgement
  are separate, and acknowledgement records receipt/signatory details rather
  than completing schedules. The example commit message now refers to
  inspection schedule completion.

## Important Files

- `AGENTS.md`
- `web/src/features/reports/pm-period-dashboard.tsx`
- `web/src/features/preventive-maintenance-forms/pm-acknowledgement-review.tsx`
- `web/src/features/preventive-maintenance-forms/form-detail.tsx`
- `web/src/features/inspections/inspection-detail.tsx`
- `web/src/routes/app/inspections/$inspectionId.tsx`
- `web/src/routes/app/preventive-maintenance-forms/$formId.index.tsx`
- `web/src/features/schedules/schedule-contract.ts`
- `server/Features/Schedules/SchedulesEndpoints.cs`
- `server/Features/Schedules/PreventiveMaintenanceCycle.cs`

## Tests Present

Existing source tests cover direct dashboard-to-review restoration and
schedule temporal validation. The navigation commit adds route-level tests for
full-form and inspection-detail round trips, both department scopes,
acknowledged-form return, and form-registry navigation. The final focused run
passed the five selected tests; eleven tests outside its name filter were
skipped.

## Verification Status

Schedule tests ran against base
`c2fe175fbc3a3a9317f09817a9dbdb5732ebf8b7` plus scoped worktree patch
`abafc8748ccfc8c05c3d5ebc06dd9780cb3c5279`. The focused navigation run passed
5 tests with 11 skipped at committed HEAD
`177b5e66f31567d343e8a84c551e9a6e7c6ca7d5` plus navigation diff
`5d08c5393806c99114d840252279cb12f5df59ae`. The schedule commits were already
present for that run. The navigation changes were later committed as
`67cdb44`; tests were not rerun at that commit. Executor3 ran no tests.

## Known Limitations

The initial navigation patch identity was not retained. The initial failing
run is not attributed to the final `5d08c539` patch. The final passing run is
recorded at HEAD `177b5e6` plus that identified patch; no post-commit rerun is
claimed for `67cdb44`.

## Related Evidence

- [IMP-035](IMP-035-pre-acceptance-integration-hardening.md)
- [TEST-042](../test-runs/TEST-042-pre-acceptance-integration-hardening.md)
- [TEST-043](../test-runs/TEST-043-pre-acceptance-integration-verification.md)
