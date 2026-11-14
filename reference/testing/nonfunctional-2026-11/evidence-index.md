# Evidence index

The test results are separate from the manuscript. Committed raw reports, measurements, screenshots, emulator logs, and summaries are preserved in this directory. Large local setup logs and downloaded tools remain under the ignored `artifacts/nonfunctional-2026-11/` directory. SHA-256 hashes for committed raw result artifacts are in [artifact-checksums.sha256](artifact-checksums.sha256).

## Primary records and summaries

- [TEST-201 Android compatibility verification](../../evidence/test-runs/TEST-201-android-compatibility-follow-up-prepared-2026-11.md) — latest run 43574384452 tested `ba58b4f2a7bde7889c59b62fb25b7748741eb4e8`. API 29 failed before authentication on the helper's unhandled `SingleChildScrollView`; API 34 startup was blocked by Pixel Launcher ANR. History, PM schedule, form lifecycle, offline persistence/restart and sync were not executed. Earlier 435656/435489 runs, blocked e8f0 preflight and TEST-198/199/200 records remain preserved.
- [TEST-200 Android smoke rerun](../../evidence/test-runs/TEST-200-nonfunctional-android-smoke-rerun-2026-11.md) — run 43444880972 passed asset details on both profiles; API 29 stopped at the lazy History selector and API 34 stopped at a completed-schedule fixture mismatch.
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

### Earlier TEST-201 compile-failure run — 43468215938

The [TEST-201 record](../../evidence/test-runs/TEST-201-android-compatibility-follow-up-prepared-2026-11.md) links API 29 job 132032945952 and artifact 14061360039, plus API 34 job 132032945197 and artifact 14060386895. Both jobs passed database/API/APK setup, then failed to compile the integration test because `offlineRemarks` was declared twice. Initial artifact download was rejected with HTTP 503, but a later supported retrieval confirmed the compile error. No functional assertions ran.

### Previous executed Android smoke — TEST-201 / run 43548924029

Run [43548924029](https://github.com/lucifron28/UniPM/actions/runs/43548924029) tested commit `9bbbe0b69ed23689d12c9a2603588d9e244787e5` on the two isolated profiles. API 29 job [132306614108](https://github.com/lucifron28/UniPM/actions/runs/43548924029/job/132306614108) uploaded [artifact 14095748055](https://github.com/lucifron28/UniPM/actions/runs/43548924029/artifacts/14095748055); API 34 job [132306613827](https://github.com/lucifron28/UniPM/actions/runs/43548924029/job/132306613827) uploaded [artifact 14095218129](https://github.com/lucifron28/UniPM/actions/runs/43548924029/artifacts/14095218129). Build/install/login/config and asset/history checks passed. API 29 failed the PM-summary wait before scrolling the lazy details list; API 34 returned to details but the test's History-control wait failed while that control was offscreen. Both jobs captured screenshots and widget/window diagnostics before teardown. Form lifecycle, offline persistence after restart, and reconnection synchronization were NOT EXECUTED. TEST-198, TEST-199 and TEST-200 evidence remains preserved.

Prepared source follow-up b3e82c43e78789d582656a44084db2a34b6df9c2 adds bounded screen-scoped scrolling, history-route verification, PM schedule assertions, and isolated scenarios. Static validation passed; at that stage shell and runtime/device execution were pending. Run 43565671645 later exercised the source, with the test-helper viewport mismatch and incomplete later workflows recorded above.


### Previous TEST-201 matrix — run 43565671645

Run [43565671645](https://github.com/lucifron28/UniPM/actions/runs/43565671645) tested commit 58b17b355aa02425c2610076c76800b3d0413693 on the testing branch. API 29 job [132358863068](https://github.com/lucifron28/UniPM/actions/runs/43565671645/job/132358863068) uploaded [artifact 14102497268](https://github.com/lucifron28/UniPM/actions/runs/43565671645/artifacts/14102497268); API 34 job [132358863365](https://github.com/lucifron28/UniPM/actions/runs/43565671645/job/132358863365) uploaded [artifact 14102302153](https://github.com/lucifron28/UniPM/actions/runs/43565671645/artifacts/14102302153). Both Linux Bash preflights, builds, installs, sign-in checks, navigation and configuration tests passed.

API 29 failed at _verifyHitTestPosition while opening Search Assets: target bounds were within the render viewport but outside the viewport selected by the test helper. History was not reached. API 34 passed asset details, HTTP 200 history API and visible history/return checks, then failed at the same Search Assets helper while starting the DEMO-EL-003 workflow. Both API logs show 32 requests with no non-2xx responses. The API 29 trace includes form-create/start requests, but its test case failed and complete form lifecycle assertions were not recorded. The API 34 PM fixture was eligible but its schedule UI was not opened. Offline network toggling, persisted local draft verification, process restart, reconnection and synchronization were not executed.

Failure screenshots are asset-history-asset-search-result-failure.png for API 29 and online-inspection-dashboard-pm-schedule-failure.png for API 34. Both artifacts retain Flutter widget/render diagnostics, Android window hierarchy, API/backend traces, emulator logcat and summary.json. Raw downloaded files remain ignored under artifacts/nonfunctional-android/run-43565671645/. The confirmed root cause is a test helper viewport mismatch; no production defect was established. The previous TEST-201/TEST-200/TEST-199/TEST-198 evidence remains preserved.

### Latest executed Android compatibility matrix — TEST-201 / run 43574384452

Run [43574384452](https://github.com/lucifron28/UniPM/actions/runs/43574384452) tested commit `ba58b4f2a7bde7889c59b62fb25b7748741eb4e8`. API 29 job [132385321984](https://github.com/lucifron28/UniPM/actions/runs/43574384452/job/132385321984) uploaded [artifact 14105902796](https://github.com/lucifron28/UniPM/actions/runs/43574384452/artifacts/14105902796); API 34 job [132385322306](https://github.com/lucifron28/UniPM/actions/runs/43574384452/job/132385322306) uploaded [artifact 14105947591](https://github.com/lucifron28/UniPM/actions/runs/43574384452/artifacts/14105947591). Both profiles passed APK build/install and profile validation, using isolated fresh synthetic databases.

API 29 displayed the sign-in screen and passed four Flutter configuration tests, then failed before credential entry: `VisibleScrollInteraction._viewportFor` could not resolve the `SingleChildScrollView`'s `_SingleChildViewport`/`RenderSingleChildViewport`. The API request-status files were empty. API 34 passed APK installation but the Pixel Launcher became unresponsive during activity startup. The ANR belongs to `com.google.android.apps.nexuslauncher`; the login UI was not verified in a responsive foreground window and the API request-status file was empty. Treat the API 29 result as a test-helper defect and API 34 as an emulator/launcher block, not as confirmed UniPM defects.

PM schedule, maintenance history, inspection form entry/save/reopen/edit, offline draft persistence after restart, reconnection/synchronization and server-state verification were NOT EXECUTED on either profile. Failure-time screenshots and diagnostics are in both artifacts; local raw downloads remain ignored under `artifacts/nonfunctional-android/run-43574384452/`. The run failed overall, no rerun was dispatched, and Android compatibility remains incomplete.
