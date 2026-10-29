---
id: TEST-188
type: test-run
title: First paired repeat after NLA guard corrections
status: executed
recordedAtUtc: 2026-10-29T08:32:04Z
testedCommit: c60c73829553a117a475702257e3fa6eb9530875
sourceBranch: feature/nla-guard-countdown-fixes
evidenceLevel: real-provider-executed
---

# First post-fix paired repeat

All 30 frozen TEST-181 questions ran once per explicit interpreter through
`POST /api/v1/analytics/pm/v3/interpret`, using the original scoring functions
and a new controlled SQL Server fixture. No fallback, report execution or GSD
record mutation occurred. Source, fixture, commit and historical hashes match
before and after the run. This is a repeat of the exposed development set,
not independent accuracy or generalization evidence.

| Measurement | Parser | DeepSeek |
| --- | ---: | ---: |
| Requests / endpoint HTTP 200 | 30 / 30 | 30 / 30 |
| Correct statuses | 25/30 | 27/30 |
| Full strict case agreement | 19/30 | 21/30 |
| Full semantic case agreement | 22/30 | 23/30 |
| Median endpoint latency | 13.903 ms | 1461.7295 ms |
| Shared SafetyRules identities | 5 | 5 |

V3U-020 and V3U-026 return Valid through DeepSeek. V3U-029 prediction and
V3U-030 deletion return Unsupported through shared guards in both modes.
V3D-012 returns HTTP 200 NeedsClarification with a null plan and GroupBy/Columns
fields. It remains a semantic failure against the frozen Top/GroupBy draft
expectation. V3D-006 is semantically correct on both paths despite strict
catalog-casing differences.

The fixture uses native SQL Server 2019 15.0.2000.5, compatibility 150, one
fictional Active fire extinguisher, North Block, Synthetic Department and null
location. Startup scheduling created one November 2026 schedule before
measurement. There were no forms or inspections. The owned API was stopped.

No raw interpreter proposal, provider attempt count, token usage, pure provider
latency, served weight revision or cost is available through this endpoint.
Reported backend codes identify declared effects, not independently observed
raw proposals. No numerical analytics results were evaluated.

The offline audit passed for all 60 case/mode scores, summaries, identities,
fixture equality and historical file hashes. Questions, complete labels,
protocols and results from TEST-181 and TEST-182 are unchanged.

After this completed run, a source review found that a prediction containing
an ambiguous OR could still reach Boolean clarification first. A separate
priority correction and regression were added. This run is retained unchanged;
TEST-189 records the repeat on that final correction commit.

- [Protocol](../../evaluation/pm-analytics-v3-post-fix/protocol.md)
- [Runner](../../evaluation/pm-analytics-v3-post-fix/run-evaluation.py)
- [Full 60 results and differences](../../evaluation/pm-analytics-v3-post-fix/results.json)
- [Per-case CSV](../../evaluation/pm-analytics-v3-post-fix/case-results.csv)
- [Offline audit](../../evaluation/pm-analytics-v3-post-fix/audit-results.py)

Raw local logs and fixture setup remain ignored under `artifacts/test-188/`.
