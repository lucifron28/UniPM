---
id: TEST-189
type: test-run
title: Paired development-set repeat after final guard-priority correction
status: executed
recordedAtUtc: 2026-10-29T08:36:39Z
testedCommit: e1ebd4bd3945d28cd26a3db02980825cd9d8795e
sourceBranch: feature/nla-guard-countdown-fixes
evidenceLevel: real-provider-executed
---

# Final post-fix paired repeat

All 30 frozen TEST-181 questions ran once through each explicit interpreter on
the same backend commit and owned native SQL Server fixture. The original
complete labels, scoring functions and subset definitions were imported without
modification. Mode order alternated by question. No selective reruns, fallback,
production tuning or label changes occurred during measurement.

Execution interval: October 29, 2026, 16:36:02 to 16:36:39, Asia/Shanghai.
Exact UTC timestamps and every endpoint response are retained in results.json.

## Results

| Measurement | DeterministicParser | DeepSeek-selected path |
| --- | ---: | ---: |
| Requests | 30 | 30 |
| HTTP 200 | 30 | 30 |
| Correct status | 25/30 | 27/30 |
| Strict clarification-field agreement | 25/30 | 25/30 |
| Semantic clarification-field agreement | 25/30 | 26/30 |
| Correct plan disposition | 25/30 | 26/30 |
| Full strict plan agreement | 19/30 | 22/30 |
| Full semantic plan agreement | 22/30 | 23/30 |
| Full strict case agreement | 19/30 | 21/30 |
| Full semantic case agreement | 22/30 | 23/30 |
| Median endpoint latency | 21.1515 ms | 1372.3815 ms |
| Endpoint latency p95 | 39.159 ms | 1645.965 ms |
| Selected interpreter identity | 25 | 25 |
| Shared SafetyRules identity | 5 | 5 |

Full case agreement requires status, clarification fields, plan disposition
and complete plan agreement together. Plan agreement alone is not relabeled as
case success. Semantic comparison preserves measures, filters, periods,
groupings, columns and scopes under the unchanged TEST-181 normalization rules.

All five unsupported controls are correct on both paths through shared guards.
These outcomes are not independent model judgments. Endpoint failures are zero;
physical provider-call counts and retries are unavailable through this endpoint.

## Reported defects

- V3U-020, "whether timely or late", and V3U-026, "80 or higher", return Valid
  and satisfy the complete frozen semantic expectation on the DeepSeek path.
  The parser also satisfies V3U-026; V3U-020 still lacks its required measure.
- V3U-029 prediction and V3U-030 deletion return Unsupported before interpreter
  dispatch on both selected paths. The separate forecast-with-OR unit regression
  also passes. That extra regression question was not added to the frozen corpus.
- V3D-012 returns HTTP 200 NeedsClarification, null plan, and
  InvalidPlanNeedsClarification with Presentation/Columns fields. It remains a
  failure against the original Top/GroupBy clarification-draft expectation.
  The response reports a backend rejection; raw model output was not observed in
  this uninstrumented repeat, so its exact original proposal is unavailable.
- V3D-006 is semantically correct on both paths. Final catalog casing differs
  from the literal expected name, so strict agreement remains false. This repeat
  does not establish what either interpreter emitted before normalization.

## Remaining divergences

Parser semantic failures: V3D-015 and V3U-019 through V3U-025. They concern the
unknown-department draft, unfamiliar measure wording, a records-versus-count
interpretation, missing building scope and ranking interpretation. Every final
plan and difference is retained, including extra or missing columns.

DeepSeek semantic failures: V3D-005, V3D-007, V3D-011, V3D-012, V3U-023,
V3U-024 and V3U-028. They concern output columns, the ambiguous-period draft,
ranking choices, invalid-plan clarification and building-versus-location scope.
None was removed, relabeled or selectively rerun.

TEST-182 recorded semantic case agreement of 19/30 and 21/30. TEST-188 recorded
22/30 and 23/30. This final repeat also records 22/30 and 23/30. Equal totals do
not imply identical plans or establish a repeatability rate. Code changes and
live model response variation both limit causal comparison across these runs.

## Controls and limitations

SQL Server version 15.0.2000.5, compatibility 150. The database contains one
fictional Active fire-extinguisher asset, North Block building, Synthetic
Department and null location. The unchanged startup scheduler created one
unassigned November 2026 schedule before measurement. No forms or inspection
records existed. Complete before/after fixture snapshots match.

Source files, source commit and historical file hashes match before and after
the run. TEST-188 is preserved alongside TEST-181, TEST-182 and other historical
records. No report preview/execution or GSD-record mutation occurred. The owned
API was stopped after measurement. Credentials were loaded only into process
environment and authentication stayed in runner memory.

Configured model alias is deepseek-flash, temperature zero. Served weight
revision, raw proposals, provider attempt counts, tokens, provider-only latency
and API cost are unavailable and are not estimated. Latencies are endpoint
round trips, not inference time.

These are exposed development questions with the original assistant-authored
complete labels. This is not held-out accuracy, independent GSD evaluation,
proof of generalization, numerical-report validation or production acceptance.

## Files and reproduction

- [Frozen repeat protocol](../../evaluation/pm-analytics-v3-post-fix-final/protocol.md)
- [Runner using the unchanged TEST-181 scorer](../../evaluation/pm-analytics-v3-post-fix-final/run-evaluation.py)
- [Full 60 responses, scores and differences](../../evaluation/pm-analytics-v3-post-fix-final/results.json)
- [Per-case CSV](../../evaluation/pm-analytics-v3-post-fix-final/case-results.csv)
- [Owned fixture launcher](../../evaluation/pm-analytics-v3-post-fix-final/start-fixture.ps1)
- [Offline audit](../../evaluation/pm-analytics-v3-post-fix-final/audit-results.py)

The offline audit recomputes every score and summary, verifies all 60 identities
and confirms the retained fixture and historical hashes without additional
endpoint calls. It passed. Run it with:

```powershell
python reference/evaluation/pm-analytics-v3-post-fix-final/audit-results.py
```

Re-execution requires a new evidence ID and fresh owned database. Both launchers
and runners refuse to overwrite an initialized run. Raw command output remains
ignored under `artifacts/test-189/`.
