---
id: TEST-146
type: test-run
title: Live DeepSeek ranking recovery and catalog-equivalence development comparison
status: executed
recordedAtUtc: 2026-10-23T12:56:06.6245664Z
testedCommit: 8a5966eff5e48ec826ed1501c67e155f31899ea2
sourceBranch: feature/nla-clarification-recovery
evidenceLevel: real-provider-executed
---

# Live DeepSeek ranking recovery development comparison

## Authorization and predeclared conditions

The researcher authorized executing the clarification recovery correction
plan in chat on October 23, 2026. The
[versioned protocol](../../evaluation/manuscript-2026-10-23/catalog-equivalence-v1-protocol.md)
was committed before this measurement. It authorizes one Development run,
at most 18 actual HTTP requests, no retries, a US$1 conservative estimated
ceiling, no report execution, and separate strict/catalog-equivalence scores.

The 18 questions, researcher-reviewed expectations and 69 declared plan
assertions were unchanged. Corpus SHA-256 remains
`dd313076a40ea5dd8eddfef610ff8db37ca6c5ba1934a286c6023a1586ca0e25`.
Keep the disclosures about earlier V3D-008/009 development-label revisions.
The fixed June 1, 2025 interpretation clock, fictional one-asset catalog,
explicit DeepSeek selection, deepseek-flash alias, temperature zero, disabled
thinking and 1024-token output cap were unchanged from TEST-144. The V3 prompt
hash also remained unchanged:
`ef3c2b23908571a469ca8c540be007468e875f2fd597769de0f31fcaca6a3473`.

The backend recovery is narrow and independently checks the requested scope.
The original typed model candidate is captured before recovery. A recovered
response is system behavior, not a correct original model response. Catalog
equivalence is auxiliary scoring only; production normalization is unchanged.

## Command and execution

After TEST-145 compiled and passed the focused checks, the clean committed
checkout ran:

```powershell
dotnet tools/UniPM.PmAnalytics.V3Evaluation/bin/Debug/net10.0/UniPM.PmAnalytics.V3Evaluation.dll deepseek artifacts/deepseek-v3-recovery-2026-10-23
```

Exit 0. The run started `2026-10-23T12:55:35.277353Z` and ended
`2026-10-23T12:56:06.6245664Z`. All 18 cases were processed. Seventeen reached
DeepSeek and returned successful HTTP responses. The SQL-injection request
was rejected by the backend before transmission. There were zero retries,
provider failures, interpretation errors or unexpected executable final
interpretations on clarification cases. No proposed report was previewed or
executed. No case or live run was repeated.

## Results and observation stages

| Measure | TEST-144, strict original rule | TEST-146, strict original rule | TEST-146, auxiliary catalog equivalence v1 |
|---|---|---|---|
| All declared expectations matched | 16/18, 88.89% | 17/18, 94.44% | 18/18, 100% |
| Classification matches | 17/18 | 18/18 | 18/18 |
| Clarification-field set matches | 17/18 | 18/18 | 18/18 |
| Plan-disposition matches | 17/18 | 18/18 | 18/18 |
| Declared final plan assertions matched | 62/69 | 68/69 | 69/69 |
| Interpretation errors | 1 | 0 | Same execution, 0 |

The raw typed model candidates matched all declared expectations in 11/17
provider-exposed cases, 64.71%, with status matches in 11/17 and assertion
matches in 69/69. These are the same aggregate candidate scores as TEST-144.
The improvement in the final ranking outcome must not be described as perfect
DeepSeek accuracy or attributed to retraining/prompt changes.

Among provider-exposed cases, strict final exactness was 16/17 and auxiliary
exactness 17/17. The additional full-system case was the pre-provider safety
rejection, not a model response. Auxiliary results were not computed for old
runs or substituted into their original records.

### V3D-011 recovery

The live model again returned a contradictory candidate: status Valid,
clarificationFields containing Top, top=null, with scheduledCount and
completedCount grouped by department for 2024. The backend independently
confirmed the same question scope and the missing ranking criterion. It
returned NeedsClarification, Top, and code RecoveredRankingClarification,
preserving the safe draft without selecting a ranking. All six declared
assertions and the status/clarification/disposition expectations now matched.

