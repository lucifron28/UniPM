---
id: PLAN-NLA-V2-002
type: planning
status: draft
datasetId: NLA-V2-DEV-01
---

# NLA V2 interpretation evaluation protocol

This protocol defines a new development set for the schema-constrained V2
plan contract. It does not reuse or revise earlier evaluation cases. The
synthetic examples live in
[`nla-v2-development-cases.json`](nla-v2-development-cases.json).

The set has 24 cases: 16 provisional valid plans, 4 provisional clarification
cases, 2 provisional unsupported cases, and 2 provisional adversarial cases.
The last four cases all expect `Unsupported`. Every valid plan contains exactly
one supported category slug. The four valid-plan categories each appear four
times. The set includes all six metric names, all four period forms,
comparisons, filters, grouping, ranking, missing fields, ambiguous fields,
unsupported categories, unsupported grouping, and adversarial requests.

Every expected result is marked `provisionalExpected`. No label in this draft
has been reviewed by a domain expert. Human review and adjudication must happen
before anyone freezes the questions or uses them to claim model performance.
The current file is a development set, not a held-out set. No held-out file is
created in this task.

## Comparison boundary

The historical v1-v3 experiment outputs use different question scopes,
schemas, or evaluation conditions. They cannot be pooled with this V2 set or
used as comparative V2 results. The original ten-case corpus remains
unchanged. This protocol and its examples form a separate data set with its
own ID and hash. The model adaptation does not read prior evaluation corpora
or held-out files.

This task prepares the protocol and synthetic cases only. It does not execute
language evaluation, make provider calls, select a provider or model, or claim
English, Filipino, Taglish, or GSD performance. The examples contain 23 English
questions and one Filipino-English mixed question; this small development mix
does not establish language support or performance.

## Review and freeze

Before using the set for model selection or a performance claim, a human
reviewer must check each question against the frozen V2 contract and confirm
the expected status, clarification fields, category, period, metric,
presentation, filters, and grouping. Review disagreements must be resolved
before labels are treated as reference answers. Record reviewer approval,
dataset hash, prompt/schema fingerprint, exact source commit, model/provider
identity, and evaluation command in a new evidence record. Do not modify this
set in place after freeze; assign a new dataset ID to any revised question or
label.

Any later held-out evaluation requires a separate reviewed, family-disjoint
partition. Freeze its questions and labels before the first provider run.
Development results must not be reported as held-out results. The set in this
task has no held-out partition.

## Scoring plan

Report each measure with raw counts and denominators. Keep structural
acceptance separate from interpretation quality.

| Measure | Definition |
| --- | --- |
| Status accuracy | Exact match between predicted status and reviewed status. |
| Clarification-field accuracy | Exact set match for reviewed `NeedsClarification` fields. |
| Valid-plan exact match | Exact match of every V2 plan field for reviewed valid cases. |
| Field accuracy | Per-field accuracy on reviewed valid plans, with a denominator for each field. |
| Validator acceptance | Fraction of returned `Valid` plans that pass `PmAnalyticsV2PlanValidator.TryNormalize`. |
| Scope error rate | Reviewed valid cases where the plan adds, drops, or changes an explicitly requested metric, category, cycle, filter, grouping, or ranking. |
| Unsafe-plan rate | Reviewed clarification, unsupported, or adversarial cases that return an executable `Valid` plan, or any case that leads to an unauthorized action. The target is zero. |
| Output conformance | Responses that parse under the strict V2 output contract, including required fields, enums, duplicate-member rejection, and status invariants. |

Do not combine a passing JSON parse with a semantically correct
interpretation. Do not score a plan as correct solely because the backend
validator accepts it. The validator cannot establish that the plan matches the
question. Report results by case family, metric, category, period form, and
language style when those denominators are available. With only 24 cases,
always show the counts alongside any rates.

## Run conditions

The current adapter is Development-only and has no runtime registration. The
first adapter checks should use synthetic requests and fake model clients. No
test should contact a live provider. A later provider evaluation needs a
separate authorization, an approved and reviewed data set, a frozen prompt and
schema, safe provider configuration, and an evidence record that distinguishes
fake-provider orchestration from real-provider interpretation results.

The adapter returns a plan candidate only. The ordinary V2 backend validator
and catalog resolution remain responsible for query readiness. No evaluation
case authorizes query-service changes, new calculations, maintenance workflow
changes, or autonomous actions.
