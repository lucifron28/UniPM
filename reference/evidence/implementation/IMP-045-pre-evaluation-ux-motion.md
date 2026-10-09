---
id: IMP-045
type: implementation
title: CPMP batch-lock enrollment deferral and pre-evaluation UX refinements
status: reviewed
recordedAtUtc: 2026-10-09T19:34:33Z
sourceCommit: ec7a7d693ec121c1282493fad3d0ca039a04effa
sourceBranch: feat/pre-evaluation-ux-motion
evidenceLevel: source-inspected
---

# CPMP batch-lock enrollment deferral and pre-evaluation UX refinements

## Objective

Prevent schedule enrollment from racing with changes to a PM batch while keeping current-year generation safe and visible to GSD. Add focused web interaction feedback, reduced-motion support, and registry accessibility refinements without changing inspection workflow semantics.

## Source Identity

- Base branch: feat/pre-evaluation-remediation at 48676738498eb6fe6a2341a697b2611f80a87b20.
- Feature branch: feat/pre-evaluation-ux-motion.
- Relevant commits:
  - 65a64d44c72e09e19d5dc06d94b83c25e7c5efd5 — defer enrollment when the PM batch is locked.
  - e1f8b9c2e2f5e4eb823669f2f9588659e7f792ad — add accessible interaction feedback and motion.
  - bd41690ef58796c4ad678397156b19a266352ee4 — normalize generated API line endings for stable contract checks.
  - ec7a7d693ec121c1282493fad3d0ca039a04effa — preserve existing line endings in Prettier checks.
- Source identity at implementation review: ec7a7d693ec121c1282493fad3d0ca039a04effa.

## Implementation Summary

A normalized application-lock key groups a department, asset category, and PM cycle into the schedule batch identity. Asset registration, annual recovery, manual schedule creation, supervisor assignment, and inspection submission synchronize against that identity. When automatic enrollment encounters a locked batch, it records a GSD-visible deferral instead of creating a schedule during an in-flight batch mutation.

Annual recovery uses the institutional current year and skips PM months earlier than the current Manila month, avoiding backfilled schedules whose deadlines already passed. Manual schedule creation is limited to the current institutional year and returns a conflict when the target batch is locked.

The schedule deferral model, migration, bounded GSD-only review endpoint, generated client, and web review panel make skipped enrollment visible and recoverable. A transaction-completion defect in manual schedule creation was corrected: the outer SQL transaction now commits after saving the schedule.

Web refinements add pressed-state feedback to shared buttons and navigation, input border transitions, reduced-motion behavior, sticky and hover registry feedback, and accessible keyboard focus, labelling, and column scope. Task ordering and field workflow behavior remain unchanged.

## Architecture And Contracts

- PM batch key: normalized department + asset category + canonical PM cycle.
- Mutation synchronization uses SQL Server application locks shared by enrollment and existing schedule/inspection batch mutations.
- Deferred enrollments are stored with a bounded reason and reviewed through a GSD-only paginated read contract.
- Annual recovery and manual generation use the institutional Manila year/month semantics.
- No API response DTOs or CPMP category/month rules were changed beyond the new deferral read model and generation-result information.
- Existing assignment, Draft reuse, geolocation, submission gating, acknowledgement, and batch identity semantics are preserved.

## Important Files

- server/Features/Schedules/ScheduleBatchMutationLock.cs
- server/Features/Schedules/PreventiveMaintenanceScheduleGenerationService.cs
- server/Features/Schedules/SchedulesEndpoints.cs
- server/Features/Assets/AssetsEndpoints.cs
- server/Models/ScheduleEnrollmentDeferral.cs
- server/Features/Schedules/ScheduleEnrollmentDeferralReason.cs
- server/Migrations/20261009182058_AddScheduleEnrollmentDeferrals.cs
- server/Data/ApplicationDbContext.cs
- tests/UniPM.Api.Tests/Schedules/PreventiveMaintenanceScheduleGenerationTests.cs
- tests/UniPM.Api.Tests/Schedules/ScheduleBatchAssignmentEndpointsTests.cs
- tests/UniPM.Api.Tests/Inspections/SqlServerInspectionSubmissionIntegrityTests.cs
- web/src/features/schedules/schedule-enrollment-deferral-review.tsx
- web/src/features/schedules/schedule-registry.tsx
- web/src/index.css
- web/src/components/ui/button.tsx
- web/src/components/ui/input.tsx
- web/prettier.config.mjs

## Database Changes

Migration 20261009182058_AddScheduleEnrollmentDeferrals adds persisted enrollment-deferral records. The migration and model snapshot compile and EF reports no pending model changes. Applying and rolling back this migration against SQL Server 2019 was not executed in this verification run.

## Tests Present

Coverage includes automatic enrollment deferral under a locked batch, current-year recovery/month filtering, manual schedule creation transaction persistence, and manual creation versus assignment synchronization. Web tests cover deferral review, schedule behavior, and accessible registry interaction; Playwright exercises the schedule UI.

## Verification Status

TEST-061 records local execution details and per-check source identities. Full backend and web test/build checks passed locally. The SQL Server-dependent integration cases were skipped because the test connection was not configured. Exact-head GitHub CI is verified separately after push.

## Known Limitations

SQL Server 2019 application-lock behavior, transaction persistence against a real SQL Server instance, and migration Up/Down behavior are NOT VERIFIED locally. Manual browser screenshots, physical-device behavior, GSD acceptance, and temporary deployment are also not verified here.

## Related Evidence

- [TEST-061: CPMP batch-lock deferral and pre-evaluation UX verification](../test-runs/TEST-061-pre-evaluation-ux-motion.md)
- [IMP-044: CPMP scheduling and WMS remediation](IMP-044-pre-evaluation-cpmp-and-wms-remediation.md)