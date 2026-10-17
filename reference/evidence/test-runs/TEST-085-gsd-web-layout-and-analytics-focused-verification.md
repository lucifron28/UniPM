---
id: TEST-085
type: test-run
title: GSD web layout and PM analytics focused verification
status: reviewed
recordedAtUtc: 2026-10-17T12:23:30Z
testedCommit: be2c514769dba76fa06848ae3eeb22680880866d
sourceBranch: feature/web-analytics-layout
evidenceLevel: locally-executed
---

# GSD web layout and PM analytics focused verification

## Objective

Verify the GSD-only PM Analytics page and source navigation, compact PMIS
layouts, shell behavior, and affected registry and acknowledgement workflows
with focused mocked Web checks.

## Tested tree identity

- Repository branch: `feature/web-analytics-layout`.
- Base commit: `be2c514769dba76fa06848ae3eeb22680880866d`.
- Tests ran on the base plus the Web source and test patch before the patch was
  committed. The resulting implementation commits are listed below. The test
  runner was not repeated after those commits, so this is not exact-head
  verification by commit SHA.
- Implementation commits corresponding to the tested source snapshot:
  - `99859084f0bde519f274bd4ba52598eb5e372a2f` — refine login and app shell
    layout.
  - `0613308a7dceaca9d720c4b1f00a3e18a7f5c4cb` — add dedicated GSD PM
    Analytics page.
  - `2e8da66dc361c2a6e57a9deb75299dccaa742f03` — compact registry layouts.
  - `52f77b6904a463c349793eab42638a7c798f851f` — compact PM review and
    acknowledgement.
- Snapshot of the tracked Web diff at the final focused checks:
  `artifacts/web-qa/layout-focused/web-working-tree.patch`, SHA-256
  `2B34FF302CDD23F2AB03D1354099945B00912F7FF5C0238822F389D6A900C558`.
- The added analytics route is not included in `git diff`; its SHA-256 is
  `E177CE59E97C991A539254FB0ACC1D9C5B88C7D5C7FE8EEE65CCE1289E207C3E`.
  These hashes identify the working snapshot, not a later commit.

## Environment and boundaries

- Windows, Node.js 22.23.3, Vitest 4.1.10, Playwright, and installed Chrome
  155.0.8059.80.
- Browser checks used a temporary installed-Chrome configuration, local Vite
  on port 5185, and deterministic Playwright API mocks with synthetic data.
  The focused browser runs used elevated loopback access after the sandboxed
  runner could not connect to its own Vite listener.
- No live API, database, external AI provider, institutional record, or
  credential source was used. No backend, API contract, generated contract,
  data model, AI, or database changes were made in this UI work.

## Commands and results

From `web`, the analytics, dashboard, and shell unit batch passed 36/36:

    & '..\artifacts\tools\node22\node-v22.23.3-win-x64\node.exe' .\node_modules\vitest\vitest.mjs run src/features/reports/pm-analytics.test.tsx src/features/reports/pm-period-dashboard.test.tsx src/components/layout/app-shell-access.test.tsx

The registry, inspection, schedule, and acknowledgement unit batch passed
63/63 on its final run:

    & '..\artifacts\tools\node22\node-v22.23.3-win-x64\node.exe' .\node_modules\vitest\vitest.mjs run src/features/assets/asset-registry.test.tsx src/features/inspections/inspection-workflow.test.tsx src/features/schedules/schedule-workflow.test.tsx src/features/preventive-maintenance-forms/preventive-maintenance-form-review.test.tsx

The run was permitted after the user approved focused test corrections beyond
the initial retry cap. Earlier attempts passed 55/63 and then 59/63. The final
corrections aligned closed `<details>` assertions with hidden visibility,
matched the compact date/location row, and updated acknowledgement copy checks.

The PM dashboard browser run passed 4/4; the app-shell run passed 3/3. In the
four registry/workflow specs, the initial 33-test run passed 31 and exposed
two stale assertions. After correcting only those tests, both affected cases
passed in a focused rerun. Together, all 33 unique cases in those four specs
were confirmed passing across the run and targeted rerun. The clipboard case
now defines a rejected clipboard method using the same browser-mock technique
as the existing unit test; the acknowledgement case checks the current receipt,
form-lock, official-history, and no-RMRF wording.

