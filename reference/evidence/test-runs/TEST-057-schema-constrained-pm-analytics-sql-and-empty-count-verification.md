---
id: TEST-057
type: test-run
title: Schema-constrained PM analytics SQL Server and empty-count verification
status: executed
recordedAtUtc: 2026-10-03T13:20:08Z
testedCommit: 684252040c961d8e5f8c89a23d91de55f9d16a63
sourceBranch: feature/schema-constrained-nla
evidenceLevel: locally-executed
---

# Schema-constrained PM analytics SQL Server and empty-count verification

## Objective

Verify PM analytics against a disposable native SQL Server 2019 database and confirm that empty count scopes are presented as unmeasurable in the web panel.

## Execution identity and environment

- Both commands ran at commit `684252040c961d8e5f8c89a23d91de55f9d16a63` on `feature/schema-constrained-nla`.
- The backend test asserted SQL Server major version 15, Full-Text Search installed, and compatibility level 150 after migrations. Its disposable database was dropped during cleanup, then `DB_ID` was checked to confirm it was absent.
- The ignored local `.env` `ConnectionStrings__DefaultConnection` value was copied in memory to `UNIPM_SQLSERVER2019_TEST_CONNECTION` for this process only. The ambient `ConnectionStrings__DefaultConnection` process variable was removed during the test and restored in `finally`. No connection value was recorded.
- API authentication and role checks used the test authentication handler; they do not demonstrate real login or production identity-provider integration.

## Commands and results

| Scope | Command | UTC start | UTC end | Exit | Passed | Failed | Skipped |
|---|---|---|---|---:|---:|---:|---:|
| Backend Release filter | `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj -c Release --filter 'FullyQualifiedName~PmAnalytics' --logger 'trx;LogFileName=pm-analytics-sql.trx' --results-directory artifacts/nla-verification` | 2026-10-03T13:15:23.5210754Z | 2026-10-03T13:16:13.3043352Z | 0 | 40 | 0 | 0 |
| Focused web unit file | `npm run test:run -- src/features/reports/pm-analytics.test.tsx` | 2026-10-03T13:16:55.1263357Z | 2026-10-03T13:18:28.7789345Z | 0 | 8 | 0 | 0 |

The backend test covered the four metrics, empty count scopes, source privacy, a 400 validation response, and 401/403 authorization responses. Empty count scopes returned no value and were marked unmeasurable. The web test checked the empty-scope message for compliance and both count metrics.

## Setup checks and failures

- The initial sandboxed scoped formatter attempt hit a local process-pipe `UnauthorizedAccessException`; `dotnet format whitespace .\UniPM.Api.Tests.csproj --include Reports\SqlServerPmAnalyticsTests.cs --no-restore` later exited 0 with authorized elevated access. `.\node_modules\.bin\prettier.cmd --write src/features/reports/pm-analytics.test.tsx` exited 0 from `web/`.
- The initial test launcher looked for the opt-in variable directly in `.env`, stopped before `dotnet test`, and created no database. The successful launch used the ignored default connection value only as a process-scoped SQL Server 2019 opt-in.
- A default-sandbox SQL metadata probe failed with `Win32Exception`; its underlying cause was not independently established. A read-only elevated probe confirmed major version 15, Full-Text Search, and compatibility 150. These setup checks are not test results.

## Generated artifacts

- Backend TRX: `artifacts/nla-verification/pm-analytics-sql.trx`.
- Focused web output: `artifacts/nla-verification/pm-analytics-vitest.log`.
- Raw artifacts are ignored and contain no recorded connection value.

## Skipped verification and limitations

- No full local suite, browser E2E, production authentication, GSD acceptance, IIS deployment, or real-provider test was run. The fixtures and API test identity are synthetic; CI for the final pull-request head remains pending.
