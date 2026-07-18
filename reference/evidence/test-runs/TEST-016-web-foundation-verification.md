---
id: TEST-016
type: test-run
title: React web foundation verification
status: executed
recordedAtUtc: 2026-07-18T04:30:00Z
testedCommit: f805bd442bc86ec95f333b0d242e36e4de9c1012
sourceBranch: feat/web-foundation
evidenceLevel: locally-executed
---

# React Web Foundation Verification

## Objective

Verify the initial React/Vite browser foundation: typed OpenAPI client
generation, environment parsing, shared Axios transport, safe error handling,
in-memory access-token state, route guards, and build tooling. No operational
workflow screen, real login submission, refresh orchestration, or API contract
change is claimed by this record.

## Commands

```powershell
cd .\web
npm ci
npm run format:check
npm run lint
npm run typecheck
npm run test:coverage
npm run api:check
npm run build
git diff --check
```

## Results

- Formatting, ESLint, TypeScript, generated-client reproducibility, and the
  production Vite build passed.
- Vitest: 4 files, 20 tests passed, 0 failed.
- Playwright: 1 Chromium route-smoke test passed, 0 failed.
- V8 coverage: 95.83% statements, 97.77% branches, 88.23% functions, and
  95.45% lines across the currently executable transport/error modules.
- The committed OpenAPI snapshot generated the committed Orval output without
  a source diff.

## Verification Boundary

The route-smoke test covers public rendering, direct navigation to a protected
route, and the not-found boundary. The committed Web CI workflow also installs
Chromium before running it. This record does not claim production deployment,
real-auth flow, backend behavior, or accessibility conformance.

## Privacy And Security Boundary

No API key, connection string, token, cookie value, prompt, external-provider
payload, or real institutional record was used or recorded. The foundation
keeps access tokens in memory only; browser refresh/session behavior is deferred
to the next focused integration branch.
