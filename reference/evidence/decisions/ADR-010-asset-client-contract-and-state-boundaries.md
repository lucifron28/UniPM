---
id: ADR-010
type: decision
title: Keep asset client state and contract boundaries explicit
status: reviewed
recordedAtUtc: 2026-07-19T07:05:00Z
evidenceLevel: source-inspected
---

# Keep Asset Client State And Contract Boundaries Explicit

## Context

The asset API already provides bounded read/write contracts but no asset search, pagination, official location catalog, or final client RBAC model.

## Decision

TanStack Query owns API asset/category state. Router search owns filters, text, and page. TanStack Table models the first real table. The browser applies text search and pagination after the API applies supported metadata filters.

The client consumes category labels from reference data, retains generated code as committed contract output, and validates runtime responses with Zod before display or caching. The create screen mirrors only the temporary `GSD` policy; backend authorization remains authoritative.

## Consequences

The registry is inspectable and route-addressable without pretending to have server search/pagination or final institutional lists. QR values may be copied, but QR generation and unsupported prototype fields/actions remain deferred.

## Related Evidence

- [IMP-014](../implementation/IMP-014-web-asset-registry.md)
- [TEST-018](../test-runs/TEST-018-web-asset-registry-verification.md)
