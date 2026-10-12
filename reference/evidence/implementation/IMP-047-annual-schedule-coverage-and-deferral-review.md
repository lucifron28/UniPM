---
id: IMP-047
type: implementation
title: Annual PM coverage safeguards and auditable GSD deferral review
status: reviewed
recordedAtUtc: 2026-10-10T01:28:27Z
sourceCommit: 7db4c34e20fd4dcca9de167b6a3acf99e8aa1c94
sourceBranch: feat/pre-evaluation-ux-motion
evidenceLevel: source-inspected
---

# Annual PM coverage safeguards and auditable GSD deferral review

## Changes

Annual recovery remains limited to the current institutional calendar year and
respects asset registration dates. With `ScheduleGeneration:EffectiveDate`
unset, generation creates current and future eligible cycles but does not
backfill earlier cycles. Missing, uncovered past cycles are counted for GSD
coverage review and surfaced in the manual generation result and worker log.
If GSD later approves a coverage start date, the optional strict `yyyy-MM-dd`
setting permits catch-up only for cycles whose institutional month-end due date
is on or after that date. Existing schedules and deferrals remain intact.

The new GSD-only deferral review endpoint records reviewer ID, UTC review time,
and an optional bounded note. A review is one-time and auditable. It does not
create a schedule, complete or waive PM work, change asset condition, or alter
inspection or acknowledgement behavior. The schedule registry provides status
filters, review metadata, and counts; the GSD dashboard links to this review
surface and separates deferred cycles from scheduled/compliance totals. The
OpenAPI snapshot and generated web client reflect the updated contract.

The new migration adds only review metadata and its consistency constraint and
index. A disposable SQL Server 2019 Up/Down/Up preservation test is present,
but native SQL Server execution was unavailable locally; see TEST-063.

## Accessibility review

The branch retains its visible primary-navigation keyboard focus outline and
global `prefers-reduced-motion` handling. New review controls have associated
labels and save feedback uses a status announcement. This was a source and
automated-browser review, not a complete WCAG or screen-reader audit.

## Scope

CPMP category/month rules, assignment, inspection, acknowledgement, and PM
completion semantics were not changed. No database was deployed or modified,
and no AI provider was called. The optional effective date remains unset until
GSD confirms an approved coverage boundary.

## Verification

See TEST-063 for exact source identity, commands, results, and limitations.
