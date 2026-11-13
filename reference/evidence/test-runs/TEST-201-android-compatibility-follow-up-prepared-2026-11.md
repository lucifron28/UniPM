---
id: TEST-201
type: test-run
title: Android compatibility verification; emulator smoke failed on both profiles
status: executed
recordedAtUtc: 2026-11-13T17:12:04Z
testedCommit: db04aed6d6079a81d45b32b811750ef24c38643e
sourceBranch: codex/nonfunctional-testing-2026-11
evidenceLevel: ci-executed
---

# TEST-201: Android compatibility verification

## Execution summary

Run [43468215938](https://github.com/lucifron28/UniPM/actions/runs/43468215938) was dispatched once on 2026-11-13 and completed with failure at 2026-11-13T17:12:04Z. It tested commit db04aed6d6079a81d45b32b811750ef24c38643e on codex/nonfunctional-testing-2026-11. The run included Android 10/API 29 on the Nexus 5 profile and Android 14/API 34 on the Pixel 7 Pro profile. Each matrix job created a separate ephemeral SQL Server and synthetic database.

Both jobs passed tool setup, ephemeral database preparation, API build and fixture seeding, APK build, and KVM setup. Both failed in the step named “Run emulator compatibility and functional smoke checks”; GitHub reports the process /usr/bin/sh exited with code 1. The assertion or subcommand that returned that code is not established by the available run metadata.

The two evidence artifacts were uploaded:

- API 29 job 132032945952; artifact [android-api-29-evidence](https://github.com/lucifron28/UniPM/actions/runs/43468215938/artifacts/14061360039), 1,084,717 bytes.
- API 34 job 132032945197; artifact [android-api-34-evidence](https://github.com/lucifron28/UniPM/actions/runs/43468215938/artifacts/14060386895), 2,586,354 bytes.

Artifact download was attempted for API 29 and rejected with HTTP 503: “Egress is over the account limit.” To respect that restriction, the API 34 artifact was not downloaded through another path. The artifact links are retained above, but screenshots, widget diagnostics, test logs, emulator logs, and API statuses could not be inspected locally. The root cause remains undetermined; this run does not confirm an application defect or a test-design defect.

## TEST-201 profile outcomes

| Check | Android 10 / API 29 | Android 14 / API 34 |
|---|---|---|
| Checkout, tool setup, isolated SQL Server and synthetic API fixture | PASSED | PASSED |
| API build and synthetic data seeding | PASSED | PASSED |
| UniPM APK build | PASSED | PASSED |
| Emulator compatibility and functional smoke step | FAILED — shell process exited 1; exact assertion unavailable | FAILED — shell process exited 1; exact assertion unavailable |
| APK installation and application startup | BLOCKED — inside the failed smoke step; artifact unavailable for inspection | BLOCKED — inside the failed smoke step; artifact unavailable for inspection |
| Sign-in UI and configuration tests | BLOCKED — exact sub-step result unavailable | BLOCKED — exact sub-step result unavailable |
| Synthetic login, authenticated navigation, dashboard, asset search and asset details | BLOCKED — exact sub-step result unavailable | BLOCKED — exact sub-step result unavailable |
| Maintenance history, including the seeded acknowledged inspection | BLOCKED — exact sub-step result unavailable | BLOCKED — exact sub-step result unavailable |
| Preventive-maintenance schedule display and online form save/reopen/edit/server persistence | BLOCKED — exact sub-step result unavailable | BLOCKED — exact sub-step result unavailable |
| Offline draft creation, backend disconnection, persistence after restart before reconnection | BLOCKED — exact sub-step result unavailable | BLOCKED — exact sub-step result unavailable |
| Reconnection, synchronization, server state, and reopened form | BLOCKED — exact sub-step result unavailable | BLOCKED — exact sub-step result unavailable |
| Controlled synchronization conflict | NOT EXECUTED — no conflict scenario is implemented in this smoke test | NOT EXECUTED — no conflict scenario is implemented in this smoke test |
| Screenshots, widget diagnostics, API and emulator logs reviewed | BLOCKED — GitHub artifact egress limit | BLOCKED — GitHub artifact egress limit |

The tested source includes a runner gate that refuses network restoration until the restarted, still-offline phase reads the encrypted snapshot and verifies the synthetic Draft, inspection fields, and two pending writes. The workflow outcome does not establish whether that assertion ran or passed.

## Failure localization and limits

The exact failing workflow step on both profiles is “Run emulator compatibility and functional smoke checks”. The only accessible failure message is that its shell process exited with code 1. No assertion text or screenshot is available because artifact downloads were blocked. Do not attribute the failure to UniPM application logic, the integration selector, the fixture, API behavior, or emulator instability without the missing evidence.

No production application code was changed. TEST-198, TEST-199, TEST-200 and their evidence remain unchanged. No rerun was dispatched. Android compatibility is incomplete and must not be reported as passed.

## TEST-200 outcomes carried forward

| Check | API 29 | API 34 |
|---|---|---|
| Install, visible login, configuration checks, authenticated navigation, dashboard, asset search/details | PASSED | PASSED |
| History | FAILED at test-side ensureVisible(view-asset-history) because the lazy details list had not built the History action. No tap or history API request occurred. | PASSED; the history page displayed a seeded record and the API returned 200. |
| PM schedule | NOT EXECUTED after the History selector failure. | PASSED. |
| Start/Resume and form save/reopen | NOT EXECUTED. | BLOCKED by using DEMO-EL-001, whose schedule is Completed and has no Draft row; no Start/Resume action was expected from this fixture. |
| Offline draft persistence and reconnection sync | NOT EXECUTED. | NOT EXECUTED. |

The earlier asset-details timeout from run 43396819069 did not recur in TEST-200, but TEST-200 does not identify the cause of that earlier timeout. No production defect was confirmed, so production code is unchanged.

## TEST-201 source review

The test scrolls the lazy asset-details list to History, verifies HTTP 200 and the seeded DEMO-EL-001 inspection ID and remarks, then confirms the record appears on the history screen. The online form lifecycle uses DEMO-EL-003 and its assigned Due schedule. It checks Start, save, reopen, edit, backend persistence, and a second reopen.

Offline work uses the separate synthetic DEMO-FE-003 asset and its eligible November 2026 schedule. Each job has an isolated database and fixture state. The runner disables emulator connectivity after fixture setup. The integration test confirms that the backend readiness request is unreachable before it saves the local Draft and inspection.

After the runner force-stops the app, the restart phase stays signed out and offline. It reads the same encrypted secure snapshot using the synthetic inspector ID emitted by phase one, verifies the Draft and inspection fields and exactly two pending writes, then emits the online-network marker. The runner refuses reconnection unless it has observed that pass marker. After HTTP 200 and same-account sign-in, the test checks the synchronized server Draft, inspection, schedule status, cleared sync banner, and reopened form.

Every uncaught integration failure emits Flutter widget/render diagnostics before the runner captures an in-app screenshot and Android hierarchy while the test is still alive. The run artifacts were uploaded but not inspected due the documented account egress restriction. The controlled conflict scenario remains NOT EXECUTED.

## Evidence preservation

This record adds TEST-201 execution evidence without changing earlier test-run records or artifacts. TEST-198, TEST-199, TEST-200 and all associated evidence remain preserved. Artifact retrieval and inspection remain blocked by the HTTP 503 account egress limit; no alternate download path or rerun was attempted.
