---
id: IMP-054
type: implementation
title: PM analytics V3 registered asset inventory subject
status: draft
recordedAtUtc: 2026-10-19T17:41:20Z
sourceCommit: fcd6e6b7b1ab71bdc64e3c4520fcb558b7551920
sourceBranch: feature/nla-v3-asset-inventory
evidenceLevel: locally-executed
---

# PM analytics V3 registered asset inventory subject

## Scope and identity

This bounded Phase 6 change adds `assetInventory` beside the existing V3
`pmExecution` subject. It is isolated on
`feature/nla-v3-asset-inventory` at source commit
`fcd6e6b7b1ab71bdc64e3c4520fcb558b7551920`, based on Phase 5 engineering
commit `4bd3ac592c9be260b8157ced6ebd9c77b7069254`.

The subject has one current registered Asset per row and no PM period. It
supports the existing asset categories plus current department, building,
general location, and exact persisted registration status filters. Its only
measure is `registeredAssetCount`; the query reads `Assets` directly and does
not join schedules, inspections, forms, acknowledgements, or referral history.
Empty category selection normalizes to the four catalogued categories. The
stored `Active`, `Inactive`, and `Retired` status codes remain registration
states and are not interpreted as inspection condition.

The implementation reuses the existing V3 subject catalog, validator,
GSD-only endpoint policy, signed stateless user-bound expiring preview,
confirmation path, typed response, and React plan/result workflow. Natural
language interpretation stays limited to `pmExecution`; the UI tells users to
configure inventory with explicit controls rather than silently remapping a
question.

## Source and regression coverage

- Added subject-specific catalog metadata and closed validation for measures,
  dimensions, filters, record columns, and null period behavior. PM execution
  still requires its existing period.
- Added a direct Asset query with stable record ordering, bounded row loading,
  filter resolution, unique-asset count, grouping, aggregate filtering,
  top/ranking, ordering, and pagination.
- Added full preview-to-execution API tests for default categories, status
  groups, department/building/location/status filters, records paging, invalid
  PM and inspection clauses, and status/condition separation.
- The fanout fixture gives one Active asset four schedules, four inspections,
  and six referral audit rows, including a non-operational finding. The
  inventory report still counts that Asset exactly once.
- Updated OpenAPI and generated TypeScript contracts. Added React and
  390×844 Playwright coverage for inventory subject selection, no PM period,
  status grouping, preview, confirmation, provenance, and results. The existing
  PM drill-down browser path remains covered.
- Updated the V3 corpus manifest to catalog version 2. Candidate questions,
  expected labels, and historical evidence were not edited.

## Verification boundary

See [TEST-114](../test-runs/TEST-114-pm-analytics-v3-asset-inventory.md) for
exact local commands and results. Native SQL Server 2019 execution remains
unverified, and the current review record is not institutional acceptance.
Phase 5 labels for all 18 provisional questions remain pending human review.
No DeepSeek/provider call, held-out evaluation, SQL connection, Azure/staging
operation, deployment, merge, database migration, or data mutation occurred.

Phase 7 remains a planning-only milestone; the batch/assignment contract and
unresolved semantic questions are in
[`nla-v3-phase7-batch-assignment-design.md`](../../planning/nla-v3-phase7-batch-assignment-design.md).
