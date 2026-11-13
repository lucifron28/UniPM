---
id: TEST-197
type: test-run
title: Android compatibility follow-up with on-device Flutter integration tests
status: executed
recordedAtUtc: 2026-11-13T09:04:04Z
testedCommit: 937c59173caa57faf9fe1ed02ed6879691b6cba9
sourceBranch: codex/nonfunctional-testing-2026-11
evidenceLevel: ci-executed
---

# TEST-197: Android compatibility follow-up

## Execution identity

[GitHub Actions run 43396819069](https://github.com/lucifron28/UniPM/actions/runs/43396819069) tested commit 937c59173caa57faf9fe1ed02ed6879691b6cba9 and completed at 2026-11-13T09:04:04Z. Both matrix jobs failed at the asset-details assertion.

- API 29 job: [131789644936](https://github.com/lucifron28/UniPM/actions/runs/43396819069/job/131789644936); API 34 job: [131789644478](https://github.com/lucifron28/UniPM/actions/runs/43396819069/job/131789644478).
- Runner: Ubuntu 24.04.5, image 20261101.360.1, Java 21, Flutter 3.47.1 stable, Android x86_64; four CPUs, usable KVM v12, SwiftShader rendering.
- Backend: ephemeral synthetic SQL Server 2022 Developer with Full-Text Search, compatibility level 150, migrations, and Development seed/account.
- Profiles: API 29 Nexus 5, 1080×1920 at 480 dpi / 360×640 dp; API 34 Pixel 7 Pro, 1440×3120 at 560 dpi / 411×891 dp.

The workflow changed the integration invocation to flutter test --no-pub --reporter expanded --device-id <emulator> integration_test/nonfunctional_smoke_test.dart and removed screenshot handshakes from the integration test. Startup/final screenshots are captured through ADB. This follows Flutter's [Android integration test guidance](https://docs.flutter.dev/testing/integration-tests).

## Results

| Check | Android 10 / API 29 | Android 14 / API 34 |
|---|---|---|
| Build, install, activity launch | PASSED — install Success; Status: ok. | PASSED — install Success; Status: ok. |
| Sign-in UI / screenshot | PASSED — foreground controls matched; screenshot saved. | PASSED — foreground controls matched; screenshot saved. |
| Configuration tests | PASSED — 4/4. | PASSED — 4/4. |
| Synthetic login and authenticated navigation | PASSED — login and navigation markers emitted. | PASSED — same. |
| Dashboard smoke | PASSED — marker emitted without Flutter rendering errors. | PASSED — same. |
| Asset search | PASSED — DEMO-EL-001 appeared and was selected. | PASSED — same. |
| Asset details | FAILED — no expected asset-code control within 30 seconds. | FAILED — same. |
| Maintenance history / inspection forms | NOT EXECUTED after the asset-details failure. | NOT EXECUTED after the asset-details failure. |
| Offline draft persistence / reconnect synchronization | NOT EXECUTED; not part of this device smoke. | NOT EXECUTED; not part of this device smoke. |

The emulator and ADB remained available through the failing step, so this is a functional smoke failure rather than an infrastructure or installation blocker. Console evidence does not distinguish route/data behavior from a selector mismatch; the cause remains undetermined. History and inspection forms were not reached. The test did not toggle networking, persist an offline draft, or verify synchronization.

## Evidence and conclusion

Artifacts preserve ADB startup/final screenshots, UI hierarchy XML, ADB/emulator diagnostics, logcat, configuration and integration output, and backend/SQL setup: [API 29](https://github.com/lucifron28/UniPM/actions/runs/43396819069/artifacts/14023631972) · [API 34](https://github.com/lucifron28/UniPM/actions/runs/43396819069/artifacts/14023742476). Full job logs: [API 29](https://github.com/lucifron28/UniPM/actions/runs/43396819069/job/131789644936) · [API 34](https://github.com/lucifron28/UniPM/actions/runs/43396819069/job/131789644478). Raw unreviewed artifacts were not copied into committed evidence.

Partial API 29 compatibility evidence: Android 10 boot, install, sign-in, synthetic login, authenticated navigation, dashboard smoke, and asset search passed. Asset details failed; history, inspection forms, offline persistence, and reconnect remain unverified.