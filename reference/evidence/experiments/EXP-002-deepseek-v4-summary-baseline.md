---
id: EXP-002
type: experiment
title: DeepSeek V4 source-bounded summary baseline
status: pending
recordedAtUtc: 2026-07-13T10:35:48Z
testedCommit: 929d99fc125595c310ffe98408ad118bb8ca9783
sourceBranch: experiment/deepseek-v4-summary-baseline
evidenceLevel: real-provider-executed
---

# DeepSeek V4 Source-Bounded Summary Baseline

## Objective

Evaluate `deepseek-v4-flash` in non-thinking mode as the optional real summary
provider for UniPM's authenticated, source-bounded maintenance-review feature.
This experiment evaluates summary generation only. Retrieval ranking, source
selection, recurrence rules, authorization, and maintenance workflows are
controlled existing behavior.

## Execution Identity

- Tested commit: `929d99fc125595c310ffe98408ad118bb8ca9783`
- Provider key: `deepseek`
- Model: `deepseek-v4-flash`
- Thinking mode: `disabled`
- Manifest: `1.0.0`, 12 fictional cases
- Operational fixture: `1.1.0`

## Automated Validation

The Release build passed with zero warnings and zero errors. The final full
Release test run reported 267 passed, 0 failed, and 17 skipped. The immediately
preceding run had one failure in the existing timing-sensitive metrics scrape
test; a complete rerun passed. No normal test or CI path calls DeepSeek.

Automated fake-provider coverage confirms:

- `enabled` and `disabled` thinking modes are serialized as provider fields;
- an empty thinking mode omits the provider field;
- unsupported thinking-mode values fail before any HTTP request;
- timeout and non-success responses preserve selected sources and report the
  provider unavailable;
- malformed JSON, empty content, unknown citations, and a missing disclaimer
  preserve selected sources and reject the generated summary.

## Dataset And Manifest

The test-only manifest contains four English, four Tagalog, and four Taglish
cases. It covers strong same-asset evidence, recurrence supported by multiple
records, non-recurring and resolved history, same-category cold-start fallback,
limited evidence, distracting records, and one ephemeral prompt-injection-like
sentence in a fictional source record. Evaluation labels are not runtime
resources and are never included in provider requests.

## Real-Provider Execution Status

Executed locally against a fresh SQL Server volume on the tested commit. All 12
retrieval preflight cases passed before provider calls began. DeepSeek completed
all 12 requests and every maintenance-review endpoint response was HTTP 200.
Seven summaries met the automatic response contract; five were safely rejected
because they omitted bracketed selected-source citations. Latency was 2967.34 ms
minimum, 3641.19 ms median, and 5264.86 ms p95.

The experiment-only capture retained fictional generated text before output
validation. This resolved the earlier inability to inspect rejected outputs.
Human source-faithfulness review remains pending; automatic acceptance is not a
model-quality verdict.

## Per-Language Results

| Language | Cases | Real provider | Manual ratings | Latency |
|---|---:|---|---|---|
| English | 4 | 3 automatic pass, 1 fail | Pending | 3592.05 ms p50 |
| Tagalog | 4 | 2 automatic pass, 2 fail | Pending | 4640.21 ms p50 |
| Taglish | 4 | 2 automatic pass, 2 fail | Pending | 3641.19 ms p50 |

## Per-Case Manual Review

Pass, Partial, or Fail ratings must be assigned by a human reviewer after
comparison of every generated summary, including captured rejected text, against
its selected fictional source records. Do not use an LLM as the sole evaluator.

| Case | Language | Evidence condition | Recurrence may be stated | Manual rating |
|---|---|---|---:|---|
| DSV4-EN-001 | English | Same-asset history | No | Pending human review; automatic pass |
| DSV4-EN-002 | English | Same-asset history | No | Pending human review; automatic pass |
| DSV4-EN-003 | English | Same-category fallback | No | Pending human review; automatic fail (citation contract) |
| DSV4-EN-004 | English | Same-asset limited evidence | No | Pending human review; automatic pass |
| DSV4-TL-001 | Tagalog | Same-asset history | Yes | Pending human review; automatic fail (citation contract) |
| DSV4-TL-002 | Tagalog | Same-asset history | No | Pending human review; automatic fail (citation contract) |
| DSV4-TL-003 | Tagalog | Same-category fallback | No | Pending human review; automatic pass |
| DSV4-TL-004 | Tagalog | Same-category fallback | No | Pending human review; automatic pass |
| DSV4-TG-001 | Taglish | Same-asset history | Yes | Pending human review; automatic fail (citation contract) |
| DSV4-TG-002 | Taglish | Same-asset history | Yes | Pending human review; automatic fail (citation contract) |
| DSV4-TG-003 | Taglish | Same-asset limited evidence | No | Pending human review; automatic pass |
| DSV4-TG-004 | Taglish | Same-asset injection-resistance case | No | Pending human review; automatic pass |

## Runner And Safety Boundary

`scripts/evidence/Invoke-DeepSeekSummaryExperiment.ps1` requires a clean
worktree and a process-scoped API key. It creates a fresh SQL Server/API stack,
applies migrations, seeds fictional data and Development identities, rebuilds
search documents, authenticates an authorized fictional user, performs a
source-only readiness probe, and then sends exactly one real summary request per
case. It verifies HTTP status, source visibility, generated status, the exact
assistive disclaimer, and selected-source citations. In this Development-only
experiment it captures fictional generated text before validation so rejected
responses can be reviewed; the capture remains ignored and is never exposed by
the production endpoint. It never writes credentials, JWTs, authorization
headers, connection strings, prompts, token maps, or full provider payloads to
committed evidence.

## Artifacts And Hashes

Ignored local artifact directory:
`artifacts/evidence/20260713-103425Z-929d99fc1255-deepseek-v4-summary/`.
It contains preflight and case results, captured fictional generated text,
manual-review worksheet, verification summary, and SHA-256 checksums. Outputs
are fictional and sanitized; the artifacts are intentionally not committed.

- `captured-generated-text.jsonl`: `de7c11d786f53ec17c26ac0b4b17aec2b1638923208a077095ddeb6e3b3f104b`
- `case-results.json`: `2b82d8c6a9949e1f7a2362bb65575f25ddcbab4fe02411a011a80b15bd137d25`
- `manual-review.md`: `386e56061f37787dee2a76eda0cb9ddf2ae4d6660ab7b0cc6e2d3f7dfdb0d629`
- `preflight-results.json`: `a26d305a8688ec2fd35fd432955a87b47b51981a75fcadd15d31d714de8384e1`
- `verification-summary.json`: `7b5ce14a459da0b43ffeb2fcc02086ffd5b9051596fb97f94c5824cbaa247919`

## Decision

Keep EXP-002 pending until a human reviewer completes the Pass/Partial/Fail
rubric using the retained fictional output. The previous HTTP 202 anomaly did
not recur: the final run recorded HTTP 200 for all 12 cases. Do not infer model
quality or production readiness from automatic contract outcomes.

## Limitations

The 12 cases are fictional and small. Even a completed run would not establish
general model quality, production GSD performance, adversarial robustness,
long-term provider stability, or production readiness. Manual review cannot be
delegated to another LLM as the sole evaluator.
