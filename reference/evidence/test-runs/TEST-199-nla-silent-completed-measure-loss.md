---
id: TEST-199
type: test-run
title: NLA V3 explicit completed-measure preservation and device-context guard
status: executed
recordedAtUtc: 2026-11-07T04:28:42Z
testedCommit: 630dc2581d1b468b48476e2438b2bc7a3eadd229
sourceBranch: feature/nla-flexibility-evidence-hardening
evidenceLevel: ci-executed
---

# TEST-199: NLA V3 explicit completed-measure preservation and device-context guard

## Tested source identity

Backend and Web CI passed on PR #117 head
`630dc2581d1b468b48476e2438b2bc7a3eadd229`. The source fix is in `e04f029`;
`630dc25` updates a previously existing test whose explicit scheduled-versus-
on-time comparison expectation became outdated with the shared bounded lexicon.
No application or test source changed after `630dc25`.

PR #117 remains open and draft. PR #116 remained at frozen head
`ef0feecee2b7f2092cd97086b8da2296c6f30e64`. No frozen evaluation, unrevealed
GSD query set, SQL, formula, schema, or deployment file was accessed or changed.

## Reproduction and full-plan comparison

Question:

> Compare completed fire-extinguisher inspections with those finished before
> the PM deadline by department in 2026.

The test supplied a mocked DeepSeek proposal containing both
`completedCount` and `completedOnTimeCount`. On the pre-fix source at
`e163486e531afc3e79ca4f537f66ee48e21b5caa`, the guard returned `Valid` but
replaced the requested measure list with only `completedOnTimeCount`. This
confirmed the silent metric-loss issue. The retained red test output records
the actual before/expected measure arrays; the other unchanged plan fields
below are reconstructed from the same fixture and the post-fix complete-plan
assertions.

Before:

```json
{
  "SchemaVersion": 3,
  "CatalogVersion": 2,
  "Subject": "pmExecution",
  "Mode": "Aggregate",
  "Period": {
    "Basis": "pmCycle",
    "Kind": "Year",
    "Year": 2026,
    "Month": null,
    "Quarter": null,
    "StartCycle": null,
    "EndCycle": null
  },
  "Categories": ["fire-extinguisher"],
  "Measures": ["completedOnTimeCount"],
  "GroupBy": ["department"],
  "BaseFilters": [],
  "MatchingFilters": [],
  "AggregateFilters": [],
  "OrderBy": [],
  "Top": null,
  "Presentation": "Table",
  "Columns": ["department", "completedOnTimeCount"]
}
```

After:

```json
{
  "SchemaVersion": 3,
  "CatalogVersion": 2,
  "Subject": "pmExecution",
  "Mode": "Aggregate",
  "Period": {
    "Basis": "pmCycle",
    "Kind": "Year",
    "Year": 2026,
    "Month": null,
    "Quarter": null,
    "StartCycle": null,
    "EndCycle": null
  },
  "Categories": ["fire-extinguisher"],
  "Measures": ["completedCount", "completedOnTimeCount"],
  "GroupBy": ["department"],
  "BaseFilters": [],
  "MatchingFilters": [],
  "AggregateFilters": [],
  "OrderBy": [],
  "Top": null,
  "Presentation": "Table",
  "Columns": ["department", "completedCount", "completedOnTimeCount"]
}
```

The candidate recognizes bounded deadline wording in the shared lexicon, keeps
a standalone generic completed mention only when the question explicitly
compares or joins it with the on-time measure, and lets the model guard
preserve that measure set. The deterministic parser and mocked DeepSeek guard
both produce the complete two-measure plan.

## Regression coverage

Seven comparison and conjunction phrasings assert the complete valid aggregate
plan, including category, PM year, department grouping, both measures, empty
filters, ordering and thresholds, table presentation, and all output columns:

- English comparison using “with,” English “versus,” and English “and”.
- Filipino `Ihambing` and `Ilang` phrasings using `natapos bago ang PM deadline`.
- Taglish `I-compare ... natapos before the PM deadline` and `... at ... finished before the PM deadline`.

The mocked DeepSeek path also covers the explicit scheduled-versus-on-time
comparison. It returns both `scheduledCount` and `completedOnTimeCount`, with
both columns, instead of asking for clarification.

Four negative controls cover a mobile app or QR scanner described as not
working in English and Filipino. The mocked proposal suggests a non-operational
equipment count; the guard returns `NeedsClarification`, an empty measure list,
and no `nonOperationalCount`. An application or scanner failure is not
classified as an equipment finding.

## Local focused verification

Command:

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~preserves_explicit_completed_and_on_time_comparison|FullyQualifiedName~app_or_scanner_failure_as_equipment_finding|FullyQualifiedName~preserves_an_explicit_bounded_outcome_comparison"
```

Result: 19 passed, 0 failed, 0 skipped; test duration 847 ms. Seven cases use
the deterministic parser, twelve use a local fake model handler, and no live
DeepSeek call was made. The local output is retained under ignored
`artifacts/nla-flexibility-evidence/draft117-silent-metric-focused-final.log`.

## CI

- [Backend CI run 42338022970](https://github.com/lucifron28/UniPM/actions/runs/42338022970) passed: 861 tests total, 806 passed, 55 skipped, 0 failed. Build, observability Compose, Prometheus, and Grafana validation passed. The skipped tests require a SQL Server 2019 connection unavailable in CI.
- [Web CI run 42338028063](https://github.com/lucifron28/UniPM/actions/runs/42338028063) passed: formatting, lint, typecheck, API contract and API checks; unit and coverage runs each had 32 files and 278 tests passed; production build passed; E2E had 64 passed and 2 skipped.
- The first backend run on `e04f029` had one failure: an existing test expected clarification for an explicitly phrased scheduled-versus-on-time comparison. Its expectation was updated to assert both measures; the exact-head backend run above then passed.

## Evidence limits

All queries are developer-authored synthetic strings, and every DeepSeek-path
case uses a local canned response. The run records no provider usage, provider
latency, or cost because there were zero live provider calls. The tests verify
interpreted plans, not SQL report execution or numerical results. CI skipped
55 SQL Server 2019 tests and two web E2E cases. No independent GSD evaluation,
field validation, merge, deployment, or new freeze approval is claimed.

Ignored raw CI logs are retained locally at:

- `artifacts/nla-flexibility-evidence/backend-ci-630dc25.log`
- `artifacts/nla-flexibility-evidence/web-ci-630dc25.log`
- `artifacts/nla-flexibility-evidence/draft117-backend-ci-failed.log`
- `artifacts/nla-flexibility-evidence/draft117-completed-ontime-repro-red.log`
