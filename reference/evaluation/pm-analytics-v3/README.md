---
id: NLA-V3-EVAL-PROTOCOL-001
status: draft
---

# PM analytics V3 first-release evaluation protocol (draft)

## Purpose and status

This draft prepares Phase 5 of the NLA V3 roadmap. It defines a proposed
development evaluation for the current `pmExecution` subject and a provisional
question set in [`dev-candidates.jsonl`](dev-candidates.jsonl).

The protocol and labels are developer-authored and **not independently
reviewed**. They are not a frozen evaluation, quality result, release
acceptance, or institutional validation. The candidate file is for review and
development use only. No held-out partition is included in this preparation.

## Scope

- English question interpretation for the existing V3 `pmExecution` catalog.
- The existing typed report plan, clarification and unsupported outcomes.
- Deterministic calculation and presentation correctness on synthetic fixtures,
  evaluated separately from question-to-plan accuracy.
- Existing authorization, signed preview, expiry, edit invalidation, and source
  drill-down behavior.
- No new measures, categories, filters, report subjects, SQL generation, or
  business rules.

This protocol does not establish support for Filipino or Taglish, production
provider quality, or user acceptance. It does not authorize a production LLM
call, a provider-cost-incurring evaluation, held-out execution, deployment,
merge, or database changes.

## Conditions and freeze requirements

The development corpus is intended to compare the deterministic rule parser
with the existing DeepSeek V3 path only. Do not add provider comparisons to this
protocol. No provider was called while preparing this draft. A DeepSeek run
requires a separately recorded development-only authorization, cost ceiling,
model identity, and bounded call count before execution.

Before any scored development run, reviewers must:

1. Review every question and expected label against the V3 catalog and approved
   reporting semantics; resolve or remove any uncertain case.
2. Record the reviewers, review date, changes, and decision for each case.
3. Freeze the reviewed development file by SHA-256 and record the exact source
   commit, catalog/schema versions, interpreter/prompt identity, model identity
   if applicable, runtime settings, and evaluation command.
4. Confirm the selected condition cannot open or read a held-out corpus.

The current candidate file is not frozen and must not be reported as a reviewed
question set. No held-out corpus is part of this draft. Creating and evaluating
one requires a separate protocol revision and explicit authorization before
execution; results must never be used for tuning.

## Evaluation method

Interpretation is evaluated without executing the proposed report. Compare the
response with each reviewed expected label:

- **Status accuracy:** exact match for `Valid`, `NeedsClarification`, or
  `Unsupported`.
- **Valid plan accuracy:** exact match on the explicitly asserted categories,
  period, measures, mode, grouping, filters, ranking, threshold, presentation,
  and requested columns. Report field-level results as well as exact-plan
  results; omitted assertions are not assumed correct.
- **Clarification accuracy:** exact set match for required clarification
  fields. A clarification must preserve safely resolved intent in a
  non-executable draft when possible. Do not encode an exclusion or negated
  category as a positive selection. When scope cannot be represented safely,
  return no draft. A clarification draft must fail preview validation; it must
  never be executed as a report.
- **Unsupported safety:** every expected unsupported case must be rejected
  without an executable plan. Report any accepted unsafe request separately as
  a blocking failure, not only as a score error.
- **Latency and cost:** report per-case and percentile latency. For a real
  provider, retain provider-reported usage, bounded attempts, and a dated
  published-rate estimate; mark estimates unavailable when usage is incomplete.

Group results by the predeclared case class and intent family. Do not combine
interpretation accuracy with deterministic calculation correctness.

Calculation and presentation checks use fixed-clock synthetic data plus
independently reviewed expected values. Include matching versus eligible
denominators, mixed categories and category-specific cycle coverage, null and
unmeasurable outcomes, thresholds, two-level groups, top/bottom order, stable
pagination, empty results, and source-record membership. Query execution must
be checked on SQL Server 2019 before claiming database compatibility.

The non-question scenario set for the reviewed protocol must include at least:

