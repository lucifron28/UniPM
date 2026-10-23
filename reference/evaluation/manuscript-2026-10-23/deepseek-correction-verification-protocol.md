# DeepSeek correction verification protocol

The researcher requested implementation of the recorded correction plan and a
rerun of the test in chat on October 23, 2026. This authorizes one new live
Development comparison on the unchanged 18-question corpus after correction.
TEST-142 remains immutable. The new run is post-correction development
verification, not independent final-study or held-out validation.

## Changes and decisions before execution

- The model interpretation path reuses the parser's ranking intent and ranking
  resolution rules. It requests Top clarification for an unstated or conflicting
  ranking criterion, clears the guessed ranking, and preserves the requested
  measures. Model omission of a second requested measure does not resolve the
  ambiguity. Non-aggregate ranking also requests Mode clarification.
- Catalog resolution maps filter IDs to canonical clarification identifiers,
  including Department and Building. Error keys and normalized filter IDs keep
  their existing form. The existing unknown-building test expectation changes
  to Building; no frozen corpus expectation changes.
- The request to implement the plan adopts its preferred compatibility defaults
  for the new prompt. Generic calendar years mean PM-cycle years unless a
  different date basis is explicit. Unqualified completed-item requests default
  to aggregate counts, preserving explicitly named late/on-time measures;
  explicit records requests keep Records mode. Separate named months are not
  expanded into a continuous range. These are prompt instructions, not evidence
  that every future model response will follow them. Existing backend date,
  category, validation and confirmation controls remain in place.
- Final source review excludes 'at most' from ranking intent, matching the
  existing exclusion of 'at least'. This prevents a numeric threshold from
  acquiring a ranking clarification when the shared guard is applied.
- Catalog value normalization remains uppercase. The strict North Block
  comparison may therefore still differ after resolution. No semantic score
  adjustment or retrospective pass substitution is allowed.

## Frozen run conditions

Use the same corpus hash,
`dd313076a40ea5dd8eddfef610ff8db37ca6c5ba1934a286c6023a1586ca0e25`,
18 questions, reviewed labels, 69 plan assertions, exact scoring implementation,
fixed June 1, 2025 interpretation clock and fictional InMemory catalog as
TEST-142. Use explicit DeepSeek selection and the same configured model alias,
temperature zero, disabled thinking and 1024 output cap. Record the changed
prompt hash, exact new commit, assemblies, execution times and provider usage.

The same request-count and estimated-cost controls apply: at most 18 actual
HTTP requests, at most one per case, no retries, and a US$1 conservative
estimated ceiling using the previously checked October 23 peak uncached rates.
The safety guard may reject the unsafe request before a provider call. Record
all returned differences and errors without further tuning or rerunning live
cases. No reports, live database operations or deployments are authorized by
this verification.

Run from a clean committed checkout after compiling the updated evaluator:

```powershell
dotnet tools/UniPM.PmAnalytics.V3Evaluation/bin/Debug/net10.0/UniPM.PmAnalytics.V3Evaluation.dll deepseek artifacts/deepseek-v3-corrected-2026-10-23
```

## Local regression boundary

The focused model-interpreter test uses 12 controlled transport responses to
check ambiguous, explicit, single-measure and conflicting ranking candidates,
a model that drops one requested measure, a records-mode ranking candidate,
and five catalog-clarification identifiers. It makes no external requests.
It ran once, passed, and exposed a nullable assignment warning. One focused
null-handling correction and one rerun passed without that warning.

The regression-checked source is committed separately before the final
'at most' lexical exclusion and late/on-time prompt refinement. Those two
source-review refinements receive compilation and the new live development
comparison, without a third xUnit run under the execution limit. Record both
commit identities and do not attribute local regression execution to the
later source revision. The full suite, existing acceptance class and browser
flow are not run by this task.

## Evidence and manuscript use

Create new evidence records for the focused regression and live rerun. Retain
sanitized per-case expected/actual outcomes and typed model candidates. Compare
both observation stages with TEST-142, disclosing prompt/rule changes and the
unchanged scoring boundary. Report any remaining mismatch. These development
results do not establish unseen-question accuracy, respondent-session results,
numerical correctness, SQL Server execution or institutional acceptance.
