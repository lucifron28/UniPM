---
id: TEST-106
type: test-run
title: Dashboard and PM analytics Playwright visual review
status: executed
recordedAtUtc: 2026-10-18T10:56:21Z
testedCommit: d474cb4725ffce031a7c903cf2201d7afe2427ab
testedFocusedSourceManifestSha256: 5e39d7cc2cee90a77edb7322149ec4e4e38789c8e61b2bcbd0f6eebb6934062c
sourceBranch: feature/staging-workflow-followups
evidenceLevel: locally-executed
---

# Dashboard and PM analytics Playwright visual review

## Execution identity

The focused browser run used the uncommitted working tree on branch
`feature/staging-workflow-followups`, based on HEAD
`d474cb4725ffce031a7c903cf2201d7afe2427ab`. It used Node `v22.23.3` and npm
`10.9.9`.

The source manifest hashes these files in the listed order. Each relative path
and file's bytes are separated by a zero byte:

- `web/playwright.config.ts`
- `web/e2e/pm-period-dashboard.spec.ts`
- `web/src/routes/app/dashboard.tsx`
- `web/src/features/reports/pm-period-dashboard.tsx`
- `web/src/features/reports/pm-analytics.tsx`
- `web/src/components/layout/app-shell.tsx`
- `web/src/components/layout/quick-navigation.tsx`
- `web/src/index.css`

The resulting SHA-256 is
`5e39d7cc2cee90a77edb7322149ec4e4e38789c8e61b2bcbd0f6eebb6934062c`.

## Command and result

From `web/`, the command was:

```text
npm run e2e -- --grep "shows a GSD-scoped PM analytics result with source navigation"
```

**Result:** exit code 0; 1 passed; duration 24.5 seconds.

The E2E test used a fictional GSD session and mocked API responses. It checked
the Dashboard and question modes, a valid mocked non-operational query, the
matching and calculation-source views, source details and navigation, and the
application-shell position while the workspace scrolled. No live API, SQL
database, Azure resource, or AI provider was contacted.

## Screenshot review

The test captured full viewport screenshots at 1440x900 for the Dashboard,
question entry, and calculation-source result. It also captured the question
view at 375x812. The images and SHA-256 manifest are preserved in ignored local
artifacts:

- `../../../artifacts/web-followup-visual-review-2026-10-18/playwright-dashboard/web-ui-polish-dashboard-desktop.png`
- `../../../artifacts/web-followup-visual-review-2026-10-18/playwright-dashboard/web-ui-polish-pm-analytics-desktop.png`
- `../../../artifacts/web-followup-visual-review-2026-10-18/playwright-dashboard/web-ui-polish-pm-analytics-result-desktop.png`
- `../../../artifacts/web-followup-visual-review-2026-10-18/playwright-dashboard/web-ui-polish-pm-analytics-mobile.png`
- `../../../artifacts/web-followup-visual-review-2026-10-18/playwright-dashboard/screenshot-manifest.json`

The Dashboard metric cards and report controls read clearly at desktop size.
The question examples sit alongside the input on desktop and stack below it on
mobile. The result screenshot intentionally shows the internally scrolled
calculation-source section, so the answer heading is outside that frame. The
mobile capture has a development TanStack devtools badge over its lower-right
corner; production visibility was not checked. The test's mobile horizontal
overflow assertion passed.

## Not verified

- Full Playwright suite, other route screenshots, Quick search's updated
  geometry and keyboard interaction, schedule/assignment browser handoffs,
  form review, deployed acceptance, and real API or database behavior.
- Production behavior of the TanStack devtools badge.
- No commit, push, deployment, or live data operation was part of this run.

The successful runner output was summarized in the local artifact
`playwright-result-summary.txt`; it is a normalized result note, not verbatim
runner stdout.
