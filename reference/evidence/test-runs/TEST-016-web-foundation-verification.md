---
id: TEST-016
type: test-run
title: React web foundation verification
status: executed
recordedAtUtc: 2026-07-18T10:05:00Z
testedCommit: 0b9a35d4415269e2e5c6730e711fbb23f00883cd
sourceBranch: feat/web-foundation
evidenceLevel: locally-executed
---

# React Web Foundation Verification

## Objective

Verify the React/Vite browser foundation correction: current authentication
OpenAPI generation, environment parsing, shared Axios transport, safe error
handling, in-memory access-token state, route guards, redirect safety, and
build tooling. No operational workflow screen, real login submission, or
refresh orchestration is claimed by this record.

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
npx playwright install chromium
npm run e2e
git diff --check
```

## Results

- Formatting, ESLint, TypeScript, generated-client reproducibility, and the
  production Vite build passed.
- Vitest: 6 files, 31 tests passed, 0 failed.
- Playwright: 5 Chromium route-smoke tests passed, 0 failed.
- V8 coverage: 96.42% statements, 96.55% branches, 90% functions, and 96.07%
  lines across the currently executable transport/error modules.
- The current Development API snapshot passed the required login, refresh,
  logout, and current-user contract sanity check; login/refresh generate typed
  session responses and current-user generates a typed user response.
- The committed OpenAPI snapshot generated the committed Orval output without
  tracked, deleted, or untracked generated-client drift.

## Verification Boundary

The route-smoke suite covers public rendering, direct login rendering,
unauthenticated protected navigation, the not-found boundary, and keyboard
focus progression between real navigation links. Unit coverage additionally
exercises authenticated access and access reevaluation after in-memory session
clearing. Web CI run `29640073441` executed its pinned Node 22 `npm ci`,
generated-client check, Chromium installation, and the same browser suite
successfully. This record does not claim production deployment, real-auth flow,
backend behavior, or accessibility conformance.

## Privacy And Security Boundary

No API key, connection string, token, cookie value, prompt, external-provider
payload, or real institutional record was used or recorded. The foundation
keeps access tokens in memory only; browser refresh/session behavior is deferred
to the next focused integration branch.
