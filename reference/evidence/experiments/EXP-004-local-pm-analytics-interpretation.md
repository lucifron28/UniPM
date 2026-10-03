---
id: EXP-004
type: experiment
title: Local PM analytics interpretation evaluation
status: executed
recordedAtUtc: 2026-10-03T22:19:46Z
testedCommit: 92c056c46defe0f79ce5533bd04c1d5a299c1322
sourceBranch: feature/nla-interpretation-evaluation
evidenceLevel: locally-executed
---

# Local PM analytics interpretation evaluation

## Objective

Measure whether an opt-in local model can map synthetic English, Filipino,
and Taglish PM analytics questions to the existing typed analytics contract.
This experiment evaluates interpretation only; it does not add metrics,
calculations, categories, maintenance rules, or workflows.

## Evaluation question

Can a bounded local Ollama model return a complete, safe, schema-valid plan for
supported questions and correctly clarify or reject unsupported requests,
without relaxing the strict deterministic parser, independent plan validator,
authorization boundary, or canonical query path?

## Execution identity

- Initial development run source: `c0d1cd8d32374756c775b2dae0b596cdbb1f18cb`.
- Model: `qwen3:4b-instruct`; model digest:
  `0edcdef34593eac1aa2be9c7d06c432dcf81945adca5eca2f27662c18f168ba0`.
- Runtime: Ollama `0.30.10`; local, loopback-only process with cleanup verified.
- Initial prompt version: `pm-analytics-interpretation-v1`; fingerprint:
  `33250e825feb116b52cef382d80410f0e5392f4662e1edb79752d084e92f7dab`.
- Configuration: temperature 0, seed 42, context 4096 tokens, 60-second
  timeout, 1024 output-token limit, 16 KiB response limit.
- Dataset: [NLA-INTERPRETATION-V1](../../evaluation/pm-analytics-interpretation/v1/README.md),
  60 development cases and 40 family-held-out cases; SHA-256
  `41656d73d9ec6d0bdbbc1b108d905025be0b59143e77d7721f692b6c95a34d35`.
- The frozen set is synthetic and developer-authored. No operational records
  or personal data were used.

## Method

The evaluator called the production interpreter with fixed development
questions and compared its structured result with frozen labels. Provider
errors were reported separately from status and plan accuracy. A later,
single-case diagnostic used only the first development control to distinguish
transport, JSON parsing, and pipeline-contract rejection. The diagnostic
retained only allowlisted structure and process metadata, not a question,
prompt, raw response, or provider payload.

The production path retains the strict rule-based interpreter as the default
and sends no HTTP requests in that mode. The local adapter is opt-in, accepts
only a bounded question, and does not receive database records. This
evaluation supplied fixed synthetic questions.
Every model plan is independently validated and serialized through the
existing canonical parser and validator before a result can be used with the
unchanged query path. The interpretation endpoint itself does not access
analytics data. No remote-provider, JWT, browser, IIS, or live GSD verification
is claimed.

## Rule-based development baseline

The default rule-based interpreter evaluated all 60 development cases at
source commit `c0d1cd8d32374756c775b2dae0b596cdbb1f18cb`, with no provider
requests. The recorded command was `dotnet
tools/UniPM.PmAnalytics.InterpretationEval/bin/Release/net10.0/UniPM.PmAnalytics.InterpretationEval.dll
--split dev --mode rule-based --source-sha
c0d1cd8d32374756c775b2dae0b596cdbb1f18cb --output
artifacts/evaluation/pm-analytics-interpretation/dev-20261003T174026Z.json`;
its report window was 2026-10-03T17:40:26.5380704Z through
2026-10-03T17:40:26.6030998Z. It reached 38/60 status accuracy and 3/15 complete-plan accuracy on
expected-valid cases. It rejected all 30 expected-Unsupported cases and passed
all three strict-template controls. This comparison shows the baseline's
limited free-phrasing coverage; it is not a model result. Its report is
`artifacts/evaluation/pm-analytics-interpretation/dev-20261003T174026Z.json`.

## Initial development results

The first v1 run used `powershell -NoProfile -ExecutionPolicy Bypass -File
artifacts/evaluation/pm-analytics-interpretation/Invoke-LocalOllamaRun.ps1
-Split dev -SourceSha c0d1cd8d32374756c775b2dae0b596cdbb1f18cb -Model
qwen3:4b-instruct`. Its report window was 2026-10-03T17:50:40.0736907Z through
2026-10-03T17:55:28.0022246Z. It evaluated all 60 development cases. The
evaluator estimated 45 provider attempts; it observed two successful
interpreter responses and 43
`InvalidOutput` outcomes. Fifteen cases were returned by deterministic guards
without a provider attempt. The attempt count is not a direct HTTP request
counter. `InvalidOutput` can include valid JSON that later fails a pipeline
invariant, as the diagnostic below demonstrates. Aggregate status
accuracy was 15/60 (25%). Complete plan accuracy on expected-valid cases was 0/15; exact
clarification-field accuracy was 5/15; unsupported rejection was 10/30. The
strict-template controls were 0/3 for status and 0/3 for complete plan. No
non-valid result contained an executable plan.

