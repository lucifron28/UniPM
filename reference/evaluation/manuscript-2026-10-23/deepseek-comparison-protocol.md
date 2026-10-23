# Supplementary DeepSeek comparison protocol

The researcher authorized execution in chat on October 23, 2026 after review
of the adviser comment and revised manuscript. This supplements TEST-139 with
a new live-provider run. It does not reconstruct the respondent sessions.

## Fixed conditions

- Use the same 18 researcher-reviewed questions, exact expected labels, and
  69 declared plan assertions as TEST-139. Corpus SHA-256 is
  `dd313076a40ea5dd8eddfef610ff8db37ca6c5ba1934a286c6023a1586ca0e25`.
- Preserve the disclosed diagnostic revisions to V3D-008 and V3D-009. No
  expectation, prompt, production rule, or model setting may change in response
  to this run. Do not rerun model mismatches to obtain better results.
- Use the existing `PmAnalyticsV3QuestionInterpreter` with explicit `DeepSeek`
  selection in Development, its existing V3 prompt, `deepseek-flash`, temperature
  zero, thinking disabled, and 1024 maximum output tokens.
- Freeze the interpreter clock to the corpus instant, June 1, 2025 UTC. This
  is the date supplied to the model, distinct from the actual execution date.
- Make at most 18 actual provider HTTP requests, sequentially, one per case.
  A counting transport blocks automatic retries without changing production
  retry behavior. Backend safety rejection may prevent a request entirely.
- Budget ceiling is US$1. Reserve a conservative estimated amount before
  sending each request, using the full request byte count plus 4096 input
  tokens and the full output cap. Use peak, uncached rates of US$0.30 input
  and US$1.20 output per million tokens, checked October 23, 2026 against
  [DeepSeek's official pricing](https://api-docs.deepseek.com/quick_start/pricing/).
  These are conservative estimates, not an invoice or provider-side billing cap.
- Record the full tested commit, branch, corpus/manifest/review hashes,
  evaluator/backend assembly hashes, prompt hash, settings, provider-reported
  model identity and usage, start/end time, and actual requests.
- Use a disposable EF InMemory catalog containing one fictional fire
  extinguisher in North Block. Leave Research department absent so unresolved
  catalog scope can request clarification. No production database is read.

## Comparison and reporting

Run all cases through the actual interpreter including sanitization, safety
guard, model-output parsing, backend constraints, validation and catalog
resolution. Compare status, exact clarification-field set, plan disposition,
and all declared assertions using the existing `ComparePartial` implementation.
Record every failure and representation difference without changing labels.
Count an unexpected executable result on an ambiguous or unsupported question
separately. No proposed plan is previewed or executed.

The earlier deterministic evaluation stopped at the guard and rule parser.
The DeepSeek run therefore also captures the typed model candidate in memory
and scores it separately using the unchanged expectations. Retain both stages
so backend corrections and catalog normalization are visible. A typed candidate
is not proof that the final backend accepted it. A backend rejection before
the provider call is not a successful model response. Do not silently combine
these populations or claim identical processing boundaries.

Save per-case expected/actual typed interpretation data, safe error codes,
comparison details and usage under ignored `artifacts/`. Do not persist request
bodies, raw provider payloads, credentials, authorization headers, or token maps.
Review sanitized results before adding an evidence record and manuscript table.
Keep TEST-139 and its approved corpus unchanged.

Report this as a single supplementary evaluation on a fixed development set.
It does not establish held-out accuracy, repeatability across runs, SQL Server
execution, numerical correctness, browser task success, GSD acceptance, or which
historical model version respondents actually used. The current official model
alias may resolve differently from the earlier respondent evaluation.

## Command

From a clean, committed task checkout, build the V3 evaluator and run:

```powershell
dotnet tools/UniPM.PmAnalytics.V3Evaluation/bin/Debug/net10.0/UniPM.PmAnalytics.V3Evaluation.dll deepseek artifacts/deepseek-v3-2026-10-23
```

One execution is authorized. Record provider failures and stop on a terminal
provider failure. Subsequent cases are marked unexecuted. Differences against
frozen labels are reported as results, without tuning or retrying the model.
