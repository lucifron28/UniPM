---
id: TEST-198
type: test-run
title: Android smoke failure localization
status: executed
recordedAtUtc: 2026-11-13T09:40:34Z
testedCommit: d2a45688bbcdeeeb9f3f7aa00c646e2c4ca9006e
sourceBranch: codex/nonfunctional-testing-2026-11
evidenceLevel: ci-executed
---

# TEST-198: Android smoke failure localization

## Objective

Inspect both failed matrix jobs from run 43401738804 and localize whether the
asset-details timeout occurred in the app or before the test reached it.

## Execution identity

[GitHub Actions run 43401738804](https://github.com/lucifron28/UniPM/actions/runs/43401738804) tested `d2a45688bbcdeeeb9f3f7aa00c646e2c4ca9006e` and completed at 2026-11-13T09:40:34Z.

- API 29 job: [131806010160](https://github.com/lucifron28/UniPM/actions/runs/43401738804/job/131806010160); artifact: [API 29 evidence](https://github.com/lucifron28/UniPM/actions/runs/43401738804/artifacts/14025926934).
- API 34 job: [131806010477](https://github.com/lucifron28/UniPM/actions/runs/43401738804/job/131806010477); artifact: [API 34 evidence](https://github.com/lucifron28/UniPM/actions/runs/43401738804/artifacts/14025747404).
- Profiles: API 29 Nexus 5, 360×640 dp; API 34 Pixel 7 Pro, 411×891 dp.

## Results

| Check | API 29 | API 34 |
|---|---|---|
| Build, install | PASSED | PASSED |
| Startup/login UI | PASSED | FAILED before integration: `am start -W` reported `Status: timeout`; Pixel Launcher showed a not-responding dialog over the login UI. |
| Configuration tests | PASSED, 4/4 | NOT EXECUTED after startup failure |
| Login, authenticated navigation, dashboard | PASSED | NOT EXECUTED |
| Dashboard search action | FAILED in the test at `ensureVisible(searchAction)`, `Bad state: No element`, `nonfunctional_smoke_test.dart:53`. The lazy dashboard list had not mounted the action. | NOT EXECUTED |
| Asset search/details and later workflows | NOT EXECUTED | NOT EXECUTED |

API 29 reached authentication and dashboard rendering, but did not send an
asset-search request. API 34 did not run the Flutter integration test, so no
asset or authenticated API request was made. The failures were not asset QR,
asset API, or asset-details behavior.

## Screenshots and diagnostics

The API 34 startup screenshot and UI hierarchy show the launcher
not-responding dialog (`android:id/aerr_wait`) over UniPM's login screen. The
API 29 final screenshot is post-teardown launcher state; the failure did not
emit a Flutter widget-tree snapshot or an in-app failure screenshot. The
artifacts preserve screenshots, hierarchy XML, integration logs, API logs,
ADB, emulator, and backend diagnostics.

## Skipped verification and limitations

Maintenance history, inspection forms, and offline persistence/reconnect were
NOT EXECUTED on either profile. No compatibility claim is made from this run.
