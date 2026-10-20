---
id: TEST-117
type: test-run
title: NLA V3 Phase 6 asset inventory final verification
status: executed
recordedAtUtc: 2026-10-20T00:57:30Z
testedCommit: fdeb4268f2b915765a3c5504d510f2c5b301508d
sourceBranch: feature/nla-v3-asset-inventory
evidenceLevel: locally-executed
---

# NLA V3 Phase 6 asset inventory final verification

## Scope and boundaries

- Source commit: `fdeb4268f2b915765a3c5504d510f2c5b301508d` on
  `feature/nla-v3-asset-inventory`. This is the evidence-only follow-up to code
  commit `b2c40f2a39c0e1b326a612cbdf09f77ea6555459`.
- Verification was limited to the Phase 6 asset inventory subject and existing
  V3/V2 regressions. Phase 7 was not implemented.
- Test execution used .NET SDK `10.0.203`. The API test process received a
  minimal environment without database or remote-provider configuration.
- No SQL Server, Azure/staging service, production data, deployment, migration,
  DeepSeek request, embedding-provider request, or held-out corpus was accessed.

## Backend checks

| Command/check | Result |
|---|---|
| `dotnet restore tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --ignore-failed-sources -p:NuGetAudit=false` | Passed; restored the test and QR generator projects, with three dependent projects already up to date. |
| `dotnet build UniPM.slnx --configuration Release --no-restore` | Passed on .NET 10.0.203: 0 errors and 3 existing warnings (CS8602, CS8620, EF1002). |
| `dotnet test UniPM.slnx --configuration Release --no-build --no-restore --logger 'console;verbosity=minimal'` | Passed: 555 passed, 50 skipped, 0 failed, 605 total. |
| Focused `FullyQualifiedName~PmAnalyticsV3` API tests | Passed: 25 passed, 2 skipped, 0 failed, 27 total. The skipped tests require SQL Server 2019. |
| Focused `FullyQualifiedName~PmAnalyticsV2` API tests | Passed: 44 passed, 1 skipped, 0 failed, 45 total. The skipped test requires SQL Server. |

The first full-suite attempt inherited machine-level JWT configuration and failed
two tests whose contract requires missing signing settings. Rerunning with an
isolated process environment passed all non-SQL tests. This was test-process
configuration, not a source change.

## Web and browser checks

Local checks at the tested source commit passed: formatting, lint, typecheck,
API contract validation, all 7 negative API contract checks, generated-client
drift validation, 247/247 unit tests across 31 files, 247/247 coverage tests,
and the production build (2,226 modules). The focused system-Chromium V3
Playwright scenarios passed 2/2, covering source-record drill-down and asset
inventory. Web CI run [#280](https://github.com/lucifron28/UniPM/actions/runs/39297020004)
passed at `be07b68`; subsequent Phase 6 changes did not change web code. The
final web tree was checked locally at the tested source commit.

## Continuous integration

Backend CI run [#362](https://github.com/lucifron28/UniPM/actions/runs/39346775885)
passed on the exact source commit `b2c40f2a39c0e1b326a612cbdf09f77ea6555459`.
After the local verification record was committed, Backend CI run
[#365](https://github.com/lucifron28/UniPM/actions/runs/39350353241) passed on
the exact pushed branch head `d1d560ae3f18ce6fd0b2982fc4a9be892fa47c1b`.

## SQL Server 2019

The V3 SQL Server 2019 integration cases were skipped because no dedicated test
connection was configured. No SQL Server 2019 container image was available;
the previous local image pull was denied with HTTP 403. Therefore the LINQ
translation and runtime behavior against native SQL Server 2019 remain
unverified. No Azure or staging connection was attempted.

## Evaluation boundary

The 18 provisional V3 evaluation labels remain pending independent human
review. No interpretation score or GSD acceptance claim is made. No provider
credits were used.
