---
id: TEST-062
type: test-run
title: Pre-evaluation UX follow-up verification
status: executed
recordedAtUtc: 2026-10-09T20:35:25Z
testedCommit: a8541f7d0bbb602137b759c06d07f16b1cb4bae3
sourceBranch: feat/pre-evaluation-ux-motion
evidenceLevel: locally-executed
---

# Pre-evaluation UX follow-up verification

## Objective

Verify the visible primary-navigation focus ring and clarify how GSD should
read a deferred next cycle. Recheck the branch's backend and web suites without
changing PMIS business behavior.

## Execution identity

All commands below ran after the source commits at
a8541f7d0bbb602137b759c06d07f16b1cb4bae3 on feat/pre-evaluation-ux-motion.
Evidence files were added afterward. The visual capture used a temporary
untracked Playwright test, which was removed after capture. No tracked source
edits followed the tested commit.

Environment: Windows, .NET SDK 10.0.300, Node.js v24.15.0, npm 11.12.1. The web
package declares Node.js >=22 <23. Local checks passed under Node 24, and
exact-head CI remains the supported-runtime check.

## Commands and results

| Command | Result |
| --- | --- |
| npm run format:check | Passed. All matched web files use Prettier formatting. |
| npm run lint | Passed with zero ESLint warnings. |
| npm run build | Passed. TypeScript check passed and Vite transformed 2,207 modules. |
| npm run test:run | 27 files passed; 207 tests passed. |
| npm run e2e -- --project=chromium --workers=1 --reporter=line | 45 passed, 1 skipped. The skipped test is the live GSD/Supervisor/Inspector role-chain workflow. |
| npm run api:contract:check | Passed the OpenAPI authentication, asset, schedule, inspection, WMS, and form contract checks. |
| dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore | 338 passed, 0 failed, 37 skipped. |
| dotnet build server/server.csproj --configuration Release --no-restore | Passed with 0 errors and one existing CS8602 nullable warning at PreventiveMaintenanceFormEndpoints.cs:540. |
| dotnet ef migrations has-pending-model-changes --project server/server.csproj --no-build | Passed: no pending model changes. A process-only dummy connection setting constructed the model; the command did not connect to or modify a database. |
| git diff --check HEAD~2..HEAD | Passed for both source commits. |

The full Playwright run used the repository's local Vite server. Registry and
workflow tests used synthetic responses. The live role-chain test skipped
because UNIPM_DEV_USER_PASSWORD was not configured. SQL Server tests skipped
because UNIPM_SQLSERVER_TEST_CONNECTION was not configured. Of the 37 backend
skips, 36 require SQL Server and one is an optional provider smoke test. No
external AI provider was called.

## Visual evidence

The temporary screenshot test used a fictional GSD session and mocked API
responses. It captured the same active Assets navigation link before and after
the CSS fix. The screenshots are local ignored artifacts and are not committed.

- [Before screenshot](../../../artifacts/pre-evaluation-ux-motion/screenshots/primary-nav-focus-before-a8541f7.png)
- [After screenshot](../../../artifacts/pre-evaluation-ux-motion/screenshots/primary-nav-focus-after-a8541f7.png)

The after image shows the visible maroon outline. No real account or
institutional data appears in either image.

## Verification limits

- The live API/database-backed GSD, Supervisor, and Inspector workflow did not
  run. The one dedicated Playwright test skipped because its local development
  password environment variable was absent.
- SQL Server 2019 migration execution, application-lock concurrency, and
  database-backed tests remain unverified. The test connection was absent.
- Generated-client regeneration/drift was not rerun for this web-only change.
  The API contract check passed, and TEST-061 records the earlier generated
  client check on unchanged API source.
- A complete WCAG 2.2 AA audit and screen-reader review were not run. This
  follow-up verifies the keyboard-visible navigation ring and existing
  responsive browser checks.
- Flutter and physical-device checks were not run because no mobile source
  changed.
- No GSD acceptance session, deployment, or 60 FPS measurement was performed.
- Local Node.js 24 differs from the package's declared Node.js 22 range.
  Exact-head CI will verify the repository's configured CI runtime.

The initial broad Vitest scan also found a temporary ignored browser-audit spec
under web/artifacts. That generated scratch file was removed, after which the
full suite passed. The related temporary audit config was removed after the
format check identified it. Screenshot artifacts were retained locally.

## CI and review state

Backend and Web CI are reported separately against the exact pushed head. The
existing PR remains a draft and unmerged.
