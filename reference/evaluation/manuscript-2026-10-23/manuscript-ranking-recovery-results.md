# Ranking clarification recovery development results

Use this supplementary table in Section 7.3.4 and Appendix A alongside the
earlier formative results. Keep the observation stage and scoring rule in the
caption or surrounding text. This run evaluates the corrected prototype on
questions already used to identify its defects; it is development verification.

## Summary

| Measure | Earlier correction, TEST-144 | Recovery run, TEST-146 |
|---|---|---|
| Final strict case agreement | 16/18, 88.89% | 17/18, 94.44% |
| Final classification agreement | 17/18, 94.44% | 18/18, 100% |
| Final strict declared plan assertions | 62/69 | 68/69 |
| Auxiliary catalog-equivalent case agreement | Not measured | 18/18, 100% |
| Auxiliary catalog-equivalent assertions | Not measured | 69/69 |
| Raw model candidate exact agreement | 11/17, 64.71% | 11/17, 64.71% |
| Interpretation errors | 1 | 0 |

There were 17 live requests, no retries, and one backend rejection before a
provider call. No report was executed. All final clarification sets and plan
dispositions matched the 18 original expectations. Exactly one case used
backend ranking clarification recovery.

## Interpretation of the result

V3D-011 again contained a model contradiction: Valid status with a Top
clarification. The new backend rule independently established the ambiguity,
validated the preserved report scope and returned NeedsClarification with a
safe draft and no selected ranking. This resolved the failure observed in
TEST-144. The raw candidate still counts as a model mismatch.

V3D-006 remains a strict mismatch because North Block becomes NORTH BLOCK
during catalog resolution. Before the new run, the researchers declared a
separate rule that recognizes case/whitespace equivalence only for known
values in the controlled catalog, preserving all other fields exactly. That
rule accounts for the auxiliary 18/18 result. The original strict score remains
17/18. Earlier runs and their labels were not rescored or overwritten.

## Suggested study text

The researchers implemented bounded recovery for a contradictory ranking
interpretation and conducted a further live development evaluation using the
same frozen 18-question dataset. The corrected system matched all expected
classifications, clarification-field sets and plan dispositions. Strict
agreement across all declared expectations was 17 of 18 cases. The remaining
difference was catalog-value capitalization. Under a separately declared
catalog-equivalence rule, the final system matched all declared expectations
in 18 of 18 cases and all 69 selected plan assertions. The backend recovered
one inconsistent model response into a validated clarification without choosing
a ranking measure. Raw model candidates matched all declared expectations in
11 of the 17 questions transmitted to DeepSeek. These findings support
conformance of the corrected interpretation path on the reviewed development
set and do not establish perfect model accuracy or performance on unseen
questions.

## Supporting evidence

- [TEST-145 focused regression](../../evidence/test-runs/TEST-145-ranking-recovery-and-catalog-equivalence-regression.md)
- [TEST-146 live comparison](../../evidence/test-runs/TEST-146-deepseek-ranking-recovery-development-comparison.md)
- [Case-by-case strict and auxiliary scores](deepseek-recovery-case-comparison.csv)
- [Typed model and final response observations](deepseek-recovery-observations.json)
- [Protocol declared before measurement](catalog-equivalence-v1-protocol.md)
