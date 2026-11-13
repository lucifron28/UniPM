---
id: TEST-195
type: test-run
title: Android 10/API 29 and Android 14/API 34 compatibility workflow rerun
status: incomplete
recordedAtUtc: 2026-11-13T04:47:20Z
testedCommit: 2fa502628a21e82686920a46680706b3ce0c68ba
sourceBranch: codex/nonfunctional-testing-2026-11
evidenceLevel: ci-executed
---

# TEST-195: Android compatibility workflow rerun

## Execution identity

The GitHub Actions run completed at 2026-11-13T04:47:20Z with overall conclusion failure. It tested commit 2fa502628a21e82686920a46680706b3ce0c68ba on codex/nonfunctional-testing-2026-11.

- Workflow run: https://github.com/lucifron28/UniPM/actions/runs/43363392229
- Android 10/API 29 job: https://github.com/lucifron28/UniPM/actions/runs/43363392229/job/131681940613
- Android 14/API 34 job: https://github.com/lucifron28/UniPM/actions/runs/43363392229/job/131681941121
- Host: GitHub-hosted Ubuntu 24.04; Java 21; Flutter 3.47.1 stable; x86_64 emulator and software rendering.
- Backend: isolated SQL Server 2022 Developer image with Full-Text Search, fresh database, application migrations, and synthetic development seed/account. The API and seed steps passed on both jobs.

## Per-profile results

| Check | Android 10 / API 29, Nexus 5 small | Android 14 / API 34, Pixel 7 Pro large |
|---|---|---|
| Observed OS and display | Android 10/API 29; 1080×1920 at 480 dpi; 360×640 dp. Expected profile check passed. | Android 14/API 34; 1440×3120 at 560 dpi; 411×891 dp. Expected profile check passed. |
| Debug APK build | Passed; APK was produced. | Passed in 336.2 seconds. |
| Install and launch | Failed/blocked: adb install returned device emulator-5554 not found. No install, launch, or app screenshot was obtained. | APK installation returned Success; MainActivity returned Status: ok. |
| Visible app UI | Not executed; final screenshot file was zero bytes. | Passed smoke check: UniPM sign-in screen and controls were visible in startup and final screenshots. |
| Focused app_config_test.dart | Not executed; ADB device was unavailable after build. | Passed 4/4 (debug HTTP URL accepted; release HTTP rejected; release HTTPS accepted; unsupported scheme rejected). |
| Authenticated integration smoke | Not executed. | Failed/timed out: Flutter Driver connected and started the test, then blocked at request_data; the 30-minute wrapper ended with exit code 124. The log contains no login_screen=PASS event and no credentials were submitted. |
| Assets, history, inspection entry, authenticated navigation | Not executed. | Not executed; the smoke did not reach credential entry or authenticated navigation. |
| Offline draft persistence, reconnect synchronization, live conflict/error handling | Not executed. | Not executed. |
| Job conclusion | Failure | Failure; screenshot/artifact upload and backend cleanup steps succeeded. |

The API 34 login-page screenshot verifies that the sign-in UI rendered in the requested 411×891 dp profile. It does not verify layout on authenticated or operational screens. The full integration run did not pass. The API 29 row verifies the expected emulator system profile and APK build only; ADB loss prevented application installation.

## Exact artifacts

Downloaded workflow evidence is retained under reference/testing/nonfunctional-2026-11/android-workflow-runs/43363392229/:

- API 29: summary.json, run-29.log, actions-job.log, adb-devices.txt, emulator-process.txt, final-29.png (empty), backend/SQL setup and logs.
- API 34: summary.json, run-34.log, integration-34.log, config-tests-34.log, actions-job.log, startup-login.png, final-34.png, ui-login.xml, ui-final.xml, logcat.txt, and backend/SQL setup and logs.
- API 34 startup screen: android-workflow-runs/43363392229/api34/startup-login.png.
- API 34 integration timed out after the runner logged the test name and VMServiceFlutterDriver request_data message is taking a long time to complete. It did not log any smoke result marker.

A previous run, 43296932992 at commit 530b5be36727698f3e66935e56e53c698e4df52b, installed and launched the APK process and passed four configuration tests on both profiles, but its screenshots showed system/launcher not-responding dialogs. The current API 34 sign-in screenshot supersedes that earlier API 34 startup screenshot; the previous API 29 screenshot remains the only captured API 29 UI attempt and still showed a System UI not-responding dialog.

## Interpretation and limitations

The workflow confirms the APK can be built for both requested Android API levels. The current run confirms API 34 installation, activity launch, rendered sign-in screen, and focused configuration tests. It does not establish API 34 authentication or app workflows because the integration driver timed out before entering credentials. API 29 installation and application startup remain blocked by emulator/ADB disconnection. Responsive behavior on operational screens, asset lookup, inspection forms, maintenance history, offline persistence, synchronization, and live error/conflict handling are not verified on either requested profile.

The synthetic API, database, and accounts were ephemeral. No institutional account or live production data was used. API 37 smoke results documented in TEST-193/194 are supplemental and are not substituted for API 29/API 34 compatibility evidence. Web latency data and ZAP/dependency results are unchanged from TEST-193/194.

## Reproduction

From the repository root:

powershell
gh workflow run nonfunctional-android-compatibility.yml --ref codex/nonfunctional-testing-2026-11
gh run view <run-id> --log
gh run download <run-id> --name android-api-29-evidence
gh run download <run-id> --name android-api-34-evidence
