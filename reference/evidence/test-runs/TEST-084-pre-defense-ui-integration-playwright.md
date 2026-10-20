---
id: TEST-084
type: test-run
title: Pre-defense UI integration Playwright verification
status: executed
recordedAtUtc: 2026-10-17T02:08:41Z
testedCommit: 961ff455ff8d4ed9c46d5cf2ccd1afc58f83048f
sourceBranch: feature/pre-defense-integration
evidenceLevel: locally-executed
---

# Pre-defense UI integration Playwright verification

## Objective

Verify the integrated desktop/mobile UI routes and capture the dashboard,
registries, form review, analytics drawer, and settled expanded-sidebar state
using the existing mocked Playwright harness.

## Execution Identity

- The full Playwright run executed at exact HEAD
  961ff455ff8d4ed9c46d5cf2ccd1afc58f83048f, after the local UI and analytics
  integration merges. It did not include the later capture-only test change.
- The follow-up app-shell run executed at that base plus the reviewed test patch
  in web/e2e/app-shell.spec.ts. The unchanged patch was then committed as
  6d7789c63a1941aef952688305e32fbce4c57dfe. It changed no production files.
- An earlier focused authentication, context-navigation, and app-shell run
  passed on base 0871625827cc9e1b0d3b142d9ee026929a696886 plus the reviewed
  working-tree patch later committed as b60f1a86da3ab0f9a3e2e31d9dbd4884a5edeec3.
  This earlier result predates the analytics integration and is supplemental,
  not an exact-head result for the integrated branch.

## Environment

- Local Windows environment, Chromium, and the Vite development server.
- Browser API responses came from deterministic Playwright route mocks and
  synthetic fixtures. The full and follow-up runs used isolated port 5198.
- No external provider, live API, staging service, database, or secret source
  was used by these browser checks.

## Commands

From the web directory, the earlier focused run was:

    npm run e2e -- e2e/authentication.spec.ts e2e/ux-context-navigation.spec.ts e2e/app-shell.spec.ts --workers=1 --reporter=line

The full integrated run used port 5198:

    npm run e2e -- --reporter=line

The follow-up capture test used the same port:

    npm run e2e -- --reporter=line e2e/app-shell.spec.ts

## Results

- Earlier focused run: 19 passed, 0 failed, 0 skipped in 53.8 seconds on its
  qualified base-plus-patch source identity.
- Full integrated run: 56 passed, 0 failed, 1 skipped out of 57 at exact HEAD
  961ff455ff8d4ed9c46d5cf2ccd1afc58f83048f.
- Follow-up app-shell run: 3 passed, 0 failed, 0 skipped on base 961ff455 plus
  the test-only capture patch, committed unchanged as 6d7789c.
- The follow-up test waits for dashboard data, returns the workspace to its
  top position, and polls until the expanded active-indicator bounds match the
  active link before capturing. The refreshed screenshot was visually reviewed
  and accepted at 1280 by 720 pixels.

## SQL Server Verification

Not applicable. These browser runs used mocked API responses and did not
connect to SQL Server.

## AI-Provider Verification

No provider was contacted. The Ask NLA capture records layout only; it does not
measure language interpretation or model quality.

## Generated Artifacts

- Earlier focused-run log and exit code:
  ../../../artifacts/pre-defense-integration/focused-playwright.log and
  ../../../artifacts/pre-defense-integration/focused-playwright.exit
- Full-run log:
  ../../../artifacts/pre-defense-integration/playwright-961ff455.log
- Follow-up log:
  ../../../artifacts/pre-defense-integration/app-shell-settled-961ff455.log
- The six full-suite screenshots are in
  ../../../artifacts/pre-defense-integration/screenshots/full-suite-961ff455/:
  dashboard 1280x720, assets 1280x900, schedules 1440x900, inspections
  1280x900, form review 1280x900, and Ask NLA drawer 1280x720.
- The refreshed expanded-sidebar screenshot is in
  ../../../artifacts/pre-defense-integration/screenshots/followup-app-shell-961ff455-plus-test-patch/
  at 1280x720.

## Failures And Corrections

The full-suite log contains a non-fatal Vite console error from TanStack
RouterCore.preloadRoute/loadRouteMatch reading _nonReactive. It appeared while
tests 37-38 were running and before the unknown-route test at 41. Source review
did not establish its cause; it is recorded as unconfirmed and was not treated
as an unknown-route failure. No production change was made for it.

The first expanded-sidebar capture showed the artificial scrolled workspace
and a transitioning indicator. The test-only follow-up loads the mocked
dashboard, resets scroll position, and waits for the indicator to align with
its active link. The targeted rerun passed and the refreshed capture was
accepted in visual review.

## Skipped Verification

- One existing Playwright case was skipped in the full run; its status is
  retained without reinterpretation.
- No backend or SQL suite, production build, exact-head CI, live API, staging,
  physical-device, or remote-browser verification was performed in this task.

## Limitations

These runs verify browser behavior with synthetic data and mocked API
responses. They do not establish live integration, deployment readiness,
accessibility completeness, or language quality. No human adjudication of NLA
output or provisional analytics labels was performed. Visual review noted that
tall filter panels can push records below the fold, the NLA drawer is narrow
and text-dense, and form-review filters show raw codes; these remain UX notes
for separate review.
