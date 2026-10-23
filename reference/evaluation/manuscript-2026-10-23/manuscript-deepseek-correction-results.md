# DeepSeek correction and development rerun

This supplements the original [TEST-142 manuscript comparison](manuscript-deepseek-comparison.md).
It records a separate post-correction run on the same frozen development set.
Keep the earlier table and its limitations. Do not present this rerun as a
held-out or independent final evaluation.

## Changes before rerunning

The researchers added backend enforcement of ranking ambiguity for model
interpretations and canonical clarification identifiers for catalog lookup
errors. The prompt clarified generic PM-cycle-year and report-mode defaults,
preserved explicitly named outcome measures, and distinguished separated
months from a continuous range. The labels and exact scoring rules remained
unchanged. The original catalog normalization also remained unchanged.

One focused regression test passed with 12 controlled responses through the
actual model interpreter. A nullable assignment warning prompted one focused
correction and the permitted rerun, which passed. That source is pinned by
TEST-143. The final prompt and 'at most' lexical refinements received a clean
build and the subsequent live evaluation; they were not followed by a third
xUnit run.

## Results

| Measure | Original formative run, TEST-142 | Corrected development run, TEST-144 |
|---|---|---|
| Cases matching all declared expectations | 13/18, 72.22% | 16/18, 88.89% |
| Classification matches | 16/18, 88.89% | 17/18, 94.44% |
| Declared final plan assertion matches | 67/69 | 62/69 |
| Typed model candidate exact matches | 9/17, 52.94% | 11/17, 64.71% |
| Unexpected executable final interpretation on an ambiguous case | 1 | 0 |
| Interpretation validation errors | 0 | 1 |

Both runs made 17 live provider requests without retries, with the eighteenth
question rejected by the backend before a provider call. No reports were
executed. The new run was conducted on October 23, 2026 against source commit
`1c81400d0e92a9da0bcf92bdc2456d52843ef8ad`.

V3D-005, V3D-007 and V3D-015 now matched all their original expectations.
V3D-006 retained the North Block versus NORTH BLOCK normalization difference.
V3D-011 remained unmatched because the model returned status Valid together
with a Top clarification. The backend rejected this contradictory envelope
and returned no final plan. This prevented the earlier accepted guessed
ranking but did not complete the expected clarification interaction.

The final assertion count decreased because the rejected V3D-011 response
contributed zero of its six final-plan assertions, rather than returning a
plan with correct fields but an incorrect decision to proceed. This result
must be disclosed alongside the improved exact-case and classification scores.
All 69 typed-candidate plan assertions matched, which still did not make that
candidate's contradictory envelope acceptable.

## Suggested manuscript text

Following formative testing, the researchers corrected missing ranking
ambiguity enforcement and inconsistent catalog clarification identifiers in
the prototype. A subsequent live DeepSeek development evaluation used the
same frozen 18-question dataset. The final system matched all declared
expectations in 16 cases and the expected classification in 17 cases.
Seventeen questions reached the provider, while the backend rejected one
unsafe request before transmission. One case retained a catalog-value
capitalization difference. Another produced an inconsistent model envelope,
which the backend rejected without returning a report plan. The rerun
therefore showed improved agreement and no accepted ambiguous ranking, while
leaving a response-consistency failure unresolved. Because the same questions
informed the corrections, these results represent development verification
and do not establish accuracy on unseen questions or independent acceptance
of the completed system.

## Supporting records

- [TEST-143 focused regression](../../evidence/test-runs/TEST-143-model-ranking-and-clarification-regression.md)
- [TEST-144 live rerun](../../evidence/test-runs/TEST-144-deepseek-v3-post-correction-comparison.md)
- [All 18 before/after cases](deepseek-corrected-case-comparison.csv)
- [Sanitized typed observations](deepseek-corrected-observations.json)
- [Protocol fixed before execution](deepseek-correction-verification-protocol.md)
