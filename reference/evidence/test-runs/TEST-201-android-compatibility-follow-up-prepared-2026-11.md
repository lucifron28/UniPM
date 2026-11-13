---
id: TEST-201
type: test-run
title: Android compatibility verification; follow-up failed in test interactions
status: executed
recordedAtUtc: 2026-11-13T18:04:35Z
testedCommit: e15c428b155c2f0ef4bf1807806a3763e4b9557f
sourceBranch: codex/nonfunctional-testing-2026-11
evidenceLevel: ci-executed
---

# TEST-201: Android compatibility verification

## Execution history

### Run 1 — integration-test compilation failure

[Run 43468215938](https://github.com/lucifron28/UniPM/actions/runs/43468215938) tested commit db04aed6d6079a81d45b32b811750ef24c38643e and completed at 2026-11-13T17:12:04Z. It covered Android 10/API 29 (Nexus 5) and Android 14/API 34 (Pixel 7 Pro).

Both profiles passed setup, database/API seeding, APK build and install, visible login startup, and all four Flutter configuration tests. The integration-test loader then failed to compile integration_test/nonfunctional_smoke_test.dart: offlineRemarks was declared twice in one function, at lines 598 and 674. The first error was the Dart compile error at line 674; Gradle failed at :app:compileFlutterBuildDebug. No authenticated functional assertion ran in this execution.

Both artifacts were uploaded. The initial API 29 artifact download returned HTTP 503 for account egress, and API 34 was not attempted at that time. A later read-only retrieval of both artifacts succeeded and established the compile error above. The startup and post-cleanup screenshots are present, but this run has no in-app functional failure state.

- API 29 job 132032945952; [android-api-29-evidence](https://github.com/lucifron28/UniPM/actions/runs/43468215938/artifacts/14061360039).
- API 34 job 132032945197; [android-api-34-evidence](https://github.com/lucifron28/UniPM/actions/runs/43468215938/artifacts/14060386895).

| Check | Android 10 / API 29 | Android 14 / API 34 |
|---|---|---|
| API/database setup, APK build and installation | PASSED | PASSED |
| Startup and visible sign-in UI | PASSED | PASSED |
| Four Flutter configuration tests | PASSED, 4/4 | PASSED, 4/4 |
| Integration test compile/load | FAILED — duplicate local declaration | FAILED — duplicate local declaration |
| Authenticated app scenarios and later workflows | NOT EXECUTED | NOT EXECUTED |

### Run 2 — authorized verification after the compile fix

[Run 43476917518](https://github.com/lucifron28/UniPM/actions/runs/43476917518) was manually dispatched once on 2026-11-13 at 17:51:24Z. It tested commit e15c428b155c2f0ef4bf1807806a3763e4b9557f on codex/nonfunctional-testing-2026-11 and completed at 18:04:35Z. The push commit used [skip ci]; no push-triggered duplicate run was created. Both matrix jobs completed their API/database setup, APK builds, emulator startup, and evidence uploads.

- API 29: job [132063008661](https://github.com/lucifron28/UniPM/actions/runs/43476917518/job/132063008661); artifact [android-api-29-evidence](https://github.com/lucifron28/UniPM/actions/runs/43476917518/artifacts/14062918669) (1,370,027 bytes).
- API 34: job [132063008219](https://github.com/lucifron28/UniPM/actions/runs/43476917518/job/132063008219); artifact [android-api-34-evidence](https://github.com/lucifron28/UniPM/actions/runs/43476917518/artifacts/14064058195) (3,381,687 bytes).

The compile blocker was resolved: both jobs loaded and ran the integration test body. Before this run, git diff --check passed. Flutter analysis exited 1 with 28 informational lints and no error- or warning-level diagnostics. Dart format verification also exited 1 because of existing formatting differences across the file. No broad formatter or lint cleanup was applied.

## Run 2 profile outcomes

| Check | Android 10 / API 29, Nexus 5 | Android 14 / API 34, Pixel 7 Pro |
|---|---|---|
| Emulator profile, APK build/install, startup, visible login UI | PASSED | PASSED; the launch command timed out, but the app UI appeared and startup was marked passed |
| Configuration tests | PASSED, 4/4 | PASSED, 4/4 |
| Synthetic login, authenticated navigation, dashboard and asset search | PASSED | PASSED |
| Asset search and asset details for DEMO-EL-001 | PASSED | PASSED |
| History API response and expected acknowledged record | PASSED — HTTP 200; inspection 34000000-0000-4000-8000-000000000001; expected remarks matched | PASSED — HTTP 200; same seeded inspection and remarks |
| Visible maintenance-history record | PASSED | PASSED |
| Return from history to asset details | FAILED at test-side wait for find.text('Asset found'); the screenshot shows the Asset lookup/details page | PASSED sufficiently to continue to the next dashboard scenario |
| Preventive-maintenance schedule display | NOT EXECUTED after the history-stage failure | PASSED |
| Synthetic eligible form fixture | NOT EXECUTED | PASSED — DEMO-EL-003, schedule 31000000-0000-4000-8000-000000000003, status Due, assigned to the smoke user, no existing Draft |
| Start Inspection / form open | NOT EXECUTED | FAILED at test interaction; tap did not hit the button |
| Online form save, reopen, edit and backend persistence | NOT EXECUTED | NOT EXECUTED |
| Offline draft creation and backend disconnection | NOT EXECUTED | NOT EXECUTED |
| App restart and offline snapshot/pending-write persistence before reconnect | NOT EXECUTED | NOT EXECUTED |
| Reconnection, server synchronization, reopened form and expected server state | NOT EXECUTED | NOT EXECUTED |
| Controlled synchronization conflict | NOT EXECUTED; this smoke test has no conflict scenario | NOT EXECUTED; this smoke test has no conflict scenario |

## Run 2 failure diagnosis

### API 29 — history-return selector

The history data and history-screen checks passed. The app requested the history endpoint twice with HTTP 200; the response contained inspection 34000000-0000-4000-8000-000000000001 with remarks “Emergency light passed the simulated power interruption test.” The same record was visible on screen.

After tester.pageBack(), the screenshot and Android window hierarchy show the app bar Asset lookup, the selected preventive-maintenance schedule, and the View maintenance history action. The app had returned to the asset-details route. The test then timed out waiting for Asset found. That text is near the top of the lazy ListView in asset_qr_lookup_page.dart; the previous scroll position was retained near the schedule/history actions, so the top list child was not built. This is a test-side return marker failure, not evidence of broken navigation.

Evidence is in the API 29 artifact: integration-29-prepare.log, api-request-status.log, asset-history-failure.png, and asset-history-failure-window.xml. The screenshot captures the actual post-tap screen before teardown.

### API 34 — Start Inspection tap missed

History API/UI and preventive-maintenance schedule checks passed. The test verified DEMO-EL-003’s synthetic schedule was Due, eligible, assigned to the test user, and had no pre-existing Draft.

The test found the start-pm button, but Flutter logged that tester.tap() used offset (205.7, 1021.1), outside the render root (411.4, 867.4). The hit test reached only the render view and integration binding. The test then timed out waiting for new-inspection-date; the captured screen still shows the DEMO-EL-003 schedule and Start Inspection button. The API log contains successful reads but no form-create or inspection-start mutation after this interaction. This is a test-side scrolling/hit-test failure; no application defect or API failure was established. The current helper selects the first vertical scrollable globally, and did not leave the target within hit-test bounds.

Evidence is in the API 34 artifact: integration-34-prepare.log, api-request-status.log, pm-form-open-failure.png, and the captured widget/render diagnostics. The warning stack points to the test tap at line 319.

Both profiles’ emulator logs were reviewed; no app crash or ANR was found. Auth, asset, QR, schedule, form-list, and history reads recorded for the reached scenarios returned HTTP 200. No inspection form was created by the failed API 34 tap.

## TEST-200 outcomes carried forward

| Check | API 29 | API 34 |
|---|---|---|
| Install, visible login, configuration checks, authenticated navigation, dashboard, asset search/details | PASSED | PASSED |
| History | FAILED at test-side ensureVisible(view-asset-history) because the lazy details list had not built the History action. No tap or history API request occurred. | PASSED; the history page displayed a seeded record and the API returned 200. |
| PM schedule | NOT EXECUTED after the History selector failure. | PASSED. |
| Start/Resume and form save/reopen | NOT EXECUTED. | BLOCKED by using DEMO-EL-001, whose schedule is Completed and has no Draft row; no Start/Resume action was expected from this fixture. |
| Offline draft persistence and reconnection sync | NOT EXECUTED. | NOT EXECUTED. |

The earlier asset-details timeout from run 43396819069 did not recur in TEST-200, but TEST-200 does not identify the cause of that earlier timeout. No production defect was confirmed, so production code is unchanged.

## TEST-201 source review before execution

The prepared test scrolls the lazy asset-details list to History, verifies HTTP 200 and the seeded DEMO-EL-001 inspection ID and remarks, and checks that the record appears on the history screen. The online form path uses DEMO-EL-003 and its assigned Due schedule, then checks Start, save, reopen, edit, backend persistence, and a second reopen.

Offline work uses the separate synthetic DEMO-FE-003 asset and its eligible November 2026 schedule. Each matrix job uses its own ephemeral database and fixture state. The runner disables emulator connectivity after API setup. The prepare phase checks that the backend is unreachable before it saves the local Draft and inspection.

After the runner force-stops the app, the restart phase remains signed out and offline. It reads the encrypted secure snapshot using the synthetic inspector ID from phase one, verifies the Draft, inspection fields, and exactly two pending writes, then emits the online-network marker. The runner restores connectivity only after observing the persistence marker. After reconnection and same-account sign-in, the test checks the synchronized server Draft, inspection, schedule status, cleared sync banner, and reopened form.

The failure wrapper logs visible text, Flutter widget and render-tree diagnostics, then captures an in-app screenshot and Android hierarchy before teardown. Run 2 captured these diagnostics for both failures, and they were inspected. A controlled synchronization-conflict scenario is not implemented and remains NOT EXECUTED. Source review describes the intended checks; only the run-specific outcomes above count as execution evidence.

## Evidence preservation and limits

TEST-198, TEST-199, TEST-200 and their records remain unchanged. The TEST-201 first execution is preserved above, followed by the single authorized follow-up. Only test code changed; no production application logic was modified. No further rerun was dispatched.

TEST-201 remains incomplete: history was verified on both profiles, but the maintenance-history return marker failed on API 29 and the Start Inspection tap missed on API 34. Online form save/reopen/edit, offline persistence, reconnection synchronization and synchronized server-state checks were NOT EXECUTED on both profiles. Android compatibility must not be reported as passed.
