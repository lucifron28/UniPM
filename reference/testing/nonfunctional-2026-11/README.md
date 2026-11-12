# UniPM non-functional test evidence — November 2026

## Scope and status

The application code tested is commit 53afe5708ca640b6fe7618589cbf3be1a1de6ae3 on the separate local branch codex/nonfunctional-testing-2026-11. The test harness and evidence were added after that source commit. No application behavior, database schema, routes, authorization, event lifecycle, or NLA interpretation/reporting logic was changed.

| Area | Result |
|---|---|
| Android | Partial. The app was installed and rendered on API 37, and the existing Flutter suite passed on that emulator. The required API 29 and API 34 configurations were not executed. A GitHub Actions matrix is prepared but has not been dispatched. |
| Web performance | Executed. Three authenticated local ASP.NET Core registry endpoints were measured with 30 samples each against synthetic SQL Server data. The React unit suite and production build also passed. |
| ZAP | Executed. OWASP ZAP 2.17.0 completed a passive-only scan of the local API. HTML and JSON reports are preserved. One low-risk header finding was verified; two informational alerts were reviewed. |

This record is intentionally incomplete for the requested Android 10/API 29 and Android 14/API 34 matrix. It makes no compatibility claim for those versions.

## Tested source and environment

- Application source commit: 53afe5708ca640b6fe7618589cbf3be1a1de6ae3
- Work branch: codex/nonfunctional-testing-2026-11
- Test dates: 2026-11-12 through 2026-11-13, Asia/Manila
- Host reported by the API measurement runtime: win32 10.0.26200 x64, 16 logical CPUs, 15.4 GiB memory
- Backend: .NET SDK 10.0.400; ASP.NET Core Release build; local SQL Server 2019 Developer 15.0.2210.4 with compatibility level 150
- React checks: Node.js 22.23.5 and npm 10.9.9, matching the repository's Node 22 range ([official Node 22.23.5 archive](https://nodejs.org/en/download/archive/v22.23.5))
- API measurement client: Node.js 24.15.0. This client only sent HTTP requests and read response bodies; it did not run the React app.
- Android supplemental run: Flutter 3.47.1 stable, Dart 3.13.1, Android 17/API 37 emulator, default display 1080×2400 at 420 dpi. A separate sign-in screen capture used 720×1280 at 320 dpi.
- ZAP: 2.17.0 cross-platform release ([official archive](https://github.com/zaproxy/zaproxy/releases/download/v2.17.0/ZAP_2.17.0_Crossplatform.zip)). The downloaded archive SHA-256 matched the official release value 94c8f767b1c2e94f0db66b3ae56514d5e3f5a728ee1b6c798e0c8fe2d61fbff0.

The API benchmark and ZAP target were restricted to 127.0.0.1. The test database UniPM_Nonfunctional_2026_11_12 was created for this task. Seeded records and the GSD development login were synthetic. The password and local JWT signing key were generated in process memory, omitted from logs/results, and cleared when each runner exited.

## Summary of actual results

### Android

- Debug APK build and package installation passed on Android 17/API 37.
- The Android activity launched and the sign-in interface rendered. The first cold display was recorded by Android ActivityTaskManager at 21.151 seconds.
- The sign-in screen was visually inspected at 360×640 dp and 411×914 dp. No clipping was visible on this screen.
- The existing Flutter suite passed 198/198 tests on the API 37 emulator. The host Flutter suite also passed 198/198.
- API 29 and API 34 were not available locally. The prepared workflow covers API 29/Nexus 5 and API 34/Pixel 7 Pro, but its GitHub Actions jobs have not run.
- Live authentication, authenticated navigation, operational form entry, persisted offline drafts, reconnect synchronization, history against the live API, and server conflict behavior were not manually exercised on a device.

See [Android matrix and limits](android-compatibility.md) and the [manuscript-ready paragraphs](manuscript-ready-paragraphs.md).

### Web and API

The API run used 5 warm-up requests and 30 measured serial requests per endpoint. Every measured request returned HTTP 200.

| Endpoint | Samples | HTTP status | Median (ms) | p95 (ms) | Maximum (ms) |
|---|---:|---|---:|---:|---:|
| Asset registry, page 1 / 25 | 30 | 30 × 200 | 11.477 | 25.715 | 40.062 |
| PM schedule registry, page 1 / 25 | 30 | 30 × 200 | 14.257 | 20.265 | 30.365 |
| Inspection registry/form review, page 1 / 25 | 30 | 30 × 200 | 19.063 | 24.176 | 87.070 |

The response included up to 25 records per page. The final sample reported total result counts of 45 assets, 139 schedules, and 88 inspections. The timing measures the local HTTP request through full response-body read. It excludes React rendering and browser work; no full page-rendering metric was taken.

The React test suite passed 278/278 tests across 32 files, and the production build/typecheck passed. npm ci also printed an install-time advisory count of 43 dependency vulnerabilities (30 moderate and 13 high); those advisories were not triaged in this test run and are separate from the ZAP results.

See [Web performance results](web-performance.md).

### OWASP ZAP

The ZAP Automation Framework run completed on 2026-11-13 at 00:03:31 Asia/Manila against the unauthenticated local API. The plan ran five scoped spider jobs, passiveScan-wait, and report generation. It contains no activeScan job.

The report contains one low-risk X-Content-Type-Options Header Missing alert and two informational alert types: Authentication Request Identified and Information Disclosure - Sensitive Information in URL. The missing response header was independently checked on four local routes. The other alerts describe authentication routes and opaque synthetic user-ID query filters; no password or session token was found in those URL findings.

See [ZAP findings and coverage](zap-findings.md), [HTML report](zap-report-2026-11-13.html), and [JSON report](zap-report-2026-11-13.json).

## Reproduction

Run commands from the repository root. The API runner requires local SQL Server 2019 access through shared memory, .NET SDK 10, and Node on PATH. It creates a database whose name must begin with UniPM_Nonfunctional_. The reset switch drops only the specifically named database after validating that prefix. Use it only for the task's isolated test database.

    powershell -NoProfile -ExecutionPolicy Bypass -File .\reference\testing\nonfunctional-2026-11\run-api-performance.ps1 -DatabaseName UniPM_Nonfunctional_2026_11_12 -ResetDatabase -SampleCount 30

The runner creates synthetic development users, seeds synthetic/demo records, starts the ASP.NET API on 127.0.0.1:5254, measures the three endpoints, writes raw logs to the ignored artifacts directory, then stops the API and clears process credentials.

For ZAP, obtain the official ZAP 2.17.0 cross-platform ZIP, verify its SHA-256 against the value in the environment section, and extract it to artifacts/nonfunctional-2026-11/zap-install/ZAP_2.17.0. Keep the local synthetic database from the API run, then run:

    powershell -NoProfile -ExecutionPolicy Bypass -File .\reference\testing\nonfunctional-2026-11\run-zap-baseline.ps1 -DatabaseName UniPM_Nonfunctional_2026_11_12

The script starts the API, runs only the checked-in passive plan against loopback, stops the server, and copies its generated HTML and JSON reports into this folder. See [the plan](zap-passive-plan.yaml).

For the React checks, use Node major version 22 as specified by web/.nvmrc and web/package.json:

    Set-Location .\web
    npm ci
    npm run test:run
    npm run build

For local Android API 37 reproduction, use the installed Flutter version and emulator ID recorded above. For the requested API 29/API 34 runs, push this branch only if desired, then manually dispatch the workflow [nonfunctional-android-compatibility.yml](../../../.github/workflows/nonfunctional-android-compatibility.yml) from GitHub Actions. The workflow has not been dispatched as part of this record.

## Boundaries

The local run is a single-user, serial development workload with synthetic data and warmed application/database paths. It is not a capacity test, production benchmark, service-level objective, or React page-render benchmark. The ZAP baseline is passive and unauthenticated; it does not assess authenticated authorization boundaries or prove that the application is secure. Android API 29/API 34 and the live device workflows listed above remain open.
