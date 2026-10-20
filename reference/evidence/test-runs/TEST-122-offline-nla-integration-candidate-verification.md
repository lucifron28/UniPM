---
id: TEST-122
type: test-run
title: Offline and NLA V3 integration candidate verification
status: executed
recordedAtUtc: 2026-10-20T03:30:20Z
testedCommit: 1a37e86f20a658b97e4b5b51c55632f7a64a8c75
sourceBranch: feature/offline-nla-v3-integration
evidenceLevel: locally-executed
---

# Offline and NLA V3 integration candidate verification

## Scope and source history

- Tested the isolated integration candidate at `1a37e86f20a658b97e4b5b51c55632f7a64a8c75`.
- The merged history preserves source branches `feature/offline-inspection-workflow` (`60788e123864dd82fd055677b773f82541bc8ef3`), `feature/nla-v3-asset-inventory` (`c6e47620bff512ad29192622309845ee4348f921`), and `feature/mobile-acknowledgement-home-ux-remediation` (`0aa12b978d22e5ef67762d8c2e369877eaa4db47`).
- Integration corrections at `e8450de` and `1a37e86` carry the complete schedule asset response fields and align Web test fixtures with current contracts. The original source branches and historical evidence were not rewritten.
- This is local engineering verification, not GSD acceptance, release qualification, or native SQL Server verification.

## Backend

Environment: .NET SDK `10.0.203`, Release configuration.

| Check | Result |
|---|---|
| `dotnet build server/server.csproj --configuration Release --no-restore` | Passed: 0 errors, 5 warnings. |
| Full `UniPM.Api.Tests` Release suite | Passed: 558 passed, 50 skipped, 0 failed (608 total). |
| Focused `FullyQualifiedName~PmAnalyticsV3` | Passed: 25 passed, 2 skipped, 0 failed (27 total); both skips require SQL Server 2019. |
| Focused `FullyQualifiedName~PmAnalyticsV2` | Passed: 44 passed, 1 skipped, 0 failed (45 total); the skip requires SQL Server. |
| V3 corpus `validate` | Passed structural validation: 18 cases, 18 labels pending; no score. |
| V3 corpus `diagnose` | Completed without provider or database calls; two unresolved differences remain, V3D-008 and V3D-009. |

The build warnings include NU1902/NU1903 for `System.Security.Cryptography.Pkcs` 9.0.4, V3 nullable filter values (CS8620), a preventive-maintenance nullable dereference (CS8602), and EF1002 in staging initialization. No warning was suppressed as part of this task.

The test process explicitly unset application database and SQL Server test connection variables, JWT/web overrides, and remote embedding-provider configuration. SQL/provider-dependent cases were skipped by their existing guards.

## Web

Environment: Node `22.23.3`, npm `10.9.9`.

| Check | Result |
|---|---|
| `npm ci` | Passed: 476 packages installed from the lockfile. |
| `npm run format:check` | Passed. |
| `npm run lint` | Passed with zero warnings. |
| `npm run build` | Passed, including `tsc -b`; Vite transformed 2,226 modules. |
| `npm run api:contract:check` | Passed, including V2/V3 analytics. |
| `npm run api:contract:test` | Passed: 7/7 negative contract checks. |
| `npm run api:check` | Passed: Orval generation and generated-client drift check. |
| `npm run test:run` | Passed: 247 tests across 31 files. |
| `npm run test:coverage` | Passed: 247 tests across 31 files. |
| Full Playwright suite | Passed: 60 passed, 1 skipped. |
| Focused V3 Playwright scenarios | Passed: 2/2, including asset inventory without a PM period. |

Playwright ran with system Chromium `154.0.8037.57` and a temporary local config pointing to `/usr/bin/chromium`; the config was removed after each run. The live three-role workflow was explicitly isolated from an API and database and skipped. An initial full browser run exposed a missing mock for the supervisor-options request in the form review fixture; the fixture now returns an empty options collection, and the unchanged browser assertion passes in the final full run.

## Mobile workflow CI

The offline source branch's exact-head Flutter CI run #49 (`39362166990`) passed at `60788e123864dd82fd055677b773f82541bc8ef3`, including formatting, analyzer, focused offline/acknowledgement tests, the full Flutter suite, and Android debug APK build. Local Flutter/Dart tooling is unavailable in this environment. No exact-head integration-branch Flutter CI status is claimed in this record.

## SQL Server and external services

- Native SQL Server 2019 execution was unavailable. No `UNIPM_SQLSERVER_TEST_CONNECTION` or `UNIPM_SQLSERVER2019_TEST_CONNECTION` was configured.
- A pull of `mcr.microsoft.com/mssql/server:2019-latest` was attempted for disposable local verification and failed because the image registry returned HTTP 403. No container or database was created.
- Playwright's browser download CDN also returned HTTP 403; system Chromium enabled the local browser suite.
- No Azure/staging/production service, live API, migration, deployment, DeepSeek request, embedding-provider request, or held-out corpus was accessed.
- All 18 provisional V3 labels still require independent human review. The two diagnostic differences are not accuracy scores or approved expectations. No score or acceptance claim is made.

## Failures and corrections

- Backend compilation initially found the integrated schedule endpoint constructing an asset response without its required registration status and QR value. The projection now returns both fields; the Release build and full backend suite pass.
- The full Web unit suite found a batch assignment fixture missing the required asset registration status. The fixture now supplies it; all 247 unit tests pass.
- The first full Playwright run found an unmocked supervisor-options endpoint returning 401 and clearing the synthetic browser session. The scenario now mocks that endpoint; the final full suite passes 60 scenarios.
- A formatter invocation overlapped the negative-contract script while it created temporary OpenAPI files and reported a transient missing-file error. A serial formatter rerun passed.

## Remaining verification

- Exact-head CI on the integration branch, especially Flutter CI for the combined mobile source, remains pending.
- Native SQL Server 2019 query execution remains unverified.
- Independent human review of the 18 question labels, GSD task acceptance, and physical Android device review remain pending.
