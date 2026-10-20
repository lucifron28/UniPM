---
id: TEST-110
type: test-run
title: PM analytics V3 reporting correctness and workflow hardening verification
status: executed
recordedAtUtc: 2026-10-19T10:15:03Z
testedCommit: e2f90bba35707a7142b3dccb1afb8eeec6143ad4
sourceBranch: feature/nla-v3-composable-reporting
evidenceLevel: locally-executed
---

# PM analytics V3 reporting hardening verification

## Objective

Verify the committed V3 reporting changes using the supported web runtime and
record unavailable backend, database and browser checks without treating them
as passing.

## Execution Identity

- Tested commit: `e2f90bba35707a7142b3dccb1afb8eeec6143ad4`.
- Branch: `feature/nla-v3-composable-reporting`.
- Date: 2026-10-19 UTC.
- Web runs used Node `v22.23.3` and npm `11.9.0`, matching the package's Node
  22 major requirement. The Node 22 binary was invoked from an npm cache under
  `/tmp`; no repository runtime configuration was changed.
- Raw command outputs are under ignored path
  `artifacts/evidence/20261019T101154Z-e2f90bb/`.

## Environment

- .NET SDK: unavailable (`dotnet: command not found`).
- `UNIPM_SQLSERVER_TEST_CONNECTION`: absent.
- `UNIPM_SQLSERVER2019_TEST_CONNECTION`: absent.
- Playwright Chromium executable: absent.
- No DeepSeek request was made and no provider cost was incurred.

## Commands

| Command | Result |
|---|---|
| `npm run build` | Passed under Node 22.23.3; includes TypeScript `tsc -b` and Vite production build. |
| `npm run lint` | Passed with zero warnings. |
| `npm run test:run -- src/features/reports/pm-analytics-v3.test.tsx` | Passed: 1 test file, 7 tests. |
| `./node_modules/.bin/prettier --check src/features/reports/pm-analytics-v3.tsx src/features/reports/pm-analytics-v3.test.tsx e2e/pm-analytics-v3.spec.ts src/api/generated/models/pmAnalyticsV3RecordRow.ts` | Passed. |
| `npm run api:check` | Passed: OpenAPI sanity, client generation and generated-client drift check. |
| `npm run api:contract:test` | Passed all 7 negative contract checks. |
| `dotnet build server/server.csproj --no-restore` | Not run: exit 127 because `dotnet` is unavailable. |
| `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore --filter FullyQualifiedName~PmAnalyticsV3` | Not run: exit 127 because `dotnet` is unavailable. |
| `npm run e2e -- e2e/pm-analytics-v3.spec.ts` | Browser test did not start: exit 1 because the Chromium executable is missing. |

The browser installation attempt used Playwright's official download and
returned HTTP 403 (`Domain forbidden`). The failed Playwright invocation
reported one configured test and zero tests executed in a browser.

## Results

The OpenAPI contract shape and generated `inspectionId` client field match.
The targeted React suite exercised question interpretation, preview, explicit
confirmation, stale-result clearing, expired preview recovery, catalog loading
and retry, source-record review, unsupported distinct-asset drill-down, and an
empty aggregate result.

## Test Counts

- Focused V3 React tests: 7 passed, 0 failed.
- Negative OpenAPI contract cases: 7 passed, 0 failed.
- Playwright: 1 configured, 0 executed because Chromium could not launch.
- Backend tests: 0 executed because the .NET SDK was unavailable.

## SQL Server Verification

Native SQL Server 2019 compatibility is **unverified**. No SQL test connection
was configured and the backend could not be built in this environment. The
catalog query's SQL translation was not exercised against SQL Server.

## AI-Provider Verification

No real or fake DeepSeek request was made in this run. The existing gate still
requires Development, `NaturalLanguageAnalytics:V3DeepSeekEnabled`, and
`DEEPSEEK_API_KEY`. Production LLM calls were not enabled.

## Generated Artifacts

Raw logs and `SHA256SUMS.txt` are ignored under
`artifacts/evidence/20261019T101154Z-e2f90bb/`, including Node 22 build, lint,
focused unit, format, API generation/drift, negative contract, backend-tooling
and browser-run outputs.

## Failures And Corrections

The browser run stopped before the test body because its expected Chromium
binary did not exist. A download attempt was blocked with HTTP 403. The backend
commands stopped because no .NET SDK is installed. These are environment
blockers; neither constitutes a passing runtime verification.

## Skipped Verification

- Backend compile, service tests and HTTP endpoint/auth tests.
- Native SQL Server 2019 translation and execution.
- Browser workflow and responsive layout in Chromium.
- Full repository test suites and CI.

## Limitations

The successful web checks provide frontend and generated-contract evidence only.
They do not verify backend execution, runtime authorization, SQL Server query
translation, real browser accessibility, DeepSeek quality, deployment, or GSD
acceptance.
