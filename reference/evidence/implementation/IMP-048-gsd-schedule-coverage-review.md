---
id: IMP-048
type: implementation
title: GSD schedule coverage review and effective-date boundary
status: reviewed
recordedAtUtc: 2026-10-10T04:59:56Z
sourceCommit: ecadf495fcfa482a6012f4fa378237596ed5fa35
sourceBranch: feat/pre-evaluation-ux-motion
evidenceLevel: source-inspected
---

# GSD schedule coverage review and effective-date boundary

## Objective

Record the current schedule coverage behavior and verify that the review surface
does not silently create PM obligations or decide an institutional coverage
start date.

## Source identity

The schedule coverage feature was introduced by commit 2776f2f
(feat(schedules): add GSD coverage review). Its current source was inspected at
ecadf495fcfa482a6012f4fa378237596ed5fa35.

Relevant paths:

- server/Features/Schedules/PreventiveMaintenanceScheduleGenerationService.cs
- server/Features/Schedules/PreventiveMaintenanceScheduleGenerationWorker.cs
- server/Features/Schedules/SchedulesEndpoints.cs
- server/Features/Schedules/ScheduleGenerationOptions.cs
- server/Features/Auth/AuthServiceCollectionExtensions.cs
- web/src/features/schedules/schedule-coverage-review.tsx
- web/src/features/schedules/schedule-registry.tsx

## Findings

The background and manual generation paths create eligible current-year and
future CPMP cycles. When the optional ScheduleGeneration:EffectiveDate is not
configured, earlier missing cycles are reported for GSD review and are not
backfilled. A configured value must use yyyy-MM-dd and is intended to require
GSD approval before catch-up generation.

The coverage-review and deferral-review endpoints use the
CanGenerateSchedules policy. The policy grants GSD only. The coverage page
lists missing past cycles, while the separate deferral review records the GSD
reviewer, review time, and optional note. Reviewing a missing cycle does not
create a schedule, waive work, or change inspection, assignment, or
acknowledgement behavior.

The repository option defaults to null. No institutional effective date was
chosen in this work. Runtime deployment configuration was not audited.

## Verification status

These are source-inspected findings. Executed migration and workflow evidence
is recorded in TEST-065 and TEST-066. GSD has not confirmed an approved
coverage boundary or accepted the review workflow.

## Related evidence

- TEST-063: earlier verification, before native SQL Server execution.
- TEST-065: Release backend, SQL Server 2019, and migration verification.
- TEST-066: web, browser, and live role-chain verification.
