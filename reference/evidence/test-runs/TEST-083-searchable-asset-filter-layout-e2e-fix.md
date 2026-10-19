---
id: TEST-083
type: test-run
title: Searchable asset filter layout E2E correction
status: executed
recordedAtUtc: 2026-10-19T04:22:22Z
testedCommit: 7ba0ab238e14e27f18e72217970896d0f415535f
sourceBranch: feature/searchable-web-filters
evidenceLevel: locally-executed
---

# Searchable asset filter layout E2E correction

## Objective

Correct the responsive asset-filter test after the Web CI run exposed its stale
assumption that all four filter controls were native selects. The Building and
Department controls are searchable comboboxes.

## Execution Identity

- Source branch: `feature/searchable-web-filters`.
- Tested source commit: `7ba0ab238e14e27f18e72217970896d0f415535f`.
- Local execution date: 2026-10-19 UTC.

## Commands

```powershell
# From web/
npm run e2e -- e2e/assets.spec.ts --config=playwright.searchable-filters.local.config.ts --grep "keeps asset filters inside their grid cells" --workers=1 --output ..\artifacts\searchable-filters\asset-filter-playwright-results
npx prettier --check e2e/assets.spec.ts
```

Playwright used a temporary local Vite config on port 5187; it was removed
after the run.

## Results

| Check | Result |
| --- | --- |
| Asset filter layout E2E at 1440×900 and 1920×1080 | Passed: 2/2. The test now measures the two native selects and two searchable comboboxes and checks grid-cell bounds and overlap. |
| Prettier for the updated E2E file | Passed. |
| Web CI at prior head `0a2805d41cdd11660ffaa7ef3e9b61e2960e0b1f` | Web unit, coverage, formatting, lint, typecheck, API checks, and build passed; Playwright had 49 passed, 2 failed, and 1 skipped. Both failures were this stale asset layout selector. |
| Backend CI at prior head | Passed. |
| Exact-head CI for this source commit | Pending push. |

## Artifacts

- `artifacts/searchable-filters/asset-filter-playwright-committed-head.log`
- `artifacts/searchable-filters/asset-filter-prettier-check.log`
- `artifacts/searchable-filters/web-ci-failure-0a2805d.log` (failed Web CI output)
- Web CI run: https://github.com/lucifron28/UniPM/actions/runs/39198677462
- Backend CI run: https://github.com/lucifron28/UniPM/actions/runs/39198680771

## Limitations

This record covers the two previously failing asset layout cases. The full Web
CI and Backend CI must pass on the final pushed PR head before merge review.
