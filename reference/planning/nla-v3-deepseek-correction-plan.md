---
id: NLA-V3-DEEPSEEK-CORRECTION-PLAN-001
status: draft
recordedAt: 2026-10-23
evidenceLevel: source-inspected
sourceCommit: 83ac94b8d219be3e50e52f303831cbb660ba1b8f
---

# DeepSeek interpretation findings and correction plan

## Recorded conclusion from TEST-142

The supplementary live evaluation demonstrates a bounded capability to
interpret supported maintenance questions, with substantial reliance on
backend safeguards. DeepSeek candidates matched all declared expectations in
9 of 17 provider-exposed cases. The final system matched all expectations in
13 of 18 cases, 16 of 18 classifications, and 67 of 69 declared plan assertions.
One of the 18 questions was rejected before a provider call. There were no
provider errors or retries, and no reports were executed.

Five final cases differed from the frozen expectations. V3D-011 is the main
behavior defect: the system selected a ranking measure where the expected
response required clarification. V3D-006 and V3D-015 involve capitalization
differences. V3D-007 retains a different draft structure. V3D-005 requests
additional time-basis clarification and needs a requirement review before
being treated as an application defect.

These results support a qualified development-set capability claim. They do
not support a claim that DeepSeek passed all 18 cases, generalized to unseen
questions, or achieved the deterministic parser's recorded score. Preserve
TEST-142 and its frozen corpus. Any correction and later verification must
receive a separate record and must disclose the changed implementation and
any separately reviewed expectation changes.

The researcher requested that this conclusion be recorded, followed by a
cause investigation and written plan. Implementation is outside this task.

Sources: [TEST-142](../evidence/test-runs/TEST-142-deepseek-v3-development-comparison.md),
[case observations](../evaluation/manuscript-2026-10-23/deepseek-comparison-observations.json),
and [manuscript comparison](../evaluation/manuscript-2026-10-23/manuscript-deepseek-comparison.md).

## Investigation and proposed corrections

This investigation inspected the committed evaluation checkout at the source
commit above. The application code is the same as the TEST-142 execution
commit, `c47633cec9822d7445b6f8f03f528ad4ff455313`; the intervening commit
adds evidence only. Existing uncommitted Web/mobile changes in the original
checkout were not inspected as the tested application version.

The findings below combine captured TEST-142 responses with source inspection.
No new test, provider request, build, replay, database operation or browser
session was run for this plan. Internal model reasoning is not observable, so
the report identifies response behavior and backend causes without asserting
why the provider internally chose a particular answer.

### V3D-011: ambiguous ranking passed through the DeepSeek path

**Observed.** The question requests scheduled and completed counts for the top
five departments without naming the ranking measure. The model returned
`Valid`, with `Top.Measure = scheduledCount`. The final interpreter retained
that choice. The expected response was `NeedsClarification` for `Top`.

**Cause supported by source.** The deterministic parser's
`ResolveRankingMeasure` accepts an explicitly stated single ranking measure,
or defaults when only one measure is selected. With multiple measures and no
ranking criterion, it returns null and the parser adds `Top` clarification.
The model path's `ApplyInterpretedQuestionConstraints` checks categories,
periods, thresholds and outcome wording, but contains no equivalent ranking
intent check. The typed plan validator checks that the proposed ranking
measure is selected and its limit/grouping are valid. It does not know whether
the user selected that measure. Both scheduledCount and completedCount are
selected, so the model's guess is structurally valid and passes validation.
The prompt contains a general instruction not to guess, but no specific
multi-measure ranking rule or corresponding example.

**Proposed correction, first priority.** Reuse the existing ranking-resolution
logic in a narrow shared question constraint applied to the model path.
Distinguish a grouping phrase from an explicit ranking measure. If the question
requests ranking across multiple measures without a unique criterion, return
`NeedsClarification` with `Top`, clear the guessed ranking, and preserve the
safe category, period, requested measures and grouping. Do not accept a model
that drops one requested measure as evidence that the question had only one.
Keep explicit ranking requests and the existing unambiguous single-measure
case working. Validate the draft and retain confirmation before execution.
Add a short prompt rule as secondary guidance; the backend rule is the
enforcement point. Do not replace DeepSeek with a full deterministic parse or
introduce another parser/service.

