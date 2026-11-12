# Evidence index

Test artifacts are separate from the manuscript. Committed reports and raw measurement data are in this directory. Large local logs and tool installations are ignored under artifacts/nonfunctional-2026-11. The generated ZAP reports are stored without line-ending conversion; artifact-checksums.sha256 records hashes for the committed reports, measurements, and screenshots.

## Committed result artifacts

- [API latency samples (CSV)](web-performance/api-latency-samples.csv)
- [API latency summary and environment (JSON)](web-performance/api-latency-summary.json)
- [ZAP report (HTML)](zap-report-2026-11-13.html)
- [ZAP report (JSON)](zap-report-2026-11-13.json)
- [Verified response-header observations (JSON)](zap-header-verification.json)
- [Android 37 compact viewport screenshot](android-api37-small-viewport.png)
- [Android 37 default viewport screenshot](android-api37-default-viewport.png)
- [Android compatibility matrix](android-compatibility.md)
- [Web performance methodology and summary](web-performance.md)
- [ZAP finding review](zap-findings.md)
- [Manuscript-ready methodology and results](manuscript-ready-paragraphs.md)
- [SHA-256 checksums for committed result artifacts](artifact-checksums.sha256)
- [Reproduction, status, and environment](README.md)

## Local raw source logs

These files are generated locally and ignored by Git. They remain available in the workspace at the paths below.

- Android installation and startup: ../../../artifacts/nonfunctional-2026-11/android-api37/adb-install.log, emulator.stdout.log, startup-log-extract.txt
- Android full Flutter suite on API 37: ../../../artifacts/nonfunctional-2026-11/android-api37/flutter-test-device-full.log
- Android app configuration tests on API 37: ../../../artifacts/nonfunctional-2026-11/android-api37/flutter-device-app-config.log
- Flutter host suite: ../../../artifacts/nonfunctional-2026-11/flutter-test-full.log
- Flutter analysis and format checks: ../../../artifacts/nonfunctional-2026-11/flutter-analyze.log, dart-format-check.log
- Flutter debug APK build: ../../../artifacts/nonfunctional-2026-11/flutter-build-apk-debug.log
- ASP.NET build, migrations, and seeding: ../../../artifacts/nonfunctional-2026-11/web-performance/20261112-235606/server-build.log, database-migrate.log, seed-development-users.log, seed-synthetic.log, seed-demo.log
- API benchmark measurements and run console: ../../../artifacts/nonfunctional-2026-11/web-performance/20261112-235606/api-latency-samples.csv, api-latency-summary.json, measurement-run.log
- API server log from benchmark: ../../../artifacts/nonfunctional-2026-11/web-performance/20261112-235606/api-server.stdout.log
- React unit tests and production build: ../../../artifacts/nonfunctional-2026-11/web-tests.log, web-build.log
- ZAP Automation Framework log and passive API server log: ../../../artifacts/nonfunctional-2026-11/zap/zap-automation.log, api-server.stdout.log
- ZAP header verification: ../../../artifacts/nonfunctional-2026-11/zap/header-verification.json
- ZAP plan validation log: ../../../artifacts/nonfunctional-2026-11/zap-plan-check.log
- Official ZAP ZIP, checked SHA-256, extracted package, and runtime logs: ../../../artifacts/nonfunctional-2026-11/zap-install/ and ../../../artifacts/nonfunctional-2026-11/zap/

No credentials or non-synthetic data are included in these reports or logs. The temporary development password and JWT signing key were not persisted.
