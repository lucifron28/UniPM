---
id: TEST-086
type: test-run
title: Web UI remediation full verification
status: executed
recordedAtUtc: 2026-10-17T13:44:57Z
testedCommit: 61dfdacc34eef7d09310f96e1902f5038c64b094
sourceBranch: feature/web-analytics-layout
evidenceLevel: locally-executed
---

# Web UI remediation full verification

## Objective

Verify the assembled Web UI remediation at its exact source commit, including
the shell, authentication, PM Analytics, registries, forms, generated client,
and browser workflows.

## Execution identity

- Branch: `feature/web-analytics-layout`.
- Tested commit: `61dfdacc34eef7d09310f96e1902f5038c64b094`.
- The full local checks in this record ran at this commit. The working tree was
  clean before verification; generated-client checking left it clean.
- The changes from the preceding Web baseline are in `web/` plus the earlier
  focused evidence record. No backend, database, API contract, or AI-provider
  source changes are included in the tested commit range.

## Environment

- Windows PowerShell, Node.js 22.23.3, and the Web project's installed
  dependencies.
- Playwright used its existing cached Chromium browser build 1228 with two
  workers. The Vite development server listened on port 5185 and returned HTTP
  200 before the full browser run started.
- Vitest and Playwright used local test fixtures and mocked API responses. No
  live backend, database, external provider, or institutional data was used.

## Commands and results

Commands ran from `web/`:

| Command | Result |
|---|---|
| `npm.cmd run test:run -- src/components/layout/app-shell-access.test.tsx` | 1 file, 16 tests passed. |
| `npm.cmd run test:run` | 29 files, 226 tests passed. |
| `npm.cmd run format:check` | Passed; all matched files use Prettier style. |
| `npm.cmd run lint` | Passed with zero warnings. |
| `npm.cmd run typecheck` | `tsc -b` passed. |
| `npm.cmd run api:contract:check` | OpenAPI contract sanity check passed. |
| `npm.cmd run api:contract:test` | All 7 negative contract checks passed. |
| `npm.cmd run api:check` | Orval generation and generated-client drift check passed; no generated diff remained. |
| `npm.cmd run build` | TypeScript and Vite production build passed. |
| `npm.cmd run e2e -- --workers=2` | 56 passed, 1 skipped, 0 failed (57 total). |

`git diff --cached --check` passed for the staged evidence record and index
update.

The skipped Playwright case is the opt-in live WMS referral workflow in
`schedule-batch-assignment.live.spec.ts`. It was not run against a live service.

## Initial browser startup stall

The first sandboxed Playwright attempt stalled before any test case started.
Its Vite listener could not be reached from that execution context; the run was
stopped and is not counted as a test result. The corrected run used an
authorized elevated execution context, started the owned Vite server there,
confirmed an HTTP 200 readiness response on port 5185, and then completed all
57 collected cases. The initial and corrected logs are retained separately.

## Earlier CI failures and their causes

The retained CI run 39032789862 predates the tested commit and reported five
browser failures. Two schedule-width assertions exposed a real responsive
layout issue: the PageHeading action group kept its intrinsic width. Local
diagnostics showed a document width of 369px at a 320px viewport, a 49px
overflow. CI reported 43px overflow at 375px; the focused local run did not
independently reproduce that CI-width failure. The
responsive heading correction was checked across 320px, 375px, 390px, and
768px, with 12 geometry states and 21 schedule unit tests passing on the
base-plus-patch snapshot. Those targeted results are retained in
`artifacts/web-qa/20261017T132631Z-schedule-mobile-dom-after-fix/`; they are not
identified as execution at `testedCommit`.

The other three CI failures were stale test expectations: app-shell overflow
was asserted as `hidden` although the tested scroll boundary uses `clip`; an
authentication test expected the sidebar preference to be stored as collapsed
by default, although the default is expanded; and a keyboard focus test
depended on old deferred-summary copy. The updated assertions check scroll
geometry, the explicit collapse preference and token-storage boundary, and
keyboard focus without relying on that summary text. These were test
corrections; the authentication implementation was unchanged.

## Browser captures

The full run wrote its Playwright screenshots under the ignored
`web/test-results/` directory. Captures include the desktop and mobile shell,
PM dashboard and analytics, asset registry, inspection review, PM form review,
schedule management, and QR print view. Navigation-indicator screenshots may
show the animation in progress; this record does not treat them as settled
visual-review evidence. No login/logout screenshot, desktop-collapsed-sidebar
capture, or dedicated photo-preview screenshot was emitted by this run.

## Raw artifacts

Ignored command output is retained under
`artifacts/web-qa/layout-focused/remediation-d5a7c253/`:

- `shell-access-react-final.log`
- `vitest-full.log`
- `format-check.log`, `lint.log`, `typecheck.log`, and `build.log`
- `api-contract-check.log`, `api-contract-negative-tests.log`, and
  `generated-client-check.log`
- `playwright-full-bundled-chromium-retry.log` and the corresponding Vite
  preflight logs
- `playwright-full-bundled-chromium.log` and
  `playwright-readiness-diagnostic.log` for the initial zero-test startup stall
- `ci-39032789862-failed.log`, retained as historical output from a CI run
  before this tested commit; it is not evidence for this SHA
- `artifacts/web-qa/20261017T132631Z-schedule-mobile-dom-after-fix/run.log` and
  `schedule-workflow-vitest.log`, with the four-width geometry diagnostics and
  21 passing schedule unit tests on the earlier base-plus-patch snapshot
- `artifacts/web-qa/20261017T132631Z-schedule-mobile-dom-after-fix/playwright-results/`,
  with the four-width schedule management and pagination captures from that
  focused run

## Verification boundaries

- This record covers local Web checks at the tested commit. GitHub CI for this
  exact commit, deployment, production use, and GSD acceptance were not
  verified here.
- Backend, native SQL Server, mobile, live API integration, live WMS referral,
  and real-provider checks were not run.
- The browser tests verify the mocked UI contracts and interactions; they do
  not establish operational acceptance or live-service behavior.
