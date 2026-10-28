---
id: TEST-180
type: test-run
title: Frozen V3D original corpus paired endpoint repeat
status: executed
recordedAtUtc: 2026-10-28T15:03:54Z
testedCommit: d6afd21d516e4e0fd246c8a6f919f66bdd70f426
sourceBranch: feature/nla-original-paired-repeat
evidenceLevel: real-provider-executed
---

# Original corpus paired repeat

All 18 frozen development questions were submitted once through each explicitly
selected interpreter, 36 endpoint requests total. All returned HTTP 200. No
fallback or selective rerun occurred. This is a repeat of the existing reviewed
development set, not held-out, independent GSD, numerical-report or field evaluation.
TEST-139, TEST-146, TEST-179 and their original sources, labels and scores are unchanged.

## Results against the frozen labels

| Metric | DeterministicParser path | DeepSeek path |
| --- | --- | --- |
| Exact status | 17/18 | 17/18 |
| Exact clarification field set | 17/18 | 16/18 |
| Exact plan disposition | 18/18 | 17/18 |
| Strict declared plan assertions | 68/69 | 67/69 |
| Catalog-equivalent declared plan assertions | 69/69 | 68/69 |
| All declared criteria, strict | 16/18 | 14/18 |
| All declared criteria, catalog-equivalent | 17/18 | 15/18 |
| Median endpoint latency | 26.56 ms | 1309.13 ms |
| Maximum endpoint latency | 113.58 ms | 1601.75 ms |

Both rubrics apply identically to both paths. Catalog equivalence allows only
trim/case normalization of known building/department values inside base filters.
It does not waive clarification-field casing, measures, filters, periods,
groupings, columns, presentation, or other scope differences.

The frozen corpus contains 69 partial plan assertions, not complete gold plans.
Full strict or full semantic gold-plan accuracy is unavailable without inventing
additional labels. The scores above are agreement with the declared criteria.
Complete final plans and responses are retained, including unasserted fields.

The complete returned plans agree strictly on 14/18 pairs and with catalog
equivalence on 14/18. Six agreeing pairs contain two null plans. Among the eight
frozen Valid cases, complete plan agreement is 7/8 under both rubrics. Among the
12 pairs with two non-null plans, it is 8/12. Agreement on status, clarification
fields, disposition and full plan together is 13/18 under both rubrics. Pairwise
agreement is not accuracy against a complete gold plan.

## Every case

Each path cell reports final status, clarification fields, and strict/catalog-equivalent
pass against all declared criteria. Both HTTP status and interpreter identity are
recorded individually in the linked CSV and full JSON. The last column compares
complete plans, preserving columns and all other fields.

| Case | Parser status; fields; strict/equivalent | DeepSeek status; fields; strict/equivalent | Full plans identical |
| --- | --- | --- | --- |
| V3D-001 | Valid; none; pass/pass | Valid; none; pass/pass | yes |
| V3D-002 | Valid; none; pass/pass | Valid; none; pass/pass | yes |
| V3D-003 | Valid; none; pass/pass | Valid; none; pass/pass | yes |
| V3D-004 | Valid; none; pass/pass | Valid; none; pass/pass | yes |
| V3D-005 | Valid; none; pass/pass | Valid; none; pass/pass | no |
| V3D-006 | Valid; none; fail/pass | Valid; none; fail/pass | yes |
| V3D-007 | NeedsClarification; Period; pass/pass | NeedsClarification; Period; fail/fail | no |
| V3D-008 | NeedsClarification; Filters,Measures; pass/pass | NeedsClarification; Filters,Measures; pass/pass | yes |
| V3D-009 | NeedsClarification; Filters,Measures; pass/pass | NeedsClarification; Filters,Measures; pass/pass | yes |
| V3D-010 | NeedsClarification; Filters,Measures; pass/pass | NeedsClarification; Filters,Measures; pass/pass | yes |
| V3D-011 | NeedsClarification; Top; pass/pass | Valid; none; fail/fail | no |
| V3D-012 | NeedsClarification; Top,GroupBy; pass/pass | NeedsClarification; GroupBy,Top; pass/pass | yes |
| V3D-013 | Valid; none; pass/pass | Valid; none; pass/pass | yes |
| V3D-014 | Valid; none; pass/pass | Valid; none; pass/pass | yes |
| V3D-015 | NeedsClarification; Department; pass/pass | NeedsClarification; department; fail/fail | no |
| V3D-016 | Unsupported; none; pass/pass | Unsupported; none; pass/pass | yes |
| V3D-017 | NeedsClarification; Categories; fail/fail | Unsupported; none; pass/pass | yes |
| V3D-018 | Unsupported; none; pass/pass | Unsupported; none; pass/pass | yes |

## Failures and divergences

- V3D-006: both final responses contain `building Equal ["NORTH BLOCK"]`.
  The frozen label contains `North Block`. Each path loses the same one strict
  assertion and passes the catalog-equivalent check. Both complete final plans
  are identical. This repeat supplies no evidence of a DeepSeek-specific casing
  error. The source-inspected shared resolver uppercases catalog values, but
  pre-normalization output was unavailable, so neither path's original spelling
  was observed during TEST-180.
- V3D-005: both satisfy the frozen assertions. DeepSeek returns two additional
  record columns, `scheduleId` and `assetId`. Full strict and full equivalent
  paired-plan agreement fail. These columns were not part of the original gold
  assertions; they are retained, not silently discarded in scoring.
- V3D-007: both ask for Period clarification. The parser retains the expected
  Aggregate `completedCount` draft. DeepSeek returns a Records draft with an
  empty measures array and a Completed schedule-status filter. DeepSeek fails
  the frozen measure assertion under both rubrics. Complete plans also differ
  in period, mode, presentation and columns. They remain non-executable drafts.
