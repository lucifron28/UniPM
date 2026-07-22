---
id: IMP-015
type: implementation
title: React web preventive-maintenance schedules
status: reviewed
recordedAtUtc: 2026-07-22T07:21:56Z
sourceBranch: feat/web-schedules
evidenceLevel: source-inspected
---

# React Web Preventive-Maintenance Schedules

## Objective

Add a source-backed schedule monitoring and creation module without turning the
existing schedule-record contract into a recurrence, adjustment, or automatic
status engine.

## Source Identity

- Implementation commit: `6f4a1ae7188fe57d91c99831f45474bd2a026a19`
- Starting main commit: `96785892f3c64356f7a45dc6263db33a328afb01`
- Source paths: schedule/reference endpoints and tests, generated web client,
  `web/src/features/schedules/`, and schedule routes.

## Implementation Summary

- Gives schedule create/list/detail and three schedule reference endpoints
  stable OpenAPI operation IDs and typed success contracts.
- Returns the public `ScheduleResponse` with the loaded asset summary after
  creation; no EF entity is exposed.
- Commits the regenerated Orval client and extends the offline contract gate
  with the schedule contracts and two focused schedule drift mutations.
- Adds authenticated list, detail, and create routes plus desktop/mobile primary
  navigation.
- Keeps committed filters and page in Router search state, sends only the six
  supported backend filters, and paginates ten rows client-side over the
  filtered response.
- Uses an unfiltered Query for truthful recorded-status counts. No status is
  inferred from a schedule date.
- Uses strict Zod response contracts, TanStack Query server state, TanStack
  Table desktop rows, responsive cards, and TanStack Form creation fields.
- Validates status, period-type, and quarter reference codes against their
  respective controlled catalogs and rejects duplicate entries before caching.
- Keeps failed summary and selector data visibly unavailable with compact
  retry actions rather than presenting empty controls as valid choices.
- Associates client and backend field errors with every create input through
  stable error IDs and ARIA validation metadata.
- Mirrors provisional GSD/Supervisor create visibility while retaining backend
  authorization as the authority; Admin alone is not treated as operational
  schedule authority.

## Boundaries

No migration, schedule entity field, recurrence generation, automatic overdue
calculation, editing, deletion, adjustment, assignment, notification, status
command, inspection submission, acknowledgement, RMRF, corrective handoff, or
final scheduling policy was added. All browser and unit-test records are
fictional.

## Related Evidence

- [ADR-011](../decisions/ADR-011-record-based-schedule-monitoring.md)
- [TEST-019](../test-runs/TEST-019-web-schedule-workflow-verification.md)