This is a failed initial development result, not evidence that the candidate
meets the interpretation objective. The full sanitized aggregate report is
retained at `artifacts/evaluation/pm-analytics-interpretation/dev-20261003T175040Z.json`.

## Diagnostic and prompt revision

The diagnostic command was
`powershell -NoProfile -ExecutionPolicy Bypass -File
artifacts/evaluation/pm-analytics-interpretation/Invoke-LocalOllamaRun.ps1
-Split dev -SourceSha c0d1cd8d32374756c775b2dae0b596cdbb1f18cb -Model
qwen3:4b-instruct -OneCaseDiagnostic`. Its report window was
2026-10-03T18:29:07.0492052Z through 2026-10-03T18:29:28.278908Z. It reached
the local provider once and returned HTTP 200 with valid JSON and all required
output fields. The model
returned `NeedsClarification` while also supplying a plan and `Percent`
presentation. The production pipeline rejected this inconsistent response at
the clarification-result-shape invariant. The owned Ollama process was cleaned
up and the port was verified free. Sanitized lifecycle metadata is retained at
`artifacts/evaluation/pm-analytics-interpretation/lifecycle-20261003T182907Z.json`.

The prompt and schema were revised at source commit
`cfc6bc5787c1d59fba898a642eaa19551c72acb9`. Version 2 embeds the serialized
schema in the system instruction and states the status-dependent null/empty
rules, canonical `yyyy-MM` cycle format, and department/grouping constraints.
The strict parser and plan validator were not weakened. This revision follows
Ollama's [structured-output guidance](https://docs.ollama.com/capabilities/structured-outputs),
which recommends providing the JSON Schema as the format and grounding the
prompt with the schema. The initial v1 result above remains unchanged.

## Prompt v2 development result

The one authorized v2 development run used
`powershell -NoProfile -ExecutionPolicy Bypass -File
artifacts/evaluation/pm-analytics-interpretation/Invoke-LocalOllamaRun.ps1
-Split dev -SourceSha cfc6bc5787c1d59fba898a642eaa19551c72acb9 -Model
qwen3:4b-instruct`. Its report window was 2026-10-03T18:54:55.9902363Z through
2026-10-03T18:57:47.4253584Z. It evaluated all 60 development cases at
`cfc6bc5787c1d59fba898a642eaa19551c72acb9`. It used the same 60-case corpus,
model digest, Ollama version, and generation settings listed above. The prompt
fingerprint was
`488aa6c6a7114907bca526ebad08027a94b4a8b7c5bfb56a0d020171af3da369`.
The evaluator estimated 45 provider attempts, received 43 successful model responses,
and recorded two provider errors. Status accuracy was 49/60 (81.67%); complete
plan accuracy on expected-valid cases was 12/15 (80%); exact clarification
fields were 10/15; unsupported rejection was 26/30. It returned three
executable plans for non-valid cases. Strict-template controls were 3/3, and
free-phrasing valid cases were 9/12 for status and plan. The report is
`artifacts/evaluation/pm-analytics-interpretation/dev-20261003T185455Z.json`.

The three unsafe executable outputs were `nla1-C01-dev-en-1`,
`nla1-C01-dev-en-2`, and `nla1-U05-dev-taglish-2`. The first two were labeled
NeedsClarification for a missing metric but returned Valid Progress plans. The
third was labeled Unsupported for a named category outside the supported set
but returned a Valid OnTimeCompliance plan for a supported category. These
failures motivated a prompt-only v3 instruction to clarify missing or
ambiguous metric/category intent and reject clearly named unsupported
categories. They did not change the schema, deterministic guards, parser,
validator, or calculations.

## Prompt v3 development result and candidate freeze

