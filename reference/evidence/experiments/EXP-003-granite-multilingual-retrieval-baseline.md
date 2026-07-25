---
id: EXP-003
type: experiment
title: Granite multilingual maintenance retrieval baseline
status: executed
recordedAtUtc: 2026-07-25T16:14:14.2693335+00:00
testedCommit: e79b0f5b4a9ecb9c64776eed9f1e48574f75ae0d
sourceBranch: experiment/granite-multilingual-retrieval-baseline
evidenceLevel: locally-executed
---

# Granite Multilingual Maintenance Retrieval Baseline

## Objective

Measure the current lexical, semantic, and RRF-fused maintenance-history
channels with a local, offline Granite embedding service. The benchmark uses
only UniPM's fictional maintenance fixture and test-only evaluation manifest.

## Execution Identity

- Tested commit: `e79b0f5b4a9ecb9c64776eed9f1e48574f75ae0d`
- Dataset and manifest versions: `1.1.0`; 30 search documents and 24 queries
- Database: isolated native SQL Server 2019 database, compatibility level 150,
  with Full-Text Search ready; the temporary database was removed after the run.
- Provider key: `granite-local` (loopback-only local service, no API key)
- Model: `ibm-granite/granite-embedding-97m-multilingual-r2`
- Dimensions and profile: `384`; provider key, full model key, current
  maintenance input profile, and dimensions form the embedding profile.
- Model repository revision: `835ad14087e140460703cf0fae09f97d469d65c2`
- Encoding package fingerprint (SHA-256): `model.safetensors`
  `f3ea88b230492811046145513710e76b4cc8c2ad49e8708da0e7247e548903be`;
  `config.json`
  `933b3105f0a4688d762a2742d3aa103335fd08d8888bc74d52a28aef35494337`;
  `modules.json`
  `e7989e94b5b809d895a9521b708312c1ccd333e183effebaf3838908da2acd53`;
  `1_Pooling/config.json`
  `2d0a5053a404b23e265843108c7013580890de5af4cb0b3933b06468d535052f`;
  `config_sentence_transformers.json`
  `b04e4fb97cb5aa034c609b3d44afba4c1cb73c40ebfc00591d7b4bdf400d7d8c`;
  `sentence_bert_config.json`
  `3852ff8b21e5e81fc6f4da316bde4f54a91e5891d7372d694b6c3ceaa2a3e6d7`;
  `tokenizer.json`
  `4f2842d568e2724370aec203652a42ac783c7937f8347a1a2cc7506d71f1582f`;
  `tokenizer_config.json`
  `99173f13b1b372bcd5656c7a47d7b7ba7cc5701a6ad7a7b13da945da5385680f`;
  and `special_tokens_map.json`
  `5da6758d4a4d592669c66160a0b843715c37e6ed17800be46739a5e33535195a`.
  Together, the revision and package fingerprint identify the CLS-pooling,
  32,768-position, tokenizer, and Sentence Transformers configuration used
  with the retained weights.

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
- Ordinary Release suite: 294 passed, 33 skipped.
- SQL-enabled Release suite with native SQL Server 2019 variables: 326 passed,
  1 optional external-provider smoke test skipped.
- Local Python contract suite: 6 passed.
- Local OpenAI-compatible smoke test: 1 passed, 0 skipped.

The previously observed cross-suite observability failure was reproduced and
resolved by serializing only the two test classes that create the shared
`UniPMMetrics` meter; runtime observability code was unchanged. Benchmark timing
is diagnostic only: provider duration was 2,267.131 ms. Per-query end-to-end
latency is retained: lexical median/p95 was 7.196/26.683 ms, semantic
6.085/14.374 ms, and fused 133.630/247.830 ms. Semantic and fused retrieval each
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
| `baselines/granite-multilingual-v1/retrieval-benchmark.json` | `b1ffe7532ac10c93e3a39e8a20547ad09b501216dde0ec8238be9fa540f627da` |
| `baselines/granite-multilingual-v1/retrieval-benchmark.md` | `ff66b68adbce55d81061911b853e040042bd23e6932edb50a035890f444e5f1b` |
