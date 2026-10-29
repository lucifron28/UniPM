---
id: TEST-181
type: test-run
title: Unified 30-question paired NLA development evaluation
status: executed
recordedAtUtc: 2026-10-28T15:19:46Z
testedCommit: 7ce492d4d925b26dae8097b34238758f0e3b5719
sourceBranch: feature/nla-unified-evaluation
evidenceLevel: real-provider-executed
---

# Unified paired development evaluation

All 30 questions ran once through each explicitly selected mode, 60 requests in
total, on the same backend commit, binary, controlled SQL Server fixture and
interpretation endpoint. Every request returned HTTP 200. No fallback,
selective rerun, production change, prompt change or label change occurred.
TEST-139, TEST-146, TEST-179, TEST-180 and their historical sources/results remain
unchanged. This record does not replace them.

## Unified comparison

Semantic case success requires correct HTTP/status, clarification fields,
plan disposition and complete semantic plan agreement against the newly frozen
full expectations. It preserves column membership and all requested parameters.

| Subset | Parser | DeepSeek path |
| --- | --- | --- |
| Original questions | 16/18, 88.9% | 15/18, 83.3% |
| Additional questions | 3/12, 25.0% | 7/12, 58.3% |
| Overall | 19/30, 63.3% | 22/30, 73.3% |

| Other measure | Parser | DeepSeek path |
| --- | --- | --- |
| Strict case agreement, all 30 | 16/30 | 20/30 |
| Exact status, all 30 | 22/30 | 25/30 |
| Exact clarification-field set, all 30 | 22/30 | 24/30 |
| Semantic clarification-field set, all 30 | 22/30 | 25/30 |
| Correct plan disposition, all 30 | 24/30 | 25/30 |
| Valid questions, complete semantic success | 8/16 | 11/16 |
| Original valid questions | 8/8 | 7/8 |
| New valid paraphrases | 0/8 | 4/8 |
| Clarification cases, complete semantic success | 8/9 | 6/9 |
| Appropriate clarification status/fields, narrower check | 9/9 | 7/9 |
| Unsupported controls correctly rejected | 3/5 | 5/5 |
| HTTP failures | 0/30 | 0/30 |
| Shared SafetyRules outcomes | 2/30 | 2/30 |
| Median endpoint latency | 11.41 ms | 1307.02 ms |
| p95 endpoint latency, nearest rank | 42.71 ms | 1756.78 ms |

Across the 16 Valid questions, both paths met the complete semantic expectation
on seven, DeepSeek alone on four, parser alone on one, and neither on four.
A 30-case overall score combines executable reports, clarification behavior
and unsupported controls; it is not accuracy on 30 executable reports.

## Corpus and expectation provenance

Original outcome counts are eight Valid, seven NeedsClarification and three
Unsupported. The earlier proposal's eight/eight/two count was incorrect.
V3D-016 sorting, V3D-017 elevator and V3D-018 SQL-output requests are the three
original Unsupported cases. Adding eight valid paraphrases, two clarification
cases and two unsupported cases gives 16 Valid, nine NeedsClarification and
five Unsupported. No original outcome label was changed to fit the proposal.

The eight new paraphrases correspond one-to-one to the eight original valid
intentions. Each pair has the same full expected plan. Questions, canonical
plans, scoring, review notes and runner were committed before interpretation
at `7ce492d4d925b26dae8097b34238758f0e3b5719`.

New full labels were authored and reviewed by the assistant using question
meaning, the closed plan contract and already exposed development evidence.
The project owner authorized execution; independent researcher/GSD review of
these new full labels has not occurred. The original questions and intentions
were already used during development. This is a unified development evaluation,
not held-out, blind or independently validated model accuracy. New wording alone
does not make a reused intention independently held out.

All 69 original partial assertions were checked against the separate full gold
and remain compatible. Full gold additionally fixes defaults the old assertions
did not specify. For example, V3D-015 now requires retaining the unresolved
Research department filter in a draft, and record-report column membership is
fully specified. Therefore TEST-181 scores are not directly interchangeable
with TEST-180's partial-assertion scores.

