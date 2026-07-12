---
id: EXP-001
type: experiment
title: Lexical SQL Server Full-Text synthetic baseline
status: reviewed
recordedAtUtc: 2026-07-12T01:03:57.2001607+00:00
testedCommit: 798278fc6d5cb0fd5ad7d19d92e694b8bee338a3
sourceBranch: chore/engineering-evidence
evidenceLevel: locally-executed
---

# Lexical SQL Server Full-Text Synthetic Baseline

## Objective

Preserve the first actually executed lexical retrieval result for the current
synthetic dataset and evaluation manifest.

## Evaluation Question

What does the current internal SQL Server Full-Text channel retrieve for the
24-query synthetic manifest when the projection is seeded and Full-Text content
readiness is polled before timed queries?

## Execution Identity

- Tested commit: `798278fc6d5cb0fd5ad7d19d92e694b8bee338a3`
- Branch: `chore/engineering-evidence`
- Channel: lexical only
- Provider key, model key, dimensions, and embedding profile: not applicable
- Provider mode: disabled for this experiment

## Dataset And Manifest

- Operational dataset: `1.1.0`
- Evaluation manifest: `1.1.0`
- Query count: `24`
- Categories: fire extinguisher, fire alarm, emergency light, and water
  drinking station
- Languages: English, Tagalog, and Taglish
- Lexicon version: `1.0.0`
- Projection version: `1.0.0`
- Evaluation data is test-only fictional data.

## Method

The benchmark created a unique temporary SQL Server database, applied existing
migrations, loaded the operational fixture, rebuilt search documents, waited
for Full-Text catalog readiness, then polled an operational fixture-derived
lexical probe until a seeded document was searchable. It then evaluated the
24 manifest queries with result limit `10` and dropped the temporary database.

## Controlled Variables

- Same committed fixture, manifest, lexicon, projection, and channel code.
- No semantic provider, fusion, RRF, score normalization, threshold, or LLM.
- SQL Server local Docker environment.
- Timing is diagnostic only and is not a statistically valid performance test.

## Command

```powershell
dotnet run --project .\tools\UniPM.RetrievalBenchmark --configuration Release --no-build -- --channels lexical --output <artifact-root>\benchmark
```

The command was executed by the committed evidence script. The SQL connection
value was supplied through the process environment and was not recorded.

## Results

| Metric | Value |
|---|---:|
| Query count | 24 |
| Hit@1 | 0.583 |
| Hit@5 | 0.583 |
| Precision@5 | 0.125 |
| Recall@5 | 0.267 |
| Recall@10 | 0.267 |
| MRR | 0.583 |
| Mean first relevant rank | 1 |

## Slice Analysis

### Language

| Language | Queries | Hit@1 | Recall@10 | MRR |
|---|---:|---:|---:|---:|
| English | 10 | 0.500 | 0.320 | 0.500 |
| Tagalog | 7 | 0.714 | 0.231 | 0.714 |
| Taglish | 7 | 0.571 | 0.226 | 0.571 |

### Asset Category

| Category | Queries | Hit@1 | Recall@10 | MRR |
|---|---:|---:|---:|---:|
| Emergency light | 6 | 0.500 | 0.131 | 0.500 |
| Fire alarm | 6 | 0.667 | 0.417 | 0.667 |
| Fire extinguisher | 6 | 0.667 | 0.297 | 0.667 |
| Water drinking station | 6 | 0.500 | 0.222 | 0.500 |

### Scenario

| Scenario tag | Hit@1 | Recall@10 | MRR |
|---|---:|---:|---:|
| cold-start | 0.250 | 0.125 | 0.250 |
| cross-language | 0.643 | 0.229 | 0.643 |
| distractor-resistance | 0.600 | 0.190 | 0.600 |
| lexicon-covered | 0.800 | 0.350 | 0.800 |
| resolved-history | 0.778 | 0.239 | 0.778 |
| same-asset-history | 1.000 | 0.500 | 1.000 |
| same-building-context | 0.667 | 0.192 | 0.667 |
| semantic-paraphrase | 0.400 | 0.247 | 0.400 |
| similar-asset-fallback | 0.571 | 0.207 | 0.571 |
| unresolved-history | 0.600 | 0.285 | 0.600 |

## Weakest Cases

The report's weakest-query section lists Q004, Q006, Q009, Q010, and Q016
with MRR and Recall@5 of zero. Q024 also returned no lexical result in the
executed report; it completed normally and did not abort the benchmark.

## Interpretation

This is a reproducible local synthetic lexical baseline for pipeline review.
It shows current behavior under the stated fixture and query set, not general
production retrieval quality or GSD workflow effectiveness.

## Decision

Preserve this report as an immutable lexical baseline. Use a new experiment ID
for future dataset, lexicon, projection, or retrieval changes. Retrieval fusion
and observability remain separate work.

## Limitations

No real semantic provider was used. No independent lexicon accuracy evaluator
exists. Synthetic data, SQL Server tokenization, local environment timing, and
the current query labels limit generalization. No production records or secrets
are present in the baseline.

## Baseline Artifacts

The sanitized copies are committed under
`../baselines/retrieval-v1.1.0/`:

| File | SHA-256 |
|---|---|
| `EXP-001-lexical-fts-baseline.json` | `7a31f48c667f8b326124c955419fae89b3441323f9475136812c48e599bd7e0d` |
| `EXP-001-lexical-fts-baseline.md` | `6d27e9ecb3039c66d591412c25641846ec0fa1f800689be691bd1ed30644646` |
