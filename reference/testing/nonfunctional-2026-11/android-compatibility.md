# Android compatibility test matrix

## Required emulator configurations

The GitHub Actions workflow defines two portrait configurations:

| Requested profile | Emulator profile | Observed display | Logical viewport | Workflow job |
|---|---|---:|---:|---|
| Android 10 / API 29, small | Nexus 5 | 1080×1920 at 480 dpi | 360×640 dp | [latest TEST-201 API 29 job, run 43468215938](https://github.com/lucifron28/UniPM/actions/runs/43468215938/job/132032945952) |
| Android 14 / API 34, large | Pixel 7 Pro | 1440×3120 at 560 dpi | approximately 411×891 dp | [latest TEST-201 API 34 job, run 43468215938](https://github.com/lucifron28/UniPM/actions/runs/43468215938/job/132032945197) |

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

## TEST-201 executed follow-up — run 43468215938

Run [43468215938](https://github.com/lucifron28/UniPM/actions/runs/43468215938) tested commit db04aed6d6079a81d45b32b811750ef24c38643e on codex/nonfunctional-testing-2026-11. It was the single authorized workflow_dispatch and completed at 2026-11-13T17:12:04Z. The exact profiles were Android 10/API 29 on Nexus 5 and Android 14/API 34 on Pixel 7 Pro. The jobs used separate ephemeral SQL Server instances and synthetic fixtures.

| Check | Android 10 / API 29 — Nexus 5 | Android 14 / API 34 — Pixel 7 Pro |
|---|---|---|
| Ephemeral database, API build and synthetic seed | PASSED | PASSED |
| UniPM APK build | PASSED | PASSED |
| Emulator compatibility and functional smoke workflow step | FAILED — shell process exited 1; exact assertion unavailable | FAILED — shell process exited 1; exact assertion unavailable |
| Installation, startup, sign-in/configuration and authenticated workflows | BLOCKED — sub-step result cannot be inspected | BLOCKED — sub-step result cannot be inspected |
| Asset details, history, schedule and online inspection form lifecycle | BLOCKED — sub-step result cannot be inspected | BLOCKED — sub-step result cannot be inspected |
| Offline draft persistence before reconnection and synchronization | BLOCKED — sub-step result cannot be inspected | BLOCKED — sub-step result cannot be inspected |
| Controlled synchronization conflict | NOT EXECUTED | NOT EXECUTED |
| Screenshot, widget, API and emulator evidence inspection | BLOCKED — artifact download account egress limit | BLOCKED — artifact download account egress limit |
| Job result | FAILED | FAILED |

The workflow uploaded [API 29 evidence](https://github.com/lucifron28/UniPM/actions/runs/43468215938/artifacts/14061360039) and [API 34 evidence](https://github.com/lucifron28/UniPM/actions/runs/43468215938/artifacts/14060386895). Download of the API 29 artifact was rejected with HTTP 503 “Egress is over the account limit.” The API 34 artifact was not downloaded through another path. Consequently, the exact failing assertion, screenshot, widget tree, API status, and emulator condition remain unavailable, and the root cause is undetermined. The only confirmed failure detail is that the named emulator compatibility step exited 1 on both profiles. No production code was changed and no rerun was dispatched.

The source review confirms that TEST-201 uses the lazily scrolled History action and checks the seeded acknowledged record; it uses DEMO-EL-003 for online Start/save/reopen/edit/server verification and separate DEMO-FE-003 for offline work. Each job has an isolated seeded database. After force-stop, the second phase remains offline, reads the encrypted persisted snapshot for the synthetic user, checks the Draft, row and two pending writes, and only then requests reconnection. The runner gates restoration on that check. The run result does not establish that these UI checks were reached.

Compatibility remains incomplete. TEST-201 has no evidence-backed pass for either Android profile, and the exact failure cause is not established. The prior TEST-198, TEST-199 and TEST-200 records and artifacts remain unchanged. No live synchronization-conflict behavior was executed.

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

`mobile/android/app/build.gradle.kts` relies on Flutter's Android SDK defaults; with Flutter 3.47.1, the resolved minimum SDK is API 24, and compile/target SDK are API 36. An earlier CI run (43296932992) recorded APK installation on both requested API levels, but that does not verify installation or UI behavior in TEST-201. TEST-201's emulator compatibility step failed on both profiles, and artifact egress restrictions prevented inspection of startup screenshots and test logs. No API 29/API 34 claim is made for TEST-201 layout fit, authentication, asset lookup, inspection entry, persistence, synchronization, history or conflict handling.

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

Do not report these results as full Android compatibility acceptance. TEST-200 verified install, login, authenticated navigation, dashboard, asset search and asset details on both profiles; it also verified API 34 history and schedule display and identified test-side History navigation and fixture-selection issues. TEST-201 failed in the emulator smoke step on both profiles, and artifact downloads were blocked by the account egress limit. The accessible evidence does not establish which functional checks ran or passed in TEST-201, so history, forms, offline persistence and synchronization remain unverified. Do not claim compatibility passed or assign a root cause.

