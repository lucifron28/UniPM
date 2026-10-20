---
id: TEST-109
type: test-run
title: Keep the Assets action visible at compact desktop widths
status: executed
recordedAtUtc: 2026-10-18T14:37:38Z
testedCommit: d474cb4725ffce031a7c903cf2201d7afe2427ab
testedFocusedSourceManifestSha256: c266cd474548185b46049b9e1db7d5547930d0ab0605737d7d72f997ccbeccfb
sourceBranch: feature/staging-workflow-followups
evidenceLevel: locally-executed
---

# Assets action column at compact desktop width

## Execution identity

The test ran on the uncommitted working tree on branch
`feature/staging-workflow-followups`, based on HEAD
`d474cb4725ffce031a7c903cf2201d7afe2427ab`. The focused source manifest covers
`web/e2e/assets.spec.ts` and `web/src/features/assets/asset-registry.tsx`,
ordered by repository-relative path. For each file, SHA-256 receives its UTF-8
relative path, a zero byte, file bytes, and a zero byte. The aggregate digest is
`c266cd474548185b46049b9e1db7d5547930d0ab0605737d7d72f997ccbeccfb`.

The environment used Node `v24.15.0` and npm `11.12.1`; the web package declares
Node `>=22 <23`.

## Change verified

The Assets registry keeps the **View details** action pinned to the right edge
of its desktop table. At widths below 1440px the lower-priority **Updated**
column is hidden so the pinned action does not cover the adjacent Status value.
The focused browser test checks that the action cell remains within the visible
table region at 1280px and that Updated is hidden there.

The reviewed 1280x900 screenshot is stored under ignored local artifacts at
`artifacts/web-qa/20261018T-final-followup/supplemental/asset-action-sticky-1280x900.png`.
Its SHA-256 is
`1066a4e1f6ab20504d6f7645b3fa2c5e2a6f349af1c7be03b9b354c410eefeb8`.

## Commands and results

From `web/`:

| Command | Result |
|---|---|
| `$env:CI='true'; $env:UNIPM_E2E_PORT='5209'; npm run e2e -- --grep "keeps asset inventory sections ordered and zoom contained"` | Passed 1/1; 1280px action visibility assertion and refreshed screenshot completed. |
| `$env:CI='true'; $env:UNIPM_E2E_PORT='5209'; npm run e2e -- --reporter=line` | Passed 57; 1 opt-in live WMS test skipped (58 total). |
| `npm run format:check` | Passed. |
| `npm run lint` | Passed with zero warnings. |
| `npm run typecheck` | Passed. |
| `npm run build` | Passed; Vite transformed 2,224 modules. |

Playwright used the temporary Vite server on port 5209 and needed local loopback
access. An initial default-port run stopped before tests because port 5173 was
occupied; the isolated run passed after the browser received loopback access.

The full 231-test web unit run is recorded in TEST-108 and preceded this
CSS-only layout adjustment; unit tests were not rerun for this follow-up.

## Not verified

- Execution on the supported Node 22 runtime and exact-head GitHub CI.
- Full backend verification, live API/database behavior, staging seed data,
  deployed authorization, or GSD acceptance.
- QR print layout for an actual label-stock size.

No Azure resource or database was accessed or changed. No deployment, commit,
push, or merge was performed for this test run.
