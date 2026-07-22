---
id: TEST-018
type: test-run
title: Web asset registry verification
status: executed
recordedAtUtc: 2026-07-22T10:47:00Z
testedCommit: 3b00844ad4b4463c984c1ec2930ed037fffdb6b3
sourceBranch: feat/web-assets
evidenceLevel: locally-executed
---

# Web Asset Registry Verification

## Commands

- `dotnet restore .\UniPM.slnx`
- `dotnet build .\UniPM.slnx -c Release --no-restore`
- `dotnet test .\UniPM.slnx -c Release --no-build`
- `npm run format:check`, `npm run lint`, `npm run typecheck`, `npm run api:contract:check`, `npm run api:contract:test`, `npm run test:run`, `npm run build` from `web/`.

## Results

- Release build: passed with 0 warnings and 0 errors.
- Backend test suite: 282 passed, 0 failed, 24 skipped, 306 total.
- Prettier & ESLint: `npm run format:check` and `npm run lint` passed cleanly with 0 warnings and 0 errors.
- TypeScript: `npm run typecheck` (`tsc -b`) passed cleanly with 0 errors.
- Vitest: 13 files and 76 tests passed (100%).
- OpenAPI contract checks: `npm run api:contract:check` passed and `npm run api:contract:test` verified all 3 negative OpenAPI mutation cases cleanly.
- Playwright E2E: expanded specs covering mobile nav primary landmark (`<nav aria-label="Primary">`), create form validation, focus management to error alert summary, backend 400 error key mapping, invalid UUID route handling without API request, and label-specific copy toast feedback.

## Negative And Visual Checks

The repeatable negative contract test script (`web/scripts/test-negative-openapi-contract.mjs`) verifies 3 negative schema mutation cases against temporary copies without altering `web/openapi/unipm-v1.json`. Figma nodes `2010:339`, `2359:8551`, and `4010:4` were source-inspected for restrained maroon/neutral layout alignment only.

## Limitations

No SQL Server, real account, real institutional data, production deployment, final RBAC, server pagination, or server asset-search verification is claimed.
