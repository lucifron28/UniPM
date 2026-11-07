---
id: NLA-V3-FLEXIBILITY-FOLLOW-UP
type: evaluation
title: NLA V3 flexibility follow-up development evidence
status: executed
recordedAtUtc: 2026-11-07T03:21:27Z
sourceCommit: 6064fa17e78a88de82dff20748521ef414d101a2
sourceBranch: feature/nla-flexibility-evidence-hardening
evidenceLevel: locally-executed
---

# NLA V3 flexibility follow-up development evidence

## Scope and source boundary

This is a parser and typed-plan follow-up to the development-only hardening on
PR #116. The untouched baseline is frozen at
ef0feecee2b7f2092cd97086b8da2296c6f30e64. Candidate implementation began at
74cedcbcd7f3a3a5bd3d9aca20c4fe15ea932daf, with numeric-test assertions at
d59b4a3c3d893d244b0d1ca7f40a5551ceb3397e and a sentence-period correction/test
at 6064fa17e78a88de82dff20748521ef414d101a2 on
feature/nla-flexibility-evidence-hardening. It is a follow-up branch, not a
new freeze. PR #116 was not changed, merged, or deployed.

No GSD Head question or answer, institutional record, saved provider response,
or frozen historical evaluation was accessed or changed. No paid or live model
call was made. Developer-authored tests and canned model proposals are the only
new inputs. The 29-case defect-driven expansion is kept separate from the
existing developer matrix because those cases were authored after reproducing
the defects.

The original reviewed developer matrix remains at
[pm-analytics-v3-final-hardening-dev](../pm-analytics-v3-final-hardening-dev/README.md).
It is unchanged and predates this regression expansion. Its current exact
focused command reports 108 framework test cases, 108 passed, 0 failed, 0
skipped on both the frozen source and candidate. The inventory and semantic
changes below do not change its questions, rubric, or historical results.

## Paired results

The defect-driven regression expansion contains 29 fixed test cases. Its
complete candidate expectations assert the typed plan contract (subject, mode,
period, categories, measures, scope, filters, thresholds, order, top, display
and columns) or the complete safe clarification/unsupported disposition. The
same test files were run against the frozen source in a detached scratch
worktree and against the candidate. The frozen source remained unchanged.

| Measure | Frozen source ef0fee | Candidate source and tests 6064fa1 |
|---|---:|---:|
| Regression-expansion cases passing | 5/29 | 29/29 |
| Regression-expansion cases failing | 24/29 | 0/29 |
| Existing focused NLA cases passing | 108/108 | 108/108 |
| Broader focused NLA cases passing | not run for this exact filter | 164/164 |
| Skipped cases in each reported run | 0 | 0 |

There was no candidate semantic regression in the fixed 29-case expansion.
This is a small, developer-authored test result, not an accuracy estimate.

### Case-by-case defect comparison

All candidate plans below are asserted field-by-field in
[PmAnalyticsV3AcceptanceTests.cs](../../../tests/UniPM.Api.Tests/Reports/PmAnalyticsV3AcceptanceTests.cs)
and [PmAnalyticsV3GuardCorrectionTests.cs](../../../tests/UniPM.Api.Tests/Reports/PmAnalyticsV3GuardCorrectionTests.cs).
The raw baseline test output, including complete serialized plans on baseline
failures that emitted them, is retained only in ignored
artifacts/nla-flexibility-evidence/frozen-baseline-regression-expansion-v3.log.
Candidate passes prove equality to the stated complete typed expectations;
they do not capture raw production-provider output.

