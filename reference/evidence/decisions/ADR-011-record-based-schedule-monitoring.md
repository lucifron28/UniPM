---
id: ADR-011
type: decision
title: Keep schedule monitoring record-based and non-generative
status: reviewed
recordedAtUtc: 2026-07-22T07:21:56Z
evidenceLevel: source-inspected
---

# Keep Schedule Monitoring Record-Based And Non-Generative

## Context

The backend stores individual preventive-maintenance schedule records and
supports bounded list filters plus create/detail operations. It does not define
approved recurrence rules, adjustment authority, assignment, or status
transition commands.

## Decision

TanStack Router owns committed filters and page. TanStack Query owns schedule,
asset, and reference-data server state. Backend catalogs own controlled status,
period-type, and quarter codes, while strict Zod contracts reject malformed or
unknown or duplicate runtime records before display or caching. Failed
reference queries leave only their affected controls disabled and retryable;
they are never represented as valid empty option sets.

Status summaries count recorded values only; the web client does not recalculate
or mutate status from the current date. Create visibility mirrors the
provisional GSD/Supervisor backend policy for user experience, but backend
authorization remains authoritative.

No recurrence, schedule adjustment, assignment, deletion, or status-transition
workflow is introduced. Testing is risk-based: contract drift, runtime data
integrity, supported filter transport, create authority/payload, and three
critical browser journeys are protected without duplicating an exhaustive
matrix across layers.

## Consequences

The module is route-addressable, inspectable, and honest about stored data. It
cannot promise automated planning or final institutional scheduling behavior,
and those workflows remain deferred pending GSD clarification and explicit
approval.

## Security And Privacy

The feature reuses the authenticated shell, memory-only access-token boundary,
generated Axios transport, and backend policies. It introduces no browser token
storage, personnel lookup, real institutional records, or new sensitive fields.

## Related Evidence

- [IMP-015](../implementation/IMP-015-react-web-preventive-maintenance-schedules.md)
- [TEST-019](../test-runs/TEST-019-web-schedule-workflow-verification.md)
