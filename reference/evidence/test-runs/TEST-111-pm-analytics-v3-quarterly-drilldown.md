---
id: TEST-111
type: test-run
title: PM analytics V3 quarterly drill-down follow-up verification
status: executed
recordedAtUtc: 2026-10-19T10:40:29Z
testedCommit: b028938204932f0e49f285d5a0b90206dd58a32a
sourceBranch: feature/nla-v3-composable-reporting
evidenceLevel: locally-executed
---

# PM analytics V3 quarterly drill-down follow-up verification

## Objective

Verify the Q1/Q4 source-record drill-down correction, preserve the historical
TEST-110 outcomes, and run focused V3 backend and Web checks at the follow-up
commit. The run made no DeepSeek calls and did not use a database connection.

## Execution Identity

- Tested commit: `b028938204932f0e49f285d5a0b90206dd58a32a`.
- Branch: `feature/nla-v3-composable-reporting`.
- Date: 2026-10-19 UTC.
- Runtime: .NET SDK `10.0.203`, Node `v22.23.3`, npm `11.9.0`.
- Browser: system Chromium `154.0.8037.57`, exposed to Playwright through a
  temporary browser-path shim. No repository browser configuration changed.
- The V3 API tests ran with `ConnectionStrings__DefaultConnection` removed from
  that process so the test factory used its configured EF InMemory provider.
  The connection value was not read or recorded.
- `UNIPM_SQLSERVER_TEST_CONNECTION` and
  `UNIPM_SQLSERVER2019_TEST_CONNECTION` were absent. No SQL Server container
  was running.
- Raw logs and their SHA-256 list are under ignored path
  `artifacts/evidence/20261019T103804Z-b028938/`.

## Commands and Results

| Command | Result |
|---|---|
| `dotnet build server/server.csproj --configuration Release --no-restore --verbosity minimal` | Passed at .NET SDK 10.0.203: 0 errors, 5 warnings. |
| `env -u ConnectionStrings__DefaultConnection dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~PmAnalyticsV3 --logger 'console;verbosity=minimal'` | Passed: 10 tests, 0 failed, 0 skipped. Covers V3 regression tests, the GSD interpret/preview/execute workflow, and route authorization. |
| `npm run format:check` | Passed across `web/`. |
| `npm run lint` | Passed with zero warnings. |
| `npm run typecheck` | Passed. |
| `npm run test:run -- src/features/reports/pm-analytics-v3.test.tsx` | Passed: 1 file, 9 tests. The February/Q1 and November/Q4 cases check category selection, quarter period, base and measure filters, and the displayed source record. |
| `npm run api:contract:check` | Passed, including PM analytics V2/V3 contract sanity. |
| `npm run api:contract:test` | Passed all 7 negative OpenAPI contract cases. |
| `npm run api:check` | Passed OpenAPI generation and generated-client drift check. |
| `npm run build` | Passed: TypeScript check and Vite production build. |
| `npm run e2e -- e2e/pm-analytics-v3.spec.ts` | Passed: 1 narrow-viewport Playwright scenario using system Chromium. |

Web commands ran with Node 22.23.3. The E2E command set
`PLAYWRIGHT_BROWSERS_PATH` to the temporary browser-path shim; no browser was
downloaded. The full Web test suite, coverage job, and full Playwright suite
were not run; only the focused V3 React and browser scenarios were requested.

## Resolved Interim Failures

- The first backend build at `98223a17fbe900a3eed2071e6658c8466e34bad3` failed
  because a V3 group-key tuple lacked explicit element names. Commit
  `ff74657f70075235cae026cb28c3b73d82a3c85a` names the tuple elements; the .NET
  10 build then passed.
- An endpoint test first inherited the injected default database provider,
  conflicting with its InMemory test provider. With that environment variable
  unset for the isolated test process, the test exposed a catalog DTO
  `IReadOnlySet` deserialization issue. Commit
  `783f3d5080fdf0334927913bf36409da7639a37e` changed the test to assert the JSON
  wire shape directly. The final focused V3 backend run passed.
- Typecheck then found unchecked fixture array indexing. Commit
  `b028938204932f0e49f285d5a0b90206dd58a32a` added presence checks; final
  typecheck and React tests passed.
- The Playwright-managed Chromium executable was absent. The available system
  Chromium ran the scenario successfully through the temporary shim.

## SQL Server 2019 Verification

Native SQL Server 2019 integration tests were not run. Neither dedicated SQL
test connection variable was configured, and no SQL Server container was
running. SQL Server 2019 query translation and execution remain unverified.

## Build Warnings

The successful backend build reported five warnings: NU1902 and NU1903 for
`System.Security.Cryptography.Pkcs` 9.0.4, CS8620 in the V3 catalog filter
values query, CS8602 in preventive-maintenance form endpoints, and EF1002 in
staging demo initialization. They did not fail the build and were not changed
in this focused follow-up.

## AI Provider Verification

No DeepSeek request was made. Provider costs were not incurred; production
provider calls remain outside this verification.

## Generated Artifacts

Raw outputs are ignored by Git at
`artifacts/evidence/20261019T103804Z-b028938/`. `SHA256SUMS.txt` records hashes
for the final command logs.
