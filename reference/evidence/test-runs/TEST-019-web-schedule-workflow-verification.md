---
id: TEST-019
type: test-run
title: Web schedule workflow verification
status: executed
recordedAtUtc: 2026-07-22T06:05:10Z
testedCommit: f6eadd288b5795154c099050a943154d38a8225e
sourceBranch: feat/web-schedules
evidenceLevel: locally-executed
---

# Web Schedule Workflow Verification

## Execution Identity

- Tested implementation: `f6eadd288b5795154c099050a943154d38a8225e`
- Starting main: `96785892f3c64356f7a45dc6263db33a328afb01`
- Environment: Windows, .NET SDK 10.0.300, Node 24.15.0, npm 11.12.1,
  Chromium through Playwright.
- The repository requires Node 22. Local Node 22 was unavailable; final Web CI
  remains the Node 22 verification gate.

## Commands

From the repository root:

- `dotnet restore .\UniPM.slnx`
- `dotnet build .\UniPM.slnx -c Release --no-restore`
- `dotnet test .\UniPM.slnx -c Release --no-build`
- `git diff --check`
- `git status --short`
- `git diff origin/main...HEAD --name-only`
- `git diff origin/main...HEAD --stat`

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
- `npm run e2e`

## Results

- Release restore/build: passed with 0 warnings and 0 errors.
- Backend tests: 284 passed, 0 failed, 24 skipped, 308 total.
- Clean web install: passed. npm reported the expected Node engine warning
  because the local runtime was Node 24, plus three high-severity dependency
  audit advisories; no uncontrolled dependency upgrade was made in this branch.
- Prettier, ESLint, TypeScript, and Vite production build: passed.
- Offline OpenAPI contract gate and generated-client drift check: passed.
- Negative OpenAPI checks: all five passed, including exactly two schedule
  mutations for missing list schema and missing `scheduleDate`.
- Vitest: 15 files and 103 tests passed, 0 failed.
- Coverage: 84.48% statements, 69.43% branches, 83.98% functions, and 84.97%
  lines. Coverage is supporting information, not the acceptance target.
- Playwright: 30 Chromium tests passed, 0 failed. The three schedule journeys
  cover URL-owned filtering and detail restoration, authorized creation with
  the exact payload boundary, and Admin-only denial with no POST.
- Final scope audit found no migration, schedule entity change, `.env`, token
  storage, update/delete/status command, recurrence engine, or date-derived
  status mutation.

## SQL Server And Provider Verification

SQL Server-specific tests were not configured and account for the database
skips. No real AI or embedding provider was configured or exercised. This web
branch does not change database schema or provider behavior.

## Visual Boundary

Figma baseline `4010:4` and schedule nodes `4010:195`, `4010:219`, and
`4010:225` were source-inspected for maroon/neutral hierarchy, spacing, and
table/card intent only. Prototype records, recurrence controls, automatic
planning, and unsupported workflow behavior were not copied.

## Limitations

This record does not claim Node 22 local execution, SQL Server execution,
production deployment, real institutional data, final RBAC, final scheduling
authority, recurrence behavior, automated overdue status, or production
readiness. Backend and Web CI results are recorded separately after push.
