---
id: TEST-196
type: test-run
title: NLA V3 DeepSeek live interpretation comparison
status: executed
recordedAtUtc: 2026-11-06T16:10:05Z
testedCommit: df2e84ece8865623cb6fa6b368777ce9ae8fd64e
sourceBranch: feature/nla-final-hardening
evidenceLevel: real-provider-executed
---

# NLA V3 DeepSeek live interpretation comparison

## Objective and identity

Compare the production DeepSeek interpretation path with the deterministic
parser on 20 developer-authored English, Filipino, and Taglish questions at
PR #116 head `df2e84ece8865623cb6fa6b368777ce9ae8fd64e`. The questions cover
named-place scope, ranking, numeric thresholds, ambiguity, and records versus
counts. This is a small diagnostic, not an accuracy evaluation.

The report preserves each question, the initial author expectation, the full
deterministic interpretation, DeepSeek's typed proposal, the guarded final
interpretation, structural differences, latency, model label, token usage, and
cost estimate in [the result JSON](TEST-196-nla-v3-deepseek-live-diagnostic-results.json).
Its SHA-256 is `EAACEE500FFD74C0E3D5FE108502432BB808189D4393371AA6D750286F65766B`.
The JSON contains typed plans and metadata only. Raw provider request and
response payloads, credentials, and headers were not retained.

## Method and scope

- Provider/model label: DeepSeek `deepseek-flash`; the provider's pricing page
  listed this alias for DeepSeek-V4.1-Flash-1017 when checked.
- Settings: temperature 0, thinking disabled, JSON output, maximum 1,024 output
  tokens. Each query made one physical request; the runner blocked requests
  above its 20-request cap.
- Isolation: the interpreter used an in-memory synthetic asset catalog with
  fictional `North Block` and `Research Office` values. It interpreted plans;
  it did not execute a report or read real PM records.
- Inputs: 8 English, 6 Filipino, and 6 Taglish queries. All 20 are included in
  the result JSON.
- Dataset boundary: no GSD query dataset or frozen historical evaluation was
  accessed or changed.

## Provider usage and cost

The provider returned HTTP 200 for all 20 requests, with no retries or provider
errors. Run window: `2026-11-06T15:36:56Z` to `2026-11-06T15:37:25Z`.

| Measure | Result |
|---|---:|
| Prompt tokens | 86,321 |
| Cache-hit prompt tokens | 77,824 |
| Cache-miss prompt tokens | 8,497 |
| Completion tokens | 3,407 |
| Reasoning tokens | Not reported by the provider |
| Mean response latency | 1,387.63 ms |
| Latency range | 1,041.40–1,758.95 ms |
| Estimated cost | USD 0.003552222 |

