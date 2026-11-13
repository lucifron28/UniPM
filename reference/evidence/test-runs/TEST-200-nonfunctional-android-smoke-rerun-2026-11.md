---
id: TEST-200
type: test-run
title: Android smoke rerun through inspection entry
status: executed
recordedAtUtc: 2026-11-13T14:50:34Z
testedCommit: 523cce1898956434c70d50ebb6b1142be459c7a8
sourceBranch: codex/nonfunctional-testing-2026-11
evidenceLevel: ci-executed
---

# TEST-200: Android smoke rerun through inspection entry

## Objective

Rerun both Android profiles after changing the smoke test to wait for the
rendered asset result, then continue through history, schedule, and inspection
entry where the fixture allows.

## Execution identity

[GitHub Actions run 43444880972](https://github.com/lucifron28/UniPM/actions/runs/43444880972) tested `523cce1898956434c70d50ebb6b1142be459c7a8` and completed at 2026-11-13T14:50:34Z.

- API 29 job: [131951621778](https://github.com/lucifron28/UniPM/actions/runs/43444880972/job/131951621778); artifact: [API 29 evidence](https://github.com/lucifron28/UniPM/actions/runs/43444880972/artifacts/14047349661).
- API 34 job: [131951621446](https://github.com/lucifron28/UniPM/actions/runs/43444880972/job/131951621446); artifact: [API 34 evidence](https://github.com/lucifron28/UniPM/actions/runs/43444880972/artifacts/14048770040).
- Profiles: API 29 Nexus 5, 360×640 dp; API 34 Pixel 7 Pro, 411×891 dp.

## Results

| Check | API 29 | API 34 |
|---|---|---|
| Build, install, startup/login, configuration | PASSED; configuration 4/4 | PASSED; configuration 4/4 |
| Login, authenticated navigation, dashboard, search action | PASSED | PASSED |
| Asset search and details | PASSED; the screen snapshot contained `Asset found`, `DEMO-EL-001`, asset fields, and `Asset lookup`; `GET /assets/by-qr/UNIPM-DEMO-EL-001` returned 200 | PASSED; the post-tap snapshot initially showed `Finding asset...`; the awaited `asset-code` appeared and the details pass marker followed. By-QR lookup returned 200. |
| Maintenance history | FAILED before tap: `ensureVisible(view-asset-history)` raised `Bad state: No element` at test line 154. The lazy details list had not built the action. No history endpoint request occurred. | PASSED; history screen and at least one acknowledged history record rendered. `GET /inspections/history/{assetId}` returned 200. |
| PM schedule | NOT EXECUTED after history selector failure | PASSED; schedule summary rendered and asset-scoped schedule API returned 200. |
| Inspection form entry/save | NOT EXECUTED | BLOCKED by fixture state; no Start/Resume action appeared, and the test failed at line 201. The form-list API returned 200; no create or inspection-write request occurred. |
| Offline persistence/reconnect | NOT EXECUTED | NOT EXECUTED |

## Root cause and interpretation

The original asset-details failure does not persist at this tested commit. The
QR value `UNIPM-DEMO-EL-001` resolves successfully, and API 34 rendered asset
details, history, and the schedule.

The API 29 failure is a test-side lazy-list interaction: `tester.ensureVisible`
requires the `view-asset-history` element to exist, but the small viewport had
not built that list child yet. The test never tapped History, so this is not an
API/history failure.

The API 34 form-entry result is consistent with the synthetic fixture and
product behavior. `DEMO-EL-001` has a Completed schedule and an acknowledged
inspection, but no Draft inspection row. The PM entry component therefore
shows its completed-without-draft state and exposes no Start/Resume action.
The smoke test incorrectly expected an inspection action on this asset. No
production change was made to bypass that lifecycle rule.

## Screenshots and diagnostics

The integration log preserves the post-tap text snapshots and pass/failure
markers. `api-request-status.log` preserves HTTP status/path entries, all
observed as 200 for the exercised requests. The captured `final-29.png` and
`final-34.png` and final UI hierarchy show the Android launcher after test
teardown, not the in-app failure screen. No widget-tree dump or failure-time
in-app screenshot was captured for the history/form failures; this limits
visual diagnosis of those final states.

## Skipped verification and limitations

An inspection form was not opened or saved on either profile. Offline draft
persistence, network loss/reconnect, and sync were NOT EXECUTED; offline
synchronization is deferred on this branch. Android compatibility therefore
remains incomplete.
