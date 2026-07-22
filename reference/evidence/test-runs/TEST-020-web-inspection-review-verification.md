---
id: TEST-020
type: test-run
title: Web inspection review verification
status: executed
recordedAtUtc: 2026-07-22T15:02:35Z
testedCommit: b78c407a0d99a592779b6b66a64aad7712cecd4d
sourceBranch: feat/web-inspections
evidenceLevel: locally-executed
---

# Web Inspection Review Verification

## Execution Identity

- Tested implementation: `b78c407a0d99a592779b6b66a64aad7712cecd4d`
- Starting main: `76876560fd9977e4d6e4f0dad7df67484b4e1471`
- Environment: Windows, .NET SDK 10.0.300, Node 24.15.0, npm 11.12.1, and
  Chromium through Playwright.
- The repository requires Node 22. Local Node 22 was unavailable, so final Web
  CI remains the Node 22 verification gate.

## Commands

From the repository root:

- `dotnet restore .\UniPM.slnx`
- `dotnet build .\UniPM.slnx -c Release --no-restore`
- `dotnet test .\UniPM.slnx -c Release --no-build`
- `git diff --check`
- `git status --short`

From `web/`:

- `npm ci`
- `npm run format:check`
- `npm run lint`
- `npm run typecheck`
- `npm run api:contract:check`
- `npm run api:contract:test`
- `npm run api:check`
- `npm run test:run`
- `npm run test:coverage`
- `npm run build`
- `npx playwright install chromium`
- `$env:CI='true'; npm run e2e`

## Results

- Release restore/build: passed with 0 warnings and 0 errors.
- Backend tests: 285 passed, 0 failed, 24 skipped, 309 total.
- Clean web install: passed. npm reported the expected Node engine warning
  under local Node 24 and three high-severity dependency audit advisories; no
  dependency change was made in this branch.
- ESLint, TypeScript, production build, the offline OpenAPI contract gate, and
  generated-client drift check: passed. The local all-files Prettier check
  reported formatting drift in 12 unchanged schedule files under Node 24;
  inspection files and generated artifacts were formatted, and Node 22 Web CI
  remains the required formatting gate.
- Negative OpenAPI checks: all seven passed, including the two inspection
  mutations for a missing list response schema and missing
  `InspectionResponse.scheduleId`.
- Vitest: 17 files and 111 tests passed, 0 failed.
- Coverage: 83.45% statements, 67.16% branches, 81.38% functions, and 83.89%
  lines. Coverage is supporting information, not the acceptance target.
- Playwright: 32 Chromium tests passed, 0 failed. The two inspection journeys
  cover URL-owned filtering with immutable source detail and asset-history
  navigation to a source record.
- Focused inspection tests cover strict response parsing, supported filter
  transport, safe source-text rendering, linked asset/schedule context, and
  unavailable asset history.
- Final scope audit found no migration, entity change, inspection POST usage in
  web feature code, inspection creation route, acknowledgement, RMRF,
  corrective-handoff, AI call, token storage, or real institutional record.

## SQL Server And Provider Verification

`UNIPM_SQLSERVER_TEST_CONNECTION` was not configured, accounting for the SQL
Server test skips. No embedding or summary provider was configured or
exercised. This branch does not change database schema, retrieval behavior, or
provider behavior.

## Limitations

This record does not claim local Node 22 execution, SQL Server execution,
production deployment, final RBAC, mobile inspection submission, final GSD
workflow approval, real institutional data, or production readiness. Backend
and Web CI results are recorded separately after push.
