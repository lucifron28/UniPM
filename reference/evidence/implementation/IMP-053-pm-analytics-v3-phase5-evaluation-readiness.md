---
id: IMP-053
type: implementation
title: PM analytics V3 Phase 5 evaluation readiness
status: draft
recordedAtUtc: 2026-10-19T16:57:25Z
sourceCommit: 616e4ba2752a6acb20a5b823bf53f03c5b3a51ac
sourceBranch: feature/nla-v3-phase5-evaluation
evidenceLevel: source-inspected
---

# PM analytics V3 Phase 5 evaluation readiness

## Scope and source identity

This milestone makes Phase 5 engineering checks reproducible and strengthens
report correctness regressions. It does not establish reviewed language labels,
SQL Server compatibility, first-release acceptance, or deployment acceptance.

- Source commit: `616e4ba2752a6acb20a5b823bf53f03c5b3a51ac`.
- Base commit: `cacb6c0a0856955a555f9caaca95f93a3f551098`.
- Branch: `feature/nla-v3-phase5-evaluation`.
- Implementation date: 2026-10-19.
- Existing V2 fallback and historical V1/V2 evaluation datasets and scores were
  preserved.

## Implementation

- Added a V3-specific corpus validator and deterministic diagnostic/scoring
  gate. It validates the fixed development-candidate corpus, manifest/hash,
  unique IDs, review ledger, dispositions, and fixture inventory. Diagnostics
  use the safety guard and deterministic rule parser; scoring is blocked until
  every candidate has human approval and the corpus is frozen. It cannot read a
  held-out corpus, execute plans, call DeepSeek, or reuse the V1/V2 evaluator.
- Kept all 18 candidate labels pending. Added per-case reviewer notes without
  changing the historical corpus or declaring acceptance evidence.
- Added independent fixed-data calculation fixtures and full V3 execution-path
  regressions for matching denominators, grouped thresholds, category-specific
  cycles, February/Q1 and November/Q4 source records, filters, pagination,
  missing coverage, and zero eligible rows.
- Added regressions for category comparisons, unsupported subjects, unsafe
  executable-SQL requests, expired preview confirmation, and existing signed
  preview token and role-boundary behavior.
- Added a loopback-only SQL Server 2019 integration test using a disposable
  database. The test remains unexecuted because a local SQL Server image was
  unavailable and the registry denied the image pull.
- Added `feature/**` to the backend workflow push branches so this feature
  branch receives backend CI.

## Review blockers

Every one of the 18 candidate labels requires independent domain/reporting
review. Deterministic diagnostics identify two concrete disagreements in
V3D-008 (`or` filter semantics) and V3D-009 (“not late” semantics). The notes
also flag missing comparison assertions in V3D-004, a missing compliance
measure assertion in V3D-006, ambiguous separated cycles in V3D-007, subject
scope in V3D-012, and sorting semantics in V3D-016. Other questions require
confirmation against approved measure, denominator, ranking, and category
definitions. See the linked review notes before any corpus freeze.

The next `assetInventory` subject is designed in
[`nla-v3-phase6-asset-inventory-design.md`](../../planning/nla-v3-phase6-asset-inventory-design.md)
but has not been implemented. Phase 6 coding is gated on Phase 5 semantic and
SQL review.

## Related evidence

- [TEST-113: Phase 5 local verification](../test-runs/TEST-113-pm-analytics-v3-phase5-verification.md)
- [V3 per-case review notes](../../evaluation/pm-analytics-v3/review-notes.md)
- [NLA V3 design and evaluation gates](../../planning/nla-v3-composable-reporting-design.md)
