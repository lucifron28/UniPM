---
id: EVIDENCE-INDEX
type: index
title: UniPM engineering evidence index
status: reviewed
evidenceLevel: source-inspected
---

# Engineering Evidence Index

This index lists committed engineering evidence. Claims are bounded by the
record's evidence level and tested/source commit.

## Records

| ID | Type | Title | Status | Evidence level | Tested/source commit | Record | Note |
|---|---|---|---|---|---|---|---|
| IMP-001 | implementation | Synthetic maintenance dataset and Development seeder | reviewed | source-inspected | `00e5401` | [record](implementation/IMP-001-synthetic-maintenance-dataset.md) | Fictional deterministic fixture and scoped seed/reset behavior. |
| IMP-002 | implementation | Versioned maintenance issue lexicon | reviewed | source-inspected | `00e5401` | [record](implementation/IMP-002-maintenance-issue-lexicon.md) | Deterministic category-bounded normalization; no accuracy baseline claimed. |
| IMP-003 | implementation | SQL Server lexical Full-Text retrieval | reviewed | source-inspected | `00e5401` | [record](implementation/IMP-003-lexical-full-text-retrieval.md) | Internal SQL Server FTS channel over the rebuildable projection. |
| IMP-004 | implementation | Provider-neutral semantic retrieval | reviewed | source-inspected | `00e5401` | [record](implementation/IMP-004-semantic-retrieval.md) | Provider abstraction and persisted document embeddings. |
| IMP-005 | implementation | Reproducible retrieval benchmark | reviewed | source-inspected | `00e5401` | [record](implementation/IMP-005-retrieval-benchmark.md) | Test-only synthetic lexical/semantic evaluation tool. |
| ADR-001 | decision | Use SQL Server Full-Text Search for lexical retrieval | reviewed | source-inspected | `00e5401` | [record](decisions/ADR-001-sql-server-full-text-retrieval.md) | Stack-constrained architecture decision. |
| ADR-002 | decision | Keep semantic embeddings behind a provider-neutral service | reviewed | source-inspected | `00e5401` | [record](decisions/ADR-002-provider-neutral-embeddings.md) | Provider, privacy, cost, and degradation boundaries. |
| TEST-001 | test-run | Current backend verification baseline | executed | locally-executed | `899ea5e` | [record](test-runs/TEST-001-current-backend-baseline.md) | Clean-worktree restore/build/full tests, SQL scope, and lexical benchmark executed. |
| EXP-001 | experiment | Lexical SQL Server Full-Text synthetic baseline | reviewed | locally-executed | `899ea5e` | [record](experiments/EXP-001-lexical-fts-baseline.md) | Immutable 24-query lexical baseline with sanitized reports. |
| IMP-006 | implementation | Optional OpenTelemetry metrics and local system-health monitoring | reviewed | source-inspected | `c4fc0df` | [record](implementation/IMP-006-observability-metrics.md) | Opt-in technical metrics and optional local Prometheus/Grafana infrastructure. |
| ADR-003 | decision | Use optional OpenTelemetry, Prometheus, and Grafana for technical health metrics | reviewed | source-inspected | `c4fc0df` | [record](decisions/ADR-003-prometheus-grafana-observability.md) | Low-cardinality local monitoring boundary; tracing, logs, and alerting deferred. |
| TEST-002 | test-run | Observability metrics and local monitoring baseline | executed | locally-executed | `2766f1b` | [record](test-runs/TEST-002-observability-baseline.md) | Backend and local Compose observability proof with sanitized artifacts and hashes. |

## Pending Evidence

- Real semantic model-quality baseline: pending a configured real provider and
  a retained real-provider benchmark run.
- Lexicon normalization accuracy baseline: pending an independent labeled
  dataset and executable precision/recall/F1 evaluator.
- Fused retrieval baseline: pending retrieval fusion implementation and an
  executed evaluation.
