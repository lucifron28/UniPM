---
id: TEST-196
type: test-run
title: Android API 29 compatibility retest and Flutter Driver timeout
status: executed
recordedAtUtc: 2026-11-13T08:48:40Z
testedCommit: 7b53e82126d2a6218bcacf92596285431f2740d8
sourceBranch: codex/nonfunctional-testing-2026-11
evidenceLevel: ci-executed
---

# TEST-196: Android API 29 compatibility retest

## Execution identity

[GitHub Actions run 43391036513](https://github.com/lucifron28/UniPM/actions/runs/43391036513) tested commit 7b53e82126d2a6218bcacf92596285431f2740d8 and completed at 2026-11-13T08:48:40Z with failure.

- API 29 job: [131770712611](https://github.com/lucifron28/UniPM/actions/runs/43391036513/job/131770712611); API 34 job: [131770712071](https://github.com/lucifron28/UniPM/actions/runs/43391036513/job/131770712071).
- Runner: Ubuntu 24.04.5, Java 21, Flutter 3.47.1 stable, Android x86_64; KVM v12 usable.
- Backend: ephemeral synthetic SQL Server 2022 Developer with Full-Text Search, compatibility level 150, migrations, and Development seed/account.
- API 29 profile: Android 10 / Nexus 5, 1080×1920 at 480 dpi, 360×640 dp.

## API 29 results

| Check | Result |
|---|---|
| APK build | PASSED before emulator startup. |
| ADB readiness / boot | PASSED — Android 10 boot-complete observed. |
| Install | PASSED — streamed install returned Success. |
| MainActivity | PARTIAL — output included Status: timeout, although shell exit code was 0. |
| Sign-in UI | PASSED — foreground hierarchy contained UniPM, Sign in, and the field-work account text; screenshot captured. |
| Configuration test | PASSED — 4/4. |
| Authenticated smoke | BLOCKED — 30-minute Flutter Driver request_data timeout, exit 124; no login marker or credential submission. |
| Asset/history/forms and offline/reconnect | NOT EXECUTED. |

API 34 build/install and Status: ok activity launch passed, but its foreground check did not find the required sign-in controls. Its authenticated smoke and config test did not run.

## Diagnostic findings

In the previous run 43363392229, API 29 built but ADB reported emulator-5554 not found while QEMU remained present. KVM was usable and retained host diagnostics showed no direct memory or disk exhaustion evidence. The exact cause was not identified.

In this retest, the emulator log briefly reported an unavailable ADB daemon on port 5037; the daemon started, and the bounded readiness check, install, and sign-in capture succeeded. This supports transient runner-side ADB readiness/transport loss as the most likely cause, but does not prove the original trigger or implicate the app.

## Evidence and conclusion

Artifacts: [API 29](https://github.com/lucifron28/UniPM/actions/runs/43391036513/artifacts/14022946828) · [API 34](https://github.com/lucifron28/UniPM/actions/runs/43391036513/artifacts/14020893378). They preserve build/install output, ADB/emulator diagnostics, UI hierarchy, screenshots, logcat, configuration output, and synthetic backend logs. No unreviewed raw log or screenshot was copied into committed evidence.

Partial API 29 evidence: emulator boot, APK installation, sign-in rendering, and configuration 4/4 passed. The Flutter Driver handshake prevented login and authenticated workflow checks in this run; offline/reconnect was not executed.
