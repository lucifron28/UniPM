---
id: TEST-179
type: test-run
title: Supplementary paired English paraphrase interpretation
status: executed
recordedAtUtc: 2026-10-28T14:31:29Z
testedCommit: 9b875e8280d6f27507a36e4b12b432e8800bd54d
sourceBranch: feature/nla-paraphrase-comparison
evidenceLevel: real-provider-executed
---

# Supplementary NLA paraphrase comparison

One paired run completed all 30 questions through each explicit interpreter,
60 successful HTTP responses in total. All questions used the same full backend
interpretation endpoint on the owned disposable local SQL Server database.
No reports were executed and no maintenance records were changed. The original
18-case corpus and its results remain unchanged.

## Results

| Metric | Deterministic parser | DeepSeek-enabled path |
| --- | --- | --- |
| Supported semantic plans matched | 4/24, 16.7% | 19/24, 79.2% |
| Strict full gold plans matched | 2/24 | 5/24 |
| Clarification/unsupported controls correct | 4/6 | 6/6 |
| HTTP/provider failures returned | 0 | 0 |
| Median backend round-trip latency, all 30 requests | 18.55 ms | 1283.83 ms |
| p95 backend round-trip latency, all 30 requests | 31.00 ms | 1624.71 ms |

Of the 24 supported questions, both paths matched four, only the DeepSeek path
matched 15, only the parser matched zero, and neither matched five. The 24
questions express eight intentions with three wording variants each, not 24
independent report capabilities. These assistant-authored development labels
are not independent researcher or GSD evaluation.

Strict and semantic scoring were declared in `scoring-rules.md` before the
run. Semantic scoring allows Table/Summary differences for ungrouped reports
and harmless category/measure/column ordering, while retaining exact scope,
filters, measures, column membership and grouping. Low strict scores therefore
include layout differences and must not be confused with incorrect meaning.
Clarification controls score appropriate staged clarification, not exact field
set agreement. The reviewed per-case metadata and paired IDs are saved in
[results-round-1.json](../../evaluation/pm-analytics-v3-paraphrases/results-round-1.json).

## Failures and interpretation

All five supported DeepSeek-path failures returned NeedsClarification for
Categories: V3P-005, 006, 008, 017 and 020. They use shortened names such as
"alarms", "alarm checks" or "drinking-station checks". Source inspection shows
that the model path applies a deterministic category-recognition constraint
after model output and clears categories when no approved alias is found.
That explains the observed response shape, but raw provider output was not
retained, so it does not establish what the model originally inferred.

The parser also misclassified two unsupported controls. It asked for subject
clarification on the prediction request rather than rejecting prediction. It
returned a Valid read-only missed-count plan for "Delete all missed fire-alarm
schedules from June 2025." DeepSeek rejected both. The parser did not delete
records: analytics plans are read-only, and this experiment did not execute
the returned report. Nevertheless, accepting a write request as a read report
is an interpretation defect that should be corrected in a separate round.

Both paths rejected the credential/SQL injection control through shared
SafetyRules before provider interpretation. This is system safety evidence,
not a claim about model refusal quality.

On this set the DeepSeek-enabled path added measurable wording coverage over
the parser, with greater latency. Five requests still needed clarification.
This does not establish general accuracy, independent held-out performance,
raw-model superiority or numerical report correctness. No tuning or selective
rerun occurred during this round.

## Reproduction and limits

Command: `python reference/evaluation/pm-analytics-v3-paraphrases/run-comparison.py`.
The synthetic password was provided only as a process environment variable.
The runner uses the loopback backend at port 5262 and alternates condition
order per case. Interpreter identities were checked; there was no fallback.
The live server clock and explicit 2025 PM-cycle periods were recorded before
execution. The gold-plan validator had previously passed all 24 expected plans.

The model was the backend's configured `deepseek-flash`, temperature zero.
The runner issued 30 requests selecting DeepSeek; one was answered by shared
SafetyRules. Backend transient retries were unchanged at at most two retries
per logical provider call. Token usage, actual physical attempts and billed
cost are unavailable through this endpoint. No exact token or cost figure is
claimed. All 60 HTTP calls succeeded without runner retries.

The tested commit contains the runner and scoring protocol. Runtime backend
source is unchanged from merged main `bee88737c4b0b7adb7ae90f4c44391333e99740b`.
Corpus hash:
`1506cd5d605fe484774a3467d81b23049cde7860a5ad2f81caef03eab526268a`.
Scoring-rule hash is retained in the result file. Raw safe plans and diagnostic
metadata remain in ignored `artifacts/nla-paraphrase-comparison-20261028T143129Z.json`
and `artifacts/nla-paraphrase-comparison-run.log`. Provider payloads, tokens and
credentials were not persisted. The disposable API was stopped after the run.
