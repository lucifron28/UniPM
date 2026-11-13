# Android compatibility test matrix

## Required emulator configurations

The GitHub Actions workflow defines two portrait configurations:

| Requested profile | Emulator profile | Observed display | Logical viewport | Workflow job |
|---|---|---:|---:|---|
| Android 10 / API 29, small | Nexus 5 | 1080×1920 at 480 dpi | 360×640 dp | [latest API 29 job, run 43396819069](https://github.com/lucifron28/UniPM/actions/runs/43396819069/job/131789644936) |
| Android 14 / API 34, large | Pixel 7 Pro | 1440×3120 at 560 dpi | approximately 411×891 dp | [latest API 34 job, run 43396819069](https://github.com/lucifron28/UniPM/actions/runs/43396819069/job/131789644478) |

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

`mobile/android/app/build.gradle.kts` relies on Flutter's Android SDK defaults; with Flutter 3.47.1, the resolved minimum SDK is API 24, and compile/target SDK are API 36. Both requested APIs are above the installation minimum, and the CI run directly confirms APK installation on both. However, neither captured startup image showed the application interface. Therefore no API 29/API 34 claim is made for layout fit, authentication, asset lookup, inspection entry, persistence, synchronization, history or conflict handling.

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

Do not report the current startup/rendering evidence as full compatibility acceptance. The latest run confirms sign-in, synthetic login, authenticated navigation, dashboard smoke, and asset search on API 29 and API 34. Both profiles fail at the asset-details check. History, inspection forms, offline persistence, and reconnect remain unverified. The earlier API 29 installation blocker did not recur, though its original trigger remains uncertain.

