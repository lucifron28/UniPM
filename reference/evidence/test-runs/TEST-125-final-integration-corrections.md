---
id: TEST-125
type: test-run
title: Final integration corrections verification
status: executed
recordedAtUtc: 2026-10-20T06:18:30Z
testedCommit: a801c515e60870649aedc8219577d942ab422205
sourceBranch: feature/offline-nla-v3-integration
evidenceLevel: ci-executed
---

# Final integration corrections verification

## Objective

Verify transient offline cached reads and exact staging initialization row
counts on the isolated integration branch without accessing Azure staging or
production.

## Execution identity

The final source commit is `a801c515e60870649aedc8219577d942ab422205`.
The backend guard change was introduced in `323c65840c3698625fa2682bc04ffeb9793d5149`;
the final mobile cache implementation and regressions are in the source history
through `a801c515e60870649aedc8219577d942ab422205`. The remote feature branch
matched that final source commit when checked.

## Environment

- Backend checks used .NET SDK 10.0.203 in Release configuration.
- Web checks used Node.js 22.23.3 and the existing dependency tree matching the
  checked-in lockfile.
- Flutter is unavailable in the local shell. The hosted Flutter workflow ran
  formatting, analysis, focused tests, the full suite, and an Android debug
  build.
- `UNIPM_SQLSERVER2019_TEST_CONNECTION` and
  `UNIPM_SQLSERVER_TEST_CONNECTION` were not configured. No SQL connection was
  attempted. No Azure database, provider, staging resource, or production
  resource was accessed.

## Commands

- `.NET 10`: solution Release build, full solution test run, focused
  `StagingDemoInitializationTests`, and
  `dotnet list UniPM.slnx package --vulnerable --include-transitive`.
- Web: `npm run format:check`, `npm run lint`, `npm run typecheck`,
  `npm run api:contract:check`, `npm run api:contract:test`, `npm run api:check`,
  `npm run test:run`, `npm run test:coverage`, and `npm run build`.
- Web browser suite: `npm run e2e -- --config /tmp/unipm-system-chromium.playwright.config.ts`,
  using installed system Chromium. The live API workflow remained disabled
  because it writes synthetic records to a database.
- Web dependency audit: `npm audit --omit=dev --audit-level=high` and
  `npm audit --audit-level=high`.

## Results

| Check | Result |
|---|---|
| .NET 10 Release solution build | Passed: 0 warnings, 0 errors. |
| Full backend test suite | Passed: 564 passed, 50 skipped, 0 failed, 614 total. |
| Focused staging initialization tests | Passed: 7 passed, 2 SQL Server 2019 tests skipped, 9 total. |
| NuGet vulnerability audit | Passed: no vulnerable packages reported across the five solution projects. |
| Web formatting, lint, typecheck, API contracts, negative contract checks, and generated-client check | Passed; negative contract cases were 7/7. |
| Web unit and coverage suites | Passed: 247 tests in 31 files in each run. Coverage run reported 77.35% statements, 70.32% branches, 72.85% functions, and 79.10% lines. |
| Web production build | Passed: 2,226 modules transformed. |
| Web Playwright E2E | Passed: 60 passed, 1 skipped. The skipped scenario writes through a live API. |
| Production Web dependency audit | Passed: 0 vulnerabilities with development dependencies omitted. |
| Flutter CI | Passed on `a801c515e60870649aedc8219577d942ab422205`: formatting, analysis, focused acknowledgement tests, full Flutter tests, and Android debug APK build all succeeded. |

Hosted workflow runs:

| Workflow | Source commit | Run | Result |
|---|---|---|---|
| Backend CI | `323c65840c3698625fa2682bc04ffeb9793d5149` | [run 39378638991](https://github.com/lucifron28/UniPM/actions/runs/39378638991) | Passed. |
| Web CI | `323c65840c3698625fa2682bc04ffeb9793d5149` | [run 39378638940](https://github.com/lucifron28/UniPM/actions/runs/39378638940) | Passed. |
| Flutter CI | `a801c515e60870649aedc8219577d942ab422205` | [run 39379029022](https://github.com/lucifron28/UniPM/actions/runs/39379029022) | Passed. |

## Failures and corrections

Early Flutter workflow runs stopped at Dart formatting before tests ran. The
formatter reported layout changes in the cache helper and the new test
setup/assertion. These were committed as formatting-only follow-ups; the final
Flutter run above passed all steps.

The full development-dependency `npm audit --audit-level=high` reported 66
advisories (44 high and 22 moderate). The production-only audit reported none.
No Web dependency changes were made as part of these two focused corrections.
The dry-run update previews could not complete in this container: npm emitted a
local-cache `ENOENT` and a dependency-graph error. They did not modify the
lockfile.

## SQL Server verification

The staging guard's SQL Server 2019 integration scenarios did not execute. The
test connection was absent and no SQL Server 2019 image was available locally.
The integration fixture is constrained to local SQL Server 2019 and creates a
uniquely named disposable database; the updated fixture now covers a clean
empty schema, the expected seeded account counts, a table moved to the wrong
schema, and a missing table. Those database scenarios remain unverified until a
disposable SQL Server 2019 instance is available. Existing partial-state and
unexpected-record assertions remain in the fixture.

## Scope limitations

The local environment did not have Flutter installed; final Flutter validation
was hosted CI. No physical-device test ran. No migration, seed/reset, Azure
operation, deployment, merge, or production-data change was performed.
