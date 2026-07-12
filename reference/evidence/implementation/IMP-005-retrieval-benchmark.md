---
id: IMP-005
type: implementation
title: Reproducible retrieval benchmark
status: reviewed
recordedAtUtc: 2026-07-12T01:10:00Z
sourceBranch: main
evidenceLevel: source-inspected
---

# Reproducible Retrieval Benchmark

## Objective

Measure lexical and semantic retrieval channels separately on fictional,
versioned maintenance data without adding fusion, an endpoint, or LLM behavior.

## Source Identity

- Source branch: `main` at merge commit `00e5401`.
- Relevant commits: `bfb50c1`, `2837e40`, `b5b84af`, `0f750ad`, `c51a04b`, and
  `b5bbf69`.
- Implementation date from Git history: 2026-07-12.
- Source paths: `tools/UniPM.RetrievalBenchmark/`, the test-only evaluation
  manifest under `tests/UniPM.Api.Tests/Retrieval/Fixtures/`, and benchmark
  tests.

## Implementation Summary

The tool validates a strict 1.1.0 evaluation manifest, creates a temporary SQL
Server database, applies migrations, seeds the operational fixture, rebuilds
the projection, waits for Full-Text content readiness, optionally indexes
embeddings, calculates fixed-depth retrieval metrics, and writes JSON/Markdown
reports.

## Architecture And Contracts

The evaluator passes only query text, filters, and a result limit to channels.
Expected IDs stay in the test-only manifest and are not supplied to retrievers.
Duplicate result IDs are execution errors. No fusion, score normalization,
threshold policy, or summary generation is implemented.

## Important Files

- `tools/UniPM.RetrievalBenchmark/Program.cs`
- `tools/UniPM.RetrievalBenchmark/SqlServerBenchmarkRunner.cs`
- `tools/UniPM.RetrievalBenchmark/RetrievalEvaluationManifestLoader.cs`
- `tools/UniPM.RetrievalBenchmark/RetrievalMetrics.cs`
- `tools/UniPM.RetrievalBenchmark/BenchmarkReportWriter.cs`
- `tests/UniPM.Api.Tests/Retrieval/RetrievalBenchmarkSqlServerTests.cs`

## Database Changes

The benchmark uses a uniquely named temporary SQL Server database and drops it
by default. It does not add migrations or alter production schema.

## Tests Present

The source tree contains manifest, strict-loader, metric, evaluator/report,
lexical SQL, and deterministic semantic SQL pipeline tests. Historical branch
execution is not claimed in this chronology record.

## Verification Status

Source-inspected chronology. A current lexical run and SQL Server verification
are recorded in `TEST-001` and `EXP-001`. No real semantic provider run was
available.

## Known Limitations

The 24-query manifest is synthetic and test-only. Its metrics do not prove
production GSD performance. No independent lexicon accuracy evaluator,
semantic model-quality baseline, or fused retrieval baseline exists.

## Related Evidence

- [TEST-001](../test-runs/TEST-001-current-backend-baseline.md)
- [EXP-001](../experiments/EXP-001-lexical-fts-baseline.md)
