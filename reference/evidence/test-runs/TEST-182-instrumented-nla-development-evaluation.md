---
id: TEST-182
type: test-run
title: Instrumented paired repeat of the frozen TEST-181 questions
status: executed
recordedAtUtc: 2026-10-28T17:27:44Z
testedCommit: a37571d0aa15333012c86152dddbaccff9e6d3ac
sourceBranch: feature/nla-instrumented-evaluation
evidenceLevel: real-provider-executed
---

# Instrumented supplementary NLA evaluation

All 30 frozen TEST-181 questions ran once through each explicit interpreter,
60 requests total, on the same endpoint and controlled SQL Server fixture.
Observation-only hooks captured raw proposals, backend effects, provider usage,
reliability and timing. No tracked production source, prompt, interpreter rule,
validation condition, retry policy, question, expectation, scoring rule or
historical result was changed. No case was selectively rerun.

Local run time was October 29, 2026, 01:27:07 through 01:27:44, Asia/Shanghai.
Exact UTC interval: 2026-10-28T17:27:07.003130+00:00 through
2026-10-28T17:27:44.138435+00:00. This is supplementary development-set evidence,
not independent accuracy, held-out evaluation or proof of generalization.
The full labels retain TEST-181's assistant-authored review status.

## Reliability and coverage

| Observation | Parser, 30 requests | DeepSeek, 30 requests |
| --- | --- | --- |
| Actual provider client dispatches | 0 | 28 |
| Successful provider HTTP responses | 0 | 28 |
| Provider retries | 0 | 0 |
| Provider HTTP failures | 0 | 0 |
| Transport failure/timeout events | 0 | 0 |
| Endpoint HTTP 200 | 30 | 29 |
| Endpoint HTTP 503 InvalidPlan | 0 | 1 |
| Shared SafetyRules outcomes | 2 | 2 |
| Typed pre-validation proposals captured | 24 | 28 |
| Raw provider content captured | not applicable | 28 |
| Complete usage/cache split | not applicable | 28/28 calls |
| Observer records/errors | 30/30, zero errors | 30/30, zero errors |

Actual dispatches are observed calls to SendAsync, not a maximum-call estimate.
HTTP 200 confirms all 28 provider responses. No provider dispatch failed; retry
limits were never reached. Server receipt or billing of an unsuccessful dispatch
could not be independently proven, but that limitation did not affect this run.
V3D-018 and V3U-030 used SafetyRules in both modes before any model/parser proposal.
These are shared safety outcomes, not separate model judgments.

V3D-012 is the endpoint failure. The provider returned HTTP 200 with a Valid
proposal containing unsupported groupBy assetId and an assetId aggregate column.
The observed schema validator rejected GroupBy and Columns. The API returned
HTTP 503 InvalidPlan with those fields. This is a model-proposal contract
violation followed by backend rejection, not provider transport or availability
failure. Its real usage and estimated cost remain included.

All provider replies reported model alias deepseek-flash and fingerprint
bc19ba97cbe016479903222bb1578db6. The configured alias was deepseek-flash,
temperature zero. An exact weight/build revision was not supplied beyond that
alias/fingerprint and is unavailable. A documentation release mapping is not
proof of the particular served weights.

## Reported tokens and estimated cost

| Provider-reported usage | Tokens |
| --- | ---: |
| Input | 120712 |
| Output | 4286 |
| Total | 124998 |
| Cached input | 114688 |
| Uncached input | 6024 |

Each response satisfies cached plus uncached equals input, and input plus output
equals reported total. No local tokenizer or character approximation was used.
No-call requests have null/not-applicable reported token fields and zero observed
provider API spend. Aggregate parser token totals of zero mean no calls occurred;
they are not zero-valued usage reports fabricated for the provider.