The strict canonical output is partly a declared layout/default convention
because the questions do not dictate every output column or presentation.
Semantic scoring allows known catalog case/trim equivalence, category/measure/
column order differences, Summary/Table for ungrouped aggregates, a declared
empty unresolved-period placeholder, and casing of known clarification field
identifiers. It does not discard columns, measures, filters, periods, grouping,
ranking, ordering, thresholds, subject or other scope. Extra technical ID
columns still fail under this frozen policy.

Documentation erratum: the frozen pre-run review says 16 root contract
properties; the closed plan actually has 15. The runner's exact 15-key check and
all full plan objects are correct. The frozen review is preserved as written.

## Every case

P/F means strict case result followed by semantic case result. Each path cell
also gives actual status and clarification fields. Full HTTP, identity, timing,
expected/actual plan differences and source commit are retained per request in
the CSV and JSON below.

| Case | Subset | Parser status; fields; strict/semantic | DeepSeek status; fields; strict/semantic |
| --- | --- | --- | --- |
| V3D-001 | original | Valid; none; P/P | Valid; none; P/P |
| V3D-002 | original | Valid; none; P/P | Valid; none; P/P |
| V3D-003 | original | Valid; none; P/P | Valid; none; P/P |
| V3D-004 | original | Valid; none; P/P | Valid; none; P/P |
| V3D-005 | original | Valid; none; P/P | Valid; none; F/F |
| V3D-006 | original | Valid; none; F/P | Valid; none; F/P |
| V3D-007 | original | NeedsClarification; Period; F/P | NeedsClarification; Period; F/F |
| V3D-008 | original | NeedsClarification; Filters,Measures; P/P | NeedsClarification; Filters,Measures; P/P |
| V3D-009 | original | NeedsClarification; Filters,Measures; P/P | NeedsClarification; Filters,Measures; P/P |
| V3D-010 | original | NeedsClarification; Filters,Measures; P/P | NeedsClarification; Filters,Measures; P/P |
| V3D-011 | original | NeedsClarification; Top; P/P | Valid; none; F/F |
| V3D-012 | original | NeedsClarification; Top,GroupBy; P/P | NeedsClarification; GroupBy,Top; P/P |
| V3D-013 | original | Valid; none; P/P | Valid; none; P/P |
| V3D-014 | original | Valid; none; P/P | Valid; none; P/P |
| V3D-015 | original | NeedsClarification; Department; F/F | NeedsClarification; department; F/P |
| V3D-016 | original | Unsupported; none; P/P | Unsupported; none; P/P |
| V3D-017 | original | NeedsClarification; Categories; F/F | Unsupported; none; P/P |
| V3D-018 | original | Unsupported; none; P/P | Unsupported; none; P/P |
| V3U-019 | additional | NeedsClarification; Measures; F/F | Valid; none; P/P |
| V3U-020 | additional | NeedsClarification; Filters,Measures; F/F | NeedsClarification; Filters,Measures; F/F |
| V3U-021 | additional | NeedsClarification; Measures; F/F | Valid; none; P/P |
| V3U-022 | additional | NeedsClarification; Measures; F/F | Valid; none; P/P |
| V3U-023 | additional | Valid; none; F/F | Valid; none; F/F |
| V3U-024 | additional | Valid; none; F/F | NeedsClarification; location; F/F |
| V3U-025 | additional | NeedsClarification; Department,Top,GroupBy; F/F | Valid; none; P/P |
| V3U-026 | additional | NeedsClarification; Filters,Measures; F/F | NeedsClarification; Filters,Measures; F/F |
| V3U-027 | additional | NeedsClarification; Period; F/P | NeedsClarification; Period; P/P |
| V3U-028 | additional | NeedsClarification; Top; P/P | Valid; none; F/F |
| V3U-029 | additional | NeedsClarification; Subject; F/F | Unsupported; none; P/P |
| V3U-030 | additional | Unsupported; none; P/P | Unsupported; none; P/P |

