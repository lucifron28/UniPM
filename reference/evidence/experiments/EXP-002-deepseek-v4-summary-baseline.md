---
id: EXP-002
type: experiment
title: DeepSeek V4 source-bounded summary baseline
status: pending
recordedAtUtc: 2026-07-13T04:30:17Z
testedCommit: f4e1b703695f3c3e99428e42e43fb2676f591304
sourceBranch: experiment/deepseek-v4-summary-baseline
evidenceLevel: locally-executed
---

# DeepSeek V4 Source-Bounded Summary Baseline

## Objective

Evaluate `deepseek-v4-flash` in non-thinking mode as the optional real summary
provider for UniPM's authenticated, source-bounded maintenance-review feature.
This experiment evaluates summary generation only. Retrieval ranking, source
selection, recurrence rules, authorization, and maintenance workflows are
controlled existing behavior.

## Execution Identity

- Base commit: `d0acd2f403a625b7dd5bd0fd38b101756b255cbb`
- Automated-test implementation commit:
  `31c24e52ec322ebd24e0ff0a11038f652615c32f`
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
all 12 requests: 5 met the automatic response contract and 7 safely failed it
because no valid generated/citable summary was returned. Latency was 3937.25 ms
minimum, 4430.92 ms median, and 5641.20 ms p95. Human ratings remain pending.

## Per-Language Results

| Language | Cases | Real provider | Manual ratings | Latency |
|---|---:|---|---|---|
| English | 4 | Executed: 2 automatic pass, 2 fail | Pending | 4637.35 ms p50 |
| Tagalog | 4 | Executed: 0 automatic pass, 4 fail | Pending | 4234.47 ms p50 |
| Taglish | 4 | Executed: 3 automatic pass, 1 fail | Pending | 4051.76 ms p50 |

## Per-Case Manual Review

Pass, Partial, or Fail ratings will be assigned only after a real execution and
human comparison of each generated summary against every selected source.

| Case | Language | Evidence condition | Recurrence may be stated | Manual rating |
|---|---|---|---:|---|
| DSV4-EN-001 | English | Same-asset history | Yes | Not executed |
| DSV4-EN-002 | English | Same-asset history | No | Not executed |
| DSV4-EN-003 | English | Same-category fallback | No | Not executed |
| DSV4-EN-004 | English | Same-asset limited evidence | No | Not executed |
| DSV4-TL-001 | Tagalog | Same-asset history | Yes | Not executed |
| DSV4-TL-002 | Tagalog | Same-asset history | Yes | Not executed |
| DSV4-TL-003 | Tagalog | Same-category fallback | No | Not executed |
| DSV4-TL-004 | Tagalog | Same-category fallback | No | Not executed |
| DSV4-TG-001 | Taglish | Same-asset history | Yes | Not executed |
| DSV4-TG-002 | Taglish | Same-asset history | Yes | Not executed |
| DSV4-TG-003 | Taglish | Same-asset limited evidence | No | Not executed |
| DSV4-TG-004 | Taglish | Same-asset injection-resistance case | No | Not executed |

## Runner And Safety Boundary

`scripts/evidence/Invoke-DeepSeekSummaryExperiment.ps1` requires a clean
worktree and a process-scoped API key. It creates a fresh SQL Server/API stack,
applies migrations, seeds fictional data and Development identities, rebuilds
search documents, authenticates an authorized fictional user, performs a
source-only readiness probe, and then sends exactly one real summary request per
case. It verifies HTTP status, source visibility, generated status, the exact
assistive disclaimer, and selected-source citations before retaining sanitized
fictional outputs and hashes. It never writes credentials, JWTs, authorization
headers, connection strings, prompts, token maps, or full provider payloads.

## Artifacts And Hashes

Ignored local artifact directory: `artifacts/evidence/20260713-043017Z-f4e1b703695f-deepseek-v4-summary/`.
It contains preflight and case results, manual-review worksheet, verification
summary, and SHA-256 checksums. Outputs are fictional and sanitized; the
artifacts are intentionally not committed.

## Decision

Keep EXP-002 pending until a human reviewer completes the Pass/Partial/Fail
rubric. Do not infer model quality or production readiness from automatic
contract outcomes or fake-provider tests.

## Limitations

The 12 cases are fictional and small. Even a completed run would not establish
general model quality, production GSD performance, adversarial robustness,
long-term provider stability, or production readiness. Manual review cannot be
delegated to another LLM as the sole evaluator.
