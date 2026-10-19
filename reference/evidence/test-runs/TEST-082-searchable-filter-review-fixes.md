---
id: TEST-082
type: test-run
title: Searchable filter review fixes verification
status: executed
recordedAtUtc: 2026-10-19T04:15:37Z
testedCommit: 63f66bce66f481cb2daf0a972b4b0e3f2bdaf5a0
sourceBranch: feature/searchable-web-filters
evidenceLevel: locally-executed
---

# Searchable filter review fixes verification

## Objective

Verify the PR #98 review fixes for searchable picker interactions, saved
schedule-assignment summaries, the searchable historical PM-cycle filter,
registry query expectations, desktop popup bounds, keyboard selection, and
combined schedule filters.

## Execution Identity

- Source branch: `feature/searchable-web-filters`.
- Tested source commit: `63f66bce66f481cb2daf0a972b4b0e3f2bdaf5a0`.
- Local execution date: 2026-10-19 UTC.
- No real institutional data or external AI provider was used.

## Environment

- Windows desktop development environment.
- Web checks ran from `web/`; backend tests ran from the repository root.
- Playwright used Chromium and a temporary local Vite config on port 5187; the
  temporary config was removed after execution.

## Commands

```powershell
# From web/
npm run test:run -- src/components/ui/searchable-picker.test.tsx src/features/schedules/schedule-workflow.test.tsx src/features/preventive-maintenance-forms/preventive-maintenance-form-review.test.tsx --reporter=dot
npm run e2e -- e2e/schedules.spec.ts --config=playwright.searchable-filters.local.config.ts --grep "searches long filters" --workers=1 --output ..\artifacts\searchable-filters\playwright-results
npm run format:check
npm run typecheck

# From repository root
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore --disable-build-servers -m:1 --filter "FullyQualifiedName~Asset_registry_is_bounded_searchable_and_keeps_legacy_array_contract"
```

## Results

| Check | Result |
| --- | --- |
| Focused web unit tests | Passed: 3 files, 46 tests. Covers picker reopen and keyboard footer actions, saved-vs-draft assignment names through failed saves, and searchable PM-cycle filtering. |
| Focused backend registry test | Passed: 1/1. The asset registry fixture count, sorted page boundaries, and category summary now include the added lookup fixtures. |
| Desktop Playwright scenario | Passed: 1/1. Exercises a 60-department list, internal scroll, viewport bounds, visible search while scrolling, asset page 2 loading, keyboard filtering/selection, and combined schedule filters. |
| Web formatting | Passed: `npm run format:check`; the OpenAPI Prettier failure is fixed. |
| Web TypeScript typecheck | Passed. |
| Native SQL Server-specific verification | Not run. |
| Exact-head GitHub CI | Pending push of the verification-evidence commit. |

The backend run emitted existing NuGet vulnerability advisory warnings, a
NuGet vulnerability-service availability warning, and an existing nullable
dereference compiler warning. The targeted test passed.

## Desktop Screenshot Review

The browser test asserted that each popup remained within the viewport and that
the scrollable list's computed maximum height was positive and no greater than
280px. Each screenshot was inspected after the list was scrolled to its later
department options; the search field stayed visible.

| Viewport | Screenshot | SHA-256 |
| --- | --- | --- |
| 1280×720 | `artifacts/searchable-filters/playwright-results/schedules-Schedule-workflo-3489a-nd-applies-combined-filters-chromium/searchable-filters-1280x720.png` | `14ECBCA554BADB519AE9A44B7875545EB17D32EF32156214CB9070059E3F23E1` |
| 1440×900 | `artifacts/searchable-filters/playwright-results/schedules-Schedule-workflo-3489a-nd-applies-combined-filters-chromium/searchable-filters-1440x900.png` | `6F9792D6B5A8C7D0F9D9DC1CC46F927E81885F9517F6C68176213B1D9849D139` |
| 1920×1080 | `artifacts/searchable-filters/playwright-results/schedules-Schedule-workflo-3489a-nd-applies-combined-filters-chromium/searchable-filters-1920x1080.png` | `CA23EDBFB1BC79E7431A0AE48BEE35B17051B1D939E47A28AA2A79B1B62D09EA` |

## Test Counts

- Web unit tests: 46 passed, 0 failed across 3 files.
- Backend registry test: 1 passed, 0 failed, 0 skipped.
- Playwright: 1 passed, 0 failed, 0 skipped.

## Generated Artifacts

- `artifacts/searchable-filters/web-focused-tests-exact-head.log`
- `artifacts/searchable-filters/backend-registry-test-exact-head.log`
- `artifacts/searchable-filters/desktop-browser-test-exact-head.log`
- `artifacts/searchable-filters/format-check-exact-head.log`
- `artifacts/searchable-filters/typecheck-exact-head.log`
- The three viewport screenshots listed above.

The logs and screenshots are ignored local artifacts; this record contains the
reviewed results and screenshot hashes. Exact-head GitHub CI remains pending.