| ID | Developer-authored query | Expected full-plan distinction | Frozen-source observation | Candidate |
|---|---|---|---|---|
| NUM-01 | Show scheduled fire-extinguisher inspections by department with at least 1,000 in 2026 | Valid aggregate; 2026 PM cycle; fire-extinguisher; scheduledCount; department; scheduledCount >= 1000; table columns department/scheduledCount | Valid but threshold was >= 1 | Full typed plan retains >= 1000 |
| NUM-02 | Same request with at least 1, 000 | Same plan as NUM-01 | Valid but threshold was >= 1 | Full typed plan retains >= 1000 |
| NUM-03 | Same request with at least 1 000 | Same plan as NUM-01 | Valid but threshold was >= 1 | Full typed plan retains >= 1000 |
| NUM-04 | Same request with at least 1000 | Same plan as NUM-01 | Period was unresolved and threshold absent | Full typed plan retains year 2026 and >= 1000 |
| NUM-05 | Same request with at least 1,00 | NeedsClarification, fields Thresholds; retain category, year, measure, grouping; no aggregate filter; table columns department | Valid with >= 1 | Safe clarification; no aggregate filter |
| NUM-06 | Same request with at least 1,2,3 | Same safe clarification plan as NUM-05 | Valid with >= 1 | Safe clarification; no aggregate filter |
| NUM-07 | Same request with at least 1 00 | Same safe clarification plan as NUM-05 | Valid with >= 1 | Safe clarification; no aggregate filter |
| NUM-08 | Same request with at least -1 | Same safe clarification plan as NUM-05 | Valid with no aggregate filter | Safe clarification; no aggregate filter |
| NUM-09 | Same request with at least 1.5 | Same safe clarification plan as NUM-05 | Correctly clarified Thresholds with no partial filter | Still clarifies; negative control retained |
| NUM-10 | Same request with 999999999999999999999999999999999999999 | Same safe clarification plan as NUM-05 | Valid with no aggregate filter | Safe clarification; no aggregate filter |
| NUM-11 | Show the top 1,000 departments by scheduled fire-extinguisher inspections in 2026 | Unsupported RankingLimitExceeded; null plan; existing top limit remains 20 | NeedsClarification with top 1 and missing department/group | Rejects the complete requested limit |
| NUM-12 | Same request with at least 1,,000 | Same safe clarification plan as NUM-05 | Valid with >= 1 | Safe clarification; no partial numeric filter |
| NUM-13 | Same request with at least 1_000 | Same safe clarification plan as NUM-05 | Valid with >= 1 | Safe clarification; no partial numeric filter |
| NUM-14 | Same request with at least 1/000 | Same safe clarification plan as NUM-05 | Valid with >= 1 | Safe clarification; no partial numeric filter |
| NUM-15 | Show scheduled fire-extinguisher inspections by department in 2026 with at least 1. | Valid aggregate; 2026 PM cycle; fire-extinguisher; scheduledCount; department; scheduledCount >= 1; table columns department/scheduledCount | Same full plan; sentence period does not affect threshold | Same full typed plan after punctuation-boundary correction |
| GUARD-01 | Show scheduled fire-extinguisher inspections by department with at least 1,000 in 2026; canned proposal includes threshold 1000 | Valid plan identical to NUM-01 | NeedsClarification with partial >= 1 threshold | Full typed plan retains >= 1000 |
| GUARD-02 | Same question with malformed 1,00; canned proposal includes partial threshold 1 | NeedsClarification, fields Thresholds; no aggregate filter; complete safe partial scope | Valid with >= 1 | Safe clarification; no aggregate filter |
| LANG-01 | Count fire-extinguisher inspections finished before the PM deadline by department in 2026. | Valid aggregate, completedOnTimeCount, 2026, fire-extinguisher, department, table columns department/completedOnTimeCount; no filters/order/top | Clarified Measures with empty measure list | Full expected plan |
| LANG-02 | Ilang fire-extinguisher inspections ang natapos bago ang deadline by department in 2026? | Same plan as LANG-01 | Valid with completedCount | Full expected completedOnTimeCount plan |
| LANG-03 | Count fire-extinguisher inspections na finished before the PM deadline by department in 2026. | Same plan as LANG-01 | Clarified Measures with empty measure list | Full expected plan |
| LANG-04 | How many fire-extinguisher inspections were never completed by the PM deadline in 2026, by department? | Valid aggregate, missedCount, 2026, fire-extinguisher, department, table columns department/missedCount | Valid with completedCount | Full expected missedCount plan |
| LANG-05 | How many fire-extinguisher inspections finished after the due date in 2026, by department? | Valid aggregate, completedLateCount, 2026, fire-extinguisher, department, table columns department/completedLateCount | Clarified Measures with empty measure list | Full expected completedLateCount plan |
| LANG-06 | How many fire-extinguisher inspections na-late pero natapos in 2026 by department? | Same late plan as LANG-05 | Valid with completedCount | Full expected completedLateCount plan |
| LANG-07 | Ilan fire-extinguisher inspections ang hindi pa tapos matapos ang deadline in 2026 by department? | Same aggregate shape as LANG-04 with missedCount | Clarified Measures with empty measure list | Full expected missedCount plan |
| SAFE-01 | Count fire-extinguisher inspections that need repair by department in 2026. | NeedsClarification, fields Measures; retain 2026/category/department; no inferred finding; columns department | Correct clarification | Same safe clarification |
| SAFE-02 | Count fire-extinguisher inspections with may sira by department in 2026. | Same safe clarification as SAFE-01 | Correct clarification | Same safe clarification |
| LANG-08 | Count fire-extinguisher inspection findings found not working by department in 2026. | Valid aggregate, nonOperationalCount, 2026, fire-extinguisher, department; table columns department/nonOperationalCount | Clarified Measures with empty measure list | Full expected plan |
| LANG-09 | Count fire-extinguisher inspection findings hindi gumagana by department in 2026. | Same plan as LANG-08 | Already resolved to nonOperationalCount | Same expected plan |
| SAFE-03 | Compare scheduled fire-extinguisher inspections with those finished before the PM deadline by department in 2026 | NeedsClarification, fields Measures; retain 2026/category/department; no measures/filters; columns department | Valid with scheduledCount only | Safe clarification; no requested outcome silently removed |