**User flow implication.** The current guided web flow does not include `Top`
in its supported clarification steps. It provides an edit-question fallback.
The minimum correction can use that existing safe flow with clear wording,
such as asking whether ranking should use scheduled or completed count.
An inline ranking selector would be a separate, optional web change. Do not
claim that selector already exists or is required to measure the backend fix.

**Future verification criterion.** For this case and fresh reversed-measure,
bottom-ranking and equivalent-wording cases, an unstated criterion must not
produce a `Valid` ranked interpretation. An explicit completed-count ranking
must remain valid. Exercise the actual model interpreter with controlled
typed candidates that guess either measure, rather than testing only the
deterministic parser.

Source anchors:
[model constraints](../../server/Features/Reports/PmAnalyticsV3QuestionInterpreter.cs#L424),
[parser ranking resolution](../../server/Features/Reports/PmAnalyticsV3QuestionInterpreter.cs#L1557),
[parser missing-field decision](../../server/Features/Reports/PmAnalyticsV3QuestionInterpreter.cs#L992),
[plan ranking validation](../../server/Features/Reports/PmAnalyticsV3PlanValidator.cs#L144),
and [web clarification support](../../web/src/features/reports/pm-analytics-v3.tsx#L1811).

### V3D-015: catalog field identifiers leak into clarification identifiers

**Observed.** The model correctly preserved the requested Research filter.
Because Research is absent from the controlled catalog, backend resolution
requested clarification. It returned `department`, while the expected
clarification identifier is `Department`.

**Cause supported by source.** `AddResolutionError` adds the lowercase filter
field identifier directly to the clarification-field collection. The final
interpreter returns that collection after catalog resolution, bypassing the
earlier model-envelope clarification whitelist. The whitelist and web guided
filter steps use `Department`, `Building` and `Location`. An existing acceptance
test also expects lowercase `building` for an unknown building, demonstrating
that this inconsistency is encoded in current coverage.

This is a response-contract inconsistency, not merely a cosmetic scoring
difference. The web source treats an unrecognized clarification identifier as
an unsupported step and requires question editing. That effect is supported
by source inspection; no browser reproduction was performed. The original
TEST-142 observation remains unchanged.

**Proposed correction, second priority.** Map catalog filter identifiers to
the existing canonical clarification identifiers when building clarification
fields. Keep validation error dictionary keys and normalized filter IDs
unchanged. Cover department, building, location, asset code and asset ID where
applicable. Reject or safely route any unmapped field instead of inventing an
identifier. Align the existing unknown-building expectation with the documented
clarification contract, with the test change disclosed separately from the
frozen development labels.

**Future verification criterion.** Unknown catalog values must return valid
canonical clarification identifiers with the intended draft filter preserved.
The web must recognize named-filter clarification and keep preview unavailable
until the user resolves the filter. This browser/client check is proposed,
not completed. It does not require sending more questions to DeepSeek.

Source anchors:
[resolution errors](../../server/Features/Reports/PmAnalyticsV3QueryService.cs#L1447),
[interpreter return](../../server/Features/Reports/PmAnalyticsV3QuestionInterpreter.cs#L354),
[clarification vocabulary](../../server/Features/Reports/PmAnalyticsV3Catalog.cs#L338),
[current acceptance expectation](../../tests/UniPM.Api.Tests/Reports/PmAnalyticsV3AcceptanceTests.cs#L299),
and [web named filters and fallback](../../web/src/features/reports/pm-analytics-v3.tsx#L1799).

### V3D-007: period repair retains a different report mode

**Observed.** The model interpreted February and November as a continuous
range, selected Records mode and used a Completed schedule-status filter.
The backend detected the period mismatch and requested Period clarification,
but kept the Records draft with an empty measures list. The frozen expectation
requires a draft containing completedCount.

**Cause supported by source.** `HasExplicitPeriodMismatch` compares the model
period with the rule parser's period resolution. A mismatch clears the period
and adds clarification, while preserving the candidate's other fields. It
does not reconcile Aggregate versus Records intent for this wording. The rule
parser defaults to Aggregate unless it detects a records request, so the two
interpretation paths choose different modes. Records mode explicitly forbids
measures. Adding completedCount to the existing Records draft would therefore
create an invalid plan.

**Proposed correction after a mode-policy review.** First document whether
unqualified wording such as "show completed fire extinguishers" defaults to
an aggregate count, consistent with the current deterministic parser, or needs
a Mode clarification. The preferred compatibility option is the existing
aggregate default when no source-record wording is present. Preserve explicit
records requests and their Completed filter. If intent cannot be resolved,
request Mode as well as Period. Keep the disjoint-month request unresolved;
do not add union periods or silently expand it into a range.

If the aggregate default is retained, reconstruct a consistent aggregate draft
with completedCount and compatible columns, rather than adding a measure to
Records mode. If a different mode policy is selected, register it in a new
reviewed dataset/protocol version. Do not rewrite the TEST-142 expectation or
its result after seeing this response.

**Future verification criterion.** Check explicit counts, explicit records and
unqualified wording with the same separated months. Every draft must preserve
resolved intent, remain non-executable until required choices are resolved,
and pass the existing mode-specific draft rules. This is a semantic policy
decision before implementation, not a confirmed model defect in isolation.

Source anchors:
[period repair](../../server/Features/Reports/PmAnalyticsV3QuestionInterpreter.cs#L528),
[period comparison](../../server/Features/Reports/PmAnalyticsV3QuestionInterpreter.cs#L599),
[parser mode and measures](../../server/Features/Reports/PmAnalyticsV3QuestionInterpreter.cs#L929),
[records wording](../../server/Features/Reports/PmAnalyticsV3QuestionInterpreter.cs#L1597),
and [Records validation](../../server/Features/Reports/PmAnalyticsV3PlanValidator.cs#L96).

### V3D-005: conservative clarification disagrees with the declared default

**Observed.** The model asked whether "during 2025" meant scheduled PM cycles
or inspection dates. Its draft matched all six declared plan assertions. The
final interpreter retained the clarification, while the frozen label expected
Valid. The model's internal reason for broadening the ambiguity is unknown.

**Cause supported by source.** The model envelope can contain a safe partial
plan and a permitted Period clarification. The interpreter accepts that
envelope; it does not override an unnecessary model clarification merely
because the deterministic parser would choose a year. The design specifies
PM-cycle periods and excludes inspection-date reporting. The prompt emphasizes
clarification for inspected-in-month wording, but does not expressly settle
every generic-year phrasing. The existing deterministic regression case treats
this exact question as Valid.

**Proposed disposition.** Agree the time-basis rule before changing code or
labels. The preferred compatibility policy is to interpret a generic calendar
year as the scheduled PM-cycle year when the user has not explicitly requested
inspection/completion dates, and to show that basis in the reviewed scope.
Preserve clarification for genuinely ambiguous date wording and rejection of
unsupported inspection-date reporting. Document the distinction and add narrow
prompt guidance. Do not strip all Period clarifications or manufacture Valid
responses from incomplete drafts to raise the score.

If the study instead permits this conservative clarification, adopt that
policy prospectively in a separately reviewed label/protocol version. The
original strict failure remains in TEST-142 either way. This case is a
requirement/model-consistency finding until that policy is settled.

Source anchors:
[time-basis design](nla-v3-composable-reporting-design.md#meaning-of-the-data),
[model prompt](../../server/Features/Reports/PmAnalyticsV3QuestionInterpreter.cs#L1863),
[date checks](../../server/Features/Reports/PmAnalyticsV3QuestionInterpreter.cs#L470),
and [existing parser regression](../../tests/UniPM.Api.Tests/Reports/PmAnalyticsV3RegressionTests.cs#L71).

### V3D-006: scoring crosses a normalization boundary

**Observed.** The model returned North Block exactly as expected. Catalog
resolution converted it to NORTH BLOCK. The original exact comparator counted
the changed filter-array value as a mismatch.

**Cause supported by source.** Catalog resolution searches using trimmed,
uppercase text and returns uppercase values. The query service then uses that
canonical form in the resolved plan. `ComparePartial` compares array elements
as exact serialized JSON. The deterministic benchmark stopped before catalog
resolution; the final DeepSeek response was measured after it.

**Proposed disposition.** Preserve the existing canonical lookup behavior.
Do not change production catalog normalization solely to satisfy the old
pre-resolution string assertion. Keep the original strict score. In a future
protocol, predeclare stage-specific expectations, or add a separately labeled
semantic comparison limited to catalog value equivalence. Always retain the
original text and canonical value. Never apply blanket case-insensitive
comparison to measures, clarification identifiers, enums or other contracts.
Keep the typed model and final system scores separate.

**Future verification criterion.** A dedicated catalog-resolution check must
show that North Block and its canonical value resolve to the same fictional
building without changing scope. A reviewed scoring rule must state which
stage and representation it measures before a new scored run.

Source anchors:
[lookup normalization](../../server/Features/Reports/PmAnalyticsV3QueryService.cs#L342),
[resolved value](../../server/Features/Reports/PmAnalyticsV3QueryService.cs#L161),
and [exact array comparison](../../tools/UniPM.PmAnalytics.V3Evaluation/Program.cs#L662).

## Proposed implementation order

1. Resolve the V3D-005 time-basis and V3D-007 mode policies in writing before
   modifying their behavior or creating new labels. This does not block the
   independently defined ranking and clarification-contract corrections.
2. Correct ranking intent enforcement in the existing interpreter/parser
   module, and canonical clarification identifiers at catalog resolution.
   Keep this backend change separate from optional web improvements.
3. Apply the agreed mode/time-basis policy with the smallest changes to the
   current prompt and constraints. Avoid exact-question exceptions, expanded
   reporting capabilities, new provider dependencies and broad refactoring.
4. Review the evaluation protocol prospectively for stage-specific catalog
   normalization. Preserve every original label and TEST-142 score.
5. Implement only the focused verification approved for that implementation
   task. Reuse the current interpreter observation/injection approach and
   controlled typed candidates to exercise model-path guards without network
   calls. The existing parser-only tests cannot prove those guards work.
6. After the local correction is verified, prepare a separately identified
   live comparison with the model/prompt/source hashes and a bounded budget.
   Freeze any new wording or reviewed label version before execution. Do not
   describe fresh wording designed during these fixes as untouched held-out
   evidence. Report the new run alongside TEST-142, including any failures.

## Verification boundaries for the future implementation

For the requested implementation scope, select one most relevant focused
test invocation. Run it once after implementation, with at most one focused
in-scope correction and one rerun under the repository limits. Proposed
regression coverage should include the ambiguous versus explicit ranking
cases and canonical catalog clarification identifiers through the actual
model interpreter. No tests are added or run by this planning task.

If web clarification behavior is included in a separately scoped change,
use its own single focused client check. Source inspection currently supports
the fallback concern but does not establish observed browser behavior. Do not
bundle backend and web test suites or run broad verification automatically.

A future provider run must distinguish typed candidates, backend-controlled
outcomes and pre-provider safety rejections. A successful measurement does
not require every model response to match. New implementation results must
not replace or retroactively improve TEST-142.

## Completion and remaining decisions

The requested conclusion is recorded and the five mismatches have source-based
explanations and proposed dispositions. Ranking enforcement and canonical
clarification identifiers are concrete correction candidates. Mode/default
and time-basis policy choices remain explicit review points. Provider-internal
reasoning, browser effects and correction effectiveness remain unverified.
Implementation, tests, additional provider calls and manuscript DOCX edits
have not started under this plan.

## Subsequent implementation and verification

After the planning task, the researcher requested implementation and a rerun.
The preferred compatibility defaults were adopted in prompt guidance, ranking
and canonical clarification checks were implemented, and no frozen label or
original evidence was changed. TEST-143 records the focused controlled
regression, and TEST-144 records the new live development comparison.

The rerun matched all declared expectations in 16/18 cases. V3D-006 retains
the original catalog normalization mismatch. V3D-011 now returned a contradictory
Valid envelope with a Top clarification, which the existing validator rejected
without a final plan. That response-consistency failure remains unresolved.
No further fix or repeat provider run followed it. See the
[correction results](../evaluation/manuscript-2026-10-23/manuscript-deepseek-correction-results.md).
The earlier planning-time statements above are preserved as the record of
what was proposed before implementation.
