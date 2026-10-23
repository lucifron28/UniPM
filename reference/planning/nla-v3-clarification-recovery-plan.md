---
id: NLA-V3-CLARIFICATION-RECOVERY-PLAN-001
status: draft
recordedAt: 2026-10-23
sourceCommit: 5f08fead3a5d21bc4a9af842d1e47e763a7600c2
evidenceLevel: source-inspected
---

# Clarification recovery and catalog comparison correction plan

## Purpose and evidence

Complete the ranking clarification workflow left unresolved by TEST-144 and
define how a future evaluation distinguishes equivalent catalog values from
incorrect scope. This is a plan for the next implementation task. No code,
tests, provider calls or manuscript DOCX changes are performed by this record.

TEST-144 matched all declared expectations in 16/18 cases and classifications
in 17/18. The remaining differences were:

- V3D-011 returned status Valid together with Top clarification and no selected
  ranking measure. The backend rejected the contradictory envelope safely,
  returning no final plan rather than the expected clarification draft.
- V3D-006 correctly resolved North Block to canonical NORTH BLOCK. The original
  exact string comparison counted the representation change as a mismatch.

The original 62/69 final-plan assertion score also remains part of the result.
The rejected ranking response contributed zero of its six final-plan
assertions. A later recovery must not retroactively change those observations.

Sources are [TEST-144](../evidence/test-runs/TEST-144-deepseek-v3-post-correction-comparison.md)
and the [sanitized observations](../evaluation/manuscript-2026-10-23/deepseek-corrected-observations.json).
All statements about existing code below are source inspection, not a new
execution or proof of correction effectiveness.

## 1. Recover only the supported ranking contradiction

### Cause

The typed model parser produced a candidate with a complete PM execution plan,
status Valid, clarificationFields containing Top, and top=null. At the
interpreter's envelope gate, Valid requires an empty clarification array.
The candidate therefore fails before `ApplyInterpretedQuestionConstraints`
can apply the question-derived ranking rule. The existing rejection is safe;
the missing behavior is recovery to a usable clarification.