The [official pricing page](https://api-docs.deepseek.com/quick_start/pricing/)
was fetched before execution. pricing.json records its retrieval UTC and HTML
SHA-256. Documented deepseek-flash off-peak USD rates per million tokens were
0.003 cached input, 0.15 uncached input and 0.60 output. Peak rates were twice
those values, in weekday 01:00-04:00 and 06:00-10:00 UTC windows excluding Chinese
public holidays. Every dispatch was at 17:27 UTC, outside those windows, so no
holiday assumption was needed.

| Cost component | Decimal calculation, USD | Estimated USD |
| --- | --- | ---: |
| Cached input | 114688 x 0.003 / 1000000 | 0.000344064 |
| Uncached input | 6024 x 0.15 / 1000000 | 0.000903600 |
| Output | 4286 x 0.60 / 1000000 | 0.002571600 |
| Total, all 28 actual calls | Sum | **0.003819264** |

This is about USD 0.00382, or 0.382 US cents. It is a token-price estimate,
not a verified invoice/account debit. All actual calls had sufficient usage
and pricing evidence; there is no unknown-cost call. Actual discounts, grants,
taxes and account adjustments were not inspected. Missing usage/cache split
would remain unavailable. The calculator returns a range for unresolved holiday
pricing rather than assuming a tier. The [completion API documentation](https://api-docs.deepseek.com/api/create-chat-completion)
defines the reported token and cache fields.

## Latency

Milliseconds; provider statistics cover 28 calls, other rows cover all 30 requests
per mode including shared guards. p95 uses nearest rank.

| Latency | Parser mean / median / p95 | DeepSeek mean / median / p95 |
| --- | --- | --- |
| Provider transport | not applicable | 1238.64 / 1247.16 / 1551.73 |
| Provider logical stage | not applicable | 1239.65 / 1247.51 / 1552.02 |
| Server pipeline elapsed | 6.58 / 3.12 / 7.94 | 1163.00 / 1244.24 / 1555.50 |
| Backend processing residual | 6.58 / 3.12 / 7.94 | 6.94 / 3.26 / 21.84 |
| Server excluding provider logical stage | 6.58 / 3.12 / 7.94 | 5.99 / 2.93 / 21.63 |
| Total endpoint round trip | 24.63 / 23.49 / 31.12 | 1174.65 / 1260.31 / 1558.66 |

Provider transport is existing ledger wall time from SendAsync through the
bounded successful body read or failure detection. It includes network transit
and provider work. Pure inference time and time to first token are unavailable.
Logical provider stage also includes request construction, response parsing,
retries and backoff. No retries occurred here.

Backend processing residual is measured server elapsed minus summed provider
transport time. It includes authentication, SQL, validation, JSON, observation
overhead and any retry waits; it is not CPU time. Server minus logical stage
separately excludes the whole provider-client stage. Endpoint time is client HTTP
round trip to the final body. Client/server residual includes loopback transport
and response/telemetry completion outside the server stopwatch. Every request's
values are retained; none was silently clamped. Instrumentation affects timing,
so this is not an uninstrumented performance benchmark. Medians from different
populations cannot be added to reconstruct a total median.

## Raw interpretation and backend effects

Raw provider content, parsed JSON and typed pre-validation candidates are saved
for 28 calls. Raw JSON and typed candidate have zero status/field/plan differences
on those calls. No requests, credentials, raw prompts, authorization headers,
token maps or reasoning traces are retained.

Parser proposals were captured from actual Parse returns on 24 requests. Missing
raw proposals are V3D-009, V3D-010, V3D-017, V3D-018, V3U-029 and V3U-030: four
early parser-path guards and two shared safety guards. No parser was reinvoked
to invent a proposal. DeepSeek's two shared guards likewise have no raw output.

Eleven of 28 DeepSeek typed proposals and one of 24 parser typed proposals
changed in status, clarification fields or plan before the final response.
The full JSON preserves question-constraint decisions/ref changes, schema
errors, catalog results and exact raw/final differences. A finalValidatedPlan is
recorded only for Valid after the observed schema/catalog path. Clarification
plans remain drafts, not fully validated executable plans.

- V3D-006: both actual proposals use North Block. Both catalog results use
  NORTH BLOCK. This directly locates this run's strict casing divergence in
  shared backend normalization, rather than either interpreter. It does not
  rewrite old results or claim access to old runs' raw output.
- V3U-020 and V3U-026: DeepSeek's actual proposals exactly match the complete
  frozen semantic expectations for progress and an 80-percent threshold. The
  backend then removes each plan and returns BooleanFilterNeedsClarification
  with Filters/Measures. This is observed backend degradation of two correct
  proposals under the rubric. A broad shared OR guard reacts to 'whether timely
  or late' and '80 or higher'. Both final paths still fail; scores are not raised.
- V3D-011 and V3U-028: the raw model proposals already choose scheduledCount for
  top-five/top-three ranking, although the gold requires Top clarification.
  Backend validation accepts them. The unstated choice is present in the model
  proposal; it is not inserted only by later validation.
- V3D-012: raw proposal contains the invalid assetId grouping/column. Captured
  schema errors reject it, producing endpoint 503 despite provider HTTP 200.
- V3D-007: the model supplies a contiguous February-November range for ambiguous
  two-month wording. Question constraints remove the period and force
  clarification. Its record-mode draft still fails the expected completedCount
  draft, so full semantic success remains false.
- V3D-008, 009 and 010: concrete model plans for ambiguous OR/negated requests are
  removed by observed constraints. Final Filters/Measures clarification matches
  these frozen controls.
- V3D-015: unknown Research is retained in the model filter. Catalog resolution
  changes Valid to NeedsClarification with department. Scope remains intact;
  the frozen semantic rubric allows clarification-identifier casing.
- V3U-024: the model uses location for North Block. Catalog lookup fails because
  the fixture has North Block only as a building. The wrong field is in the
  observed proposal; lookup correctly requests clarification. The intended
  building mapping remains TEST-181's assistant-authored label limitation.
- V3D-016: the model proposes sorted output; a backend constraint changes it to
  UnsupportedQuestionOrdering, matching the frozen unsupported outcome.

On the same 28 captured DeepSeek proposals, descriptive full-rubric semantic
agreement is 16/28 before constraints and 19/28 on final responses. Five failing
proposals become successful final outcomes; two successful proposals become
failing final outcomes. The two shared safety successes bring the final path
total to 21/30. This is not independent model accuracy, and missing proposals
are excluded rather than silently scored as errors or successes.

## Per-case transitions

Each cell gives typed proposal status, final status, dispatch count and whether
status/fields/plan changed. No observed change with no proposal does not prove
anything about an unobserved candidate. CSV/JSON preserve all numeric values.

| Case | Parser | DeepSeek |
| --- | --- | --- |
| V3D-001 | Valid -> Valid; 0 calls; no observed change | Valid -> Valid; 1 calls; no observed change |
| V3D-002 | Valid -> Valid; 0 calls; no observed change | Valid -> Valid; 1 calls; no observed change |
| V3D-003 | Valid -> Valid; 0 calls; no observed change | Valid -> Valid; 1 calls; no observed change |
| V3D-004 | Valid -> Valid; 0 calls; no observed change | Valid -> Valid; 1 calls; no observed change |
| V3D-005 | Valid -> Valid; 0 calls; no observed change | Valid -> Valid; 1 calls; no observed change |
| V3D-006 | Valid -> Valid; 0 calls; changed | Valid -> Valid; 1 calls; changed |
| V3D-007 | NeedsClarification -> NeedsClarification; 0 calls; no observed change | Valid -> NeedsClarification; 1 calls; changed |
| V3D-008 | NeedsClarification -> NeedsClarification; 0 calls; no observed change | Valid -> NeedsClarification; 1 calls; changed |
| V3D-009 | not reached -> NeedsClarification; 0 calls; no observed change | Valid -> NeedsClarification; 1 calls; changed |
| V3D-010 | not reached -> NeedsClarification; 0 calls; no observed change | Valid -> NeedsClarification; 1 calls; changed |
| V3D-011 | NeedsClarification -> NeedsClarification; 0 calls; no observed change | Valid -> Valid; 1 calls; no observed change |
| V3D-012 | NeedsClarification -> NeedsClarification; 0 calls; no observed change | Valid -> HTTP 503 InvalidPlan; 1 calls; changed |
| V3D-013 | Valid -> Valid; 0 calls; no observed change | Valid -> Valid; 1 calls; no observed change |
| V3D-014 | Valid -> Valid; 0 calls; no observed change | Valid -> Valid; 1 calls; no observed change |
| V3D-015 | NeedsClarification -> NeedsClarification; 0 calls; no observed change | Valid -> NeedsClarification; 1 calls; changed |
| V3D-016 | Unsupported -> Unsupported; 0 calls; no observed change | Valid -> Unsupported; 1 calls; changed |
| V3D-017 | not reached -> NeedsClarification; 0 calls; no observed change | Unsupported -> Unsupported; 1 calls; no observed change |
| V3D-018 | not reached -> Unsupported; 0 calls; no observed change | not reached -> Unsupported; 0 calls; no observed change |
| V3U-019 | NeedsClarification -> NeedsClarification; 0 calls; no observed change | Valid -> Valid; 1 calls; no observed change |
| V3U-020 | NeedsClarification -> NeedsClarification; 0 calls; no observed change | Valid -> NeedsClarification; 1 calls; changed |
| V3U-021 | NeedsClarification -> NeedsClarification; 0 calls; no observed change | Valid -> Valid; 1 calls; no observed change |
| V3U-022 | NeedsClarification -> NeedsClarification; 0 calls; no observed change | Valid -> Valid; 1 calls; no observed change |
| V3U-023 | Valid -> Valid; 0 calls; no observed change | Valid -> Valid; 1 calls; no observed change |
| V3U-024 | Valid -> Valid; 0 calls; no observed change | Valid -> NeedsClarification; 1 calls; changed |
| V3U-025 | NeedsClarification -> NeedsClarification; 0 calls; no observed change | Valid -> Valid; 1 calls; no observed change |
| V3U-026 | NeedsClarification -> NeedsClarification; 0 calls; no observed change | Valid -> NeedsClarification; 1 calls; changed |
| V3U-027 | NeedsClarification -> NeedsClarification; 0 calls; no observed change | NeedsClarification -> NeedsClarification; 1 calls; no observed change |
| V3U-028 | NeedsClarification -> NeedsClarification; 0 calls; no observed change | Valid -> Valid; 1 calls; no observed change |
| V3U-029 | not reached -> NeedsClarification; 0 calls; no observed change | Unsupported -> Unsupported; 1 calls; no observed change |
| V3U-030 | not reached -> Unsupported; 0 calls; no observed change | not reached -> Unsupported; 0 calls; no observed change |

## Unchanged scoring and historical comparison

Under the imported, unchanged TEST-181 rubric:

| Score | Parser | DeepSeek |
| --- | --- | --- |
| Strict case success | 16/30 | 19/30 |
| Semantic case success | 19/30 | 21/30 |
| Original semantic | 16/18 | 14/18 |
| Additional semantic | 3/12 | 7/12 |
| Expected Valid questions, semantic | 8/16 | 11/16 |

All 30 deterministic HTTP/final responses exactly match TEST-181, including plans
and diagnostics. This supports observation conformance on this exercised set,
not every possible input or timing equivalence. DeepSeek's prior TEST-181 result
was 22/30; this new run is 21/30 because V3D-012 now fails validation. These are
fresh provider outputs. No selective repetition was used to recover an old score.

## Runtime, preservation and verification

Protocol/harness commit: a37571d0aa15333012c86152dddbaccff9e6d3ac.
The isolated build copies tracked server sources at parent
06e8827182037fa1dbd2bcaf41a3fc6d2a40ef37; production source is identical at the
tested commit. The results include 203 original source hashes, all observation
edits, observer hash, patch hash and instrumented assembly hash. Four copied
source files receive observation insertions; one helper is added. Reversing the
insertions reconstructs original business source text exactly. The tracked
server tree is never edited. This is explicitly an instrumented evaluation
binary, not an unchanged production binary.

Build command: dotnet build artifacts/test-182/runtime/server.csproj.
It passed with zero warnings and errors. Fixture migration/identity preparation
passed. No compile repair, interpreter tuning or case rerun was needed. No browser,
mobile, deployment or numerical-report test was performed.

Native SQL Server 2019 engine 15.0.2000.5, compatibility 150, held one fictional
Active fire-extinguisher asset TEST182-FE-01, North Block, Synthetic Department,
null location. Unchanged startup scheduling created one unassigned November
2026 cycle before measurement. No inspections/forms existed. Complete catalog,
schedule and count snapshots match before and after. The normal GSD database
was not used. No reports were previewed/executed and no GSD records were changed.

TEST-181 file hashes match before/after, including original questions, labels,
scoring and results. Other historical records and production source are
unchanged. The independent offline audit recomputed 60 final score decisions,
validated reported token/cache sums, decimal cost, timing arithmetic, actual
calls, all source/file hashes and parser conformance. It passed with no new
network/provider requests. The owned API process was stopped. Local logs and
copied runtime remain ignored under artifacts/test-182.

## Files

- [Protocol](../../evaluation/pm-analytics-v3-instrumented/protocol.md)
- [Observer generator](../../evaluation/pm-analytics-v3-instrumented/instrument.py)
- [Observation-only patch](../../evaluation/pm-analytics-v3-instrumented/instrumentation.patch)
- [Observer helper](../../evaluation/pm-analytics-v3-instrumented/TraceObserver.cs)
- [Documented pricing](../../evaluation/pm-analytics-v3-instrumented/pricing.json)
- [Runner](../../evaluation/pm-analytics-v3-instrumented/run-instrumented.py)
- [Full raw/final data and traces](../../evaluation/pm-analytics-v3-instrumented/results.json)
- [60 per-request rows](../../evaluation/pm-analytics-v3-instrumented/per-request.csv)
- [28 provider-call rows and cost components](../../evaluation/pm-analytics-v3-instrumented/provider-calls.csv)
- [Summary statistics](../../evaluation/pm-analytics-v3-instrumented/summary.json)
- [Offline audit](../../evaluation/pm-analytics-v3-instrumented/audit-results.py)
- [Reproduction guide](../../evaluation/pm-analytics-v3-instrumented/README.md)

This measures provider usage and exposes backend effects on a known development
corpus. It does not establish independent accuracy, unseen-language generalization,
numeric-report correctness, production readiness or institutional acceptance.
