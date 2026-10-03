---
id: TEST-046
type: test-run
title: Web contextual navigation and mobile UX verification
status: executed
recordedAtUtc: 2026-10-01T03:25:21Z
testedCommit: b54a50355c958ae3e99f0c3512847fdccecab703
sourceBranch: refactor/web-mobile-ux-audit
evidenceLevel: locally-executed
---

# Web contextual navigation and mobile UX verification

Local Windows checks used Node 22.23.3, Flutter 3.44.5 and Dart 3.12.2.
The table identifies the executed commit for each check. Independent web edits
were present during the first mobile run; the tested mobile files matched the
listed committed content. Later corrections changed tests only. No production
source changed after the initial formatting, lint, typecheck and build checks.

## Executed results

| Check | Commit at execution | Result and raw artifact under `artifacts/ux/` |
|---|---|---|
| Pre-edit asset/form unit baseline | `352e9076006a361c9ec724dca48f0f1f108f578c` | 26 passed in two files, `web-baseline-unit.log` |
| Web Prettier check across web | `9b87ebea7f960160367a650933e43ff6a7eaa0cd` | Exit 0, `web-format.log` |
| Web ESLint, zero warnings allowed | `9b87ebea7f960160367a650933e43ff6a7eaa0cd` | Exit 0, `web-lint.log` |
| Web TypeScript build-mode check | `9b87ebea7f960160367a650933e43ff6a7eaa0cd` | Exit 0, `web-typecheck.log` |
| Web full unit run | `9b87ebea7f960160367a650933e43ff6a7eaa0cd` | 166 passed, five old exact URL assertions failed, `web-unit.log` |
| Corrected form review unit file | `fb14ced03cfd8f714be2b5020b62546f62e4db63` | All 16 passed, `web-unit-final.log`; combined with unchanged files, all 171 tests covered |
| Production Vite build | `9b87ebea7f960160367a650933e43ff6a7eaa0cd` | Exit 0, `web-build.log` |
| Six relevant Chromium browser files | `fb14ced03cfd8f714be2b5020b62546f62e4db63` | 29 passed, three assertion defects failed, `web-playwright-executed.log` |
| Corrected desktop/mobile dashboard cases | `edf2fed9aeb508e47655de6a0843324cea7130b3` | All three passed, `web-playwright-corrected.log`; combined with unchanged cases, all 32 covered |
| Initial ten-file Flutter run | `4a339413a36b30d5a2420d1ae1c7ccaa55b5d438` | 104 passed, ten fixture/harness assertions failed, `flutter-tests.log` |
| Corrected ten-file Flutter run | `0eb69f6ed5bcb2e6fbbdc79df0fb858ccc73a6d9` | 112 passed, two remaining callback/visibility assertions failed, `flutter-tests-final.log` |
| Corrected Home/field-worker files | `fb14ced03cfd8f714be2b5020b62546f62e4db63` | All nine passed, `flutter-tests-corrected.log`; combined with 105 unchanged tests, all 114 covered |
| Final changed-file Dart formatting | `edf2fed9aeb508e47655de6a0843324cea7130b3` | Eight files, zero changes, `flutter-format-final.log` |
| Final corrected Flutter analysis | `b54a50355c958ae3e99f0c3512847fdccecab703` | Exit 0, no issues, `flutter-analyze-verified.log` |
| Diff whitespace check against main base | `b54a50355c958ae3e99f0c3512847fdccecab703` | Exit 0 |

The last change renames an unused test callback argument to Dart's wildcard;
its one-file format check passed and the final analyzer reported no issues.
The prior focused test runs executed the same callback body.

## Commands and scope

Web commands used the installed Node 22 binary directly, with these scripts:

```text
node22 node_modules/prettier/bin/prettier.cjs --check .
node22 node_modules/eslint/bin/eslint.js . --max-warnings 0
node22 node_modules/typescript/bin/tsc -b
node22 node_modules/vitest/vitest.mjs run
node22 node_modules/vitest/vitest.mjs run src/features/preventive-maintenance-forms/preventive-maintenance-form-review.test.tsx
node22 node_modules/vite/bin/vite.js build
```

The ignored Playwright `.mts` runner used an isolated Vite server on port 5180,
Chromium, two workers and retained failure traces. The `.ts` runner and its first
ESM import attempt failed before any browser tests ran, then the runner was
corrected. It did not reuse or stop the original or updated demo servers.

The browser files were `assets`, `inspections`, `schedules`,
`pm-period-dashboard`, `pm-acknowledgement-review` and `ux-context-navigation`.
Only the three failed dashboard cases were rerun after correcting selectors and
contextual URL assertions. New coverage checks dashboard chains at 1440x900 and
375x667, reload retention, direct fallbacks, inspection/asset/schedule returns,
paged registry returns, batch/form returns, keyboard Tab/Enter, visible focus
and page-width containment. Existing tests cover registry pagination and
responsive dashboard/batch flows.

Flutter used `flutter test --no-pub --reporter expanded` on ten files:
`home_page_return_ux_test`, `asset_code_lookup_and_search_test`,
`asset_qr_lookup_test`, `field_worker_workflow_test`,
`mobile_field_workflow_ux_test`, `preventive_maintenance_form_draft_test`,
`preventive_maintenance_form_acknowledgement_test`,
`damaged_qr_manual_entry_e2e_test`, `batch_progress_calculation_test` and
`scanned_asset_pm_presentation_test`.

After the second run, only Home and field-worker files were rerun. Mutable
repositories keep old Home data while the child route is open, then expose new
progress/acknowledgement after AppBar Back; read counts confirm one reload on
return. Focused coverage also preserves assignment isolation, Draft resume,
QR/manual lookup, geolocation, row-save behavior, whole-batch gating,
acknowledgement, CPMP month-end presentation and progress calculations.

## Corrections and limits

Web failures were old exact href/search expectations and an ambiguous dashboard
heading selector. Tests now assert the full return context and original filters.
Mobile failures were off-screen/lazy widget assertions, missing fake callbacks,
search input text counted as a result, and a required API fixture boolean omitted
in an existing affected test. Two ordering fixtures also needed valid FE months
and distinct batch identities. These fixes did not change production behavior.
The final analyzer caught and cleared a test wildcard naming style issue.

The API contract and backend/domain behavior did not change. Local API
regeneration and backend/database suites were not run for this frontend task.
GitHub CI verification after pushing is reported separately on the final branch
head. JSDOM's existing scrollTo warnings were non-failing. Dependency restoration
reported inherited web package vulnerabilities; dependencies were not changed.

Broader Flutter suites, physical Android, iOS, real camera/touch behavior and
production deployment were not executed. Widget and browser coverage is not
institutional validation or a full accessibility certification. The paused RAG
worktree's file status and diff SHA256 matched its saved baseline; initial string
comparison needed CRLF normalization. No protected-worktree write occurred.
