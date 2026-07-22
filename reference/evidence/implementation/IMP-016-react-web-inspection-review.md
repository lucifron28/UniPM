---
id: IMP-016
type: implementation
title: React web inspection review
status: reviewed
recordedAtUtc: 2026-07-22T15:02:35Z
sourceBranch: feat/web-inspections
evidenceLevel: source-inspected
---

# React Web Inspection Review

## Objective

Add an authenticated, source-record review surface for existing inspection
data without introducing web inspection submission or any unresolved GSD
workflow.

## Source Identity

- Implementation commit: `b78c407a0d99a592779b6b66a64aad7712cecd4d`
- Starting main commit: `76876560fd9977e4d6e4f0dad7df67484b4e1471`
- Source paths: inspection endpoints and OpenAPI contract test, generated web
  client, `web/src/features/inspections/`, inspection routes, asset detail,
  primary navigation, and focused browser tests.

## Implementation Summary

- Gives existing inspection POST, list, detail, and history endpoints stable
  OpenAPI operation IDs and typed success/error metadata without changing their
  business behavior.
- Publishes typed inspection and history models through the committed OpenAPI
  snapshot and generated client. The offline contract gate checks the four
  inspection operations, their typed success schemas, and two inspection drift
  mutations.
- Adds authenticated inspection list and detail routes plus desktop and mobile
  navigation. The registry owns filters and page through Router search state,
  sends only supported API filters, and paginates ten returned records
  client-side.
- Uses strict Zod contracts, TanStack Query, a responsive TanStack Table/card
  presentation, unfiltered recorded-outcome summary counts, and visible retry
  states for list, summary, and contextual data failures.
- Adds a compact latest-five inspection history panel to asset detail with a
  route-backed registry link and immutable source-record links.
- Keeps raw returned remarks and recommendations as source text. No search,
  AI, review generation, acknowledgement, RMRF, corrective-handoff, or
  submission UI is introduced.

## Boundaries

The web application does not call `recordInspection`, render an inspection
creation route, or create a write mutation. Existing backend inspection
submission, its authorization policy, schedule completion behavior, and SQL
integrity protections remain unchanged. All UI records and browser fixtures are
fictional.

## Related Evidence

- [ADR-012](../decisions/ADR-012-web-inspections-source-record-review-only.md)
- [TEST-020](../test-runs/TEST-020-web-inspection-review-verification.md)
