---
id: TEST-144
type: test-run
title: Live DeepSeek V3 post-correction development comparison
status: executed
recordedAtUtc: 2026-10-23T08:15:08.4196914Z
testedCommit: 1c81400d0e92a9da0bcf92bdc2456d52843ef8ad
sourceBranch: feature/nla-deepseek-validation-fix
evidenceLevel: real-provider-executed
---

# Live DeepSeek V3 post-correction development comparison

## Authorization and fixed conditions

The researcher requested implementation of the recorded fixes and a rerun.
The [committed verification protocol](../../evaluation/manuscript-2026-10-23/deepseek-correction-verification-protocol.md)
authorized one new Development comparison, at most 18 actual HTTP requests,
no retries, and a US$1 conservative estimated ceiling. The live run began from
a clean checkout of the tested commit above. It includes the regression-checked
changes from `e379baebb6c2a513a6dc5c5a4d8f96956de1e1b6` and the final source
review refinements described in TEST-143.

The frozen 18-question corpus, labels, 69 assertions, exact comparator, fixed
June 1, 2025 interpretation clock, model alias `deepseek-flash`, temperature
zero, disabled thinking, 1024 output cap and fictional InMemory catalog were
unchanged from TEST-142. The corpus hash remains
`dd313076a40ea5dd8eddfef610ff8db37ca6c5ba1934a286c6023a1586ca0e25`.
The new prompt hash is
`ef3c2b23908571a469ca8c540be007468e875f2fd597769de0f31fcaca6a3473`.

This is verification on the same development questions used to formulate the
fixes. It is not an independent or held-out final evaluation. No failed case
was retried, label changed, score adjusted, or original evidence replaced.

## Commands and outcome

```powershell
dotnet build tools/UniPM.PmAnalytics.V3Evaluation/UniPM.PmAnalytics.V3Evaluation.csproj --no-restore --nologo -v minimal
dotnet tools/UniPM.PmAnalytics.V3Evaluation/bin/Debug/net10.0/UniPM.PmAnalytics.V3Evaluation.dll deepseek artifacts/deepseek-v3-corrected-2026-10-23
```

Build exit 0, zero warnings and errors. Live evaluation exit 1 because one
case returned `InvalidOutput:InvalidEnvelope`. All 18 cases were attempted.
The run began at `2026-10-23T08:14:45.1692595Z` and ended at
`2026-10-23T08:15:08.4196914Z`. Seventeen requests received successful HTTP
responses. The SQL-injection question was rejected before a provider call.
There were no transport/provider HTTP failures or retries, but there was one
model interpretation validation error. These are different error categories.

The existing evaluator, request limits and exact comparison logic were not
changed for this rerun. The actual interpreter included sanitization, safety
checks, model parsing, constraints, validation and catalog resolution. No
proposed report was previewed or executed.

## Before and after

| Measure | TEST-142 | TEST-144 |
|---|---|---|
| Full-system cases matching all declared expectations | 13/18, 72.22% | 16/18, 88.89% |
| Classification matches | 16/18, 88.89% | 17/18, 94.44% |
| Clarification-field set matches | 15/18 | 17/18 |
| Plan-disposition matches | 16/18 | 17/18 |
| Declared final plan assertion matches | 67/69 | 62/69 |
| Typed model candidates matching all declared expectations | 9/17 | 11/17 |
| Typed candidate assertion matches | 68/69 | 69/69 |
| Unexpected executable final interpretations on clarification cases | 1 | 0 |
| Interpretation validation errors | 0 | 1, V3D-011 |
| Actual HTTP requests / retries | 17 / 0 | 17 / 0 |

Among the 17 provider-exposed cases, final exact matches were 15/17. The
additional full-system match was the pre-provider safety rejection. It must
not be counted as a model response. A high typed-candidate field score does
not establish valid classification or an acceptable response envelope.

V3D-005 now returned Valid with the declared records scope. V3D-007 returned
Period clarification with the expected completedCount draft. V3D-015 returned
the canonical Department clarification. Each matched all original expectations.

Two cases remain unmatched:

- V3D-006 still returns the canonical NORTH BLOCK catalog value rather than
  the expected pre-resolution North Block spelling. Its model candidate
  matches. The original strict normalization-boundary mismatch is retained.
- V3D-011 returned a typed candidate with status Valid, clarificationFields
  containing Top, top=null, and a question asking which measure should rank
  the departments. These fields contradict the Valid envelope contract. The
  existing envelope validator rejected it with `InvalidOutput:InvalidEnvelope`
  before a final response plan was produced. The case did not satisfy the
  expected NeedsClarification outcome and remains a failure. It no longer
  produced an accepted guessed ranking in this run.

All six declared plan assertions for V3D-011 matched the typed candidate, but
zero matched a final plan because none was returned. Together with the one
building-value mismatch, this explains the final assertion score of 62/69.
The score must not be raised by substituting candidate fields for the rejected
final response. The lower assertion score and improved case score measure
different aspects and are reported together.

The local controlled regression demonstrates rejection of structurally valid
guessed rankings. The live case demonstrates safe rejection of a contradictory
model envelope, not successful recovery to a clarification. Further work on
that response-consistency failure remains open. No relaxation of envelope
validation, additional fix, repeated xUnit run or repeated provider run followed
this result, under the repository execution limits.

## Identity, usage and artifacts

| Item | SHA-256 |
|---|---|
| Evaluator assembly | `3584949b2013cfa8f9bc46ca6d001d340007947037457298d061fd7f55dd877f` |
| Backend assembly | `c47df684877993891dd3648babce52a21940841a5bb04e2cfea5694ed85885bc` |
| Raw local report | `c507fd48a6d82a71294a096ed68890caf5f6789570c69c43ab67f5336f2041c6` |
| Sanitized observations JSON | `a18be492d1ee76b15dbeee7acc195b414e0f0d33afb0f5f2282eb4017fdbccfb` |
| Reviewed case CSV | `7226c58f0f2dc5919e5b7c5234ff9627a817713dabc457ae4263a6edb241fd7d` |

Provider usage was complete for all 17 HTTP responses: 70,181 input tokens and
2,695 output tokens. The conservative peak uncached estimate was
US$0.0242883. Total reservation was US$0.1707396, below the US$1 estimated
ceiling. This is not an invoice. Rates and their official source are retained
in the protocol and identity. Nearest-rank provider latency was p50 1,250.79 ms
and p95 2,075.69 ms over 17 calls, not browser task timing.

Raw safe logs remain ignored under
`artifacts/deepseek-v3-correction-2026-10-23/`. Identity, typed observations,
report and checksums remain ignored under
`artifacts/deepseek-v3-corrected-2026-10-23/`. No raw provider payloads,
prompts, credentials or token maps were persisted. Reviewed data contains
synthetic questions and the fictional lookup catalog; free-text model detail
fields are omitted from committed observations.

See the [case comparison](../../evaluation/manuscript-2026-10-23/deepseek-corrected-case-comparison.csv),
[sanitized observations](../../evaluation/manuscript-2026-10-23/deepseek-corrected-observations.json),
and [manuscript comparison](../../evaluation/manuscript-2026-10-23/manuscript-deepseek-correction-results.md).
TEST-142 remains the earlier formative baseline.

No SQL Server, numerical calculation, browser, respondent, physical-device,
deployment or full-suite verification ran. This record does not establish
field acceptance, unseen-question accuracy, model repeatability or complete
resolution of the clarification workflow.
