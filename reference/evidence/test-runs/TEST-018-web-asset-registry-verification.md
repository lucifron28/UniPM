---
id: TEST-018
type: test-run
title: Web asset registry verification
status: executed
recordedAtUtc: 2026-07-19T07:05:00Z
testedCommit: 34ff8561b1fcad7f933dfe520b9de67075333684
sourceBranch: feat/web-assets
evidenceLevel: locally-executed
---

# Web Asset Registry Verification

## Commands

- `dotnet restore .\UniPM.slnx`
- `dotnet build .\UniPM.slnx -c Release --no-restore`
- `dotnet test .\UniPM.slnx -c Release --no-build`
- `npm ci`, `npm run lint`, `npm run typecheck`, `npm run api:contract:check`, `npm run api:check`, `npm run test:run`, `npm run test:coverage`, `npm run build`, and `npm run e2e` from `web/`.

## Results

- Release build: passed with 0 warnings and 0 errors.
- Backend test suite: 282 passed, 0 failed, 24 skipped, 306 total. Skips were SQL Server and configured-provider dependent.
- Vitest: 10 files and 66 tests passed. Coverage: 91.23% statements, 79.03% branches, 89.09% functions, and 91.98% lines.
- Playwright: 12 Chromium tests passed using fictional intercepted sessions and asset records.
- Offline contract check and generated-client drift check passed; generation did not require the running backend. The committed lockfile now installs on the Node 22 CI toolchain.

## Negative And Visual Checks

The committed contract checker rejects required operation/schema omissions by construction; no temporary snapshot mutation was retained in this run. Figma nodes `2010:339`, `2359:8551`, and `4010:4` were source-inspected for restrained maroon/neutral layout alignment only. No pixel-perfect, branding, or production claim is made.

## Limitations

`npm run format:check` reported 35 pre-existing untouched formatting findings, so it is not claimed as passing. No SQL Server, real account, real institutional data, production deployment, final RBAC, server pagination, or server asset-search verification is claimed.
