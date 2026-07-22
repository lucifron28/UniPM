---
id: IMP-014
type: implementation
title: React web asset registry
status: reviewed
recordedAtUtc: 2026-07-22T11:53:00Z
sourceBranch: feat/web-assets
evidenceLevel: source-inspected
---

# React Web Asset Registry

## Objective

Add the first source-backed operational web module without extending the provisional asset domain or institutional workflows.

## Source Identity

- Implementation commits: `f01c231c9321ce02ed8c876d39bb681da9acfd5e`
- Source paths: asset endpoints and tests, generated web client, and `web/src/features/assets/`.

## Implementation Summary

- Adds explicit OpenAPI metadata and public `AssetResponse` output for asset create, list, detail, and QR lookup.
- Commits the regenerated typed client, an offline asset-contract gate, and a repeatable negative OpenAPI mutation test suite (`api:contract:test`).
- Adds runtime Zod validation, TanStack Query-backed asset/reference-data queries, and authenticated list, detail, and provisional GSD create routes.
- Resolves all draft PR #32 review findings: authoritative search route state, mobile `<nav aria-label="Primary">` landmark, loading/error summary cards with retry button, empty vs filter no-match states, Zod schema create-form validation with error alert focus management and backend 400 error key mapping, invalid UUID route detection skipping API calls, and label-specific copy toast feedback.
- Restores TypeScript 6 (`~6.0.2`), Node 24 web stream polyfilling for JSDOM/MSW (`src/test/polyfill.ts`), and `pool: 'vmThreads'` in `vite.config.ts`.
- Keeps supported server filters server-side; text search and ten-row pagination are browser-side over the returned filtered list.
- Derives summary counts from returned data, uses category display labels from reference data, supports responsive table/cards, and copies identifiers without producing QR graphics.

## Boundaries

No migration, asset-model field, edit/delete/status workflow, schedule, inspection, PM, audit, work-order, QR-printing, export, official location list, or final RBAC rule was added. All test values are fictional.

## Related Evidence

- [ADR-010](../decisions/ADR-010-asset-client-contract-and-state-boundaries.md)
- [TEST-018](../test-runs/TEST-018-web-asset-registry-verification.md)
