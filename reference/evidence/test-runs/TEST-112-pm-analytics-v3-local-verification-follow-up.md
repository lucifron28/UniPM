---
id: TEST-112
type: test-run
title: PM analytics V3 local verification follow-up
status: executed
recordedAtUtc: 2026-10-19T11:50:58Z
testedCommit: 41b3aa956082f06b4d24f88f3309998e0dc9eb47
sourceBranch: feature/nla-v3-composable-reporting
evidenceLevel: locally-executed
---

# PM analytics V3 local verification follow-up

## Basis and limits

This record transcribes the user's local Codex verification report for the
exact commit shown below. The tests were run in an isolated snapshot because
the user's current checkout was on `feature/single-local-env` with existing
uncommitted changes. That checkout was preserved, the temporary snapshot was
removed, and no change was made to it by this task. Raw logs were not supplied
to this workspace, and these commands were not rerun here. The report did not
include an exact execution time or `dotnet --version`.

## Execution identity

- Tested commit: `41b3aa956082f06b4d24f88f3309998e0dc9eb47`.
- Source branch: `feature/nla-v3-composable-reporting`.
- Reported execution date: 2026-10-19; execution time not supplied.
- Web runtime: Node `v24.15.0`; the project requires Node 22.
- Web checks used the existing `node_modules` tree. The report states its
  package and lockfile matched the tested source. `npm ci --offline` could not
  find cached `zustand@5.0.14`.
- The existing V3 test configuration had DeepSeek disabled. No provider calls,
  Azure operations, deployments, or merges occurred.

## Commands and results

| Command | Reported result |
|---|---|
| `npm run format:check` | Passed. |
| `npm run lint` | Passed. |
| `npm run typecheck` | Passed. |
| `npm run api:contract:check` | Passed, including PM Analytics V3. |
| `npm run api:contract:test` | Passed: 7/7 negative contract checks. |
| `npm run test:run` | Passed: 246 tests across 31 files. |
| `npm run build` | Passed: 2,226 modules transformed. |
| `dotnet restore tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --ignore-failed-sources` | Passed with NU1902 and NU1903 warnings for `System.Security.Cryptography.Pkcs` 9.0.4. |
| `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~PmAnalyticsV3" --logger "console;verbosity=minimal"` | Passed: 10/10 tests. |

## Unverified checks

- Browser E2E did not run. `npx playwright install --list` failed because the
  expected Playwright browser registry directory was missing. No browser was
  downloaded. The E2E suite includes a local API write workflow, and no
  disposable API was verified.
- Native SQL Server 2019 integration tests were skipped because
  `UNIPM_SQLSERVER_TEST_CONNECTION` was not configured. No SQL connection was
  attempted.
- Hosted Web CI was not included in this report.
- No V3 corpus scoring result is claimed. The candidate corpus remains
  unreviewed and the repository has no V3 scoring harness.

## Environment limitations

The successful Web checks used Node 24.15.0 rather than the required Node 22.
The offline dependency installation could not resolve the cached `zustand`
package, so the existing `node_modules` tree was used. The restore warnings did
not fail the focused backend test run.