| Scenario | Expected check |
|---|---|
| Multi-category compliance with a category cycle missing in a group | Selected category cycles stay in the denominator; the measure is unmeasurable when required coverage is open or absent. |
| Zero eligible rows and missing inspection coverage | Return the documented empty or unmeasurable state; never invent a percentage. |
| Quarter source-record review for fire extinguishers | Q1 selects February and Q4 selects November; category, period, filters, and returned sources match the aggregate contribution. |
| Top/bottom with a different display sort | Select the ranked groups first, then apply the requested output ordering. |
| Expired, tampered, or other-user preview token | Reject execution without returning stale results. |
| Provider timeout, quota failure, malformed output, or unsupported fields | Return a bounded provider/interpretation error; do not create an executable plan or execute a report. |
| Unauthorized role and direct execute without a valid preview | Deny the request; GSD-authorized valid preview execution remains unchanged. |

These are planned protocol scenarios, not claims that each underlying path has
already passed native SQL Server or browser acceptance.

End-to-end browser acceptance separately measures whether a reviewer can
inspect and edit the plan, understand the preview, confirm the same plan, and
trace results to supported source records. Record task completion, clarification
turns, edits correcting wrong previews, and failures. A local browser run is not
GSD acceptance.

## Engineering harness status (2026-10-19)

A V3-specific .NET 10 harness now validates the fixed development corpus,
reports deterministic parser differences case by case, and refuses to produce
a score until every label is approved and the candidate SHA is frozen. It uses
the safety guard and deterministic V3 rule parser only. It does not execute
plans, call DeepSeek, inspect held-out data, or use the V1/V2 datasets or score
format. Commands from the repository root:

```sh
dotnet run --project tools/UniPM.PmAnalytics.V3Evaluation -- validate
dotnet run --project tools/UniPM.PmAnalytics.V3Evaluation -- diagnose
dotnet run --project tools/UniPM.PmAnalytics.V3Evaluation -- score
```

`validate` currently passes for 18 structurally valid candidates and reports
all 18 labels pending. `diagnose` is a review aid, not an accuracy result. The
`score` command currently exits without a score because labels are unreviewed.
After human approval and freezing, the score report will include status,
clarification-set, disposition, and declared-plan-assertion results grouped by
case class and intent family, plus deterministic parser elapsed time. It
measures the safety guard and rule parser; it does not run catalog resolution,
query execution, a provider, or end-to-end API latency.
The separate [`review-notes.md`](review-notes.md) lists per-case issues. The
synthetic calculations in
[`synthetic-calculation-fixtures.json`](synthetic-calculation-fixtures.json)
are independently authored engineering expectations; they are not yet
independently reviewed acceptance evidence.

## Acceptance gates and blockers

Phase 5 remains open until all of the following have evidence:

- independent review and freeze of the development question set;
- development condition results with exact source and configuration identity;
- independently reviewed deterministic calculation expectations;
- native SQL Server 2019 execution and authorization/preview regression checks;
- browser workflow and accessibility review, including responsive layouts;
- a release record that lists passes, failures, skips, limitations, and any
  blocking unsafe result.

Held-out evaluation and GSD task-success acceptance are separate gates. This
draft does not satisfy them. A production release remains separately authorized.

## Candidate set format

Each JSONL row has a stable `caseId`, `intentFamily`, `caseClass`, synthetic
`question`, `expectedStatus`, `expectedClarificationFields`, and
`expectedPlanDisposition` (`Executable`, `ClarificationDraft`, or `None`),
`expectedPlanAssertions`, and `executablePlanAllowed`. Assertions state only
the normalized plan properties that a reviewer must verify for that question.
For clarification cases, assertions describe intent that should be preserved
in a draft; they do not authorize preview or execution. Unsupported cases must
return no plan. A later evaluator must compare these fields as typed values and
must reject preview/execution for clarification and unsupported outcomes.

The existing `tools/UniPM.PmAnalytics.InterpretationEval` CLI evaluates the
older V1/V2 contract. Keep this corpus in the V3 harness; never pass it to the
V1/V2 CLI or combine its results with historical V1/V2 scores.

## Provenance

- Preparation source commit: `d18966feed237d83d4901c938fc5b118c7dfda3b`.
- Branch: `feature/nla-v3-composable-reporting`.
- Candidate labels: provisional, developer-authored, unreviewed.
- V3 harness corpus structure: validated; 18 cases, 18 pending labels.
- Deterministic diagnostics: differences are listed per case; no accuracy score.
- Synthetic calculation fixtures: added for engineering regression; independent review pending.
- Provider calls: none.
- Held-out data: none created, read, or evaluated for this protocol.
