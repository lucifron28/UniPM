---
id: IMP-007
type: implementation
title: Internal reciprocal-rank fusion retrieval
status: reviewed
recordedAtUtc: 2026-07-12T10:34:23Z
sourceBranch: feat/retrieval-fusion
testedCommit: 58668f963b5cc3aa2b835e8d1f2f985bdf4a3581
evidenceLevel: source-inspected
---

# Internal Reciprocal-Rank Fusion

## Objective

Combine the existing lexical and semantic maintenance-history channels for
future source selection without adding a public review endpoint, context
scoring, sanitization, or summary generation.

## Implementation Summary

The internal `IFusedMaintenanceRetriever` validates a bounded fused request,
executes the existing metrics-decorated lexical and semantic channels, and
returns one inspectable response. `ReciprocalRankFusion` deduplicates by
inspection ID, preserves source metadata and component ranks, and applies:

```text
fusionScore = sum(1 / (60 + oneBasedRank))
```

The initial reproducible parameter is RRF `K=60`. The default fused output is
10 results, the default per-channel candidate depth is 20, and both are capped
at 100. Final ordering is fusion score descending, matched channel count
descending, best component rank ascending, inspection date descending, and
inspection ID ascending.

## Contracts And Degradation

Fused results retain `FusionScore`, `LexicalRank`, `SemanticRank`,
`RawLexicalRank`, `RawSemanticScore`, and `MatchedChannelCount`. Matching
inspection IDs must have identical stable source metadata across channels;
conflicts fail as a data-integrity error.

Lexical validation, availability, and execution failures fail the fused
operation. Semantic validation fails the operation, while semantic unavailable,
execution, or data failures return lexical-only results with `IsDegraded=true`
and a bounded channel status. Semantic empty is not degradation. If both
channels return no results, the response is empty and non-degraded.

## Metrics And Benchmark

The outer fused metrics decorator records channel `fused` with bounded outcomes
`success`, `empty`, `degraded`, `validation_error`, `unavailable`, `failure`,
and `cancelled`. It records no query, provider, model, source ID, or exception
details.

The benchmark CLI accepts `fused` in any valid lexical/semantic combination.
Fused selection initializes both underlying channels once, uses the production
RRF implementation, serializes component ranks and fusion metadata, and fails
when the response is degraded. Benchmark format version `1.1.0` is used for
the optional fusion fields. Deterministic provider tests validate orchestration
only and are not model-quality evidence.

## Boundaries And Limitations

- No public HTTP route is added.
- No database migration or persistence change is added.
- Raw lexical and semantic scores are never compared or added.
- No maintenance-context, issue-key, same-asset, location, or recency boosts
  are applied.
- No thresholds, insufficient-evidence policy, source selection, sanitization,
  prompt construction, summaries, LLM calls, authentication, or client work
  are included.
- A real fused quality baseline remains pending a configured real provider.

## Related Evidence

- [ADR-004](../decisions/ADR-004-reciprocal-rank-fusion.md)
- [TEST-003](../test-runs/TEST-003-retrieval-fusion-baseline.md)