For valid rows, all unspecified plan fields are also asserted: schema 3,
catalog 2, subject pmExecution, Aggregate mode, empty base/matching filters,
empty ordering, null top, and Table presentation. Clarification rows assert the
complete safe partial plan, not only status. The intended complete plans are
encoded in the listed tests; the baseline logs preserve the observed plan
payloads printed for failing assertions. Query text is synthetic and
developer-authored.

The comparison has no report calculation or returned-record denominator: these
tests exercise interpretation only. Endpoint acceptance cases use isolated
in-memory test databases, but do not preview or execute reports. The focused
parser and fake-handler tests use the fixed clocks declared by their test
fixtures. No fixed synthetic report dataset was needed or consulted.

## Reproduction and rubric

The original matrix's fixed plan expectations and parser tests are unchanged.
The 29-case expansion was created after confirmed endpoint/fake-guard
reproduction and is explicitly a regression expansion, not a prospective or
held-out score. Its test cases encode the intended status, clarification set,
and full plan before the paired baseline/candidate assertion runs.

The first expanded numeric run failed three new malformed-token controls:
1,,000, 1_000 and 1/000 were ignored as threshold operands and returned Valid
without a threshold. The strict value parser was not at fault; the token
capture stopped before these separators, so unresolved-threshold detection
never ran. The follow-up captures these separator forms as one token for
strict rejection. The same nine-case malformed/out-of-range endpoint test then
passed 9/9. The frozen-source paired rerun still returns Valid with >= 1 for
each of the three new controls.

Local environment: Windows 11 (10.0.26200), .NET SDK 10.0.300, runtime
10.0.8. The focused NLA run on the exact source/test commit
6064fa17e78a88de82dff20748521ef414d101a2 was:

    dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~PmAnalyticsV3FinalHardeningTests|FullyQualifiedName~PmAnalyticsV3GuardCorrectionTests|FullyQualifiedName~PmAnalyticsV3AcceptanceTests'