The PM Analytics source-scroll regression and existing QR print-visibility
case passed 2/2 after the desktop shell correction. The regression uses a
1440×900 viewport, clicks Calculation sources, calls native
`#pm-analytics-sources-title.scrollIntoView({block: 'start'})`, then verifies
that document scroll and the sidebar, header, and workspace top positions stay
fixed while the workspace itself scrolls.

The separate TypeScript build check (`tsc --build`) exited 0 with no
diagnostics. Prettier passed for all changed Web source and test files after
formatting one PM form test. `git diff --check` exited 0; Git printed only
line-ending notices for existing CRLF-configured files.

## Visual review

The selected synthetic screenshot gallery is at
`artifacts/web-qa/20261017T120238Z-layout-accuracy-elevated/contact-sheet.html`.
It contains 23 selected gallery entries, including desktop captures at
1280×720, 1440×900, and 1920×1080.
The review caught native source-heading scrolling moving the header and
sidebar. The desktop shell now contains that scroll within the workspace, and
the focused browser regression verifies the corrected geometry. The 1920-pixel
matching-source overview shows its heading but does not by itself establish
that source rows are visible in that frame.

## Signatory read-contract observation

Acknowledgement submits the signatory name, position, and signature and returns
them in the immediate acknowledgement response. The current GET summary
contracts do not return signatory details after reload. Persisted display after
reload would require a separately scoped read-contract change; no backend or
API change was included here.

## Raw artifacts

Ignored logs and screenshots are retained under `artifacts/`:

- `artifacts/web-qa/layout-focused/unit-rerun.log` — analytics/dashboard/shell
  unit rerun, 36/36.
- `artifacts/web-qa/layout-focused/unit-registry-form.log` — first registry
  and form attempt, 55/63.
- `artifacts/web-qa/layout-focused/unit-registry-form-rerun.log` — intermediate
  attempt, 59/63.
- `artifacts/web-qa/layout-focused/unit-registry-form-final-rerun.log` — final
  registry and form run, 63/63.
- `artifacts/web-qa/layout-focused/playwright-pm-period-dashboard.log` — PM
  dashboard, 4/4.
- `artifacts/web-qa/layout-focused/playwright-app-shell.log` — shell, 3/3.
- `artifacts/web-qa/layout-focused/playwright-registry-workflows-reviewed.log`
  — four registry/workflow specs, 31/33 before the two test-only corrections.
- `artifacts/web-qa/layout-focused/playwright-registry-workflows-corrections.log`
  — corrected clipboard and acknowledgement cases, 2/2.
- `artifacts/web-qa/layout-focused/playwright-scroll-and-print.log` — analytics
  shell-scroll regression and QR print case, 2/2.
- `artifacts/web-qa/layout-focused/tsc-build-final.log` was not retained by the
  shell; the command's captured process exit code was 0 and it emitted no
  diagnostics.
- `artifacts/web-qa/layout-focused/prettier-changed-files-final.log` and
  `git-diff-check-final.log` preserve the successful formatting and whitespace
  check outputs.
- `artifacts/web-qa/20261017T120238Z-layout-accuracy-elevated/` contains the
  screenshot gallery, manifest, and selected images.

The sandboxed Playwright attempt could not reach its local Vite listener and
was interrupted before its test output was available; it is not counted as a
test result. A later first filter attempt used paths prefixed with `e2e/` even
though the temporary config already sets that test directory; Playwright found
zero tests. The corrected filename-relative runs above provide the results.

## Verification not performed

- Full Web or repository test suite, production build, lint, CI, deployment, or
  native SQL Server checks.
- Live API integration, real-provider evaluation, held-out interpretation
  quality, or GSD acceptance.
- Test execution after the implementation commits. The tested tree was the
  recorded base plus the patch snapshot above.
