# Android compatibility test matrix

## Required emulator configurations

The GitHub Actions workflow defines two portrait configurations:

| Requested profile | Emulator profile | Observed display | Logical viewport | Workflow job |
|---|---|---:|---:|---|
| Android 10 / API 29, small | Nexus 5 | 1080×1920 at 480 dpi | 360×640 dp | [latest TEST-201 API 29 job, run 43565671645](https://github.com/lucifron28/UniPM/actions/runs/43565671645/job/132358863068) |
| Android 14 / API 34, large | Pixel 7 Pro | 1440×3120 at 560 dpi | 411×891 dp | [latest TEST-201 API 34 job, run 43565671645](https://github.com/lucifron28/UniPM/actions/runs/43565671645/job/132358863365) |

Initial matrix run: Ubuntu 24.04, Java 21, Flutter 3.47.1 stable, x86_64 software-rendered emulators, and no KVM acceleration. Exact source commit: 530b5be36727698f3e66935e56e53c698e4df52b. Run: [43296932992](https://github.com/lucifron28/UniPM/actions/runs/43296932992), triggered 2026-11-12T20:06:20Z and completed successfully. In Asia/Manila the run occurred on 2026-11-13.

## Previous completed startup run — 43296932992

| Check | Android 10 / API 29 | Android 14 / API 34 |
|---|---|---|
| Debug APK build | **PASS** — 222.6 s | **PASS** — 231.2 s |
| APK installation | **PASS** — `adb install` returned `Success` | **PASS** — `adb install` returned `Success` |
| Activity/process start | **PARTIAL** — `am start -W` returned `Status: timeout` but reported `MainActivity`; app process PID 5239 was present | **PARTIAL** — `am start -W` returned `Status: timeout` but reported `MainActivity`; app process PID 4623 was present |
| Startup screenshot / visible app UI | **FAIL** — screenshot shows “System UI isn't responding” over a blank screen; UniPM controls are not visible | **FAIL** — screenshot shows “Pixel Launcher isn't responding” over a blank screen; UniPM controls are not visible |
| Focused `test/app_config_test.dart` | **PASS** — 4/4 | **PASS** — 4/4 |
| GitHub Actions job conclusion | **PASS** — scripted workflow completed | **PASS** — scripted workflow completed |
| Login, authenticated navigation, asset/form/history flows | **NOT EXECUTED** | **NOT EXECUTED** |
| Overflow/responsive content review | **BLOCKED** — app content is not visible in the captured screen | **BLOCKED** — app content is not visible in the captured screen |
| Offline persistence, reconnect synchronization, live errors/conflicts | **NOT EXECUTED** | **NOT EXECUTED** |

The successful job conclusion means that the APK built and installed, the `MainActivity` process was present, a non-empty screenshot was captured, and the four app-configuration tests passed. It does **not** establish that the UniPM startup screen rendered correctly: both real screenshots show an Android launcher/system UI not-responding dialog and no app controls. The screenshots do not identify the cause, so the evidence does not attribute this behavior to the app or to the emulator. The requested configurations were exercised, but visual startup and functional compatibility remain incomplete pending a responsive rendered screen.

- [API 29 startup screenshot](android-workflow-runs/432969/api29/startup-29.png) · [helper log](android-workflow-runs/432969/api29/run-29.log) · [full Actions job log](android-workflow-runs/432969/api29/actions-job.log)
- [API 34 startup screenshot](android-workflow-runs/432969/api34/startup-34.png) · [helper log](android-workflow-runs/432969/api34/run-34.log) · [full Actions job log](android-workflow-runs/432969/api34/actions-job.log)

The focused Flutter tests check development/release API URL configuration only. They are automated unit/configuration checks run with the emulator selected; they do not sign in or exercise server-backed workflows. The Actions workflow does not start the API or create a test account.

## Earlier workflow attempts

The earlier runs are retained in the evidence directory; they explain the workflow harness corrections and are not substituted for the latest results.

| Run and tested commit | API 29 | API 34 |
|---|---|---|
| [43284530284](https://github.com/lucifron28/UniPM/actions/runs/43284530284), `87069560a29c65714dfd8898892786bcb5e208bf` | APK built; install timed out after 3 minutes; no launch/screenshot/tests | APK built and ADB push started; install timed out after 3 minutes; no screenshot/tests |
| [43288290256](https://github.com/lucifron28/UniPM/actions/runs/43288290256), `e0baa9b30bf9198eb765cefde974c1861fa2e1f9` | Install succeeded; `am start` reported timeout and `MainActivity`; helper stopped before process/screenshot/config tests | Install timed out after 8 minutes; no screenshot/tests |
| [43293011562](https://github.com/lucifron28/UniPM/actions/runs/43293011562), `37fa6a5c57a77290a1db4425d18a8a9509a2e8a7` | Install and process start observed; screenshot attempt failed because `/sdcard` capture returned permission/timeout errors; focused tests did not run | Install and process start observed; screenshot attempt failed with permission denied; focused tests did not run |
| [43296932992](https://github.com/lucifron28/UniPM/actions/runs/43296932992), `530b5be36727698f3e66935e56e53c698e4df52b` | Build, install, process check, screenshot capture, and four config tests completed; screenshot visually failed as described above | Build, install, process check, screenshot capture, and four config tests completed; screenshot visually failed as described above |

Each earlier API helper log is retained under `android-workflow-runs/<run-id>/api29/` and `api34/`. The GitHub Actions run links remain the source for the corresponding workflow records.


## Latest workflow rerun — 43363392229

The rerun tested commit 2fa502628a21e82686920a46680706b3ce0c68ba and completed 2026-11-13T04:47:20Z with overall failure. See the TEST-195 record and the [full workflow run](https://github.com/lucifron28/UniPM/actions/runs/43363392229).

| Check | Android 10 / API 29 — Nexus 5 small | Android 14 / API 34 — Pixel 7 Pro large |
|---|---|---|
| Emulator profile | Android 10, API 29; 1080×1920 px, 480 dpi; 360×640 dp. Screen-profile check passed. | Android 14, API 34; 1440×3120 px, 560 dpi; 411×891 dp. Screen-profile check passed. |
| APK build | Passed; APK produced. | Passed in 336.2 s. |
| Install / activity | Blocked: ADB reported device emulator-5554 not found before installation. | Install succeeded; MainActivity launch returned Status: ok. |
| Visible screen | No app screen; final screenshot is empty. | Startup smoke passed: sign-in page and controls were visible. Screenshots are startup-login.png and final-34.png. |
| App configuration tests | Not executed after ADB loss. | Passed 4/4. |
| Authenticated integration | Not executed. | Failed/timed out after Flutter Driver connected and started the test; request_data stalled until exit code 124. The test did not log login_screen=PASS and did not submit credentials. |
| Asset/history/forms and authenticated navigation | Not executed. | Not executed; the integration did not reach authentication. |
| Offline drafts, reconnect sync, conflicts/errors | Not executed. | Not executed. |
| Job conclusion | Failure. | Failure. |

The API 34 screenshot verifies rendering of the sign-in page within its recorded 411×891 dp viewport only. It does not establish an authenticated or operational-page layout. API 29 was not installed or launched in this rerun. The earlier API 29 screenshot from run 43296932992 shows a System UI not-responding dialog; the previous API 34 launcher screenshot is superseded by the current visible sign-in screenshot.

Full logs and artifacts for the rerun are retained in android-workflow-runs/43363392229/api29/ and api34/, including summaries, helper logs, Actions logs, emulator diagnostics, configuration test output, integration timeout output, and screenshots. Artifact links: [API 29](https://github.com/lucifron28/UniPM/actions/runs/43363392229/artifacts/14009432873) and [API 34](https://github.com/lucifron28/UniPM/actions/runs/43363392229).

## API 29 ADB recovery retest — 43391036513

Run [43391036513](https://github.com/lucifron28/UniPM/actions/runs/43391036513) tested 7b53e82126d2a6218bcacf92596285431f2740d8 and completed at 2026-11-13T08:48:40Z. The API 29 emulator log briefly reported an unavailable ADB daemon; the daemon started, bounded readiness checks observed Android 10 boot-complete, and install succeeded. KVM v12 was usable on the four-CPU Ubuntu 24.04.5 runner. The original transport-loss trigger remains uncertain.

| Check | Android 10 / API 29 — Nexus 5 | Android 14 / API 34 — Pixel 7 Pro |
|---|---|---|
| Profile | 1080×1920, 480 dpi, 360×640 dp. Passed. | 1440×3120, 560 dpi, 411×891 dp. Passed. |
| Build / install | Passed / passed. | Passed / passed. |
| Activity launch | Status: timeout, shell exit 0. | Status: ok. |
| Sign-in UI | PASS — foreground hierarchy matched required controls; screenshot captured. | NOT VERIFIED — controls were not found. |
| Configuration tests | 4/4 passed. | Not executed. |
| Authenticated smoke | BLOCKED — Flutter Driver request_data timed out after 30 minutes before login. | Not executed. |
| Remaining workflows / offline | Not executed. | Not executed. |

API 29 was no longer blocked at installation; API 34 installed and launched but did not pass the UI check. See [TEST-196](../../evidence/test-runs/TEST-196-nonfunctional-android-api29-compatibility-retest-2026-11.md), [API 29 artifact](https://github.com/lucifron28/UniPM/actions/runs/43391036513/artifacts/14022946828), and [API 34 artifact](https://github.com/lucifron28/UniPM/actions/runs/43391036513/artifacts/14020893378).

## Follow-up using on-device integration tests — 43396819069

Run [43396819069](https://github.com/lucifron28/UniPM/actions/runs/43396819069) tested 937c59173caa57faf9fe1ed02ed6879691b6cba9 and completed at 2026-11-13T09:04:04Z. The workflow uses flutter test integration_test/nonfunctional_smoke_test.dart on the selected Android device and captures startup/final screenshots through ADB. This follows Flutter's [Android integration testing guide](https://docs.flutter.dev/testing/integration-tests).

| Check | Android 10 / API 29 — Nexus 5 | Android 14 / API 34 — Pixel 7 Pro |
|---|---|---|
| Profile / install / launch | Passed; 1080×1920, 480 dpi, 360×640 dp; Status: ok. | Passed; 1440×3120, 560 dpi, 411×891 dp; Status: ok. |
| Sign-in UI / screenshot | PASS — foreground controls verified; screenshot saved. | PASS — foreground controls verified; screenshot saved. |
| Configuration tests | 4/4 passed. | 4/4 passed. |
| Synthetic login / navigation | PASS — markers emitted. | PASS — markers emitted. |
| Dashboard smoke | PASS — no Flutter rendering errors. | PASS — no Flutter rendering errors. |
| Asset search | PASS — DEMO-EL-001 result found and selected. | PASS — result found and selected. |
| Asset details | FAIL — timed out waiting for asset-code within 30 seconds. | FAIL — same timeout. |
| History / inspection forms / offline / reconnect | Not executed. | Not executed. |

The final failure occurred after Android boot, install, UI rendering, and authenticated navigation, so it is a functional smoke failure rather than an ADB or installation blocker. Console evidence does not distinguish an app route/data problem from a selector mismatch; no cause is claimed. Artifacts: [API 29](https://github.com/lucifron28/UniPM/actions/runs/43396819069/artifacts/14023631972) · [API 34](https://github.com/lucifron28/UniPM/actions/runs/43396819069/artifacts/14023742476). The artifacts include screenshots, hierarchy dumps, ADB/emulator diagnostics, logcat, and test/backend output.

## Previous executed field-work smoke — TEST-200 / 43444880972

Run [43444880972](https://github.com/lucifron28/UniPM/actions/runs/43444880972) tested `523cce1898956434c70d50ebb6b1142be459c7a8` on `codex/nonfunctional-testing-2026-11`. It is recorded in [TEST-200](../../evidence/test-runs/TEST-200-nonfunctional-android-smoke-rerun-2026-11.md). Each matrix job provisioned its own fresh ephemeral SQL database and synthetic fixture.

| Check | Android 10 / API 29 — Nexus 5 | Android 14 / API 34 — Pixel 7 Pro |
|---|---|---|
| Install, visible sign-in, configuration tests | **PASSED**; configuration 4/4 | **PASSED**; configuration 4/4 |
| Synthetic login, authenticated navigation, dashboard, asset search/details | **PASSED** | **PASSED** |
| Maintenance history | **FAILED in test interaction** — `ensureVisible(view-asset-history)` raised `Bad state: No element` because the action was in a lazy details list and had not been built. History was not tapped; no history API request occurred. | **PASSED** — history opened, the seeded record rendered, and the history API returned 200. |
| PM schedule | **NOT EXECUTED** after the history interaction failure. | **PASSED** — schedule summary rendered. |
| Start/Resume and form save/reopen | **NOT EXECUTED**. | **BLOCKED by test fixture selection** — the test used `DEMO-EL-001`, whose schedule was Completed and whose acknowledged inspection had no Draft row, so the product correctly exposed no Start/Resume action. |
| Offline draft persistence and reconnection sync | **NOT EXECUTED** | **NOT EXECUTED** |
| Job conclusion | **FAILED** | **FAILED** |

This run shows the earlier asset-details timeout did not persist: both API levels reached and passed the asset-details check. TEST-200 does not establish what caused the earlier timeout in run 43396819069. The confirmed TEST-200 failures are in test interaction and fixture selection; source evidence does not show a production defect. Full details and artifacts are linked from TEST-200.

## TEST-201 initial execution — run 43468215938

Run [43468215938](https://github.com/lucifron28/UniPM/actions/runs/43468215938) tested commit db04aed6d6079a81d45b32b811750ef24c38643e on codex/nonfunctional-testing-2026-11. It was the single authorized workflow_dispatch and completed at 2026-11-13T17:12:04Z. The exact profiles were Android 10/API 29 on Nexus 5 and Android 14/API 34 on Pixel 7 Pro. The jobs used separate ephemeral SQL Server instances and synthetic fixtures.

| Check | Android 10 / API 29 — Nexus 5 | Android 14 / API 34 — Pixel 7 Pro |
|---|---|---|
| Ephemeral database, API build and synthetic seed | PASSED | PASSED |
| UniPM APK build/install, startup, visible login and configuration tests | PASSED; configuration 4/4 | PASSED; configuration 4/4 |
| Integration-test compile/load | FAILED — duplicate `offlineRemarks` declaration in one scope | FAILED — same compile error |
| Authenticated functional assertions | NOT EXECUTED | NOT EXECUTED |
| Asset details, history, schedule, forms and offline/reconnect | NOT EXECUTED | NOT EXECUTED |
| Job result | FAILED | FAILED |

The workflow uploaded [API 29 evidence](https://github.com/lucifron28/UniPM/actions/runs/43468215938/artifacts/14061360039) and [API 34 evidence](https://github.com/lucifron28/UniPM/actions/runs/43468215938/artifacts/14060386895). Initial artifact retrieval was blocked by HTTP 503 account egress, but a later supported retrieval confirmed the duplicate-local compilation error recorded in TEST-201. No functional smoke assertion ran in this execution.

The source review confirms that TEST-201 uses the lazily scrolled History action and checks the seeded acknowledged record; it uses DEMO-EL-003 for online Start/save/reopen/edit/server verification and separate DEMO-FE-003 for offline work. Each job has an isolated seeded database. After force-stop, the second phase remains offline, reads the encrypted persisted snapshot for the synthetic user, checks the Draft, row and two pending writes, and only then requests reconnection. The runner gates restoration on that check. The run result does not establish that these UI checks were reached.

The compile failure was corrected in the next TEST-201 source revision. Its later execution history and the TEST-200 findings remain in the TEST-201 record; TEST-198, TEST-199 and TEST-200 source records and artifacts are unchanged.

## TEST-201 controlled interaction rerun — run 43548924029

Run [43548924029](https://github.com/lucifron28/UniPM/actions/runs/43548924029) was the single manual workflow dispatch authorized for commit `9bbbe0b69ed23689d12c9a2603588d9e244787e5` on `codex/nonfunctional-testing-2026-11`. It started at 2026-11-14T02:55:41Z and completed at 2026-11-14T03:07:06Z. Each profile used a separate ephemeral SQL Server/API and synthetic data. Both jobs passed setup, database/API seed, APK build, emulator startup/profile check, install, visible login and four configuration tests. Both uploaded artifacts, then failed in the emulator compatibility/functional smoke step.

| Check | Android 10 / API 29 — Nexus 5, 360×640 dp | Android 14 / API 34 — Pixel 7 Pro, 411×891 dp |
|---|---|---|
| Build/install/startup/sign-in/configuration tests | PASSED; configuration 4/4 | PASSED; configuration 4/4 |
| Synthetic login, authenticated navigation, dashboard, asset search and asset details | PASSED | PASSED |
| History API, seeded acknowledged inspection and visible history record | PASSED — HTTP 200; expected ID and remarks matched | PASSED — HTTP 200; expected ID and remarks matched |
| Return from history to asset details | PASSED; test proceeded to the next dashboard scenario | FAILED at the test-side wait for `view-asset-history`; screenshot shows the app already returned to the details route |
| PM schedule display | FAILED at the test-side wait for the `PM cycle:` summary; schedule/form reads returned 200, but the UI assertion was not observed | NOT EXECUTED after the history-return assertion failure |
| Inspection form entry, save, reopen, edit and server verification | NOT EXECUTED | NOT EXECUTED |
| Offline draft creation and persistence through app restart | NOT EXECUTED | NOT EXECUTED |
| Reconnection, synchronization and server-state verification | NOT EXECUTED | NOT EXECUTED |
| Controlled synchronization conflict | NOT EXECUTED; scenario is not implemented in this smoke | NOT EXECUTED; scenario is not implemented in this smoke |
| Job conclusion | FAILED | FAILED |

- API 29 job [132306614108](https://github.com/lucifron28/UniPM/actions/runs/43548924029/job/132306614108), artifact [android-api-29-evidence](https://github.com/lucifron28/UniPM/actions/runs/43548924029/artifacts/14095748055) (1,245,429 bytes). Reviewed failure screenshot: `dashboard-pm-schedule-failure.png`; widget diagnostics and Android window hierarchy are in the artifact.
- API 34 job [132306613827](https://github.com/lucifron28/UniPM/actions/runs/43548924029/job/132306613827), artifact [android-api-34-evidence](https://github.com/lucifron28/UniPM/actions/runs/43548924029/artifacts/14095218129) (3,359,541 bytes). Reviewed failure screenshot: `asset-history-failure.png`; widget diagnostics and Android window hierarchy are in the artifact.

Both failures are test-side lazy-list visibility errors, not confirmed app failures. On API 29, the test waited for a PM summary before scrolling the details `ListView`; the asset screen and HTTP 200 reads were present, while the PM list child was not yet built. On API 34, the pageBack navigation succeeded, but the test waited for the History control without scrolling the current details list; the control below the visible completed-schedule content was not built. The failures and screenshots were captured before teardown. No UniPM crash/ANR or failed API status was found. Form save/reopen/edit, offline persistence, app restart, reconnection/sync and conflict handling remain unverified. Raw artifact copies are under ignored `artifacts/nonfunctional-android/run-43548924029/`; no raw artifacts were committed.

## Supplemental Android 17 / API 37 smoke

A separate local smoke used an Android 17/API 37 emulator and synthetic development data. This evidence is supplemental and does not replace API 29/API 34 results.

| Check | Result |
|---|---|
| Install, sign-in screen and login | Passed; authenticated Maintenance Inspector navigation observed. The home screenshot still had loading task panels. |
| Asset lookup | Passed; synthetic CCMS asset search and asset details opened. |
| Maintenance history | Passed; two acknowledged synthetic records displayed. |
| PM schedule selection | Passed up to schedule details; the draft form list stayed on a loading state. |
| PM form entry | Blocked; an authenticated read-only form-list request timed out after 20,046 ms. No form was entered or submitted. |
| Offline draft persistence/reconnect sync | Not executed on device. |
| Live conflict/error handling | Not executed on device. |
| Full Flutter suite | 198/198 passed on API 37; tests for drafts, history, offline behavior and errors use fakes/test doubles. |

Details, logs references, and actual screenshots are in [the API 37 smoke record](android-api37-live/README.md). The two API 37 viewport screenshots show only the sign-in screen and do not establish operational-page overflow behavior.

## Compatibility boundary and reproduction

`mobile/android/app/build.gradle.kts` relies on Flutter's Android SDK defaults; with Flutter 3.47.1, the resolved minimum SDK is API 24, and compile/target SDK are API 36. Run 43548924029 verified build/install, visible login, configuration tests, authenticated navigation, asset details and history display on both requested Android profiles. The two jobs then stopped at test-side lazy-list waits: API 29 PM-summary visibility and API 34 history-control visibility. No claim is made for PM schedule UI completion on API 29, inspection form behavior, offline persistence, process restart, reconnection synchronization or conflict handling.

To repeat the workflow after pushing this branch:

```powershell
gh workflow run nonfunctional-android-compatibility.yml --ref codex/nonfunctional-testing-2026-11
gh run list --workflow nonfunctional-android-compatibility.yml --branch codex/nonfunctional-testing-2026-11
```

The existing run can be inspected with:

```powershell
gh run view 43296932992 --log
gh run download 43296932992 --name android-api-29-evidence
gh run download 43296932992 --name android-api-34-evidence
```

Do not report these results as full Android compatibility acceptance. The latest TEST-201 run is 43565671645 on commit 58b17b355aa02425c2610076c76800b3d0413693. It passed history on API 34, but failed the test helper viewport check before API 29 asset selection and before API 34 PM schedule UI. Form lifecycle, offline persistence after restart, and reconnection synchronization remain incomplete; no production defect was confirmed.

## Prepared TEST-201 lazy-list correction — source commit b3e82c43e78789d582656a44084db2a34b6df9c2 (later exercised in Run 43565671645)

After run 43548924029, a test-only correction was committed and pushed to `codex/nonfunctional-testing-2026-11` at `b3e82c43e78789d582656a44084db2a34b6df9c2`. This is not a tested commit: no workflow was dispatched and no emulator/API/screenshot evidence was produced for it. The last executed commit remains `9bbbe0b69ed23689d12c9a2603588d9e244787e5`.

The prepared test scopes a bounded `scrollUntilVisible` helper to the active screen's vertical scrollable, checks that targets are inside the rendered viewport and hit-testable, verifies history return by the visible `Asset lookup` app-bar title, and scrolls to the PM schedule before comparing the displayed cycle and due date with the `DEMO-EL-003` API fixture. Form controls and the offline/sync interaction path use the same helper. History, online form, and offline-draft setup are separate integration test cases. The runner may continue restart/synchronization checks after an earlier prepare case fails only after it observes verified backend disconnection and the offline-persistence completion marker; it continues to report the overall prepare phase as failed.

Source validation for the prepared test passed git diff --check, Dart format verification, and dart analyze with zero errors and 32 informational lints. The authorized run 43565671645 later exercised this source; its runtime result is documented below.

## Authorized run preflight — commit e8f0e9053aefa01b6c1e892a5ca1b8916ba24ea2 (not dispatched)

At this point the requested single workflow dispatch had not been made. The branch was at commit e8f0e9053aefa01b6c1e892a5ca1b8916ba24ea2; bash -n passed under Git for Windows Bash/MSYS, but WSL/Linux and Docker were unavailable, and gh auth status reported an invalid token. That preflight was later superseded by the one authorized push-triggered run 43565671645, documented in the latest section.

| Required scenario | Android 10 / API 29 | Android 14 / API 34 |
|---|---|---|
| Installation, startup and authentication | NOT EXECUTED | NOT EXECUTED |
| Asset search and details | NOT EXECUTED | NOT EXECUTED |
| Maintenance history and return navigation | NOT EXECUTED | NOT EXECUTED |
| PM schedule display | NOT EXECUTED | NOT EXECUTED |
| Inspection form open/save/reopen/edit | NOT EXECUTED | NOT EXECUTED |
| Offline Draft persistence after process restart | NOT EXECUTED | NOT EXECUTED |
| Reconnection, synchronization and server-state verification | NOT EXECUTED | NOT EXECUTED |

These were blocked-before-dispatch outcomes at that point in the evidence history, not Android failures. The later push-triggered run is recorded below; Run 43548924029 remains preserved as the prior execution.



## TEST-201 push-triggered execution — run 43565671645

Run [43565671645](https://github.com/lucifron28/UniPM/actions/runs/43565671645) tested commit 58b17b355aa02425c2610076c76800b3d0413693 on codex/nonfunctional-testing-2026-11. The single authorized push-triggered matrix started at 2026-11-14T06:18:12Z and completed at 2026-11-14T06:30:10Z. Both Linux Bash syntax preflights, ephemeral API/database seed, APK build/install, emulator profile validation, visible login, and four configuration tests passed.

| Check | Android 10 / API 29 — Nexus 5 | Android 14 / API 34 — Pixel 7 Pro |
|---|---|---|
| Login, authenticated navigation, dashboard | PASSED | PASSED |
| Asset search/details | FAILED / PARTIAL — the asset-history case failed before tap; later API logs show DEMO-EL-003 search/QR responses at HTTP 200, but no separate passing UI assertion was retained | PASSED for DEMO-EL-001 |
| Maintenance history and return | NOT EXECUTED after the earlier search-action failure | PASSED; HTTP 200, expected acknowledged ID/remarks and visible record, return route passed |
| DEMO-EL-003 PM schedule | BLOCKED; API schedule reads were 200, but visible cycle/due-date assertion has no pass evidence | BLOCKED; eligible Due fixture confirmed, but UI schedule was not opened |
| Inspection form lifecycle | FAILED / INCOMPLETE; API log has form POST 201 and start-inspection POST 200, but save/reopen/edit/server-state assertions were not verified | NOT EXECUTED; the dashboard interaction failed before form entry |
| Offline persistence before restart | BLOCKED; no offline-network marker and no verified local snapshot | BLOCKED; no offline-network marker and no verified local snapshot |
| Restart, reconnection and sync | NOT EXECUTED | NOT EXECUTED |
| Job | FAILED | FAILED |

The first recorded error on API 29 was at integration_test/nonfunctional_smoke_test.dart:1072 during _verifyHitTestPosition, reached from _openAssetFromDashboard. The Search Assets target bounds were y=374–448 in a page-level HomePage ListView render viewport ending at y=592, but the helper validated against a different, shorter scroll viewport ending at y=343. Flutter diagnostics show the Search Assets ListTile beneath the dashboard RefreshIndicator/ListView viewport. The test failed before tapping. API 34 first passed the history scenario, then failed at the same helper in online-inspection: target y=572.4–644.4 was inside the page-level viewport ending at y=867.4 but outside the helper viewport ending at y=555.4. The screenshots show the action on screen. This is a confirmed test-helper scrollable-selection/bounds mismatch, not a production or API defect.

Both API logs contain 32 requests and no non-2xx status. API 34 history returned HTTP 200 twice and passed the seeded acknowledged record assertion. API 29 form-create/start endpoints returned 201/200, but the integration test reported failure and did not record complete form lifecycle verification. The runner did not request offline connectivity changes and did not execute restart/synchronization on either profile. The Flutter report was 0 passed/3 failed on API 29 and 1 passed/2 failed on API 34; the later API 29 and offline test cases had no distinct failure diagnostics in the captured log.

- API 29 job [132358863068](https://github.com/lucifron28/UniPM/actions/runs/43565671645/job/132358863068), artifact [14102497268](https://github.com/lucifron28/UniPM/actions/runs/43565671645/artifacts/14102497268). Failure screenshot: asset-history-asset-search-result-failure.png.
- API 34 job [132358863365](https://github.com/lucifron28/UniPM/actions/runs/43565671645/job/132358863365), artifact [14102302153](https://github.com/lucifron28/UniPM/actions/runs/43565671645/artifacts/14102302153). Failure screenshot: online-inspection-dashboard-pm-schedule-failure.png.

Both artifacts include integration output, widget/render diagnostics, Android window hierarchy, API statuses, backend logs, emulator logcat, and the runner summary. The raw files remain in the ignored artifacts/nonfunctional-android/run-43565671645/ directory. No application or integration-test source was changed for this run. The smallest next correction is test-only: use the target control’s actual scrollable ancestor or verify and tap the control against the render viewport when it is already visibly hit-testable. The run authorization is consumed; no rerun was launched.
