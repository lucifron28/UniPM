---
id: TEST-018
type: test-run
title: Web asset registry verification
status: executed
recordedAtUtc: 2026-07-22T04:40:24Z
testedCommit: 39be84c8405e0afe55300040eb011e9c94c90ad3
sourceBranch: feat/web-assets
evidenceLevel: locally-executed
---

# Web Asset Registry Verification

## Commands

- `dotnet restore .\UniPM.slnx`
- `dotnet build .\UniPM.slnx -c Release --no-restore`
- `dotnet test .\UniPM.slnx -c Release --no-build`
- `npm ci` on Node 22 with npm 10 from `web/`.
- `npm run format:check`, `npm run lint`, `npm run typecheck`,
  `npm run api:contract:check`, `npm run api:check`, `npm run test:run`,
  `npm run test:coverage`, `npm run build`, and `npm run e2e` from `web/`.

## Results

- Release build: passed with 0 warnings and 0 errors.
- Backend test suite: 282 passed, 0 failed, 24 skipped, 306 total.
- Clean web install: `npm ci` passed with Node 22/npm 10. The lockfile retains
  TypeScript 6 and records the resolver-required optional TypeScript 5.9.3
  peer under `vite-tsconfig-paths`.
- Prettier & ESLint: `npm run format:check` and `npm run lint` passed cleanly with 0 warnings and 0 errors.
- TypeScript: `npm run typecheck` (`tsc -b`) passed cleanly with 0 errors.
- Vitest: 13 files and 95 tests passed (100%).
- Coverage: 89.47% statements, 80.22% branches, 88.42% functions, and
  89.93% lines.
- OpenAPI contract and generated-client drift checks: `npm run api:contract:check`
  and `npm run api:check` passed.
- Playwright E2E: 27 Chromium tests passed (100%), including browser-backed
  asset text/page URL restoration, out-of-range page replacement, direct detail
  restoration and 404 handling, GSD creation boundaries, field-error recovery,
  malformed success handling, category-reference failure, copy failure, and
  browser-storage absence.

## Negative And Visual Checks

The repeatable negative contract test script (`web/scripts/test-negative-openapi-contract.mjs`) remains part of Web CI. Figma nodes `2010:339`, `2359:8551`, and `4010:4` were source-inspected for restrained maroon/neutral layout alignment only.

## Limitations

No SQL Server, real account, real institutional data, production deployment,
final RBAC, server pagination, or server asset-search verification is claimed.