- V3D-011: the frozen label requires Top clarification. The parser follows it.
  DeepSeek returns Valid with a top-five ranking by `scheduledCount`, choosing
  a ranking measure without the required clarification. Its six plan assertions
  pass, but status, fields and disposition fail. No resulting report was executed.
- V3D-015: the parser returns `Department`, matching the frozen field identifier.
  DeepSeek returns `department`, failing exact field-set agreement. Its draft
  also retains the unresolved Research base filter, while the parser draft has
  no base filter. Unknown Research is not a known catalog alias and is not
  normalized away. Both return NeedsClarification and match the five partial
  plan assertions. Complete paired plans differ.
- V3D-017: the frozen label requires Unsupported with no fields. The parser
  returns NeedsClarification with Categories and no plan. DeepSeek returns
  Unsupported with no fields and no plan. The null plans agree, while the
  responses and parser gold result do not.

These are observed final backend-path results. Without pre-normalization
candidates, the other divergences cannot be attributed conclusively to model
output, parser output, or subsequent backend guards solely from this run.

## Execution and provenance

The precommitted [protocol](../../evaluation/pm-analytics-v3-repeat/protocol.md)
and [runner](../../evaluation/pm-analytics-v3-repeat/run-repeat.py) were frozen at
`d6afd21d516e4e0fd246c8a6f919f66bdd70f426`. Runtime source was built from parent
`0a1928952e315275a95a250127f72341140c388a`; the tested commit adds only this
protocol and runner. Production source is identical at those commits. One
backend binary served both modes through
`POST /api/v1/analytics/pm/v3/interpret` on an isolated local listener.

- Execution UTC: 2026-10-28 15:03:29.802213 through 15:03:54.053310.
- Native SQL Server 2019 engine 15.0.2000.5, database compatibility 150.
- Newly migrated disposable fixture: one fictional Active fire-extinguisher
  asset, building `North Block`, department `Synthetic Department`, null
  location. The normal GSD database was not used.
- The unchanged startup scheduler created one unassigned November 2026 schedule
  before measurement. Its full row is captured alongside the catalog before
  and after measurement. There were zero inspection records and zero forms.
  Before/after snapshots match exactly. No report previews, executions or GSD
  record writes were performed by the evaluation.
- Both paths used the same live server clock and explicit historical question
  periods. The historical corpus manifest's fixed 2025-06-01 clock was not
  injected into production. This repeat is not a recreation of that old runtime.
- Each mode resolved to its requested interpreter for 17 cases. V3D-018 resolved
  to the shared SafetyRules guard in both modes before interpreter invocation.
  SafetyRules is recorded explicitly and is not fallback. Thus 18 DeepSeek-mode
  endpoint requests do not mean 18 model invocations.
- Configured model alias `deepseek-flash`, temperature zero. The endpoint does
  not expose provider-reported model revision, usage, invoice cost, or physical
  retry count. The runner makes no retries. Unchanged backend transient retries
  remain possible, bounded by three physical attempts per provider-exposed
  request. The conservative pre-run bound was 54; with one shared guard the
  observed run's upper bound is 51, not a measured physical-attempt count.
- No raw pre-normalization output was captured. Full final backend responses
  are saved. No model prompt, provider payload, credential or token map is saved
  in the reviewed result files.

Corpus SHA-256, normalized LF:
`dd313076a40ea5dd8eddfef610ff8db37ca6c5ba1934a286c6023a1586ca0e25`.
Production C# source SHA-256:
`d04a1398ee20a46ec3810af13054d7f8fd9cb7e3e98d3a353814456813bfcf7a`.
Backend assembly SHA-256:
`0775ae0ab00853038d3ed3d1248635b9c53bfedc2b28a82590119be1fc4d776a`.
Interpreter source, including prompt, SHA-256:
`433598a89615d8073603a73f3978f4233390b4aa4dc4f5306c60cbcecb7677c5`.
The JSON includes protocol hash, both fixture snapshots and per-request source
commit, requested/resolved identities, latency, status, fields, disposition,
complete response, strict differences and catalog-normalization differences.

## Preparation and audit

`dotnet build server/server.csproj --no-restore` passed with zero warnings and
zero errors. Fixture migration and synthetic identity preparation succeeded.
The first fixture's catalog INSERT failed because SQLCMD needed QUOTED_IDENTIFIER
for an indexed computed column; setup was corrected with SQLCMD `-I`, and a new
owned fixture was created. A snapshot preflight also rejected incompatible
SQLCMD `-h -1` / `-y 0` switches; that setup argument was corrected before any
interpretation requests. The startup-created schedule was included in the
frozen fixture protocol before the run. These were setup failures, not hidden
case attempts. No interpretation case was rerun.

A separate offline recomputation checked all 36 unique case/mode pairs, both
rubrics, interpreter identities, HTTP results, and unchanged fixture snapshots.
It passed without any further endpoint requests. Production files and historical
corpus/evidence paths have no changes from the pre-task commit. The owned API
process was stopped after completion. Raw local logs remain ignored under
`artifacts/test-180/`.

Reviewed outputs:

- [Every case, CSV](../../evaluation/pm-analytics-v3-repeat/case-results.csv)
- [Complete results and final responses, JSON](../../evaluation/pm-analytics-v3-repeat/results.json)

The run is evidence about this reviewed development corpus and current backend
path. It does not establish unseen-language generalization, report numerical
correctness, or an interpretation-only accuracy estimate for raw DeepSeek output.
