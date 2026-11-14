# UniPM non-functional test evidence — November 2026

## Scope and status

This evidence set records local synthetic-data performance and passive security checks, a header remediation follow-up, npm advisory triage, and GitHub Actions emulator runs. Application business behavior, schema, routes, authorization, event lifecycle, and NLA interpretation/reporting logic were not changed in this testing work.

| Area | Outcome |
|---|---|
| Android 10/API 29 and Android 14/API 34 | Latest TEST-201 run [43574384452](https://github.com/lucifron28/UniPM/actions/runs/43574384452), commit `ba58b4f2a7bde7889c59b62fb25b7748741eb4e8`: API 29 installed and displayed sign-in, then the test helper failed to resolve the login page's `SingleChildScrollView` viewport before authentication. API 34 installed but a Pixel Launcher ANR blocked responsive startup. History, PM schedule, form lifecycle, offline persistence/restart, and synchronization were not executed. Compatibility remains incomplete. See the [matrix](android-compatibility.md) and [TEST-201](../../evidence/test-runs/TEST-201-android-compatibility-follow-up-prepared-2026-11.md). |
| Supplemental Android 17/API 37 | Local synthetic-account smoke passed login/navigation, asset search/details, and display of two maintenance-history records. PM form-list loading timed out; offline/reconnect synchronization and live conflict recovery were not executed. This is not evidence for API 29/API 34. |
| Web/API performance | Executed on the actual local ASP.NET Core API with synthetic SQL Server data: 30 sequential measurements per endpoint, all HTTP 200. React tests/build also passed. |
| OWASP ZAP | Authorized unauthenticated passive scan repeated after the header fix. The initial missing-header alert is absent from the after-fix report; two informational alert types remain. |
| npm audit | Original audit: 43 vulnerable package entries (13 high, 30 moderate; 3 runtime and 40 development). After compatible lockfile remediation: 10 moderate development-only entries; production-only audit: 0. |

These outcomes are bounded evidence, not a production load test, a complete mobile compatibility acceptance, or a full security assessment.

## Tested source and environment

- Original API performance, first ZAP report, and mobile test source: `53afe5708ca640b6fe7618589cbf3be1a1de6ae3`
- Response-header remediation: `31da92eb73fa7df8dafaaf378624390ed034010b`
- Web dependency remediation: `77e23526c9ef906c6157bfe70d0c45769e7d029b`
- Initial Android matrix source: `530b5be36727698f3e66935e56e53c698e4df52b`
- Latest executed Android compatibility run: 43574384452, commit `ba58b4f2a7bde7889c59b62fb25b7748741eb4e8`
- TEST-201 Run 5 found an uncovered test-helper case (`SingleChildScrollView`/`RenderSingleChildViewport`) on API 29 and a Pixel Launcher ANR on API 34. See the run evidence for exact per-profile outcomes and artifact links.
- Work branch: `codex/nonfunctional-testing-2026-11`
- Test window: 2026-11-12 through 2026-11-14 UTC/Asia-Manila
- Host for local API checks: Windows 11 10.0.26200 x64; 16 logical CPUs; 15.4 GiB RAM
- Backend: .NET SDK 10.0.400; local SQL Server 2019 Developer 15.0.2210.4, compatibility level 150
- Web checks: Node.js 22.23.5 and npm 10.9.9
- API measurement client: Node.js 24.15.0; it sent HTTP requests and read response bodies but did not run React or a browser
- Android: GitHub Actions Ubuntu 24.04, Flutter 3.47.1 stable, Java 21, x86_64 software-rendered AVDs; API 29 Nexus 5 (1080×1920 at 480 dpi) and API 34 Pixel 7 Pro (1440×3120 at 560 dpi)
- ZAP: version 2.17.0, official cross-platform archive SHA-256 `94c8f767b1c2e94f0db66b3ae56514d5e3f5a728ee1b6c798e0c8fe2d61fbff0`

The performance benchmark and ZAP targets were restricted to `127.0.0.1`. The test database `UniPM_Nonfunctional_2026_11_12`, seeded records, and development account were synthetic. No credential or JWT signing key is included in the committed evidence.

## Android results

GitHub Actions run [43296932992](https://github.com/lucifron28/UniPM/actions/runs/43296932992) completed on source SHA `530b5be36727698f3e66935e56e53c698e4df52b`; both jobs concluded success. Each profile built and installed the app, observed `MainActivity` and a running package process, captured a screenshot, and passed `test/app_config_test.dart` 4/4. `am start -W` returned `Status: timeout` on both even though the activity and process were reported.

The actual API 29 image shows “System UI isn't responding”; the API 34 image shows “Pixel Launcher isn't responding”. Neither image shows the UniPM interface. Accordingly, the CI helper's green result is limited to build/install/process/configuration checks; visible app startup failed visual review, and layout overflow was not assessed. API 29/API 34 login, asset lookup, inspections, offline drafts, synchronization, history, and conflict/error recovery were not executed.

Actual files: [API 29 screenshot](android-workflow-runs/432969/api29/startup-29.png), [API 29 helper log](android-workflow-runs/432969/api29/run-29.log), [API 29 full job log](android-workflow-runs/432969/api29/actions-job.log); [API 34 screenshot](android-workflow-runs/432969/api34/startup-34.png), [API 34 helper log](android-workflow-runs/432969/api34/run-34.log), [API 34 full job log](android-workflow-runs/432969/api34/actions-job.log).

An earlier local Android 17/API 37 run passed 198/198 Flutter tests; the host suite also passed 198/198, `flutter analyze --no-pub` found no issues, and the Dart formatting check reported 64 files with no changes. The tests for assets, maintenance history, offline drafts, and errors use fakes/test doubles. The local authenticated API 37 smoke is reported separately in [android-api37-live/README.md](android-api37-live/README.md). It passed login/navigation, asset lookup, and two synthetic history records; the PM form list timed out after 20,046 ms, and offline persistence/reconnect sync and live conflict/error handling were not executed.

The sequence of earlier CI install/startup/screenshot failures and their helper logs is retained in `android-workflow-runs/` and summarized in the [Android matrix](android-compatibility.md). Those attempts are preserved, not counted as API 29/API 34 passes.

### Android 10/API 29 compatibility retests

[Run 43391036513](https://github.com/lucifron28/UniPM/actions/runs/43391036513) tested 7b53e82126d2a6218bcacf92596285431f2740d8. Android 10 booted, installed the APK, and showed the sign-in controls in the foreground hierarchy; the startup screenshot and 4/4 configuration tests passed. The authenticated Flutter Driver smoke timed out at request_data before login. [Run 43396819069](https://github.com/lucifron28/UniPM/actions/runs/43396819069), tested at 937c59173caa57faf9fe1ed02ed6879691b6cba9, used Flutter's device integration test path. API 29 and API 34 both installed, displayed sign-in, passed configuration checks, synthetic login, authenticated navigation, and dashboard smoke. Both then failed after selecting the DEMO-EL-001 search result because the asset-details control did not appear within 30 seconds. Maintenance history, inspection forms, offline persistence, and reconnect synchronization were not reached or executed. See the [Android matrix](android-compatibility.md), [TEST-196](../../evidence/test-runs/TEST-196-nonfunctional-android-api29-compatibility-retest-2026-11.md), and [TEST-197](../../evidence/test-runs/TEST-197-nonfunctional-android-api29-workflow-followup-2026-11.md). Artifacts: [API 29 first retest](https://github.com/lucifron28/UniPM/actions/runs/43391036513/artifacts/14022946828) · [API 29 follow-up](https://github.com/lucifron28/UniPM/actions/runs/43396819069/artifacts/14023631972).
Run 43444880972, recorded in TEST-200, tested 523cce1898956434c70d50ebb6b1142be459c7a8 and passed through asset details on both profiles. API 29 stopped at a test-side lazy History selector before the history endpoint; API 34 displayed the seeded history record. TEST-201 Run 43548924029 later passed history but failed at two lazy-list waits. The prepared source then passed static validation and was exercised by Run 43565671645, which confirmed a test-helper scroll-viewport mismatch. Form lifecycle, offline persistence after restart and reconnection synchronization remain incomplete. The earlier e8f0 preflight was blocked before execution; its one-run authorization was later used for the push-triggered run.

## Web/API performance

The local benchmark used five warm-up requests followed by 30 serial measurements per endpoint. Each timed request included reading its full HTTP response body. Every measured request returned HTTP 200; results are API latency, not browser page-render measurements.

| API | Samples | Status | Median (ms) | p95 (ms) | Maximum (ms) |
|---|---:|---|---:|---:|---:|
| Asset registry, page 1 / 25 | 30 | 30 × 200 | 11.477 | 25.715 | 40.062 |
| Preventive-maintenance schedule registry, page 1 / 25 | 30 | 30 × 200 | 14.257 | 20.265 | 30.365 |
| Inspection registry/form review, page 1 / 25 | 30 | 30 × 200 | 19.063 | 24.176 | 87.070 |

The pages contained up to 25 records. Final reported totals were 45 assets, 139 schedules, and 88 inspections. The run was single-user, serial, and used warmed application/database paths; it is not a capacity or service-level test. See [web-performance.md](web-performance.md), the [raw CSV](web-performance/api-latency-samples.csv), and the [summary JSON](web-performance/api-latency-summary.json).

The React suite passed 278/278 tests across 32 files. The production build and typecheck passed. These are separate from the measured API latency.

## OWASP ZAP and npm audit

The initial ZAP 2.17.0 passive baseline completed on 2026-11-13 at 00:03:31 Asia/Manila against the unauthenticated local API. It reported one low-risk `X-Content-Type-Options Header Missing` alert, two informational `Authentication Request Identified` instances, and four informational `Information Disclosure - Sensitive Information in URL` instances. Four local routes independently confirmed the absent header.

The API added `X-Content-Type-Options: nosniff` in `server/Program.cs` at commit `31da92eb73fa7df8dafaaf378624390ed034010b`. The focused header regression passed 1/1 across five routes, and the Release backend build passed with zero warnings/errors. The repeated passive scan completed 2026-11-13 at 01:13:32 Asia/Manila; the missing-header alert is absent. The same two informational alert types remain (2 and 4 instances); their reviewed parameters were opaque synthetic user IDs, not passwords or session tokens. Reports: [before HTML](zap-report-2026-11-13.html), [before JSON](zap-report-2026-11-13.json), [after HTML](zap-report-after-header-fix-2026-11-13.html), and [after JSON](zap-report-after-header-fix-2026-11-13.json). The scan was passive and unauthenticated and does not assess all security properties.

The original npm audit reported 43 affected package entries (13 high, 30 moderate). Comparing the source lockfile classified 3 entries as runtime/transitive and 40 as development; compatible fixes removed all 3 runtime entries from the audit graph. The latest full audit reports 10 moderate development-tool entries and no high/critical findings; `npm audit --omit=dev` reports zero. Remaining ESLint/minimatch and TypeScript-ESLint entries are documented with their constraints and deferred options in [security-dependency-triage.md](security-dependency-triage.md). The original, full-after, and production-after npm JSON reports are preserved.

The passive ZAP scan is a bounded development baseline only; it is not complete security coverage and does not prove the system secure.

## Reproduction

API performance (isolated, prefixed synthetic database):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\reference\testing\nonfunctional-2026-11\run-api-performance.ps1 -DatabaseName UniPM_Nonfunctional_2026_11_12 -ResetDatabase -SampleCount 30
```

Repeat the passive ZAP baseline after the synthetic database exists and ZAP 2.17.0 has been checked against the archive hash above:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\reference\testing\nonfunctional-2026-11\run-zap-baseline.ps1 -DatabaseName UniPM_Nonfunctional_2026_11_12
```

React tests/build, using the repository's Node 22 range:

```powershell
Set-Location .\web
npm ci
npm run api:check
npm run test:run
npm run build
```

Android workflow run history and evidence links are in [android-compatibility.md](android-compatibility.md). The latest run is [43574384452](https://github.com/lucifron28/UniPM/actions/runs/43574384452), tested commit `ba58b4f2a7bde7889c59b62fb25b7748741eb4e8`. API 29 failed before authentication due to an uncovered test-helper viewport type; API 34 was blocked by a Pixel Launcher ANR. PM schedule UI, full form lifecycle, offline persistence after restart, and reconnection synchronization remain unexecuted in this run. The prior 43565671645, 43548924029, and blocked e8f0 preflight remain in the history.

## Evidence and boundaries

Committed artifacts, log sources, screenshots, and checksums are indexed in [evidence-index.md](evidence-index.md) and [artifact-checksums.sha256](artifact-checksums.sha256). Earlier Android run artifacts are checked into android-workflow-runs; Run 43574384452 logs and screenshots are preserved in the GitHub artifacts and the ignored local run folder, not committed.

Do not present these results as production testing, load-capacity evidence, a browser-render performance result, full Android workflow acceptance, authenticated ZAP coverage, active security testing, or complete security certification. Android API 29/API 34 functional workflows remain incomplete. Run 43574384452 identifies a test-helper viewport mismatch on API 29 and a launcher infrastructure block on API 34; it does not establish PM, form, offline, or synchronization behavior.
