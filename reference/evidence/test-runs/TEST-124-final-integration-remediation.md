---
id: TEST-124
type: test-run
title: Offline and NLA V3 integration remediation verification
status: executed
recordedAtUtc: 2026-10-20T05:41:09Z
testedCommit: 4356ad2e06d3100020c95dee252f9b19da3a119a
sourceBranch: feature/offline-nla-v3-integration
evidenceLevel: ci-executed
---

# Offline and NLA V3 integration remediation verification

## Source and CI

The integration branch contains the offline retry/re-authentication changes,
the dependency and analyzer fixes, and a Web CI path-filter update that runs
client checks for backend changes. The source commits and hosted runs are:

| Area | Source commit | Exact workflow run | Result |
|---|---|---|---|
| Backend | `ed84690eb393152a53f006187dc06ba5dd853a01` | [Backend CI #372](https://github.com/lucifron28/UniPM/actions/runs/39375148623) | Passed. |
| Flutter | `616d94ab32f21cb773935bb3281a746f5c1d24c1` | [Flutter CI #55](https://github.com/lucifron28/UniPM/actions/runs/39375650399) | Passed formatting, analysis, focused and full tests, and Android debug APK build. |
| Web | `4356ad2e06d3100020c95dee252f9b19da3a119a` | [Web CI #285](https://github.com/lucifron28/UniPM/actions/runs/39375805282) | Passed the Web verification workflow, including API contracts, generated-client checks, unit/coverage tests, build, and Playwright. |

No server source changed after the backend source commit, and no mobile source
changed after the Flutter source commit. The Web run is at the branch head used
for this record.

## Local verification

| Check | Result |
|---|---|
| .NET 10 solution Release build | Passed: 0 warnings, 0 errors. |
| Full .NET 10 Release tests | Passed: 558 passed, 50 skipped, 0 failed, 608 total. |
| Package vulnerability audit | Passed: `System.Security.Cryptography.Pkcs` resolves to 9.0.21 through `Microsoft.Data.SqlClient`; no vulnerable packages reported. |
| Web formatting, lint, typecheck, V2/V3 API contract checks, negative contract tests, and generated-client drift check | Passed; negative contract checks were 7/7. |
| Web unit suite and coverage suite | Passed: 247 tests across 31 files in each run. |
| Vite production build | Passed: 2,226 modules transformed. |
| Playwright E2E | Passed: 60 passed, 1 skipped. Used installed system Chromium because the Playwright CDN returned HTTP 403. The live API workflow was skipped with its endpoint and password unset. |

The first solution test invocation inherited `UNIPM_JWT_SIGNING_KEY`, which
overrode tests that intentionally validate missing/weak signing configuration.
The rerun unset JWT, provider, Azure, and database variables and passed. No
provider credentials or live database connection were used.

Flutter is not installed in this environment; the exact branch workflow above
performed formatting, analysis, the focused tests, the full test suite, and the
Android debug build. Earlier exact-head workflow attempts stopped at Dart
formatting. The formatter diagnostics were applied in follow-up commits and
the latest workflow passed.

## SQL Server 2019 and evaluation gates

SQL Server 2019 integration remains unverified. No SQL test connection was
configured, and pulling the disposable `mcr.microsoft.com/mssql/server:2019-latest`
image returned HTTP 403. No container or database was created. SQL-dependent
tests account for part of the 50 skipped backend tests; the full suite also
skips provider-dependent coverage when no provider is configured.

The 18 provisional NLA evaluation labels remain pending independent human
approval. No evaluation score, held-out result, institutional acceptance, or
DeepSeek/provider request is claimed. No Azure, staging/production database,
migration, deployment, or merge operation occurred.