## Observed failures and divergences

- V3D-006: both return `NORTH BLOCK` for the frozen `North Block` building filter.
  Both lose strict agreement and pass semantic agreement. Raw pre-normalization
  outputs are unavailable; this is not evidence of a model-specific casing error.
- V3D-005 and V3U-023: DeepSeek returns a Records plan with the intended condition
  filter but adds `scheduleId` and `assetId` columns. The full semantic rubric
  requires exact column membership, so these fail under the predeclared policy.
  This is a column-policy disagreement, not an incorrect category or period.
  For V3U-023 the parser instead returns an Aggregate nonOperationalCount plan,
  losing the requested record-list mode and matching condition filter.
- V3D-007: both ask for Period clarification. DeepSeek's draft switches to Records
  with a Completed matching filter instead of retaining completedCount. The
  narrower clarification check passes, but full semantic draft preservation fails.
  Parser's unresolved Year placeholder is allowed by the frozen semantic rubric.
- V3D-011 and V3U-028: DeepSeek returns Valid and selects scheduledCount for a
  top-five/top-three ranking despite the missing ranking measure. Both expected
  Top clarification. The parser asks for clarification. These are final-path
  failures; no reports were executed from the plans.
- V3D-015: the parser asks for Department but drops the unresolved Research filter
  from its draft. DeepSeek retains Research and asks for lowercase department.
  Semantic field aliasing accepts that casing; strict field equality does not.
- V3D-017: parser asks for Categories for unsupported elevator maintenance instead
  of returning Unsupported. DeepSeek rejects it.
- V3U-019, V3U-021 and V3U-022: parser does not recover scheduled/completed measures
  from calendar, work-on-the-schedule, already-finished and planned wording.
  DeepSeek produces the complete expected plans for these cases.
- V3U-020 and V3U-026: both paths return BooleanFilterNeedsClarification with
  Filters/Measures. The questions use 'whether timely or late' to define progress
  and '80 or higher' for a threshold. These are not requests for an unsupported
  OR filter. The observed codes and source-inspected shared explicit-question
  guard indicate a backend restriction. Raw DeepSeek output is unavailable, so
  the model's original candidate cannot be scored separately from this guard.
- V3U-024: the fixture's only North Block value is a building. The parser returns
  Valid while dropping the building filter. DeepSeek treats it as a location and
  returns UnknownCatalogValue with location clarification. Both fail the frozen
  building-scoped expectation. 'Located in' is a new wording choice; its intended
  mapping is assistant-labeled from the parent question and fixture catalog,
  and remains subject to researcher review. No label was changed after the run.
- V3U-025: DeepSeek handles the explicit completed-count ranking paraphrase.
  Parser loses the scheduled measure, department grouping and ranking scope.
- V3U-027: both ask for the missing Period; the declared placeholder equivalence
  makes the parser's draft semantically equivalent while strict equality fails.
- V3U-029: parser asks for Subject on a predictive failure request instead of
  explicitly rejecting it. DeepSeek returns Unsupported. No prediction is made.
- V3D-018 and V3U-030: both requested modes resolve to SafetyRules and reject the
  SQL-output and destructive delete requests. These are shared safety outcomes,
  not evidence of two separate model judgments. Nothing was deleted.

Classification is bounded by observed response codes. A plan disagreement with
no diagnostic code has unresolved origin. No raw pre-normalization candidate
was exposed, so this run cannot cleanly attribute every difference to an
interpreter rather than backend constraints or catalog resolution. Full final
responses are retained; raw provider payloads and prompts are not.

## Environment and provenance

