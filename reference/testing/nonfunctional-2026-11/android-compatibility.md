# Android compatibility test matrix

## Required emulator configurations

The GitHub Actions workflow defines two portrait configurations:

| Requested profile | Emulator profile | Observed display | Logical viewport | Workflow job |
|---|---|---:|---:|---|
| Android 10 / API 29, small | Nexus 5 | 1080×1920 at 480 dpi | 360×640 dp | [API 29 job, run 43296932992](https://github.com/lucifron28/UniPM/actions/runs/43296932992/job/131457612840) |
| Android 14 / API 34, large | Pixel 7 Pro | 1440×3120 at 560 dpi | approximately 411×891 dp | [API 34 job, run 43296932992](https://github.com/lucifron28/UniPM/actions/runs/43296932992/job/131457612496) |

Workflow execution used Ubuntu 24.04, Java 21, Flutter 3.47.1 stable, x86_64 Android emulators, software rendering, and no KVM acceleration. Exact source commit: `530b5be36727698f3e66935e56e53c698e4df52b`. Run: [43296932992](https://github.com/lucifron28/UniPM/actions/runs/43296932992), triggered 2026-11-12T20:06:20Z and completed successfully. In Asia/Manila the run occurred on 2026-11-13.

## Results from the latest run

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

Do not report the current startup/rendering evidence as full compatibility acceptance. A future run should produce a responsive UniPM screen before layout or authenticated-feature checks can be claimed.