Version 3 was evaluated once with
`powershell -NoProfile -ExecutionPolicy Bypass -File
artifacts/evaluation/pm-analytics-interpretation/Invoke-LocalOllamaRun.ps1
-Split dev -SourceSha 92c056c46defe0f79ce5533bd04c1d5a299c1322 -Model
qwen3:4b-instruct`. Its report window was 2026-10-03T21:47:07.77993Z through
2026-10-03T21:49:46.7854874Z. This run used source commit
`92c056c46defe0f79ce5533bd04c1d5a299c1322`. The v3 prompt instructs the
model to recognize semantic synonyms and abbreviations, clarify a missing or
ambiguous metric, clarify an absent or ambiguous supported category, and
reject a clearly named unsupported category. The prompt fingerprint was
`49cc3e40fdc86471da826d357076adfd36de54fdb3a51d631f5aa4fc8f6e2ef9`; the
model was `qwen3:4b-instruct` with digest
`0edcdef34593eac1aa2be9c7d06c432dcf81945adca5eca2f27662c18f168ba0`, on
Ollama `0.30.10`, at temperature 0, seed 42, context 4096, timeout 60 seconds,
1024 output tokens, and a 16 KiB response limit.

All 60 development cases were evaluated. The evaluator estimated 45 provider
attempts, received 44
successful model responses, one provider error, and 15 guard-handled cases.
Status accuracy was 38/60 (63.33%); complete plan accuracy on expected-valid
cases was 5/15 (33.33%); exact clarification fields were 9/15; unsupported
rejection was 19/30. No non-valid result contained an executable plan. The
three v2 unsafe IDs no longer produced executable plans: the two
missing-metric cases returned NeedsClarification, and the unsupported-category
case ended in an InvalidOutput provider error. This prevented execution through
abstention or error, but did not correctly resolve the semantic labels. For
the first two, the returned clarification field was Year rather than the
expected Metric. The third case did not reach a semantic Unsupported result.
The v3 free-phrasing score fell to 2/12 (16.67%); strict-template controls
remained 3/3. By language, status accuracy was English 15/20, Filipino 12/20,
and Taglish 11/20, with one Taglish provider error.

Complete-plan outcomes by expected metric and language were:

| Expected metric | English | Filipino | Taglish | Total |
| --- | ---: | ---: | ---: | ---: |
| Progress | 3/3 | 0/1 | 0/2 | 3/6 |
| OnTimeCompliance | — | 1/3 | 0/1 | 1/4 |
| CompletedLate | 0/1 | — | 0/2 | 0/3 |
| NonOperational | 1/1 | 0/1 | — | 1/2 |
| **Total** | **4/5** | **1/5** | **0/5** | **5/15** |

The v3 report is
`artifacts/evaluation/pm-analytics-interpretation/dev-20261003T214707Z.json`.
The v3 prompt was frozen as a conservative experimental candidate solely for
unbiased held-out measurement: one rule-based reference run and one model-
assisted run on the held-out split. The v2-to-v3 change removed the three
non-valid executable outputs through abstention or provider error, but reduced
status accuracy, complete-plan accuracy, unsupported rejection, and
free-phrasing performance. This development result did not establish adequate
language quality or readiness for release.

## Held-out results

The two authorized held-out runs each evaluated all 40 family-disjoint cases at
source commit `92c056c46defe0f79ce5533bd04c1d5a299c1322` and corpus SHA-256
`41656d73d9ec6d0bdbbc1b108d905025be0b59143e77d7721f692b6c95a34d35`. The
rule-based reference ran first with no provider calls. The frozen v3 model run
used the configuration above. No prompt or dataset tuning followed either
held-out run.

| Held-out mode | Status accuracy | Complete plan on expected Valid | Exact clarification fields | Unsupported rejection | Non-valid executable outputs |
| --- | ---: | ---: | ---: | ---: | ---: |
| Rule-based reference | 25/40 (62.5%) | 2/10 (20%) | 3/10 (30%) | 20/20 (100%) | 0 |
| Model-assisted v3 | 15/40 (37.5%) | 2/10 (20%) | 6/10 (60%) | 4/20 (20%) | 1 |

The evaluator estimated 34 provider attempts and received 34 successful model
responses, and recorded no provider or evaluator errors; six cases were
handled by deterministic guards. Status accuracy by language (model v3 /
rule-based reference) was English 8/14 vs. 10/14, Filipino 4/13 vs. 8/13,
and Taglish 3/13 vs. 7/13. On expected-valid cases, the model scored 2/8 on
free phrasing and 0/2 on strict-template controls; the rule-based reference
scored 0/8 and 2/2, respectively. Model latency was 2,766.75 ms p50 and
5,090.01 ms p95. Available model usage was 32,775 prompt tokens and 1,021
completion tokens; monetary cost was unavailable.