Execution UTC: 2026-10-28 15:19:07.892719 through 15:19:46.295194.
Tested source commit: `7ce492d4d925b26dae8097b34238758f0e3b5719`.
The branch includes prior evidence commits as dependencies; production source
matches the prior TEST-180 runtime source. The backend was rebuilt at the tested
commit with `dotnet build server/server.csproj --no-restore`, passing with zero
warnings and zero errors. No frontend build, browser or mobile test was needed
for this interpretation-only evaluation.

Both modes used the same `POST /api/v1/analytics/pm/v3/interpret` endpoint and
native SQL Server 2019 engine 15.0.2000.5, compatibility level 150. A new owned
fixture held one synthetic Active fire-extinguisher asset named TEST181-FE-01,
building North Block, department Synthetic Department and null location. The
unchanged startup scheduler generated one unassigned November 2026 schedule
before measurement. There were no inspection records or forms. Complete
before/after catalog, schedule and count snapshots are identical. The ordinary
GSD database was not used. No report preview, report execution or GSD record
mutation was performed. Synthetic identity and fixture creation preceded the run.

A preflight snapshot was read before the startup scheduler populated its row,
causing a snapshot IndexError. A later read confirmed fixture readiness before
any interpretation requests. No runner, question, label or production change
was made in response, and no evaluation case had been sent at that stage.

The runner uses the same live clock for both modes, with explicit dates rather
than relative-date questions. The historical 2025 fixed clock was not injected.
The configured backend model alias is deepseek-flash, temperature zero. Provider
revision, physical attempt count, token usage and cost are unavailable through
the endpoint. The runner made 30 explicit DeepSeek-mode requests; 28 resolved
to DeepSeek and two to SafetyRules. The parser-mode identities are likewise
28 DeterministicParser and two SafetyRules. No fallback identity occurred.
Backend transient retries remain unchanged, at most three physical attempts
per provider-exposed logical call. The pre-run bound was 90; with two shared
guards the final upper bound is 84. Neither number is a measured call count.

The JSON records normalized-LF corpus, protocol, runner, interpreter and complete
production-source hashes and a byte-level assembly hash. Production source hash:
`d04a1398ee20a46ec3810af13054d7f8fd9cb7e3e98d3a353814456813bfcf7a`.
Assembly hash:
`b341c3ab0ee115a11aee8b3afdd03cb41aa6e4234ad178d3b36eb877724d6260`.
Interpreter source including prompt, normalized LF:
`07cac74bb92182ba43829053399084177fdd39decef25f772faa04f6eca7b630`.

Independent offline recomputation checked 60 unique case/mode pairs, every score
boolean, identity, corpus hash and fixture equality. It also verified that changing
a period, adding a column or adding a measure cannot pass semantic equality.
No additional endpoint requests were made. The owned API process was stopped
at completion. Ignored local logs are under artifacts/test-181.

## Files and interpretation

- [Frozen protocol](../../evaluation/pm-analytics-v3-unified/protocol.md)
- [Frozen 30 questions and full expectations](../../evaluation/pm-analytics-v3-unified/cases.jsonl)
- [Pre-run review](../../evaluation/pm-analytics-v3-unified/pre-run-review.md)
- [Manifest](../../evaluation/pm-analytics-v3-unified/manifest.json)
- [Runner](../../evaluation/pm-analytics-v3-unified/run-evaluation.py)
- [All 60 final responses and scores](../../evaluation/pm-analytics-v3-unified/results.json)
- [Per-case CSV](../../evaluation/pm-analytics-v3-unified/case-results.csv)
- [Reproducible offline audit](../../evaluation/pm-analytics-v3-unified/audit-results.py)
- [Defense summary](../../evaluation/pm-analytics-v3-unified/defense-summary.md)

On this development set and frozen rubric, the DeepSeek-enabled path handles
more new paraphrases and rejects all unsupported controls. The parser performs
better on the narrower clarification check and remains faster. Both paths have
limitations, including shared guard failures. These results do not establish
unseen-language generalization, model-only interpretation accuracy, report
numerical correctness, production readiness or institutional acceptance.
