---
id: TEST-201
type: test-run
title: Android compatibility verification; follow-up failed in test interactions
status: executed
recordedAtUtc: 2026-11-14T06:30:10Z
testedCommit: 58b17b355aa02425c2610076c76800b3d0413693
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

### Run 3 — authorized API 29/API 34 interaction rerun

[Run 43548924029](https://github.com/lucifron28/UniPM/actions/runs/43548924029) was manually dispatched once on 2026-11-14. It tested `9bbbe0b69ed23689d12c9a2603588d9e244787e5` on `codex/nonfunctional-testing-2026-11`, started at 2026-11-14T02:55:41Z and completed at 2026-11-14T03:07:06Z. It ran the full workflow matrix on separate ephemeral databases: Android 10/API 29 (Nexus 5, 360×640 dp) and Android 14/API 34 (Pixel 7 Pro, 411×891 dp).

- API 29 job [132306614108](https://github.com/lucifron28/UniPM/actions/runs/43548924029/job/132306614108); artifact [android-api-29-evidence](https://github.com/lucifron28/UniPM/actions/runs/43548924029/artifacts/14095748055) (1,245,429 bytes).
- API 34 job [132306613827](https://github.com/lucifron28/UniPM/actions/runs/43548924029/job/132306613827); artifact [android-api-34-evidence](https://github.com/lucifron28/UniPM/actions/runs/43548924029/artifacts/14095218129) (3,359,541 bytes).
- Both jobs passed Flutter setup/package resolution, isolated SQL Server/API seed, APK build, emulator profile checks, install/startup, visible sign-in, and the four app-configuration tests. Both uploaded their evidence artifacts. Both job conclusions were failure at `Run emulator compatibility and functional smoke checks`.

## Run 3 profile outcomes

| Check | Android 10 / API 29, Nexus 5 | Android 14 / API 34, Pixel 7 Pro |
|---|---|---|
| Build, install, startup, visible sign-in, configuration tests | PASSED; configuration 4/4 | PASSED; configuration 4/4 |
| Synthetic login, authenticated navigation, dashboard, asset search/details | PASSED | PASSED |
| History API and seeded acknowledged record | PASSED — HTTP 200; inspection 34000000-0000-4000-8000-000000000001 and expected remarks matched | PASSED — HTTP 200; same seeded inspection and remarks |
| Visible maintenance-history record | PASSED | PASSED |
| Return from history to asset details | PASSED — test continued to the next dashboard scenario | FAILED at the test wait for the History action; screenshot shows the app already returned to Asset lookup/details |
| PM schedule display | FAILED at test wait for the `PM cycle:` summary; schedule/form reads returned HTTP 200, but the UI summary was not built in the current viewport | NOT EXECUTED after the history-return wait failure |
| Inspection form entry, save, reopen, edit, server verification | NOT EXECUTED | NOT EXECUTED |
| Offline draft creation and local persistence before reconnect | NOT EXECUTED | NOT EXECUTED |
| App restart, offline snapshot verification, reconnection and server synchronization | NOT EXECUTED | NOT EXECUTED |
| Controlled synchronization conflict | NOT EXECUTED; no conflict scenario is implemented in this smoke | NOT EXECUTED; no conflict scenario is implemented in this smoke |
| Job conclusion | FAILED | FAILED |

### Run 3 failure diagnosis

**API 29 — PM schedule wait missed a lazy list child.** After the test returned from history and opened `DEMO-EL-003`, the screen showed the asset-details page. The test immediately waited for `find.textContaining('PM cycle:')` (`integration_test/nonfunctional_smoke_test.dart`, schedule-summary wait) without scrolling the asset-details list. The 360×640 dp screenshot `dashboard-pm-schedule-failure.png` and its window hierarchy show the viewport at the asset card; the widget diagnostics identify the asset details' outer `ListView`, while the PM section below that card was not yet built. The API log records HTTP 200 for `GET /api/v1/schedules?assetId=30000000-0000-4000-8000-000000000003` and the forms read. The exact failure was `Timed out waiting for preventive-maintenance schedule summary.` This is a test-side lazy-list/scroll omission; the schedule UI was not verified and no application/API failure was established.

**API 34 — history return reached the details route, but its History control was offscreen.** The history endpoint returned HTTP 200 and the expected acknowledged record was visible. After `tester.pageBack()`, the failure screenshot `asset-history-failure.png`, widget diagnostics, and Android window hierarchy show `Asset lookup`, `DEMO-EL-001`, and its completed June 2026 schedule with the expected “no existing Draft inspection row to resume” state. The test then timed out waiting for the `view-asset-history` key without first scrolling the current lazy details list. The page route had returned successfully; the History action below the visible PM content had not been built. This is a test-side visibility assertion failure, not an application navigation failure.

Both profiles' recorded API requests returned HTTP 200; no failed API status or UniPM process crash/ANR was found. The failure wrapper captured Flutter widget diagnostics, screenshots, and window hierarchy before teardown. The API 29 artifact contains `integration-29.log`, `api-request-status.log`, `dashboard-pm-schedule-failure.png`, and `dashboard-pm-schedule-failure-window.xml`; API 34 contains `integration-34.log`, `api-request-status.log`, `asset-history-failure.png`, and `asset-history-failure-window.xml`. Extracted raw files remain under the ignored `artifacts/nonfunctional-android/run-43548924029/` directory; no raw logs or screenshots were committed.

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

The failure wrapper logs visible text, Flutter widget and render-tree diagnostics, then captures an in-app screenshot and Android hierarchy before teardown. Runs 2 and 3 captured these diagnostics for their failures, and the Run 3 artifacts were inspected. A controlled synchronization-conflict scenario is not implemented and remains NOT EXECUTED. Source review describes the intended checks; only the run-specific outcomes above count as execution evidence.

## Prepared lazy-list correction — source commit b3e82c43e78789d582656a44084db2a34b6df9c2 (exercised by Run 4)

The test-only follow-up was committed and pushed to codex/nonfunctional-testing-2026-11 as b3e82c43e78789d582656a44084db2a34b6df9c2. At that stage it was a source candidate, not a tested commit. The later runtime execution is recorded in Run 4 below.

The follow-up adds a shared bounded scrolling helper that scopes to the visible Scaffold's vertical scrollable (and the dashboard RefreshIndicator when present), uses `scrollUntilVisible` with a viewport-relative delta and at most 24 attempts, and verifies the target's rendered bounds and hit-test path before tapping. The history-return assertion now checks the visible `Asset lookup` app-bar title. PM schedule verification scrolls to the keyed schedule panel and compares the visible cycle and due date against the eligible `DEMO-EL-003` schedule returned by the API. Form actions, fields, save/reopen/edit, offline entry, and synchronization controls use the same visibility/hit-test checks. The prepare phase now has independent history, online-form, and offline-draft test cases; the runner can continue to restart/sync evidence after an earlier case failure only when offline reachability and the persisted-draft completion marker are present, while retaining the overall failure.

Static validation on the prepared source completed as follows: `git diff --check` passed; Dart format verification reported one file unchanged; `dart analyze integration_test/nonfunctional_smoke_test.dart` exited 0 with no errors and 32 informational lints. At that preparation stage, runner shell syntax validation was not completed: Git Bash failed to initialize its MSYS named object with `NtCreateDirectoryObject(...): 0xC0000022`, and WSL was unavailable. The later preflight attempt is recorded below. No integration/device test, build, API exercise, screenshot capture, or workflow execution was performed for this source commit. Therefore these changes have not yet established that the lazy-list fixes work on either Android profile or that later form/offline scenarios pass.

## Authorized rerun preflight — commit e8f0e9053aefa01b6c1e892a5ca1b8916ba24ea2 (blocked before dispatch)

At that point the requested single workflow dispatch had not been made. The branch was at commit e8f0e9053aefa01b6c1e892a5ca1b8916ba24ea2; bash -n passed under Git for Windows Bash/MSYS, but WSL/Linux and Docker were unavailable, and gh auth status reported an invalid token. That preflight was later superseded by the one authorized push-triggered run 43565671645, documented in Run 4 below.

| Required scenario | Android 10 / API 29 | Android 14 / API 34 |
|---|---|---|
| Installation, startup and authentication | NOT EXECUTED — dispatch blocked | NOT EXECUTED — dispatch blocked |
| Asset search and details | NOT EXECUTED — dispatch blocked | NOT EXECUTED — dispatch blocked |
| Maintenance history and return navigation | NOT EXECUTED — dispatch blocked | NOT EXECUTED — dispatch blocked |
| PM schedule display | NOT EXECUTED — dispatch blocked | NOT EXECUTED — dispatch blocked |
| Inspection form open/save/reopen/edit | NOT EXECUTED — dispatch blocked | NOT EXECUTED — dispatch blocked |
| Offline Draft persistence after process restart | NOT EXECUTED — dispatch blocked | NOT EXECUTED — dispatch blocked |
| Reconnection, synchronization and server-state verification | NOT EXECUTED — dispatch blocked | NOT EXECUTED — dispatch blocked |

This was an execution-environment/authentication block at that point, not an Android test result. Run 43548924029 remained the latest execution until the later Run 4 below; its earlier artifacts and results remain unchanged.

## Evidence preservation and limits

TEST-198, TEST-199, TEST-200 and their records remain unchanged. The TEST-201 compile failure and both later interaction runs are preserved above. No production application logic was modified. Run 3 was the single authorized dispatch for commit `9bbbe0b69ed23689d12c9a2603588d9e244787e5`; no additional workflow was dispatched.

TEST-201 remains incomplete. The history record and display passed on both profiles. API 29 failed a test-side lazy PM-summary wait; API 34 failed a test-side wait for the offscreen History control even though the details route was visible. Inspection form entry/save/reopen/edit, offline persistence after restart, and reconnection synchronization were NOT EXECUTED on either profile. The source-only correction at `b3e82c43e78789d582656a44084db2a34b6df9c2` is prepared but unexecuted; no production defect was confirmed. Android compatibility must not be reported as passed.


## Run 4 — push-triggered TEST-201 matrix

Run [43565671645](https://github.com/lucifron28/UniPM/actions/runs/43565671645) was the single authorized execution. It was triggered by a push to codex/nonfunctional-testing-2026-11 and tested commit 58b17b355aa02425c2610076c76800b3d0413693. It started at 2026-11-14T06:18:12Z and completed at 2026-11-14T06:30:10Z. The trigger commit changed only a comment in the watched workflow file; no production or integration-test source was changed for this execution. The Linux Bash syntax preflight passed before emulator execution in both jobs.

Both isolated API profiles passed ephemeral database/API setup and synthetic seeding, APK build, emulator profile checks, installation, visible sign-in startup, and the four Flutter configuration tests. Both jobs failed at Run emulator compatibility and functional smoke checks and uploaded evidence.

| Check | Android 10 / API 29 — Nexus 5, 360×640 dp | Android 14 / API 34 — Pixel 7 Pro, 411×891 dp |
|---|---|---|
| Installation, startup, sign-in, configuration tests | PASSED; configuration 4/4 | PASSED; configuration 4/4 |
| Synthetic login, authenticated navigation, dashboard | PASSED | PASSED |
| Asset search and details | FAILED / PARTIAL — the asset-history case failed before tap; later API logs show DEMO-EL-003 search/QR responses at HTTP 200, but no separate passing UI assertion was retained | PASSED for DEMO-EL-001, including the asset-details screen |
| Maintenance history and return navigation | NOT EXECUTED; asset selection failed first | PASSED; history GET returned HTTP 200, seeded inspection 34000000-0000-4000-8000-000000000001 and expected remarks matched, record was visible, and return to Asset lookup passed |
| DEMO-EL-003 PM schedule fixture and visible schedule | BLOCKED; schedule reads returned HTTP 200, but no visible-cycle or due-date pass was recorded | BLOCKED; fixture check confirmed the assigned Due schedule, but the dashboard search interaction failed before the schedule UI was opened |
| Inspection form open, save, reopen, edit, server verification | FAILED / INCOMPLETE; Flutter listed the online-form test as failed. API logs include form POST 201 and start-inspection POST 200, but no verified saved-row, reopen, edit, or final server-state result was captured | NOT EXECUTED; the online-form test failed before asset details or form entry, and no form-create/start request was recorded |
| Offline draft entry and local persistence before restart | BLOCKED; the Flutter runner listed the test as failed, no offline-network request was observed, and local persistence was not verified | BLOCKED; the Flutter runner listed the test as failed, no offline-network request was observed, and local persistence was not verified |
| Process restart, reconnection, synchronization and server-state verification | NOT EXECUTED; restart phase was not executed | NOT EXECUTED; restart phase was not executed |
| Controlled synchronization conflict | NOT EXECUTED; no conflict case is implemented | NOT EXECUTED; no conflict case is implemented |
| Workflow job | FAILED | FAILED |

### Run 4 failure diagnosis

The first recorded API 29 failure was in asset-history at stage asset-search-result. The assertion was “Open asset search center is outside its visible scroll viewport.” The target bounds were x=20–340, y=374–448; the selected scroll viewport ended at y=343, while the Flutter render viewport ended at y=592. The stack points to _verifyHitTestPosition, _scrollUntilVisible, _tapAfterScrolling, and _openAssetFromDashboard in integration_test/nonfunctional_smoke_test.dart. The failure occurred before tester.tap, asset selection, or the history request. The Flutter report listed all three prepare tests as failed; it did not retain a separate SMOKE_FAILURE diagnostic for the online and offline cases. API logs show some later online-form requests, but those requests alone do not prove the UI lifecycle assertions.

API 34 passed the asset-history test, including the seeded acknowledged record and return navigation. Its next online-inspection test confirmed the synthetic DEMO-EL-003 schedule was Due and assigned to the smoke user, then failed at dashboard-pm-schedule with the same assertion before opening asset search. The target bounds were x=20–391.4, y=572.4–644.4; the selected scroll viewport ended at y=555.4, while the render viewport ended at y=867.4. The Flutter report listed the offline test as failed too, but no separate failure marker was retained for that case.

Flutter debugDumpApp and debugDumpRenderTree show the Search Assets ListTile beneath the dashboard HomePage RefreshIndicator, ListView and page-level Viewport (512 dp high on API 29 and 770.3 dp on API 34). The helper instead checked smaller viewport rectangles ending at y=343 and y=555.4. The screenshots show the action inside the render viewport. This is a test-side scrollable-selection/bounds defect: failure occurs before tap, so it does not establish a navigation, API, or production defect. Both API request logs contain 32 requests and no HTTP 4xx/5xx responses. API 34 recorded the history route twice with HTTP 200. API 29 recorded successful form-create/start requests but no history request.

The runner summary marks emulator network toggling as not requested, offline persistence before reconnect as not executed, and the restart phase as not executed on both profiles. The controlled offline draft and synchronization workflow therefore remains unverified. The screenshots and Flutter widget/render diagnostics were captured before application teardown.

- API 29 job [132358863068](https://github.com/lucifron28/UniPM/actions/runs/43565671645/job/132358863068); artifact [android-api-29-evidence, 1,435,042 bytes](https://github.com/lucifron28/UniPM/actions/runs/43565671645/artifacts/14102497268). Reviewed files: asset-history-asset-search-result-failure.png, asset-history-asset-search-result-failure-window.xml, integration-29-prepare.log, api-request-status.log, api-server.log, logcat.txt, and summary.json.
- API 34 job [132358863365](https://github.com/lucifron28/UniPM/actions/runs/43565671645/job/132358863365); artifact [android-api-34-evidence, 3,544,108 bytes](https://github.com/lucifron28/UniPM/actions/runs/43565671645/artifacts/14102302153). Reviewed files: online-inspection-dashboard-pm-schedule-failure.png, online-inspection-dashboard-pm-schedule-failure-window.xml, integration-34-prepare.log, api-request-status.log, api-server.log, logcat.txt, and summary.json.

The smallest justified follow-up is test-only: select the actual vertical Scrollable ancestor of search-assets, or use the already-visible-on-screen tap helper after checking hit-test bounds against the render viewport. Keep the existing history and form assertions. No correction or rerun is included here; the single-run authorization has been consumed. TEST-198, TEST-199, TEST-200, and all earlier TEST-201 run records remain preserved. Android compatibility is incomplete.