The rule-based command was `dotnet
tools/UniPM.PmAnalytics.InterpretationEval/bin/Release/net10.0/UniPM.PmAnalytics.InterpretationEval.dll
--split heldout --mode rule-based --source-sha
92c056c46defe0f79ce5533bd04c1d5a299c1322 --output
artifacts/evaluation/pm-analytics-interpretation/heldout-rule-based-20261003T220135Z.json`;
its report window was 2026-10-03T22:01:47.3088548Z through
2026-10-03T22:01:47.3662028Z. The model command was `powershell -NoProfile
-ExecutionPolicy Bypass -File
artifacts/evaluation/pm-analytics-interpretation/Invoke-LocalOllamaRun.ps1
-Split heldout -SourceSha 92c056c46defe0f79ce5533bd04c1d5a299c1322 -Model
qwen3:4b-instruct -AuthorizeHeldoutRun`; report timestamps were
2026-10-03T22:02:30.2182972Z through 2026-10-03T22:04:31.7019519Z. Its
lifecycle record says cleanup succeeded; the wrapper's terminal output
confirmed that the local provider port was free after the run.

The sole non-valid executable output was `nla1-C13-heldout-fil-1`. The frozen
label expects `NeedsClarification` with `AssetCategory` and no plan. The
normalized result was `Valid` with `{ metric: Progress, assetCategory:
fire-extinguisher, pmCycle: 2026-11, department: null, groupBy: None }` and
`Percent` presentation. The synthetic input uses the abbreviation `FE`. This
corpus version labels it as unresolved category intent, while the model
expanded it to a supported category. That label is provisional: abbreviations
are a desired input class, but `FE` is not in this frozen alias set and the
label has not had independent fluent-speaker review. Preserve the label and
report as frozen; do not treat this one disagreement as evidence that
analytics data was accessed or that authorization was bypassed.

The plan passed structural normalization because it used supported enum values
and a valid cycle. The deterministic scope matcher compares a category only
when it recognized one in the question; `FE` is absent from the explicit alias
list, leaving no category conflict to reject (`NaturalLanguageAnalyticsQuestionGuard.cs:32-50,205-215`).
The interpretation pipeline then accepted the normalized plan and canonical
question (`NaturalLanguageAnalyticsInterpretationPipeline.cs:92-138`). The
evaluator invoked only the interpretation interface; it did not call the
analytics query endpoint, execute a database query, or return source records.

The held-out result is weaker than the rule-based reference on status and
unsupported rejection, matches it on complete-plan accuracy, and has one
non-valid executable output. The improved clarification-field score does not
offset those failures. This candidate does not meet a language-quality or
release-readiness bar. The held-out sample is now unblinded; it is retained
only as measurement evidence and must not be used for further tuning.

## Limitations

The cases and labels are synthetic and developer-authored, not independently
reviewed. Filipino and Taglish labels require fluent-speaker review before any
language-quality claim. The reported held-out scores measure only this frozen
sample and configuration; they do not establish real GSD terminology,
institutional acceptance, or production readiness. No remote model,
unscreened institutional text, database-backed analytics execution, or
real-user behavior was evaluated.

This record documents executed interpretation experiments. It does not
establish release readiness. The existing strict rule-based behavior,
authorization, independent validation, and canonical query safeguards remain
authoritative regardless of model scores. See
[TEST-058](../test-runs/TEST-058-guarded-pm-analytics-interpretation-verification.md)
and [the approved planning phase](../../planning/schema-constrained-nla.md).

## Baseline artifacts

- Rule-based v1 development report: `artifacts/evaluation/pm-analytics-interpretation/dev-20261003T174026Z.json`.
- Initial local-model v1 development report: `artifacts/evaluation/pm-analytics-interpretation/dev-20261003T175040Z.json`.
- Successful one-case diagnostic lifecycle: `artifacts/evaluation/pm-analytics-interpretation/lifecycle-20261003T182907Z.json`.
- Prompt v2 development report: `artifacts/evaluation/pm-analytics-interpretation/dev-20261003T185455Z.json`.
- Prompt v3 development report: `artifacts/evaluation/pm-analytics-interpretation/dev-20261003T214707Z.json`.
- Prompt v3 freeze metadata: `artifacts/evaluation/pm-analytics-interpretation/freeze-20261003T220014Z.json`.
- Rule-based held-out report: `artifacts/evaluation/pm-analytics-interpretation/heldout-rule-based-20261003T220135Z.json`.
- Model-assisted held-out report: `artifacts/evaluation/pm-analytics-interpretation/heldout-20261003T220230Z.json`.
- Model-assisted held-out lifecycle: `artifacts/evaluation/pm-analytics-interpretation/lifecycle-20261003T220226Z.json`.
- Prompt v3 source: `server/Features/Reports/OllamaNaturalLanguageAnalyticsInterpreter.cs` at `92c056c46defe0f79ce5533bd04c1d5a299c1322`.
