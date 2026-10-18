---
id: TEST-108
type: test-run
title: Staging follow-up full web QA and screenshot review
status: executed
recordedAtUtc: 2026-10-18T14:25:20Z
testedCommit: d474cb4725ffce031a7c903cf2201d7afe2427ab
testedWebSourceManifestSha256: 4339a6e166940ea8ed0f469dd11ff0740c0cba7cb14f9557a7616f4e5fc05c6c
sourceBranch: feature/staging-workflow-followups
evidenceLevel: locally-executed
---

# Staging follow-up full web QA and screenshot review

## Execution identity

Verification ran against the uncommitted working tree on branch
`feature/staging-workflow-followups`, based on HEAD
`d474cb4725ffce031a7c903cf2201d7afe2427ab`. The web source manifest covers 36
changed or added Web files, including the final Quick search keyboard fix and
its Playwright test. The manifest and digest are preserved under ignored
`artifacts/web-qa/20261018T-final-followup/`.

The environment used Node `v24.15.0` and npm `11.12.1`. The web package
declares Node `>=22 <23`; these final local results therefore do not establish
execution on a supported Node 22 runtime.

## Commands and results

From `web/`:

| Command | Result |
|---|---|
| `npm run format:check` | Passed; Prettier reported all files formatted. |
| `npm run lint` | Passed with `--max-warnings 0`. |
| `npm run typecheck` | Passed. |
| `npm run test:run` | Passed: 231 tests across 29 files. Vitest emitted existing jsdom `window.scrollTo()` not-implemented warnings. |
| `npm run e2e -- --reporter=line` | Passed: 57 passed, 1 skipped, 58 total; 37.5 seconds. |
| `npm run build` | Passed; typecheck and Vite production build completed, 2,224 modules transformed. |

The one skipped Playwright case is the opt-in live WMS acceptance test, which
requires `UNIPM_DEV_USER_PASSWORD`. All other API behavior in these browser tests
was mocked. The full Playwright run used `CI=true` and
`UNIPM_E2E_PORT=5209`; its raw output is preserved in the ignored artifact
`artifacts/web-qa/20261018T-final-followup/full-playwright-final.log`.

The final focused regression command was:

```text
npm run e2e -- --grep "quick search resets its active destination|keeps the re-laid-out sections and management deep links usable"
```

It passed **2/2**. The first case verifies the active destination resets when
the user filters Quick search results, then opens the GSD Supervisor assignment
queue with Enter. The second verifies schedule planning/history deep links and
captures the settled active Schedules navigation state. Playwright disables
animations for that screenshot only. The changed files also passed a focused
Prettier check before the full format check.

## Screenshot review

The final full-suite archive contains 34 screenshots: 15 desktop/landscape and
19 mobile/portrait. The images use fictional sessions and mocked data, not a
live API or database. Review files are:

- `artifacts/web-qa/20261018T-final-followup/visual-review.md`
- `artifacts/web-qa/20261018T-final-followup/gallery.html`
- `artifacts/web-qa/20261018T-final-followup/screenshot-manifest.json`
- `artifacts/web-qa/20261018T-final-followup/contact-sheets/`

No blocking visual defect was confirmed. The Assets table's action column
requires horizontal scrolling at 1280x900. The QR print preview shows one label
on a mostly blank page; its final print layout remains dependent on an
unspecified label stock. The lower-right TanStack Query badge is a development
overlay. The visual report records these as polish/context findings, not as
live deployment results.

## Not verified

- Full Web tests/build on the declared Node 22 runtime and exact-head GitHub CI.
- Full backend suite or live API/SQL behavior in this UI verification.
- Live authorization and assignment submissions, deployed WMS entry, Azure
  Blob photo persistence, physical-device behavior, or GSD/staging acceptance.
- 1,000+ asset responsiveness, the live schedule worker, and schedule effective
  date or adjustment authority approval.

No Azure resource or database was accessed or changed. No deployment, commit,
push, or merge was performed as part of this verification.
