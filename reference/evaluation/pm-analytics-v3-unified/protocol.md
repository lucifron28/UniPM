# TEST-181 unified paired evaluation protocol

Freeze this protocol, the runner, manifest and all 30 questions before any run.
The original 18 question texts, outcome labels and partial assertions remain
unchanged in their historical files. TEST-139, TEST-146, TEST-179 and TEST-180
are retained. New full canonical expectations are a separate TEST-181
specification, not a correction to any old score.

## Corpus and review provenance

There are 16 Valid, nine NeedsClarification and five Unsupported cases. The
original set actually has eight Valid, seven NeedsClarification and three
Unsupported, including original V3D-016's unsupported sorting operation.
The proposal's eight/eight/two count was incorrect. Add eight valid paraphrases,
two clarification cases and two unsupported operations without relabeling the
originals. Report both subset and outcome-class denominators.

The eight new paraphrases correspond one-to-one to V3D-001 through V3D-006,
V3D-013 and V3D-014. Preserve their categories, time periods, filters, measures,
comparison grouping, ranking and threshold. They were newly authored for this
run, not chosen from previous successes. Original intentions and prior results
have already been exposed. Expectations were authored and reviewed by the
assistant against question meaning, the closed plan contract and all historical
partial assertions. The user approved execution, but has not independently
reviewed every new full label. Do not claim researcher-reviewed new gold,
held-out accuracy, independent acceptance, or freedom from development bias.

Each executable plan is fully specified, including schema/catalog version,
subject, mode, complete period, categories, measures, groupings, all filters,
ordering, top, presentation and columns. Clarification drafts also have complete
canonical expectations where a draft is expected; unsupported and designated
null-draft cases require null. Canonical record columns use the nine reporting
columns specified in the corpus. Extra technical IDs are not silently ignored.
V3D-015's new full draft retains the unresolved Research filter to preserve
requested scope; the historical partial assertions never specified that filter.
V3D-007 and V3U-027 have a null unresolved period. Unspecified ranking stays null.
A compliance request means on-time compliance, not completion progress.

## Execution controls

Use the same current backend binary, native SQL Server 2019 fixture and
POST /api/v1/analytics/pm/v3/interpret endpoint for both explicit interpreter
modes. The newly created isolated database contains one synthetic Active
fire-extinguisher asset, North Block, Synthetic Department, null location.
The unchanged startup scheduler creates one unassigned November 2026 cycle.
Capture all catalog and schedule rows and domain counts before/after the run.
No normal GSD database access, report previews, report executions or record
mutations are allowed by the runner. Synthetic identity/fixture preparation
occurs before measurement and is disclosed.

Run all 30 once per mode, alternating first mode by case. Request
DeterministicParser or DeepSeek explicitly. Reject unexpected resolved identity;
shared SafetyRules responses are permitted and counted separately. DeepSeek
unavailability must produce a failure, not parser fallback. No adapter retries,
selective reruns, prompt/parser/production tuning or gold edits after freezing.
Unchanged backend transient retries allow at most three physical attempts per
logical provider call. Pre-run maximum is 30 DeepSeek-mode requests and 90
physical attempts; actual physical attempts, usage and cost are unavailable.

Use a shared live server clock with recorded UTC start/end. All periods and
prediction dates are explicit. The old corpus's historical fixed clock is not
injected into production. This is a current-system repeat and extension.

## One rubric for all 30 and both paths

Status is exact Valid, NeedsClarification or Unsupported. Plan disposition is
Executable for Valid plus a plan, ClarificationDraft for NeedsClarification plus
a plan, None for no plan, Invalid otherwise. HTTP errors fail every case.
Strict clarification fields use exact set equality, disallowing duplicates.

Full strict plan agreement compares the complete plan to the new full canonical
gold. Object key order is ignored; arrays, values, nulls and all properties are
exact. Strict case success requires HTTP 200, correct status, exact fields,
correct disposition and full strict plan equality. Null expected/actual plans
are successful control dispositions, not correct executable plans.

Full semantic plan agreement applies only these declared equivalences:

- Known building/department filter values may differ by trim/case when matching
  NORTH BLOCK or SYNTHETIC DEPARTMENT. Unknown Research is not folded away.
- Category, measure and column order may differ; membership and duplicate
  multiplicity must remain identical. No extra IDs, measures or columns allowed.
- Table/Summary are equivalent only for ungrouped Aggregate plans. Grouped and
  Records plans must retain Table. No other presentation is accepted.
- An unresolved period may be null or the Year placeholder with basis pmCycle
  and all year/month/quarter/start/end properties null. An actual dated period
  is never discarded. Missing/unused properties of real periods are not waived.

All remaining fields, scope, filters, operators, thresholds, groupings, ordering,
ranking, subject and versions must match. Semantic clarification-field comparison
allows casing of known field identifiers only; it does not accept arbitrary or
missing fields. Semantic case success still requires correct status, field set,
disposition and complete semantic plan equality. Both paths use identical rules.

Report clarification appropriateness separately: correct NeedsClarification,
required semantic field set, and no executable disposition. This narrower
measure does not establish preservation of all draft parameters. Unsupported
controls require Unsupported, no fields and null plan. Report Valid cases and
controls separately as well as original/additional/overall totals. An overall
30-case score combines heterogeneous tasks, not 30 executable report questions.

Retain every final response, per-case differences, requested/resolved identities,
HTTP status, latency, source commit, full corpus/protocol hashes and runtime
source/binary hashes. Backend round-trip latency is not isolated model timing.
Classify observed provider/transport failure and explicit backend constraint or
catalog-resolution codes. Otherwise mark origin unresolved. The endpoint does
not expose raw pre-normalization candidates; do not claim model-only error
attribution or raw-output evidence. Capture no raw prompts, provider payloads,
credentials or tokens. Never modify labels or code to improve the result.

## Pre-run review and limits

Offline checks must verify all 30 unique IDs, class/subset counts, complete plan
keys, one-to-one paraphrase plans, original texts/status/field/disposition labels,
and compatibility with every historical non-null partial assertion. Inspect new
paraphrases for preserved intent and all control rationales. This is an
assistant review of assistant-authored labels, not an independent review.
Commit the frozen specification and runner, build unchanged backend source,
prepare the isolated fixture, then run once. Afterward independently recompute
saved scores without further endpoint requests and preserve every failure.
