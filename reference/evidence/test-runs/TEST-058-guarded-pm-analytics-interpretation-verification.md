---
id: TEST-058
type: test-run
title: Guarded PM analytics interpretation verification
status: executed
recordedAtUtc: 2026-10-03T17:53:30Z
testedCommit: c0d1cd8d32374756c775b2dae0b596cdbb1f18cb
sourceBranch: feature/nla-interpretation-evaluation
evidenceLevel: locally-executed
---

# Guarded PM analytics interpretation verification

## Objective

Record the focused backend Release verification for the guarded natural-language
PM analytics interpreter. This is implementation and endpoint evidence, not a
model-quality result.

## Execution identity

- The successful run tested commit
  `c0d1cd8d32374756c775b2dae0b596cdbb1f18cb` on
  `feature/nla-interpretation-evaluation`.
- The frozen formal PMIS baseline and TEST-055 through TEST-057 remain separate
  records. This run does not alter the `formal-testing-baseline-2026-10-03` tag
  or attribute new results to those earlier commits.

## Implemented boundary (source-inspected)

- The following contract is source-inspected at the tested commit. The local test run below does not establish semantic model quality.
- The strict rule-based interpreter remains the default.
  `NaturalLanguageAnalytics:Enabled` defaults to `false`, and the default path
  sends no HTTP request.
- When enabled, the only provider adapter is local Ollama at an HTTP loopback
  address. The typed client disables redirects. This phase adds no remote
  provider. The provider receives a fixed instruction and the masked question,
  not operational records.
- The question limit is 512 characters. The provider call budget is 100 calls
  per process. Configuration accepts a 1 to 60 second timeout, 1 to 1024
  output tokens, and a response body limit of at most 16 KiB. The service
  validates HTTP loopback addressing, the model name, and configured limits
  when an enabled request runs.
- Results have `Valid`, `NeedsClarification`, or `Unsupported` status.
  Clarification fields are `Metric`, `AssetCategory`, `Year`, `Month`,
  `Department`, and `GroupBy`. `NeedsClarification` and `Unsupported` results
  have no plan. Provider failures are separate safe 503 responses with fixed
  `ProviderUnavailable`, `Timeout`, or `InvalidOutput` codes.
- The backend requires an explicit four-digit year and month before accepting
  a valid plan. It checks the four existing metrics, four asset categories,
  department and grouping scope, and CPMP category-month rules. A valid result
  includes a backend-generated canonical question that round-trips through the
  unchanged strict parser and validator. The caller can use that canonical
  question with `/api/v1/analytics/pm/query`. The interpretation endpoint does
  not execute analytics.
- `Progress` is presented as `Percent` for progress or rate requests and as
  `Count` for explicit inspection-count requests. `OnTimeCompliance` remains
  `Percent`; `CompletedLate` and `NonOperational` remain counts. Count
  presentation uses the existing numerator and adds no metric or calculation.
- The request mask covers email addresses, Philippine-style phone numbers,
  and employee, student, staff, or personnel identifiers matching its
  patterns. It does not detect arbitrary personal names. These tests do not
  verify name masking or model accuracy.

## Commands and results

The successful local run used this command:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --configuration Release --disable-build-servers -m:1 -p:UseSharedCompilation=false --filter 'FullyQualifiedName~NaturalLanguageAnalyticsInterpretationTests|FullyQualifiedName~PmAnalyticsTests' --logger 'trx;LogFileName=TEST-NLA-Interpretation-c0d1cd8-corrected.trx' --results-directory artifacts/nla-verification
```

| Started UTC | Finished UTC | Exit | Passed | Failed | Skipped | Total |
|---|---|---:|---:|---:|---:|---:|
| 2026-10-03T17:36:37.1785753Z | 2026-10-03T17:36:51.3263558Z | 0 | 60 | 0 | 1 | 61 |

The successful run used runner session `20524` and took about 14 seconds. Tests
exercised the default interpreter, guard and plan validation, provider response
handling through a fake HTTP handler, and endpoint authorization with the test
authentication handler.
The skipped test was
`SqlServerPmAnalyticsTests.Gsd_analytics_uses_sql_server_2019_metrics_and_enforces_access`.
No local native SQL Server test ran. Endpoint authorization tests use the test
authentication handler; they do not verify JWT login or an external identity
provider.

The build emitted the existing nullable warning `CS8602` in the unchanged
`server/Features/PreventiveMaintenanceForms/PreventiveMaintenanceFormEndpoints.cs:442`.
That file is outside this change.

## Initial sandbox launch and cleanup

An initial default-sandbox launch at the same commit used runner session
`14547`, started at `2026-10-03T17:28:22.4603982Z`, and exited 1 at
`2026-10-03T17:35:14.8705063Z`. It logged only
`Determining projects to restore...`, executed no tests, and generated no TRX.
It is classified as **NOT EXECUTED** and is excluded from the counts above.

Process inspection tied ten `dotnet.exe` children to launch parent PID `6428`;
their creation times were from `2026-10-03T17:28:23Z` through
`2026-10-03T17:28:24Z`. Cleanup rechecked the parent and child identities,
stopped only those owned children, and confirmed all ten were absent. The exact
PID list remains in the ignored run metadata. The corrected command above then
ran with the native SDK executable and serialized build settings.

## Generated artifacts and limits

- Successful-run log: `artifacts/nla-verification/TEST-NLA-Interpretation-c0d1cd8-corrected.log`.
- Successful-run TRX: `artifacts/nla-verification/TEST-NLA-Interpretation-c0d1cd8-corrected.trx`.
- Successful-run metadata: `artifacts/nla-verification/TEST-NLA-Interpretation-c0d1cd8-corrected.txt`.
- Initial-launch log and metadata:
  `artifacts/nla-verification/TEST-NLA-Interpretation-c0d1cd8.log` and
  `artifacts/nla-verification/TEST-NLA-Interpretation-c0d1cd8.txt`.

The tests used local fakes; no live model or remote AI provider was called. This
run did not repeat native SQL verification, run browser tests, verify real JWT
authentication, or run the complete backend suite. It establishes no GSD,
professor, or adviser acceptance and no model-quality result. See
[PLAN-NLA-001](../../planning/schema-constrained-nla.md) for the guarded
interpreter contract and [TEST-057](TEST-057-schema-constrained-pm-analytics-sql-and-empty-count-verification.md)
for the earlier SQL and empty-count execution.
