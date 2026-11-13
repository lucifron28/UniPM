---
id: TEST-199
type: test-run
title: Android smoke selector follow-up
status: executed
recordedAtUtc: 2026-11-13T14:37:35Z
testedCommit: bf669562ab553211f9f706e77b5a449d1c6d02c8
sourceBranch: codex/nonfunctional-testing-2026-11
evidenceLevel: ci-executed
---

# TEST-199: Android smoke selector follow-up

## Objective

Rerun the existing Android integration smoke after stabilizing dashboard
navigation, and inspect the new failure on both profiles.

## Execution identity

[GitHub Actions run 43442563935](https://github.com/lucifron28/UniPM/actions/runs/43442563935) tested `bf669562ab553211f9f706e77b5a449d1c6d02c8` and completed at 2026-11-13T14:37:35Z.

- API 29 job: [131943627515](https://github.com/lucifron28/UniPM/actions/runs/43442563935/job/131943627515); artifact: [API 29 evidence](https://github.com/lucifron28/UniPM/actions/runs/43442563935/artifacts/14047592153).
- API 34 job: [131943628049](https://github.com/lucifron28/UniPM/actions/runs/43442563935/job/131943628049); artifact: [API 34 evidence](https://github.com/lucifron28/UniPM/actions/runs/43442563935/artifacts/14046682617).
- Profiles: API 29 Nexus 5, 360×640 dp; API 34 Pixel 7 Pro, 411×891 dp.

## Results

| Check | API 29 | API 34 |
|---|---|---|
| Build, install, startup/login, configuration | PASSED; configuration 4/4 | PASSED; configuration 4/4 |
| Login, authenticated navigation, dashboard, search action | PASSED | PASSED |
| Search result/details | FAILED at `ensureVisible(assetSearchResult)`, `Bad state: No element`, `nonfunctional_smoke_test.dart:107` | FAILED at the same test line/assertion |
| Asset API | `GET /assets?search=DEMO-EL-001&limit=30` returned 200; no by-QR lookup was reached | Same; no by-QR lookup was reached |
| Maintenance history and inspection forms | NOT EXECUTED | NOT EXECUTED |

The test had waited for `find.text('DEMO-EL-001')`, which could match the
search field's entered text before the result card was built. It then called
`ensureVisible` on the absent result-card key. This is a test interaction
defect; the API search response was successful. The next test revision waits
for the result card's key directly.

## Screenshots and diagnostics

Artifacts contain startup/final screenshots, API status logs, integration
output, and device diagnostics. The final screenshots are launcher state after
test teardown; this run did not preserve an in-app screenshot or widget dump
at the failed result-card interaction.

## Skipped verification and limitations

Asset QR lookup/details, maintenance history, inspection forms, and offline
checks were NOT EXECUTED on either profile. No compatibility claim is made
from this run.
