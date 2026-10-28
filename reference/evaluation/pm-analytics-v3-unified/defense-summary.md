# Unified NLA interpretation evaluation

TEST-181 ran 30 development questions through both explicit interpreter modes,
using the same backend endpoint, source version and isolated SQL Server fixture.
Questions and complete expected results were frozen before the 60-request run.
Every request returned HTTP 200; no fallback or selective rerun occurred.

| Question subset | Deterministic parser | DeepSeek-enabled path |
| --- | --- | --- |
| Original 18, semantic success | 16/18 | 15/18 |
| Additional 12, semantic success | 3/12 | 7/12 |
| Overall semantic success | 19/30, 63.3% | 22/30, 73.3% |
| Overall strict success | 16/30, 53.3% | 20/30, 66.7% |

The 30 include 16 valid reports, nine clarification requests and five unsupported
controls. Among valid reports, semantic success was 8/16 for the parser and
11/16 for DeepSeek. Among the eight newly worded valid questions, it was 0/8
and 4/8. Appropriate clarification was 9/9 and 7/9; unsupported rejection was
3/5 and 5/5. Median endpoint latency was 11.41 ms and 1307.02 ms.

Semantic success requires preserving the complete requested plan, including
column membership. Declared equivalences permit known catalog casing and order
of columns/categories/measures, plus limited presentation and unresolved-period
representations. A technical ID added to the declared record columns is counted
as a failure. Strict scoring additionally requires the exact canonical plan.

Both paths return NORTH BLOCK for original V3D-006. Both fail strict casing and
pass catalog equivalence. The new run also identifies two requests rejected by
a shared Boolean-filter guard and DeepSeek ranking requests accepted without
the expected clarification. These failures remain in the scores.

A defensible statement is: “In the unified development evaluation, the
DeepSeek-enabled path achieved 22/30 semantic successes compared with 19/30 for
the deterministic-parser path. It handled four of eight new paraphrases while
the parser handled none. Both paths had failures, and the results include
backend constraints and catalog resolution as well as interpretation.”

The full expectations are assistant-authored and have not received independent
researcher/GSD review. The original questions and intentions were used during
development. This is not held-out or independent accuracy, and no numerical
report results were tested. Raw pre-normalization output was unavailable.
TEST-179 and TEST-180 remain supporting evidence under their own scoring rules.
Do not substitute these new full-plan scores for the historical partial scores.

[Complete TEST-181 record](../../evidence/test-runs/TEST-181-unified-nla-development-evaluation.md)
