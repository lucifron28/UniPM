---
id: TEST-104
type: test-run
title: Dashboard Playwright attempt blocked before test start
status: executed
recordedAtUtc: 2026-10-18T10:47:09Z
testedCommit: d474cb4725ffce031a7c903cf2201d7afe2427ab
testedFocusedSourceManifestSha256: 8263bbe679867b18e335c38b168491068319858f772e580d8e364ff521171544
sourceBranch: feature/staging-workflow-followups
evidenceLevel: locally-executed
---

# Dashboard Playwright attempt blocked before test start

## Execution identity

This attempt ran on branch `feature/staging-workflow-followups`, based on HEAD
`d474cb4725ffce031a7c903cf2201d7afe2427ab`, with uncommitted changes. The
focused source manifest covers the Playwright configuration, the target E2E
spec, the Dashboard route, the analytics component, and the application shell.
It hashes each relative path and file bytes, separated by zero bytes, yielding
`8263bbe679867b18e335c38b168491068319858f772e580d8e364ff521171544`.

The run used Node `v22.23.3` and npm `10.9.9`.

## Attempt and result

| Command or check | Result |
| --- | --- |
| `npm run e2e -- --grep 'shows a GSD-scoped PM analytics result with source navigation'` (from `web/`) | The Playwright runner emitted only a color-environment warning and did not report test discovery or assertions after more than 90 seconds. The session was stopped with Ctrl+C; tool exit code 1 reflects interruption, not an assertion failure. No test count or screenshot was produced. |
| `npm run dev -- --port 5173 --strictPort` (from `web/`) | Vite 8.1.3 reported ready in 3.161 seconds at `http://localhost:5173/`. |
| PowerShell `Invoke-WebRequest http://localhost:5173/` | Failed before an HTTP response with `HttpRequestException`: socket access was denied by workspace permissions. |
| Process cleanup | The Playwright runner and Vite server were stopped. No local server remains running. |

Codex's browser surface also reports that local browser use is disabled in Work
settings. Raw, non-secret command output is preserved in ignored
`artifacts/web-followup-visual-review-2026-10-18/playwright-dashboard-nla-node22-attempt.log`.

## Not verified

- The target E2E test, browser rendering, updated screenshots, live API,
  deployed acceptance, full Playwright suite, and GitHub CI.
- No staging account, Azure endpoint, database, or provider was used. No
  deployed application or live data was changed. The local Vite server was
  started for diagnosis and stopped afterward.
- No commit, push, or deployment occurred.
