---
id: TEST-193
type: test-run
title: NLA V3 final-hardening verification
status: executed
recordedAtUtc: 2026-11-06T12:26:06Z
testedCommit: 6449cbf2bd5d385a30a1b92c1e2c56112edf85bb
sourceBranch: feature/nla-final-hardening
evidenceLevel: locally-executed
---

# NLA V3 final-hardening verification

## Objective and identity

Verify the final local backend and browser regression behavior for the bounded
NLA V3 interpretation hardening. This record identifies the exact feature
branch source used by the final backend suite and browser suite:

- Source commit: `6449cbf2bd5d385a30a1b92c1e2c56112edf85bb`.
- Implementation commit: `b7e13a3b5afa313285b3116c4ce71a46888bca6d`.
- Feature branch: `feature/nla-final-hardening`.
- Base: `main` at `53afe5708ca640b6fe7618589cbf3be1a1de6ae3`.
- Environment: Windows, .NET SDK `10.0.300`, test runtime `.NET 10.0.8`,
  Node `v24.15.0`, npm `11.12.1`, Playwright Chromium.
- The web workflow pins Node 22; local web results here used Node 24. GitHub CI
  is required to verify the exact PR head on the pinned CI runtime.

## Commands and results

| Scope | Command or invocation | Tested source | Result |
|---|---|---|---|
| Focused V3 hardening | 95-case focused filter (exact command below) | `6449cbf2bd5d385a30a1b92c1e2c56112edf85bb` | 95 passed, 0 failed, 0 skipped. |
| Full backend build | `dotnet build .\UniPM.slnx --configuration Release --no-restore` | `6449cbf2bd5d385a30a1b92c1e2c56112edf85bb` | Passed; 0 warnings, 0 errors. |
| Full backend suite | `dotnet test .\UniPM.slnx --configuration Release --no-build --verbosity normal --logger "trx;LogFileName=backend-tests-final-head.trx" --blame-hang-timeout 5m` | `6449cbf2bd5d385a30a1b92c1e2c56112edf85bb` | 746 passed, 54 skipped, 0 failed; 800 total. |
| C# formatting | `dotnet format .\UniPM.slnx --verify-no-changes --include server\Features\Reports\PmAnalyticsV3LanguageLexicon.cs server\Features\Reports\PmAnalyticsV3QuestionInterpreter.cs tests\UniPM.Api.Tests\Reports\PmAnalyticsV3FinalHardeningTests.cs tests\UniPM.Api.Tests\Reports\PmAnalyticsV3AcceptanceTests.cs tests\UniPM.Api.Tests\Reports\PmAnalyticsV3GuardCorrectionTests.cs` | `6449cbf2bd5d385a30a1b92c1e2c56112edf85bb` | Passed; no formatting changes required. |
| Web formatting and lint | `npm run format:check`; `npm run lint` | `6449cbf2bd5d385a30a1b92c1e2c56112edf85bb` | Both passed. |
| Web checks | `npm run typecheck`; `npm run api:contract:check`; `npm run api:contract:test`; `npm run api:check`; `npm run test:run`; `npm run test:coverage`; `npm run build` | `b7e13a3b5afa313285b3116c4ce71a46888bca6d` | All passed before the test-only follow-up at `6449cbf`. The follow-up changed only a backend acceptance test and Playwright spec; no web application source, API contract, or dependency manifest changed. |
| Web unit suite | `npm run test:run` | `b7e13a3b5afa313285b3116c4ce71a46888bca6d` | 278 passed in 32 files. |
| Web coverage | `npm run test:coverage` | `b7e13a3b5afa313285b3116c4ce71a46888bca6d` | 278 passed in 32 files; 67.08% line coverage. This repository has no overall coverage threshold in the CI command. |
| Web production build | `npm run build` | `b7e13a3b5afa313285b3116c4ce71a46888bca6d` | Passed. |
| Web browser suite | `$env:CI='true'; $env:UNIPM_E2E_PORT='5176'; npm run e2e -- --workers=2` | `6449cbf2bd5d385a30a1b92c1e2c56112edf85bb` | 64 passed, 2 SQL-backed cases skipped, 0 failed. The new Filipino guided-scope case passed. |
| Docker Compose validation | `docker compose --env-file .env.example -f docker-compose.sqlserver2025.yml --profile observability config --quiet` with CI fixture variables | `b7e13a3b5afa313285b3116c4ce71a46888bca6d` | Passed. No production services were contacted. |
| Prometheus and Grafana configuration | `docker run --rm --entrypoint /bin/promtool ... prom/prometheus:v3.5.0 check config ...`; `python -m json.tool observability/grafana/dashboards/unipm-system-health.json` | `b7e13a3b5afa313285b3116c4ce71a46888bca6d` | Grafana JSON validation passed. Prometheus `promtool` did not run because Docker Desktop's Linux engine was unavailable. |

Focused V3 hardening command:

```powershell
dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj --filter 'FullyQualifiedName~PmAnalyticsV3FinalHardeningTests|FullyQualifiedName~Filipino_relative_year_uses_fixed_Manila_clock|FullyQualifiedName~Filipino_named_place_returns_a_safe_partial_plan_for_guided_field_selection' --logger 'console;verbosity=minimal'
```

Raw logs and the TRX report are retained only in ignored
`artifacts/nla-final-hardening/`.

## Skips, failed attempts, and corrections

- The 54 skipped backend tests require `UNIPM_SQLSERVER2019_TEST_CONNECTION`
  or another explicitly configured optional provider/service. This includes
  the V3 SQL Server 2019 translation, scale, and inventory cases. No native SQL
  Server 2019 numerical verification is claimed.
- Two browser scenarios that depend on a SQL-backed live service skipped. The
  remaining browser suite used mocked API data and a local Vite server.
- The new full browser suite initially ran with eight local Chromium workers and
  timed out in existing unrelated inspection, schedule, and context-navigation
  cases. A second eight-worker run also timed out in two existing UI cases.
  The full suite passed at two workers. These attempts did not identify a
  product regression; exact-head GitHub CI remains the final check.
- The initial scoped C# format check found whitespace-only formatting issues;
  the formatter corrected them before the final backend build and suite.
- A local `promtool` container could not start because the Docker Desktop Linux
  engine was unavailable. This is an unexecuted local infrastructure check,
  not a passing result.

## AI provider boundary

No paid DeepSeek request was made. DeepSeek guard tests use a local mocked HTTP
handler with canned responses. No report was confirmed or executed against
production or real institutional data. The browser suite used fictional
accounts and mocked API responses.

## Exact-head CI

GitHub Actions results were pending when this local test-run record was
created. The PR's backend and web workflow results must be checked on its exact
head before describing CI as passing. No native SQL Server 2019, GSD acceptance,
or production-provider evidence is implied by these local checks.