Source anchors are the
[envelope gate](../../server/Features/Reports/PmAnalyticsV3QuestionInterpreter.cs#L257),
[status rules](../../server/Features/Reports/PmAnalyticsV3QuestionInterpreter.cs#L383),
and [draft validator](../../server/Features/Reports/PmAnalyticsV3QuestionInterpreter.cs#L685).

### Proposed implementation

Add one small recovery helper in the existing interpreter. Apply it only to
actual DeepSeek candidates after successful typed parsing and before the
existing envelope gate. Do not modify saved approved interpretations or the
deterministic parser. Keep all current sanitizer and unsafe-question gates
ahead of the provider call.

Recover only when every condition below is established:

1. The candidate status is exactly Valid, its clarification-field list contains
   exactly one canonical Top identifier, and its plan is present with top=null.
2. The candidate has the supported PM execution subject, schema/catalog
   versions, Aggregate mode and a complete valid scope. The existing plan
   validator must accept the draft without inventing missing values.
3. The question independently establishes a supported top/bottom ranking
   request and a limit within the existing bounds. It requests multiple
   measures without selecting a unique ranking criterion. Reuse the existing
   question/ranking helpers; a model detail string or error code is not proof.
4. The requested measures and grouping are preserved in the candidate. If the
   model dropped a measure, changed scope or supplied unsupported fields,
   leave recovery to the existing rejection/constraint behavior. Do not fill
   arbitrary missing fields to make the candidate fit this exception.

For the recoverable case, change the envelope to NeedsClarification, retain
Top and the safe plan, keep top=null, and supply a backend-owned clarification
message asking which requested measure should determine ranking. Use an
explicit recovery code in the existing Code field so evidence can identify
the recovery without logging provider prose or changing the public DTO shape.
The exact code name is an implementation choice to be recorded when added.

Then pass the recovered candidate through the unchanged envelope checks,
question constraints and clarification-draft validation. If any stage fails,
retain the existing InvalidOutput response. Never promote a contradictory or
incomplete candidate to Valid. Do not turn every malformed response into a
clarification, relax the schema, add model retries, or generate a fallback
report answer.

The model's original typed candidate must remain separate in evaluation data.
A backend recovery is a system outcome, not evidence that the raw model
produced the correct status. Record the recovery code and case identity in
sanitized results; do not overwrite the captured candidate.

### User workflow

Return NeedsClarification with the resolved category, period, measures and
grouping. The question-driven flow must remain unresolved until the user
provides a ranking criterion and reviews a validated plan. Do not automatically
preview or execute the draft. Preserve the existing signed-preview and
confirmation requirements for every eventual report execution.

The current guided web flow routes Top to its edit-question fallback. The
minimum correction uses that existing path with a clear question such as
"Rank by scheduled count or completed count?" An inline ranking selector is
optional, separately scoped web work. Recovery does not promise such a
selector or prohibit intentional manual editing of a report through the
existing validated preview flow.

### Acceptance criteria for later implementation

| Controlled case | Required outcome |
|---|---|
| Observed Valid plus Top contradiction, with independently confirmed ambiguous ranking and valid scope | NeedsClarification, Top, preserved draft and explicit backend recovery code |
| Same contradiction with requested measures reordered | Same safe recovery, without choosing the first measure |
| Valid candidate already guessing a ranking measure | Existing ranking constraint requests clarification; no new exception needed |
| Consistent NeedsClarification candidate | Existing clarification behavior preserved |
| Explicit ranking criterion with a consistent Valid candidate | Existing Valid path preserved |
| Malformed JSON, missing plan, unknown/duplicate clarification identifiers, invalid schema or unsupported field | Existing rejection preserved |
| Top contradiction without ranking intent, with an invalid limit, or with changed/dropped requested scope | No recovery under this narrow rule |
| Unsafe or unsupported question | Existing rejection preserved; no provider call where the safety guard already blocks it |

Use controlled typed responses through the actual model-interpreter path to
verify both recovery and rejection. Do not test only the deterministic parser
or only the helper in isolation. No recovery behavior is currently verified.

## 2. Declare catalog equivalence before another scored evaluation

### Cause and application disposition

The query service trims and uppercases catalog values during resolution.
`ComparePartial` compares the serialized filter array exactly. The original
expectation contains pre-resolution spelling, while the final response contains
the canonical resolved spelling. Keep production catalog normalization and
keep every original strict score unchanged.

Source anchors are
[catalog resolution](../../server/Features/Reports/PmAnalyticsV3QueryService.cs#L342)
and [exact array comparison](../../tools/UniPM.PmAnalytics.V3Evaluation/Program.cs#L662).

### Proposed evaluation change

Create a new versioned scoring protocol before any future scored run. Continue
reporting the original strict comparison for continuity. Add a separately
named catalog-equivalence result with its own documented rules and identity.
Do not relabel TEST-142 or TEST-144, replace their JSON/CSV files, or claim
their original strict failures were passes.

Limit equivalence to string values of the declared catalog-backed population
filters, initially building, department and location. Verify their lookup
semantics against the controlled fixture before including each field. Preserve
filter identifiers, operators, array lengths/order, category/period scope and
all other asserted plan properties exactly. Do not fold case for measure IDs,
clarification IDs, enums, GUIDs, arbitrary text or entire JSON documents.

Predeclare normalization of those catalog value strings as trim plus invariant
uppercase, consistent with the actual lookup boundary. Derive expected
canonical values from the frozen fictional fixture and declared rule before
execution, not by copying actual model output. Confirm that equivalent values
resolve to the same unique catalog scope. A different building or changed
operator must still fail. If catalog membership cannot establish equivalence,
retain the mismatch.

An auxiliary semantic case match requires every other declared expectation to
match as well. Report the typed model result, strict final-system result and
catalog-equivalence result separately. A higher semantic score does not
establish better raw-model accuracy or erase an interpretation error.

## 3. Implementation and verification order

1. Commit the narrow backend recovery and focused regression coverage as one
   logical change. Reuse the current modules and transport injection approach;
   do not introduce a competing interpreter or evaluator.
2. Select one most relevant targeted test invocation after implementation.
   Cover the acceptance table through the actual interpreter. Under the
   repository limits, allow only one focused correction and one rerun for an
   actionable in-scope failure. Stop on an unresolved or external failure.
3. Commit the separate scoring protocol and optional auxiliary comparator
   changes as another logical change, with normalization rules frozen before
   measuring their result. Label strict and semantic results explicitly.
4. If live evaluation is requested, freeze the exact implementation, prompt,
   corpus, scoring-version and fixture hashes before one new Development run.
   Propose the same maximum 18 HTTP requests, no retries, no report execution
   and US$1 conservative estimated budget ceiling. Check current pricing and
   provider configuration when preparing that run. This plan makes no calls.
5. Create a new evidence record and before/after manuscript table. Retain all
   original results and any new failures. Do not rerun model cases to obtain
   a desired score. Describe the work as development verification because
   these questions already informed the fixes.

If an inline ranking chooser is included later, implement and verify it as a
separate web change. Source inspection indicates the current fallback; no new
browser behavior has been observed in this planning task.

## Completion criteria and study claims

The correction is complete when the observed contradiction produces a safe,
usable clarification through the existing path, invalid/unsupported cases
still reject, explicit ranking still works, and ranking remains unselected
until the user resolves it. The evaluation correction is complete when its
declared equivalence rule recognizes only the same catalog scope and reports
both strict and semantic results without rewriting history.

Do not promise 100% before execution. If all 18 later match, describe which
scoring rule and observation stage achieved that result. It establishes
conformance on this fixed development set, not guaranteed model reliability,
unseen-question accuracy, numerical correctness, SQL Server execution,
historical respondent behavior or independent GSD acceptance.
