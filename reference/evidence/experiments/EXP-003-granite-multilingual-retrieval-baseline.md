---
id: EXP-003
type: experiment
title: Granite multilingual maintenance retrieval baseline
status: executed
recordedAtUtc: 2026-07-25T15:31:33.7837853+00:00
testedCommit: 8f91e04a1ada74e26ecef79d41a44e2b7c4f5b76
sourceBranch: experiment/granite-multilingual-retrieval-baseline
evidenceLevel: locally-executed
---

# Granite Multilingual Maintenance Retrieval Baseline

## Objective

Measure the current lexical, semantic, and RRF-fused maintenance-history
channels with a local, offline Granite embedding service. The benchmark uses
only UniPM's fictional maintenance fixture and test-only evaluation manifest.

## Execution Identity

- Tested commit: `8f91e04a1ada74e26ecef79d41a44e2b7c4f5b76`
- Dataset and manifest versions: `1.1.0`; 30 search documents and 24 queries
- Database: isolated native SQL Server 2019 database, compatibility level 150,
  with Full-Text Search ready; the temporary database was removed after the run.
- Provider key: `granite-local` (loopback-only local service, no API key)
- Model: `ibm-granite/granite-embedding-97m-multilingual-r2`
- Dimensions and profile: `384`; provider key, full model key, current
  maintenance input profile, and dimensions form the embedding profile.
- Artifact SHA-256: `f3ea88b230492811046145513710e76b4cc8c2ad49e8708da0e7247e548903be`
- Model revision: not retained in the locally cached model metadata; the
  artifact hash above identifies the model material actually used.

## Encoding And Local Service

The local experiment-only Python service bound to `127.0.0.1` and implemented
the existing OpenAI-compatible `POST /v1/embeddings` contract. Source-inspected
model configuration selects CLS pooling (`last_hidden_state[:, 0]`) followed by
L2 normalization. Its Sentence Transformers configuration declares empty query
and document prompts, so no task prefix was added. Maximum model context is
32,768 tokens; the service applied its configured input-size guard before
tokenization. Inference used `model.eval()` and `torch.inference_mode()`.

The service returned ordered, finite, normalized 384-dimensional vectors. The
handler-level contract tests passed (6), and the existing adapter smoke test passed (1) with
the local endpoint, full model identity, and 384 configured dimensions. The
runtime was CPU-only with Python `3.14`, Torch `2.13.0+cpu`, Transformers
`5.14.1`, and tokenizers `0.22.2`; no device name, username, endpoint, or
absolute path is retained.

## Method

The benchmark applied migrations to an isolated database, seeded the fictional
fixture, rebuilt `MaintenanceSearchDocument`, and indexed Granite document
embeddings. It then ran unchanged lexical, semantic, and RRF-fused evaluation
with result limit 10, RRF K 60, and candidate limit 20. The run planned and
made 25 local inference requests: 2 document batches plus 23 distinct query
embeddings. Query vectors were reused across semantic and fused evaluation,
were never persisted, and the Granite profile excluded stale Voyage,
deterministic, mismatched-dimension, and stale-source vectors.

## Results

| Channel | Hit@1 | Hit@5 | Precision@5 | Recall@5 | Recall@10 | MRR |
|---|---:|---:|---:|---:|---:|---:|
| Lexical | 0.583 | 0.583 | 0.125 | 0.267 | 0.267 | 0.583 |
| Granite semantic | 1.000 | 1.000 | 0.458 | 0.965 | 1.000 | 1.000 |
| RRF fused | 1.000 | 1.000 | 0.458 | 0.965 | 1.000 | 1.000 |

Semantic retrieval improved over lexical for Q004, Q006, Q009, Q010, Q016,
Q017, Q018, Q020, Q023, and Q024. No query had lexical reciprocal rank higher
than semantic. Fusion did not improve or regress the aggregate metrics relative
to semantic retrieval on this compact fixture.

### Language Slices

| Channel | English Recall@5 | Tagalog Recall@5 | Taglish Recall@5 |
|---|---:|---:|---:|
| Lexical | 0.320 | 0.231 | 0.226 |
| Granite semantic | 1.000 | 1.000 | 0.879 |
| RRF fused | 1.000 | 1.000 | 0.879 |

All semantic and fused language slices had Hit@1 and MRR of 1.000. Taglish
remained the weakest semantic slice by Recall@5, although Recall@10 was 1.000.

### Category And Scenario Observations

Semantic/fused Recall@5 was 0.958 for emergency lights, 1.000 for fire alarms,
0.900 for fire extinguishers, and 1.000 for water drinking stations. The
lowest scenario Recall@5 was distractor resistance (0.830), then cold start
(0.850); semantic paraphrase reached 0.960. Complete category and scenario
tables are retained in the reviewed Markdown baseline.

## Verification

- `dotnet restore .\UniPM.slnx`: passed.
- `dotnet build .\UniPM.slnx -c Release --no-restore`: passed.
- Ordinary Release suite: 294 passed, 32 skipped.
- SQL-enabled Release suite with native SQL Server 2019 variables: 325 passed,
  1 optional external-provider smoke test skipped.
- Local Python contract suite: 6 passed.
- Local OpenAI-compatible smoke test: 1 passed, 0 skipped.

The previously observed cross-suite observability failure was reproduced and
resolved by serializing only the two test classes that create the shared
`UniPMMetrics` meter; runtime observability code was unchanged. Benchmark timing
is diagnostic only: provider duration was 1,904.229 ms. Per-query end-to-end
latency is retained: lexical median/p95 was 13.800/36.212 ms, semantic
5.854/12.298 ms, and fused 79.663/207.522 ms. Semantic and fused retrieval each
scored 93 eligible candidates across 24 queries; the 500-candidate cap was not
reached.

## Result And Limitations

**CONDITIONAL PASS.** Granite integration, profile isolation, SQL Server 2019
indexing, semantic evaluation, and fused evaluation completed reproducibly for
this controlled fixture. The result is not evidence of real institutional
maintenance performance, production readiness, a required Python deployment
component, or independent semantic-quality validation. It uses one local CPU
run, a small fictional corpus, existing relevance labels, and no human review
of retrieved results. No raw vectors, model weights, secrets, real records, or
external provider payloads were retained.

## Reviewed Baseline Artifacts

| File | SHA-256 |
|---|---|
| `baselines/granite-multilingual-v1/retrieval-benchmark.json` | `35ff1c99220605dfc9aeaf4f671b5f4777ec487bd72fa671296eb2f736991cb4` |
| `baselines/granite-multilingual-v1/retrieval-benchmark.md` | `8b19b434cdbe02e6f055e4733ef90ddc583506babd781a5355edc2bed912b234` |