Exactly one live case used this recovery. The original candidate remains in
the supporting JSON with its incorrect status, keeping model and backend
results separate. The runtime interpretation correction is therefore visible
in this run rather than inferred solely from a controlled test. The web Top
fallback was not exercised in a browser, and no ranking selector was added.

### V3D-006 catalog equivalence

The strict final response still contains NORTH BLOCK instead of North Block,
so its original exact filter-array assertion remains unmatched. The auxiliary
rule recognizes that both values refer to the same known building in the
fixed one-asset catalog. It leaves all other fields, operators and arrays
unchanged. This accounts for the sole difference between 17/18 strict and
18/18 auxiliary exactness. It is a scoring-boundary distinction, not a changed
model answer, changed label or altered database lookup behavior.

The auxiliary rule accepts only known building/department values declared by
the fixed fixture. Location is null and unknown values are not folded. It
does not apply case-insensitive comparison to arbitrary strings or contract
identifiers. Its complete definition was frozen before execution, with hash
`e7e66ec5fdc78b55a91aafcfb22a547a667ce108ecd316a6a8d760a47e59bde7`.
Protocol-file hash was
`c6ef12ec8b61becb04ee03509a9df224002ab5430f52d136a0445be4c9301e1e`.

## Usage and provenance

Provider-reported model alias was deepseek-flash and fingerprint
`e72d3c7f663d3e1383b00b3884cb4501`. Usage was complete for all 17 responses:
70,181 input tokens and 2,708 output tokens. Using conservative peak uncached
rates checked against the
[official pricing page](https://api-docs.deepseek.com/quick_start/pricing/)
on October 23, 2026, the upper estimate was US$0.0243039. Pre-request
reservations totaled US$0.1707396, below the US$1 estimated ceiling. This is
not a verified invoice. Nearest-rank provider latency was p50 1,502.15 ms and
p95 3,951.56 ms over 17 calls, not browser task timing.

| Item | SHA-256 |
|---|---|
| Evaluator assembly | `c8bed3cb16dd6c0e00ba901bd8d4e063b04af3b5777b618b6ab53f8f2e002639` |
| Backend assembly | `8ed24ffd56f768ea21d31dc8dd058b88ce17a1bd2c6088ee6b30312434d3dd89` |
| Raw local report | `b56386b6efcbc537ee5c392b9fcb139ca62ea464f82d2e03c6ccfcac66c87249` |
| Sanitized observations | `02593eb7667af3021cb554eaa39f87d8ead32b482ea6aafb2f61e24dfc0debb4` |
| Case-comparison CSV | `18290b5e52e22c43962f49fd985858ef040c2c356ee6ad725964de5a8a565162` |

Raw safe logs and local-check metadata remain ignored under
`artifacts/nla-recovery-checks-2026-10-23/`. Identity, typed cases, report,
request count and `SHA256SUMS.txt` remain ignored under
`artifacts/deepseek-v3-recovery-2026-10-23/`. No raw provider payloads, prompts,
credentials or token maps were persisted. Reviewed observations contain only
synthetic questions and fixture data, with free-text detail omitted.

Supporting files are the
[18-case comparison](../../evaluation/manuscript-2026-10-23/deepseek-recovery-case-comparison.csv),
[sanitized observations](../../evaluation/manuscript-2026-10-23/deepseek-recovery-observations.json),
and [manuscript-ready result](../../evaluation/manuscript-2026-10-23/manuscript-ranking-recovery-results.md).

## Limits

This completes the specified backend recovery and auxiliary catalog-comparison
verification on the fixed development set. It does not establish unseen-question
accuracy, repeated-run reliability, broader model quality, SQL Server 2019
execution, numerical correctness, browser/physical-device behavior, historical
respondent-session outcomes or independent GSD acceptance. No full test suite
or deployment ran. TEST-142 through TEST-144 remain unchanged, and the
auxiliary 18/18 result must always be identified by its scoring rule and stage.
