---
id: TEST-047
type: test-run
title: PM task badge and readable record label verification
status: executed
recordedAtUtc: 2026-10-01T17:37:11Z
testedCommit: 94268bbde8994c887e9372c317e750ec265f74b3
sourceBranch: refactor/web-mobile-ux-audit
evidenceLevel: locally-executed
---

# PM task badge and readable record label verification

This follow-up changes presentation only. Mobile task badges use existing
schedule urgency variants while form badges retain their variants. Web registry
cards omit the form UUID, dashboard asset rows show form and acknowledgement
status, and corrective findings lead with asset code and inspection date.
Inspection IDs remain in collapsed technical details. No API or domain changes
were made.

## Executed results

Windows checks used Node 22.23.3, Flutter 3.44.5 and Dart 3.12.2. Every command
below exited 0. Raw logs remain ignored under `artifacts/ux-followup/`.

| Check | Exact tested commit | Result | Log |
|---|---|---|---|
| Changed-file Dart format check | `56b2697e466d47b5c34cee1d8b13b4f16848bc29` | Two files, zero changes | `flutter-format.log` |
| Flutter analyze | `56b2697e466d47b5c34cee1d8b13b4f16848bc29` | No issues | `flutter-analyze.log` |
| Focused Flutter presentation tests | `56b2697e466d47b5c34cee1d8b13b4f16848bc29` | Seven passed | `flutter-tests.log` |
| Web Prettier check | `94268bbde8994c887e9372c317e750ec265f74b3` | Passed | `web-format.log` |
| Web ESLint, zero warnings allowed | `94268bbde8994c887e9372c317e750ec265f74b3` | Passed | `web-lint.log` |
| TypeScript build-mode check | `94268bbde8994c887e9372c317e750ec265f74b3` | Passed | `web-typecheck.log` |
| Focused web unit tests | `94268bbde8994c887e9372c317e750ec265f74b3` | 22 passed in two files | `web-unit.log` |
| Production Vite build | `94268bbde8994c887e9372c317e750ec265f74b3` | Passed | `web-build.log` |
| Relevant Chromium tests | `94268bbde8994c887e9372c317e750ec265f74b3` | Four passed in two files | `web-playwright.log` |
| Diff whitespace check against `89d620e` | `94268bbde8994c887e9372c317e750ec265f74b3` | Passed | Terminal output, no errors |

Independent web edits were present during mobile verification. The tested
mobile files matched the listed mobile commit. No production code changed
after these checks.

## Commands

```text
dart format --output=none --set-exit-if-changed lib/ui/widgets/batch_pm_card.dart test/scanned_asset_pm_presentation_test.dart
flutter analyze --no-pub
flutter test --no-pub --reporter expanded test/scanned_asset_pm_presentation_test.dart
node22 node_modules/prettier/bin/prettier.cjs --check .
node22 node_modules/eslint/bin/eslint.js . --max-warnings 0
node22 node_modules/typescript/bin/tsc -b
node22 node_modules/vitest/vitest.mjs run src/features/preventive-maintenance-forms/preventive-maintenance-form-review.test.tsx src/features/reports/pm-period-dashboard.test.tsx
node22 node_modules/vite/bin/vite.js build
node22 web/node_modules/@playwright/test/cli.js test --config artifacts/ux/playwright.config.mts pm-period-dashboard.spec.ts pm-acknowledgement-review.spec.ts
git diff --check 89d620e..HEAD
```

The ignored browser runner used Chromium, two workers and an isolated Vite
server on port 5180. Unit tests emitted existing jsdom scrollTo notices and the
browser runner emitted color-environment warnings; neither failed a check.

## Not verified

Physical Android/iOS appearance, live-backend operation and backend tests were
not run for this presentation-only follow-up. The full Flutter suite was not
run. Browser checks used mocked API fixtures. GitHub CI is verified separately
against the exact pushed head after committing this record. No merge is
included in this task.
