---
id: IMP-014
type: implementation
title: React web asset registry
status: reviewed
recordedAtUtc: 2026-07-19T07:05:00Z
sourceBranch: feat/web-assets
evidenceLevel: source-inspected
---

# React Web Asset Registry

## Objective

Add the first source-backed operational web module without extending the provisional asset domain or institutional workflows.

## Source Identity

- Implementation commits: `300859ce00821caf8ff074a2cf3691b3e5f1957b` and the Node 22 CI lockfile correction `34ff8561b1fcad7f933dfe520b9de67075333684`
- Source paths: asset endpoints and tests, generated web client, and `web/src/features/assets/`.

## Implementation Summary

- Adds explicit OpenAPI metadata and public `AssetResponse` output for asset create, list, detail, and QR lookup.
- Commits the regenerated typed client and an offline asset-contract gate.
- Adds runtime Zod validation, Query-backed asset/reference-data queries, and authenticated list, detail, and provisional GSD create routes.
- Keeps supported server filters server-side; text search and ten-row pagination are browser-side over the returned filtered list.
- Derives summary counts from returned data, uses category display labels from reference data, supports responsive table/cards, and copies identifiers without producing QR graphics.

## Boundaries

No migration, asset-model field, edit/delete/status workflow, schedule, inspection, PM, audit, work-order, QR-printing, export, official location list, or final RBAC rule was added. All test values are fictional.

## Related Evidence

- [ADR-010](../decisions/ADR-010-asset-client-contract-and-state-boundaries.md)
- [TEST-018](../test-runs/TEST-018-web-asset-registry-verification.md)