Result: 164 passed, 0 failed, 0 skipped (16 seconds test duration). The exact
existing-matrix command separately passed 108 passed, 0 failed, 0 skipped on
the frozen source and candidate. The paired expansion reported the frozen
source: 24 failed, 5 passed, 0 skipped (9 seconds); candidate: 29 passed, 0
failed, 0 skipped (10 seconds). The nine-case malformed numeric endpoint test
passed 9/9 after correction. A sentence-final-period threshold case first
failed against the candidate, then passed after removing the punctuation-end
rejection; it passed on both frozen source and corrected candidate in the
paired run. These are test-run durations, not per-query latency or production
performance measurements.

Fourteen fake-handler model proposals were used in semantic guard tests. Each
test asserts a single local handler call. These are deterministic canned
proposals: 0 live provider requests, 0 input/output tokens, no provider latency,
and no charge. The prior live-provider cap remains exhausted; no new DeepSeek
call is authorized by this record.

Raw command output and TRX files are local ignored artifacts under
artifacts/nla-flexibility-evidence/ and tests/UniPM.Api.Tests/TestResults/.
They are not committed. This record reports the exact commit and summarized,
reviewed results only.

## Runtime, cost, and deferred work

Source inspection confirms that reporting loads at most 50,001 candidate rows
to enforce the 50,000-row execution bound, then groups, filters, orders, and
pages in memory. Page size is capped at 100; chart groups at 200. No
representative 50,000-row load, latency, or memory benchmark was run. SQL
Server Query Store and a representative SQL Server 2019 benchmark remain
available future options; this parser change does not justify rewriting
aggregation, adding caches, a data warehouse, or schema/index changes.

The provider client has a 60-second timeout and permits up to two retries per
logical call. Its 60-call ledger is constructed per scoped interpreter; it is
not an application-wide or per-user budget, concurrency limit, or rate limiter.
This follow-up does not alter these bounds. Code changes contain no SQL,
business calculation, database schema, or web application changes, so optional
SQL Server 2019 numerical tests were not run. Local web E2E was not run; the
exact-code-head backend and web CI runs are recorded in
[TEST-198](../../evidence/test-runs/TEST-198-nla-flexibility-exact-head-ci.md).

## Manuscript-ready summary

| State | Statement supported by this evidence |
|---|---|
| Implemented | Complete grouped integer tokens are parsed and range-checked; malformed, negative, fractional-integer, and overflow operands clarify. A bounded guard mapping preserves explicit catalog-supported on-time, missed/late, and contextual non-operational paraphrases. A hard conflicting or compounded measure still clarifies. |
| Experimentally evaluated | Existing 108-case focused parser/API matrix passed on both baseline and candidate. A separate 29-case regression expansion improved from 5/29 to 29/29 against the frozen source. Fake proposals and isolated in-memory API tests only; no live provider, report execution, or GSD data. |
| Proposed | The linked capability inventory groups 40 operational questions into existing-contract, new-approved-contract, external-data, and unsafe classes. It does not implement new measures or formulas. |
| Threats to validity | Expectations and queries are developer-authored, test-only, and not independently adjudicated by GSD or held out. Tests cover interpretation, not result calculation, report truth, production latency, real user language, or field acceptance. No post-fix live model evidence exists. |

## Release and evaluation recommendation

The follow-up is a candidate for owner review and a newly approved pre-GSD
freeze because known numeric false-valid behavior and specific bounded
paraphrase loss are covered by passing regressions. Do not expose this branch
to the GSD evaluation or call it frozen automatically. The owner should review
the bounded mappings, full-plan expectations, capability gaps, and CI results,
then explicitly approve a distinct freeze SHA and scoring protocol before any
GSD questions are opened. No merge or deployment is authorized here.
