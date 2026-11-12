---
id: TEST-194
type: test-run
title: Non-functional testing follow-up — Android emulators and security findings
status: executed
recordedAtUtc: 2026-11-12T20:33:38Z
testedCommit: 530b5be36727698f3e66935e56e53c698e4df52b
sourceBranch: codex/nonfunctional-testing-2026-11
evidenceLevel: ci-executed
---

# TEST-194: Android and security follow-up evidence

## Objective and scope

Continue TEST-193 by executing the API 29/API 34 GitHub Actions emulator matrix, reviewing the real screenshots and job logs, verifying the X-Content-Type-Options remediation with a repeated passive ZAP scan, and triaging the npm audit results. This record supplements TEST-193; its web/API timing data, initial ZAP report, and API 37 results remain unchanged. No NLA interpretation/reporting logic or application business functionality was modified in this follow-up.

## Source identity and execution environment

- Testing branch: `codex/nonfunctional-testing-2026-11`
- Android workflow source commit: `530b5be36727698f3e66935e56e53c698e4df52b`
- Header fix commit: `31da92eb73fa7df8dafaaf378624390ed034010b`
- npm lockfile fix commit: `77e23526c9ef906c6157bfe70d0c45769e7d029b`
- Android run: [GitHub Actions run 43296932992](https://github.com/lucifron28/UniPM/actions/runs/43296932992), created 2026-11-12T20:06:20Z and completed 2026-11-12T20:26:46Z; run conclusion `success`.
- Android host/tooling: Ubuntu 24.04, Java 21, Flutter 3.47.1 stable, x86_64 emulator, software rendering, no KVM.
- Security reports and package audits: local development environment, loopback-only API target, synthetic database/account; report dates are given below.

## Android configuration and results

| Configuration | Device/profile and measured viewport | Automated result | Screenshot review |
|---|---|---|---|
| Android 10 / API 29 | Nexus 5; 1080×1920 at 480 dpi, approximately 360×640 dp. Job [131457612840](https://github.com/lucifron28/UniPM/actions/runs/43296932992/job/131457612840), completed 2026-11-12T20:20:48Z with conclusion `success`. | Debug APK build passed in 222.6 s. Install returned `Success`. `am start -W` returned `Status: timeout` while reporting `MainActivity`; package process PID 5239 was present. `test/app_config_test.dart` passed 4/4. | **Failed visual startup smoke:** screenshot shows “System UI isn't responding” over a blank screen; UniPM controls are not visible. Layout and feature workflows are therefore unverified. |
| Android 14 / API 34 | Pixel 7 Pro; 1440×3120 at 560 dpi, approximately 411×891 dp. Job [131457612496](https://github.com/lucifron28/UniPM/actions/runs/43296932992/job/131457612496), completed 2026-11-12T20:26:46Z with conclusion `success`. | Debug APK build passed in 231.2 s. Install returned `Success`. `am start -W` returned `Status: timeout` while reporting `MainActivity`; package process PID 4623 was present. `test/app_config_test.dart` passed 4/4. | **Failed visual startup smoke:** screenshot shows “Pixel Launcher isn't responding” over a blank screen; UniPM controls are not visible. Layout and feature workflows are therefore unverified. |

Both jobs installed and started the application process and ran the focused tests. Their green workflow conclusion is not evidence that the app rendered: each screenshot records an Android UI/launcher not-responding dialog. The screenshots do not establish the cause, so this record does not attribute it to UniPM or the emulator. Login, authenticated navigation, asset lookup, inspection entry, responsive layout/overflow, offline draft persistence, reconnect synchronization, history, and live conflict/error handling were **not executed** on API 29 or API 34. The workflow does not start the API or seed accounts.

Actual artifacts checked into the repository:

- API 29: [startup screenshot](../../testing/nonfunctional-2026-11/android-workflow-runs/432969/api29/startup-29.png), [helper output](../../testing/nonfunctional-2026-11/android-workflow-runs/432969/api29/run-29.log), [full Actions log](../../testing/nonfunctional-2026-11/android-workflow-runs/432969/api29/actions-job.log).
- API 34: [startup screenshot](../../testing/nonfunctional-2026-11/android-workflow-runs/432969/api34/startup-34.png), [helper output](../../testing/nonfunctional-2026-11/android-workflow-runs/432969/api34/run-34.log), [full Actions log](../../testing/nonfunctional-2026-11/android-workflow-runs/432969/api34/actions-job.log).
- Earlier failed helper logs are preserved at `reference/testing/nonfunctional-2026-11/android-workflow-runs/<run-id>/api29/` and `/api34/`; the run URLs and attempt results are in `android-compatibility.md`.

The focused test file covers API URL configuration, not live integration. It accepts an HTTP development API URL, rejects HTTP for release, accepts HTTPS for release, and rejects unsupported schemes. Prior API 37 unit tests and the separate API 37 authenticated smoke remain supplemental; see TEST-193 and `android-api37-live/README.md`. API 37 live smoke passed login/navigation, synthetic asset lookup, and display of two history rows; PM form-list retrieval timed out, and on-device offline/reconnect and conflict handling were not executed.

## X-Content-Type-Options remediation and ZAP follow-up

The original ZAP Automation Framework passive report was generated at `2026-11-12T16:03:31.047Z` (2026-11-13 00:03:31 Asia/Manila) against `http://127.0.0.1:5254`. It reported one low-risk `X-Content-Type-Options Header Missing` alert, two informational `Authentication Request Identified` instances, and four informational `Information Disclosure - Sensitive Information in URL` instances.

The header was added as `X-Content-Type-Options: nosniff` in `server/Program.cs` at `31da92eb73fa7df8dafaaf378624390ed034010b`. The focused `FoundationEndpointsTests.Responses_include_the_content_type_nosniff_header` regression passed 1/1 across `/`, `/api/info`, `/health/live`, `/health/ready`, and `/not-found`; the Release backend build passed with zero warnings and errors. The regression output is retained at [security-header-regression-test.log](../../testing/nonfunctional-2026-11/security-header-regression-test.log).

The authorized passive scan was repeated at `2026-11-12T17:13:32.349620Z` (2026-11-13 01:13:32 Asia/Manila) against the same local scope. The actual [after-fix HTML report](../../testing/nonfunctional-2026-11/zap-report-after-header-fix-2026-11-13.html) and [JSON report](../../testing/nonfunctional-2026-11/zap-report-after-header-fix-2026-11-13.json) contain no X-Content-Type-Options alert. They retain the same two informational alert types, with 2 and 4 instances respectively. The URL parameters reviewed were opaque synthetic user identifiers, not passwords or session tokens. The before-fix reports and independent four-route header check remain preserved. This verifies the response header only within an unauthenticated passive baseline; it is not complete security coverage.

## npm advisory triage

The initial audit of the source lockfile at `53afe5708ca640b6fe7618589cbf3be1a1de6ae3` reported 43 affected package entries: 13 high and 30 moderate. A comparison to that lockfile classified 3 entries as runtime/transitive (2 high, 1 moderate) and 40 as development (11 high, 29 moderate).

Compatible lockfile updates in `77e23526c9ef906c6157bfe70d0c45769e7d029b` moved `zod` 4.4.3→4.6.7, `follow-redirects` 1.16.0→1.16.3, and `seroval` 1.6.9→1.8.1 beyond their vulnerable ranges. Orval was updated within its 8.x major, and Vitest/coverage and other affected development transitives were updated compatibly. One transitive development-only `nanoid` 5.1.16 resolution is required by the updated Scalar tool graph; PostCSS has a separate patched `nanoid` 3.3.20. No direct application dependency was major-upgraded.

The after-update full audit reports 10 moderate affected package entries and zero high/critical; all 10 are `dev: true` in the current lockfile. The production-only audit reports zero. Remaining entries are constrained by the ESLint 10/minimatch and TypeScript-ESLint version ranges; proposed changes that would downgrade the current lint toolchain or move below the manifest range were deferred. Raw before/full-after/production-after JSON and detailed disposition are retained in `security-dependency-triage.md`.

The updated lockfile installed with Node.js 22.23.5/npm 10.9.9; the generated API check passed, React tests passed 278/278 across 32 files, and production build/typecheck passed. This is dependency/build evidence; it does not establish absence of all software supply-chain risk.

## Reproduction commands

Dispatch the two Android jobs from the repository root:

```powershell
gh workflow run nonfunctional-android-compatibility.yml --ref codex/nonfunctional-testing-2026-11
gh run view <run-id> --log
gh run download <run-id> --name android-api-29-evidence
gh run download <run-id> --name android-api-34-evidence
```

Repeat the local passive ZAP scan against the isolated synthetic database using the verified ZAP 2.17.0 archive:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\reference\testing\nonfunctional-2026-11\run-zap-baseline.ps1 -DatabaseName UniPM_Nonfunctional_2026_11_12
```

Run npm checks from `web/` with Node 22:

```powershell
npm ci
npm audit --json > npm-audit-full.json
npm audit --omit=dev --json > npm-audit-production.json
npm run api:check
npm run test:run
npm run build
```

## Limitations and completion status

- **Android:** both requested API/version profiles actually ran; build, installation, process detection, and four configuration tests passed. The startup screenshots failed visual review. Authenticated and operational scenarios on API 29/API 34 remain unexecuted. This requirement is not complete as full mobile compatibility acceptance.
- **Security header:** fix, focused regression, and repeated passive scan executed; the original alert is absent from the after report. Remaining informational alerts were reviewed, not suppressed.
- **npm audit:** initial and updated reports were compared; all remaining affected package entries are development-only, with no production audit findings. Ten moderate development-tool entries remain.
- **Performance:** original 90-sample API run remains documented in TEST-193; no new latency run was needed for this follow-up.
- No production service or third-party target was scanned, no destructive attack was run, and no credential or real user record was used.
