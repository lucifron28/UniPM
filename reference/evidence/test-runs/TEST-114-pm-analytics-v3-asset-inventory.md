---
id: TEST-114
type: test-run
title: PM analytics V3 asset inventory verification
status: executed
recordedAtUtc: 2026-10-19T17:45:45Z
testedCommit: fcd6e6b7b1ab71bdc64e3c4520fcb558b7551920
sourceBranch: feature/nla-v3-asset-inventory
evidenceLevel: locally-executed
---

# PM analytics V3 asset inventory verification

## Execution identity and boundaries

- Tested source commit: `fcd6e6b7b1ab71bdc64e3c4520fcb558b7551920`.
- Base: Phase 5 commit `4bd3ac592c9be260b8157ced6ebd9c77b7069254`.
- Branch: `feature/nla-v3-asset-inventory`.
- Runtime: .NET SDK `10.0.203`, Node `v22.23.3`, npm `10.9.9`, system
  Chromium `154.0.8037.57`.
- Web checks used the existing `web/node_modules`; no clean dependency install
  was run during this milestone.
- Backend API regression fixtures use EF InMemory. No SQL connection was
  attempted. `UNIPM_SQLSERVER_TEST_CONNECTION` was not configured and no
  SQL Server image was present locally.
- No provider request, held-out dataset, Azure/staging operation, deployment,
  merge, migration, production-data change, or database reset occurred.

## Backend and evaluation checks

| Command/check | Result |
|---|---|
| `dotnet build UniPM.slnx --no-restore` | Passed on .NET 10: 0 errors, 13 warnings. Warnings include existing `System.Security.Cryptography.Pkcs` 9.0.4 NU1902/NU1903 advisories, CS8620 in the filter-value projection, CS8602 in the PM form endpoints, and EF1002 in the staging demo initializer. |
| `dotnet build UniPM.slnx --configuration Release --no-restore` | Passed: 0 errors, 13 warnings with the same package/security and existing analyzer warnings. |
| `dotnet test UniPM.slnx --configuration Release --no-build --no-restore --logger "console;verbosity=minimal"` | Passed: 553, failed 0, skipped 49, total 602. The final run cleared injected DB, JWT/web, SQL, embedding-provider, and DeepSeek environment settings for this process; the skipped cases include environment-dependent SQL Server tests. |
| `dotnet format whitespace UniPM.slnx --verify-no-changes --include server/Features/Reports/PmAnalyticsEndpoints.cs server/Features/Reports/PmAnalyticsV3Catalog.cs server/Features/Reports/PmAnalyticsV3Contracts.cs server/Features/Reports/PmAnalyticsV3PlanValidator.cs server/Features/Reports/PmAnalyticsV3QueryService.cs tests/UniPM.Api.Tests/Reports/PmAnalyticsV3EndpointTests.cs` | Passed for the changed V3 C# files. Workspace loading emitted a warning. |
| `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~PmAnalyticsV3 --logger "console;verbosity=minimal"` | Passed 23, failed 0, skipped 1, total 24. The SQL Server 2019 test was skipped because its connection variable was absent. |
| `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-build --no-restore --filter FullyQualifiedName~PmAnalyticsV2 --logger "console;verbosity=minimal"` | Passed 44, failed 0, skipped 1, total 45. The SQL Server test was skipped. |
| `dotnet run --project tools/UniPM.PmAnalytics.V3Evaluation --no-build -- validate` | Passed structural validation for 18 cases; 18 labels remain pending; no score was produced. |

The inventory API regressions exercise catalog and authorization, signed
preview and full execution, one-asset counting despite many joined-source
records, all three registration statuses, default categories, current
department/building/location/status filters, grouping, stable record paging,
null PM identifiers, and rejection of PM periods, inspection matching filters,
and unrecognized status values. Existing shared V3 token regressions cover
expiry, tampering, and user binding; no inventory-specific cross-user preview
test was added.

The initial full-suite diagnostic inherited the cloud environment's
`ConnectionStrings__DefaultConnection` and failed before establishing a
database connection because test hosts registered SQL Server and InMemory
providers together. A second isolated run exposed two startup-error assertions
affected by injected JWT/web settings. The final run cleared those
configuration names and all provider/SQL test bindings for this process; it
passed. No connection strings or secret values were read or printed, and no
provider call or database connection was made.

## Web and browser checks

All commands ran from `web/` with Node `v22.23.3`.

| Command/check | Result |
|---|---|
| `npm run format:check` | Passed. |
| `npm run lint` | Passed with zero lint warnings. |
| `npm run typecheck` | Passed. |
| `npm run api:contract:check` | Passed, including PM Analytics V2/V3. |
| `npm run api:contract:test` | Passed: 7/7 negative contract checks. |
| `npm run api:check` | Passed after commit: OpenAPI client generation and generated-client drift check. |
| `npm run test:run -- src/features/reports/pm-analytics-v3.test.tsx` | Passed: 10/10 focused React tests. |
| `npm run test:run` | Passed: 247 tests across 31 files. The test environment logged unsupported `window.scrollTo()` notices; no test failed. |
| `npm run build` | Passed TypeScript and production build; 2,226 modules transformed. |
| `npx playwright test e2e/pm-analytics-v3.spec.ts --config=playwright.system.config.mjs` | Passed: 2/2 V3 browser scenarios, including PM source-record review and asset inventory at 390×844. A temporary config selected `/usr/bin/chromium`; it was removed after the run. |

The full Playwright suite and accessibility audit were not run. The focused
browser run used mocked API responses and did not exercise a live server.

## SQL Server and remote CI

Native SQL Server 2019 translation and execution remain unverified. The
SQL-specific tests were skipped; there was no configured test connection or
preloaded SQL Server image. Do not substitute the EF InMemory results for a
native compatibility claim. No Azure staging database was read or changed.

The branch was pushed after this record was drafted; remote branch identity and
hosted CI status are recorded in the task completion report. This record makes
no hosted CI pass claim.

## Evaluation and acceptance limits

All 18 candidate labels in `NLA-V3-EVAL-PROTOCOL-001` remain pending
independent human review. The validator ran only in `validate` mode. No
diagnostic or scoring run was performed for this milestone, and no V3 accuracy
score, frozen-corpus result, held-out result, or GSD acceptance claim is made.
See [`review-notes.md`](../../evaluation/pm-analytics-v3/review-notes.md) for
the per-case decisions and missing coverage that need reviewer input.
