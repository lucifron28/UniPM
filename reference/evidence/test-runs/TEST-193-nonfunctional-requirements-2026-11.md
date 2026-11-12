---
id: TEST-193
type: test-run
title: Non-functional testing evidence for Android, web API performance, and ZAP
status: executed
recordedAtUtc: 2026-11-12T16:23:32Z
testedCommit: 53afe5708ca640b6fe7618589cbf3be1a1de6ae3
sourceBranch: codex/nonfunctional-testing-2026-11
evidenceLevel: locally-executed
---

# TEST-193: Non-functional testing evidence

## Objective

Execute reproducible Android compatibility, web/API performance, and passive OWASP ZAP checks against the existing UniPM application. Preserve actual output, record environment and source identity, and distinguish executed results from work still pending.

## Execution identity

- Tested application commit: 53afe5708ca640b6fe7618589cbf3be1a1de6ae3
- Separate test branch: codex/nonfunctional-testing-2026-11
- Test window: 2026-11-12 through 2026-11-13, Asia/Manila
- Scope: local synthetic development data and loopback API only
- Application code, schema, routes, authorization, event lifecycle, and NLA interpretation/report logic were not changed.

## Environment

- Host runtime string: win32 10.0.26200 x64; benchmark process saw 16 logical CPUs and 15.4 GiB memory
- .NET SDK 10.0.400
- SQL Server 2019 Developer 15.0.2210.4, compatibility level 150
- ASP.NET API: Release build at http://127.0.0.1:5254
- React checks: Node 22.23.5/npm 10.9.9
- API measurement client: Node 24.15.0
- Flutter 3.47.1 stable/Dart 3.13.1
- Only local Android image available: Android 17/API 37, 1080×2400 at 420 dpi. Compact viewport override: 720×1280 at 320 dpi.
- OWASP ZAP 2.17.0; official distribution archive SHA-256 verified.

## Commands

API build, isolated database migration/seeding, and 30-sample measurements:

    powershell -NoProfile -ExecutionPolicy Bypass -File .\reference\testing\nonfunctional-2026-11\run-api-performance.ps1 -DatabaseName UniPM_Nonfunctional_2026_11_12 -ResetDatabase -SampleCount 30

React build and existing test suite, using Node 22:

    Set-Location .\web
    npm ci
    npm run test:run
    npm run build

Android API 37 debug build, install, and emulator suite:

    Set-Location .\mobile
    flutter build apk --debug --dart-define=UNIPM_API_BASE_URL=http://10.0.2.2:5254/
    adb install -r .\build\app\outputs\flutter-apk\app-debug.apk
    adb shell monkey -p io.github.lucifron28.unipm 1
    flutter test --device-id emulator-5554 --no-pub --reporter expanded

The scoped ZAP run used run-zap-baseline.ps1 and zap-passive-plan.yaml. It completed with an Automation Framework success result and generated the preserved HTML/JSON reports.

## Results

- Web/API performance: executed. Ninety measured authenticated GETs across three actual API endpoints; all 90 returned HTTP 200. Per-route median, p95, and maximum values are in web-performance.md and the raw CSV.
- React: 278 tests passed across 32 files; production build and typecheck passed.
- Backend: Release build passed with zero warnings and zero errors.
- ZAP: passive baseline completed against loopback. One low-risk missing response-header alert was independently confirmed. Two informational alert types were reviewed. The report is not a complete security assessment.
- Android: APK installation, activity launch, sign-in rendering, and 198 Flutter tests were observed on API 37. Requested API 29/API 34 were not executed. The API 29/API 34 workflow is prepared but not dispatched.
- Live Android login, authenticated operational navigation, live PM form submission, persistent offline draft/reconnect sync, server-backed history, and real API conflict handling were not executed.

The three requested areas therefore have actual evidence for local API performance and ZAP, with supplemental Android API 37 evidence. The required Android 10/API 29 and Android 14/API 34 execution remains outstanding.

## Test counts and HTTP results

- React unit suite: 278 passed, 32 test files
- Flutter host suite: 198 passed
- Flutter suite on Android API 37 emulator: 198 passed
- Flutter app configuration subset on Android API 37: 4 passed
- API performance: 30/30 HTTP 200 per route, zero request failures
- ZAP: one low-risk alert and two informational alert types; report and verification details are in zap-findings.md

## Generated artifacts

Committed result files and links to ignored raw logs are enumerated in reference/testing/nonfunctional-2026-11/evidence-index.md. The actual ZAP report is preserved in HTML and JSON. Raw API samples, per-request CSV, environment summary, and Android screenshots are retained.

## Failures and corrections

- The first ZAP plan validation identified report fields in the wrong Automation Framework location. The report job schema was corrected, the plan passed ZAP autocheck, and the passive run completed.
- Initial API 37 screenshots were taken during the native splash phase. The app was allowed to finish its cold launch; a later capture showed the sign-in interface. Android's startup log recorded 21.151 seconds to the displayed activity.
- The React package requires Node 22; installed system Node 24 was outside that engine range. The official portable Node 22.23.5 release was downloaded, its official checksum verified, and the frontend checks were rerun under that version.
- The web production build generated a routeTree.gen.ts change. It was inspected and restored; no application source changes are part of this test branch.

## Skipped and incomplete verification

- Android 10/API 29 and Android 14/API 34 emulator runs: NOT EXECUTED. The local SDK had only API 37 images. The prepared GitHub Actions matrix has not been dispatched.
- Live authenticated mobile workflows and backend synchronization/conflict scenarios: NOT EXECUTED.
- React browser rendering performance: NOT MEASURED; API timings are separate and include the response-body read only.
- Authenticated ZAP scan and active scanning: NOT RUN.
- npm install advisory output: 43 vulnerabilities (30 moderate, 13 high) were reported, but advisory details were not retained or triaged here.

## Limitations

This evidence is tied to the source commit listed above and the local development configuration. The API benchmark is single-user and serial with warm-up requests; the ZAP run is passive and unauthenticated; Android evidence is supplemental API 37 only. No production, load-capacity, full-security, or two-version Android compatibility claim is made.
