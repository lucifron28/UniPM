# Supplementary NLA wording comparison

Corpus: V3P-001. Prepared October 28, 2026.

## Completed first round

The owner requested immediate execution after preflight. The paired run is
complete, using the unchanged corpus and assistant-authored development labels.
[TEST-179](../../evidence/test-runs/TEST-179-supplementary-nla-paraphrases.md)
records results and limitations; [scoring rules](scoring-rules.md) were committed
before execution. The preparation status below preserves the initial state,
not the current execution status. No independent label approval is claimed.

Status: author-prepared, pending label review. The initial question file is
hash-recorded in `manifest.json`. No interpretation, scoring, preview, report
execution or provider call has been performed on these questions.

## Question being evaluated

Does the existing DeepSeek interpreter resolve alternative English wording for
supported reports more often than the deterministic interpreter on this set?
Both must target the same report catalog and backend validation boundary.

This is a supplementary development comparison. It is not an independent
held-out study: the author has inspected the existing parser, catalog and prior
evaluation material. The questions have not been selected by observed parser
or model success. Do not call the set representative of all user language.

The original 18-question evaluation, its reviewed labels, scores and manifests
remain separate and unchanged. New results must be identified by this corpus
ID and source version, including if presented after manuscript submission.

## Contents and intended scope

`cases.jsonl` contains 24 English questions, three per report intention:

| Intention | Category | Scheduled PM cycle |
| --- | --- | --- |
| Scheduled count | Fire extinguishers | August 2025 |
| Completed count | Fire alarms | June 2025 |
| Completion percentage | Water drinking stations | May 2025 |
| Non-operational count by department | Emergency lights | December 2025 |
| Completed after original month-end | Fire extinguishers | August 2025 |
| Unfinished beyond original month-end | Fire alarms | June 2025 |
| Scheduled and completed counts by department | Water drinking stations | May 2025 |
| Completed within first Inspector assignment period | Emergency lights | December 2025 |

Each supported question has a complete proposed expected report plan using
schema 3 and catalog 2. Periods refer to scheduled cycles, not dates on which
an inspection was entered. Late and missed work use original PM deadlines.
The Inspector-period intention explicitly uses its separate 30-calendar-day
window. These remain interpretation questions even when a fixture has no
matching records. No numerical result is predicted here.

Six controls are recorded separately: three clarification requests and three
unsupported requests. The clarification labels currently specify concepts,
not approved exact API clarification-field sets. Resolve those mappings in
review before scoring them. No control may yield an executable report without
the required scope or accept a record mutation, prediction, SQL or credential
request. Safe partial drafts may be retained for clarification when compatible
with the actual contract; they must remain non-executable.

English-only scope avoids implying support for Filipino or Taglish. A later
multilingual study needs its own reviewed scope and corpus version.

## Review and freeze before execution

1. Review every question against its expected meaning. Resolve ambiguous
   wording, presentation defaults, output columns and exact clarification
   fields before either interpreter is exercised. Confirm the expected plans
   pass the unchanged plan validator; validation is not interpretation scoring.
2. Check exact and near wording overlap with earlier evaluation corpora, demo
   questions and regression fixtures. Record overlap rather than claiming that
   all language constructions are new. Do not select cases based on failure.
3. Record reviewer identity, date and each label decision. Any correction
   creates a new corpus hash and a documented review revision. Preserve this
   initial snapshot in Git history. It is not yet a researcher-approved freeze.
4. Record the reviewed file hash, tested commit, catalog version, fixed UTC
   clock, interpreter/prompt file identities, backend validation rules and model
   identifier. Select any equivalence rules before seeing outputs.
5. Freeze that reviewed version. Do not tune prompts or parser rules between
   the two conditions. Subsequent fixes require a separately named evaluation
   round with the earlier results preserved.

## Paired execution and scoring

Use separate deterministic-only and DeepSeek-only conditions on every case.
Verify the resolved interpreter identity and disable fallback between them.
Do not infer the condition merely from provider availability. If the existing
runner cannot guarantee this, extend it before running scored requests.

Run one attempt per question per condition, sequentially, with zero automatic
retries and at most 30 logical provider requests in the initial round. Record
timeouts, malformed plans, safety rejections and provider errors as distinct
outcomes. Inspect any provider client's internal retry behavior before starting;
the logical request limit alone does not prove a physical call limit. Do not
rerun only failed cases and replace the original score.

For supported requests report status agreement and complete semantic plan
agreement separately. Verify categories, subject, period, measures, grouping,
base and matching filters, aggregate thresholds, ordering, ranking, columns and
presentation. Additional filters or measures can alter meaning even if the
request returns Valid. Predeclare how harmless order/default differences are
handled; retain a strict comparison beside any semantic equivalence score.

Report the paired four-way outcome table: both correct, DeepSeek only correct,
parser only correct, neither correct. Report controls separately. Keep counts
by intention and wording class; 24 questions are only eight report intentions,
so do not treat the paraphrases as independent evidence of broad superiority.

Record per-case and median/p95 latency. Keep provider request count and
provider-reported usage where available. Report unavailable usage explicitly;
do not invent cost or apply undated prices. Record which condition ran first
and keep cold-start/network effects visible in the latency limitations.

Only sanitized synthetic questions may reach the backend provider path. Never
persist provider payloads, credentials, raw operational prompts or token maps.
Raw local diagnostics belong in ignored `artifacts/`; reviewed results belong
in a new engineering evidence record with the exact tested commit and hashes.

## Next work

The [preflight review](preflight-review.md) records successful gold-plan
validation, the bounded wording-overlap scan and remaining scoring decisions.
Run `dotnet run --project tools/UniPM.PmAnalytics.V3Evaluation -- validate-paraphrases`
for structural and gold-plan validation without invoking either interpreter.

The existing V3 evaluation runner is hardwired to the earlier corpus. Do not
replace its files to run this set. Prepare a separate runner input path or
bounded adapter, validate the proposed gold plans, complete label review and
freeze, then run the paired comparison. No application behavior needs to change
to prepare this study. No results are claimed by this preparation commit.
