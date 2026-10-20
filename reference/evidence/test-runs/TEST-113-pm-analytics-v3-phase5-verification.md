---
id: TEST-113
type: test-run
title: PM analytics V3 Phase 5 evaluation and correctness verification
status: executed
recordedAtUtc: 2026-10-19T16:57:25Z
testedCommit: 616e4ba2752a6acb20a5b823bf53f03c5b3a51ac
sourceBranch: feature/nla-v3-phase5-evaluation
evidenceLevel: locally-executed
---

# PM analytics V3 Phase 5 evaluation and correctness verification

## Execution identity and boundaries

- Tested source commit: `616e4ba2752a6acb20a5b823bf53f03c5b3a51ac`.
- Base commit: `cacb6c0a0856955a555f9caaca95f93a3f551098`.
- Branch: `feature/nla-v3-phase5-evaluation`.
- Runtime: .NET SDK `10.0.203`, Node `v22.23.3`.
- V3 backend tests used EF InMemory test databases. No Azure or staging
  connection was read or attempted.
- No DeepSeek request or other provider call was made. No provider costs were
  incurred. No held-out data was created, read, or evaluated.
- No migrations, deployment, production-data changes, or merges occurred.

## Backend and evaluator

| Command/check | Result |
|---|---|
| `dotnet restore tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --ignore-failed-sources` | Passed; reported NU1902 and NU1903 advisories for existing transitive `System.Security.Cryptography.Pkcs` 9.0.4. |
| `dotnet build UniPM.slnx --no-restore` | Passed under .NET 10: 0 errors, 13 warnings. Warnings include the same package advisories plus existing CS8620, CS8602, and EF1002 findings. |
| Focused V3 backend tests (`FullyQualifiedName~PmAnalyticsV3`) | Passed: 20, failed: 0; one SQL Server 2019 test skipped because its connection variable was absent. |
| Focused V2 interpreter, grammar, validator, endpoint, and interpretation compatibility tests | Passed: 73/73, 0 skipped. |
| `dotnet format whitespace UniPM.slnx --verify-no-changes` for changed C# files | Passed; solution loading emitted a warning. |
| V3 evaluator `validate` | Passed structurally: 18 cases, all 18 labels pending, no score. |
| V3 evaluator `diagnose` | Completed without provider/database calls. It found differences in V3D-008 and V3D-009. These are diagnostic differences, not accuracy results. |
| V3 evaluator `score` | Correctly blocked with exit code 3; `scoreProduced: false`, 18 pending labels. |

The V3 regression set covers multi-category compliance, grouping/ranking,
duplicate catalog suggestions, category comparison intent, unsafe request
rejection, role authorization, signed preview tampering and cross-user access,
expiry, and full report execution against independent synthetic fixtures.
Fixtures cover matching denominators, thresholds, category cycles, pagination,
missing coverage, zero eligible rows, and February/Q1 and November/Q4 source
membership with filters and contributing records.

## Web/API/browser checks

All Web commands ran from `web/` with Node `v22.23.3` and the committed lockfile.

| Command/check | Result |
|---|---|
| `npm ci` | Passed using a task-local npm cache. |
| `npm run format:check` | Passed. |
| `npm run lint` | Passed with zero reported lint warnings. |
| `npm run api:contract:check` | Passed, including PM Analytics V2/V3. |
| `npm run api:contract:test` | Passed: 7/7 negative contract checks. |
| `npm run build` | Passed TypeScript and Vite build; 2,226 modules transformed. |
| `npm run test:run -- src/features/reports/pm-analytics-v3.test.tsx` | Passed: 9/9 focused React tests. |
| `npm run api:check` | Passed OpenAPI client generation and generated-client drift check; no tracked generated-client changes. |
| `npx playwright test e2e/pm-analytics-v3.spec.ts` | Passed: 1/1 narrow-viewport V3 scenario at 390×844 using system Chromium `154.0.8037.57`. A temporary config pointed Playwright at `/usr/bin/chromium` and was removed after the run. |

The complete browser suite and accessibility audit were not run. This local
scenario is not GSD workflow acceptance.

## SQL Server 2019 and hosted CI

The added SQL Server 2019 integration test creates a unique disposable local
database, sets compatibility level 150, verifies the SQL Server major version,
checks Q1/February and Q4/November record membership and matching denominators,
then drops its database. Its helper only permits loopback connections. It was
not executed because `UNIPM_SQLSERVER2019_TEST_CONNECTION` was absent and
`docker pull mcr.microsoft.com/mssql/server:2019-latest` was denied with
`Forbidden`. No database connection was attempted. Native SQL Server 2019
translation/execution remains unverified.

The GitHub commit-status API returned HTTP 403 Forbidden, so hosted CI status
could not be independently read in this environment. The branch was pushed
separately; this record does not claim hosted CI passed.

## Interpretation review blockers

All 18 provisional question labels remain pending human review. The most
material unresolved differences are V3D-008 (whether `or` requires a no-plan
clarification or a safe partial draft) and V3D-009 (“not late” might mean
on-time, incomplete, or exclude late records). V3D-004's comparison expectation
omits category grouping; V3D-006 omits an implied compliance measure; V3D-007
asks about noncontiguous months; V3D-012 may belong to the unimplemented
inventory subject; and V3D-016 needs a decision on editable sort intent.
Remaining per-case questions and corpus coverage gaps are listed in
[`review-notes.md`](../../evaluation/pm-analytics-v3/review-notes.md).

No V3 accuracy score, SQL Server compatibility result, held-out result, or GSD
acceptance claim is made.
