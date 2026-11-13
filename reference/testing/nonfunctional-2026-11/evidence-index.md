# Evidence index

The test results are separate from the manuscript. Committed raw reports, measurements, screenshots, emulator logs, and summaries are preserved in this directory. Large local setup logs and downloaded tools remain under the ignored `artifacts/nonfunctional-2026-11/` directory. SHA-256 hashes for committed raw result artifacts are in [artifact-checksums.sha256](artifact-checksums.sha256).

## Primary records and summaries

- [TEST-193 original non-functional evidence](../../evidence/test-runs/TEST-193-nonfunctional-requirements-2026-11.md) â€” original API timings, initial passive ZAP scan, React suite/build, and supplemental API 37 tests. Preserved unchanged.
- [TEST-194 Android/security follow-up](../../evidence/test-runs/TEST-194-nonfunctional-android-security-followup-2026-11.md) â€” exact API 29/API 34 Actions outcomes and screenshots; X-Content-Type-Options remediation and repeated ZAP scan; npm advisory classification.
- [TEST-195 Android compatibility rerun](../../evidence/test-runs/TEST-195-nonfunctional-android-compatibility-rerun-2026-11.md) â€” latest run 43363392229; API 29 ADB disconnect; API 34 visible sign-in screen, configuration tests passed, authenticated driver timed out.
- [Android compatibility matrix](android-compatibility.md)
- [Web performance method and results](web-performance.md)
- [ZAP finding review](zap-findings.md)
- [npm dependency triage](security-dependency-triage.md)
- [Manuscript-ready methodology and results](manuscript-ready-paragraphs.md)
- [Reproduction commands, environment, and status](README.md)

## Raw result files

- API latency [samples CSV](web-performance/api-latency-samples.csv) and [summary JSON](web-performance/api-latency-summary.json)
- ZAP before-fix [HTML](zap-report-2026-11-13.html) and [JSON](zap-report-2026-11-13.json)
- ZAP after-fix [HTML](zap-report-after-header-fix-2026-11-13.html) and [JSON](zap-report-after-header-fix-2026-11-13.json)
- [Independent response-header observations](zap-header-verification.json) and [header regression test log](security-header-regression-test.log)
- npm [initial audit](npm-audit-before-2026-11-13.json), [full after-remediation audit](npm-audit-full-after-2026-11-13.json), [production-only audit](npm-audit-production-after-2026-11-13.json), and [triage](security-dependency-triage.md)
- API 37 [compact](android-api37-small-viewport.png) and [default](android-api37-default-viewport.png) sign-in screenshots
- API 37 [authenticated synthetic smoke record and screenshots](android-api37-live/README.md)

### GitHub Actions Android run 43296932992

Run URL: https://github.com/lucifron28/UniPM/actions/runs/43296932992

- API 29 / Android 10 / Nexus 5: [job 131457612840](https://github.com/lucifron28/UniPM/actions/runs/43296932992/job/131457612840), [startup screenshot](android-workflow-runs/432969/api29/startup-29.png), [helper log](android-workflow-runs/432969/api29/run-29.log), [full Actions log](android-workflow-runs/432969/api29/actions-job.log)
- API 34 / Android 14 / Pixel 7 Pro: [job 131457612496](https://github.com/lucifron28/UniPM/actions/runs/43296932992/job/131457612496), [startup screenshot](android-workflow-runs/432969/api34/startup-34.png), [helper log](android-workflow-runs/432969/api34/run-34.log), [full Actions log](android-workflow-runs/432969/api34/actions-job.log)
- Earlier failed-attempt helper logs: `android-workflow-runs/43284530284/`, `android-workflow-runs/43288290256/`, and `android-workflow-runs/43293011562/`; run URLs and stopping points are in [the Android matrix](android-compatibility.md).

The latest CI jobs built and installed the APK, observed the activity process, and passed four app-configuration tests per profile. Both screenshots show an Android system/launcher not-responding dialog over a blank screen. These files preserve actual results; they do not evidence successful UI rendering or authenticated compatibility on API 29/API 34.

## Local source logs

These local logs are generated under the ignored `artifacts/nonfunctional-2026-11/` workspace directory and are not committed:

- API 37 install/startup: `artifacts/nonfunctional-2026-11/android-api37/adb-install.log`, `emulator.stdout.log`, `startup-log-extract.txt`
- API 37 Flutter suite and configuration tests: `flutter-test-device-full.log`, `flutter-device-app-config.log`; host suite/analyzer/format/build logs are in the same parent directory
- API 37 authenticated smoke setup, requests, device hierarchies, and build/install logs: `artifacts/nonfunctional-2026-11/android-api37-live/`
- ASP.NET build, database migration, synthetic seeding, and performance logs: `artifacts/nonfunctional-2026-11/web-performance/20261112-235606/`
- React test/build logs: `artifacts/nonfunctional-2026-11/web-tests.log`, `web-build.log`
- ZAP automation and passive API server logs: `artifacts/nonfunctional-2026-11/zap/zap-automation.log`, `api-server.stdout.log`; ZAP plan validation: `artifacts/nonfunctional-2026-11/zap-plan-check.log`
- Official ZAP ZIP, verified checksum, extracted tool, and runtime files: `artifacts/nonfunctional-2026-11/zap-install/` and `artifacts/nonfunctional-2026-11/zap/`

No credentials or non-synthetic data are included in the committed reports, logs, or screenshots. Temporary synthetic-account credentials and the JWT signing key are excluded from Git and cleared after local runs.

### GitHub Actions Android rerun 43363392229

Run URL: https://github.com/lucifron28/UniPM/actions/runs/43363392229
Tested commit: 2fa502628a21e82686920a46680706b3ce0c68ba

- API 29 / Android 10 / Nexus 5: job 131681940613; artifact ID 14009432873; helper, summary, diagnostics, and full Actions job log under android-workflow-runs/43363392229/api29/. The final screenshot is empty because ADB lost the emulator before installation.
- API 34 / Android 14 / Pixel 7 Pro: job 131681941121; helper, summary, integration timeout log, configuration test log, full Actions job log, and screenshots under android-workflow-runs/43363392229/api34/. The startup screenshot shows UniPM sign-in. The authenticated test timed out before credential entry.
- SHA-256 hashes for all 54 downloaded run files (including zero-byte screenshots/logs) are recorded in `artifact-checksums.sha256`.