Cost uses the provider-attributed cache counts and off-peak rates at run time:
USD 0.003 per million cache-hit input tokens, USD 0.15 per million cache-miss
input tokens, and USD 0.60 per million output tokens. The estimate is not an
invoice. DeepSeek notes that rates can change in its [official pricing page](https://api-docs.deepseek.com/quick_start/pricing/).

## Interpretation comparison

| Cases | Deterministic parser | DeepSeek proposal and guarded result | Review |
|---|---|---|---|
| LIVE-001–003 | Clarified the unqualified `North Block` as Building or Location and retained the year, category, measure, and department grouping. | Proposed valid plans; the guard returned the intended clarification and retained resolved fields. | Expected behavior; the guard prevented guessing the place dimension. |
| LIVE-004 | Valid building filter for `North Block`. | Valid; filter normalized to the catalog form. | Agreement. |
| LIVE-005 | Asked for Department clarification. | Proposed a Department filter for `Research Office`; the guard asked for clarification. | Taglish suffix `noong 2025` was not recognized as a date boundary for the named value. Corrected locally in `b872a20`; covered by a synthetic regression. No additional live call was made after the 20-call cap. |
| LIVE-006 | Returned `UnsupportedTimeBasis`. | Proposed the expected top-five missed-count ranking; the guard returned `UnsupportedTimeBasis`. | The generic `inspections in 2025` match was treated as a completion-date request despite the PM context. The initial valid expectation was retained in the JSON. The recognizer was narrowed in `b872a20`; a synthetic regression checks PM-cycle year parsing and explicit completion-date rejection. |
| LIVE-007–008 | Valid descending rankings: five buildings by non-operational findings and three departments by completed-late inspections. | Valid matching ranking plans. | Agreement. |
| LIVE-009 | Asked which measure should determine the ranking. | Proposed both scheduled and completed measures; the guard asked for the ranking basis and preserved both. | Expected clarification. |
| LIVE-010 | Returned `UnsupportedTimeBasis`. | Proposed the threshold plan; the guard returned `UnsupportedTimeBasis`. | Same broad year/completion-date recognizer issue as LIVE-006. The query expected the scheduled PM-cycle year. The post-run correction is locally regression-tested, not provider-retested. |
| LIVE-011 | Asked for Department clarification and did not preserve both thresholds. | Proposed Department grouping and both thresholds; the guard asked for clarification. | `higit sa` and leading Filipino grouping were missing from the deterministic grammar. Corrected in `b872a20`; synthetic deterministic and fake-provider guard checks preserve both thresholds. |
| LIVE-012–013 | Asked for Building or Department clarification. | Proposed the expected groups and thresholds; the guard asked for the dimension. | Leading `List buildings` and `Show departments` phrasing was not resolved as grouping. Corrected in `b872a20`; synthetic regression coverage was added. |
| LIVE-014 | Valid `onTimeCompliancePercent` plan. | Valid matching plan. | The initial expectation of clarification was incorrect: product policy maps “compliance” to on-time compliance unless the question asks for progress. |
| LIVE-015 | Asked for threshold and Department clarification. | Asked which percentage measure `above 80%` means; the guard also asked for Department and threshold clarification and dropped the known scheduled-count threshold. | The follow-up correction in `6912c3a` preserves the resolved Department group and `scheduledCount >= 10` condition while leaving the 80% measure unresolved. A synthetic guarded-path regression passed. No additional live call was made after the cap. |
| LIVE-016 | Asked for clarification on the `late or missed` disjunction. | Proposed an `In [Late, Missed]` filter; the guard returned the same safe clarification. | Both branches were represented in the proposal; the final path did not silently choose one. |
| LIVE-017 | Asked whether December meant PM cycle or actual inspection date. | Asked the same clarification and retained the known record fields. | Agreement. |
| LIVE-018 | Returned an aggregate `nonOperationalCount` plan for a “List emergency lights” request. | Proposed and returned a records plan with `condition = NonOperational`. | Deterministic list intent was wrong. `b872a20` added direct supported-category list phrasing. CI later found that this cue also captured category-led aggregate questions; `66f89f3` limits it when metric, threshold, or grouping language follows. |
| LIVE-019 | Valid `nonOperationalCount` plan. | Valid matching count plan. | Agreement. |
| LIVE-020 | Valid record plan filtered to late timeliness. | Valid record plan with the same late filter and an additional redundant condition-enum filter. | No outcome-changing mismatch observed. |

## Follow-up implementation and verification

The comparison produced three focused commits:

- `b872a20` (`fix(nla): repair multilingual interpretation gaps`) recognizes
  Filipino date cues after named values, `higit sa`, leading department/building
  grouping, PM-cycle year phrasing, and list requests naming a supported asset
  category.
- `6912c3a` (`fix(nla): preserve scope in partial clarifications`) distinguishes
  category record-list wording from category-led grouping and preserves resolved
  grouping and numeric filters when a percentage measure still needs
  clarification.
- `66f89f3` (`fix(reports): preserve aggregate intent after categories`) keeps
  direct category list wording from overriding a following measure, threshold,
  or grouping cue.

The focused backend regression run passed **14/14, 0 skipped, 0 failed** on
`6912c3a2e64a5d1392ae6323dc70d3d9e0214420`. Full backend CI at that commit
found five parser regressions where direct category list wording overrode
aggregate intent. The correction in `66f89f3` passed all six targeted cases
covering those failures, including the two relative-year and two inclusive
completion examples. Both backend CI runs then passed **759 tests, skipped 55,
failed 0** on `66f89f38784078cadefabb66f5448784fe7f2180`:
[pull-request run](https://github.com/lucifron28/UniPM/actions/runs/42251227943),
[push run](https://github.com/lucifron28/UniPM/actions/runs/42251237756).

Both web CI runs passed on the same commit, including unit tests, coverage,
format, lint, type and API contract checks, build, and browser tests. The E2E
step reported **64 passed, 2 skipped**:
[pull-request run](https://github.com/lucifron28/UniPM/actions/runs/42251228040),
[push run](https://github.com/lucifron28/UniPM/actions/runs/42251237807).
Notion sync also passed in [run 42251233755](https://github.com/lucifron28/UniPM/actions/runs/42251233755).

The focused fixes and CI used synthetic data. No additional provider call was
made after the 20-request cap. No merge or deployment was performed.

## Limitations

This is one provider call per developer-authored question at the pre-fix head.
It is not a held-out evaluation, an accuracy score, numerical report execution,
GSD acceptance, or evidence about production data. The fixes were validated by
focused synthetic tests; the 20-call cap did not permit a post-fix provider
repeat. Independent GSD evaluation remains a separate next step.
