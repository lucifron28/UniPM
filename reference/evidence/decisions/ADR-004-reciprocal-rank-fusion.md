---
id: ADR-004
type: decision
title: Use Reciprocal Rank Fusion for internal retrieval combination
status: reviewed
recordedAtUtc: 2026-07-12T10:34:23Z
sourceBranch: feat/retrieval-fusion
testedCommit: 58668f963b5cc3aa2b835e8d1f2f985bdf4a3581
evidenceLevel: source-inspected
---

# Reciprocal-Rank Fusion

## Context

Lexical SQL Server Full-Text Search and semantic cosine retrieval produce
different score systems. A fused internal channel needs deterministic ranking,
source traceability, and an explicit semantic degradation boundary before the
maintenance-review contract is designed.

## Decision

Use Reciprocal Rank Fusion with `K=60` and one-based ordinal ranks:

```text
fusionScore(document) = sum(1 / (K + rankInChannel))
```

The service retrieves a bounded candidate list from each existing channel,
deduplicates by inspection ID, verifies stable metadata agreement, and applies
deterministic tie-breaking. Lexical remains the required baseline channel.
Semantic unavailability or execution/data failure is reported as degraded
lexical-only retrieval; semantic validation errors and lexical failures remain
errors. No exception message or provider detail enters response metadata.

`K=60` is an initial reproducible parameter, not a claim of empirical
optimality. It is recorded in fused responses and benchmark metadata.

## Alternatives Considered

- Raw FTS-rank plus cosine-score addition was rejected because the values are
  not compatible measurements.
- Min-max or z-score normalization was deferred because it introduces a
  dataset- and window-dependent calibration contract.
- Weighted linear combination was rejected because weights would be arbitrary
  before benchmark evidence exists.
- Union and deduplication without fusion was rejected because it gives no
  principled order when channels disagree.

## Boundaries

Context boosts, issue-key boosts, same-asset rules, location/recency boosts,
thresholds, source selection, and insufficient-evidence policy remain separate
review-layer decisions. The fused service has no public endpoint, no writes,
no query-vector persistence, no prompt construction, and no LLM integration.

## Operational And Privacy Implications

Each channel is called at most once per fused request, with bounded candidate
depth and result limits and cancellation propagation. Metrics expose only
bounded channel/outcome dimensions. Query text, source text, source IDs,
vectors, provider payloads, endpoints, and credentials are excluded from
metrics and benchmark metadata. A semantic provider remains operationally
optional for core workflows, while fused quality evaluation requires a real
provider and must not treat degraded results as successful fused evidence.

## Evidence References

- [IMP-007](../implementation/IMP-007-reciprocal-rank-fusion.md)
- [TEST-003](../test-runs/TEST-003-retrieval-fusion-baseline.md)
