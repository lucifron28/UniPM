---
id: TEST-017
type: test-run
title: React browser authentication verification
status: executed
recordedAtUtc: 2026-07-19T02:17:28Z
testedCommit: c3bb6baefad62dacbe655102534b0aca38d2269c
sourceBranch: feat/web-auth-integration
evidenceLevel: locally-executed
---

# React Browser Authentication Verification

## Objective

Verify the browser login, refresh-cookie restoration, current-user state,
single-flight refresh, bounded request replay, logout, protected routes, and
generation-bound request retry, refresh, and cookie-writer race controls at
implementation commit `c3bb6baefad62dacbe655102534b0aca38d2269c`.

## Environment

- Local runtime: Node `v24.15.0`, npm `11.12.1` on Windows.
- The project declares Node 22; local npm 11 reported the expected engine
  warning, then completed the clean install.
- Browser: Playwright Chromium.
- No backend, SQL Server, Docker, credential, or external network dependency
  was used by Vitest, API generation, the production web build, or Playwright.

## Commands

```powershell
cd .\web
npm ci
npm run format:check
npm run lint
npm run typecheck
npm run api:contract:check
npm run api:check
npm run test:run
npm run test:coverage
npm run build
npx playwright install chromium
npm run e2e

cd ..
dotnet restore .\UniPM.slnx
dotnet build .\UniPM.slnx -c Release --no-restore
dotnet test .\UniPM.slnx -c Release --no-build
git diff --check
git status --short
git diff --name-only
```

## Results

- npm 11 clean install, Prettier, ESLint, TypeScript, and the offline OpenAPI
  authentication contract gate passed. The local Node 24 engine warning remains
  expected because the project and CI target Node 22.
- `api:check` regenerated the client from the committed snapshot and found no
  tracked, deleted, or untracked generated-client drift.
- Vitest: 9 files passed; 62 tests passed; 0 failed.
- V8 coverage: 91.45% statements, 78.85% branches, 90.38% functions, and
  92.28% lines.
- Production Vite build: passed; 2,144 modules transformed.
- Playwright: 11 Chromium tests passed; 0 failed.
- Backend Release build: passed with 0 warnings and 0 errors.
- Backend Release tests: 280 passed, 0 failed, 24 skipped, 304 total.
- The worktree and implementation diff checks were clean after verification.

Unit and integration coverage includes bootstrap single-flight behavior,
refresh single-flight behavior for concurrent 401 responses in one generation,
one bounded replay, preserved request configuration, aborted-waiter handling,
refresh-flight cleanup, malformed login/refresh/current-user responses, safe
login errors, complete Query clearing, late refresh after logout, stale
bootstrap after login, stale failure after a newer authenticated session, and
generation-crossing cookie-mutation ordering. Browser coverage includes
keyboard login, validation, generic failure, real fictional identity rendering,
direct protected restoration, anonymous redirect, logout, storage inspection,
unsafe redirect rejection, and the not-found boundary.

Three focused regressions cover the remaining request-generation boundary: an
old Session A 401 cannot start a refresh after Session B becomes current; a
generation change while refresh is pending prevents replay; and a stale
expected generation cannot dispatch the generated refresh operation. The old
request retains its 401 and never receives Session B's token.

The browser race regression starts a delayed Session A refresh, initiates logout
and Session B login, then releases the stale response. It verifies the actual
HttpOnly cookie remains Session B's cookie, verifies Session B remains locally
authenticated, and proves a later Session B 401 starts a new refresh rather
than reusing Session A's flight.

## Verification Boundary

The 24 skipped backend tests require optional SQL Server or real-provider
configuration; this frontend-only branch did not configure either. No SQL
Server integration, real authentication account, real credential, IIS
deployment, mobile browser, second browser engine, real institutional record,
or production deployment was exercised.

The responsive mobile, tablet, and desktop screenshots retained only under
ignored `artifacts/web-auth-visual/` were inspected against the approved Figma
references. They support a source-inspected alignment statement only. This
record does not claim pixel-perfect Figma fidelity, final accessibility or
responsive certification, production branding approval, final legal copy,
complete CSRF protection, final RBAC correctness, MFA, SSO, or production
authentication readiness.

No real password, access token, refresh cookie, JWT secret, connection string,
provider credential, or raw sensitive response was used or recorded. Test
identities and tokens were explicitly fictional placeholders, and browser
storage remained empty after login.
