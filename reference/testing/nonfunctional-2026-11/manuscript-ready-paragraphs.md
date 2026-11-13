# Manuscript-ready methodology and results

## Methodology

Web/API performance was measured against a local ASP.NET Core Release API using synthetic development records in SQL Server 2019 Developer (15.0.2210.4; compatibility level 150). The client used Node.js 24.15.0 on Windows 11 and collected five warm-up requests followed by 30 sequential measured GET requests for each endpoint. Timings ended after the complete response body was read. Login was excluded. The API performance run used source commit 53afe5708ca640b6fe7618589cbf3be1a1de6ae3.

OWASP ZAP 2.17.0 Automation Framework spidered and passively analyzed the loopback API before and after the X-Content-Type-Options fix. The scan was unauthenticated; no active scanner, production environment, or third-party target was used. Dependency audit reports before and after compatible lockfile changes were retained.

The Android GitHub Actions matrix specified Android 10/API 29 on the Nexus 5 small profile (360×640 dp) and Android 14/API 34 on the Pixel 7 Pro large profile (411×891 dp). The latest run was 43363392229 at commit 2fa502628a21e82686920a46680706b3ce0c68ba, using Ubuntu 24.04, Java 21, Flutter 3.47.1, x86_64 emulators, and synthetic development records/accounts.

## Results

The API benchmark produced 90 measured requests; all returned HTTP 200. Asset registry latency was median 11.477 ms, p95 25.715 ms, and maximum 40.062 ms. Preventive-maintenance schedule registry latency was 14.257 ms, 20.265 ms, and 30.365 ms. Inspection/form review latency was 19.063 ms, 24.176 ms, and 87.070 ms. The React suite passed 278 tests across 32 files; the production build and TypeScript checks passed.

The initial ZAP passive baseline reported one low-risk X-Content-Type-Options Header Missing alert, two informational Authentication Request Identified instances, and four informational Sensitive Information in URL instances. The response header was added as X-Content-Type-Options: nosniff. The focused five-route regression test passed 1/1; the after-fix passive report contained no missing-header alert and retained the same informational findings. The initial npm audit contained 43 affected package entries (13 high, 30 moderate; three runtime and 40 development). After compatible updates, the full audit contained 10 moderate development-only entries and no high or critical findings; the production-only audit returned zero findings.

The latest Android workflow completed with overall failure. API 29 reported the expected Android 10/API 29 profile and 360×640 dp viewport, and the debug APK built; ADB lost the emulator before installation, so the application was not installed or launched. API 34 reported Android 14/API 34 and a 411×891 dp viewport; the APK installed, MainActivity returned Status: ok, and the UniPM sign-in page was visible in the captured screenshot. The focused app-configuration tests passed 4/4. The authenticated Flutter Driver test connected but timed out at request_data before it logged a login-screen test pass or submitted credentials. Authenticated navigation, asset lookup, maintenance history, and inspection entry were therefore not verified on either profile. On-device offline persistence, reconnect synchronization, and live conflict/error handling were not executed. API 37 live-smoke evidence remains supplemental only.

## Limitations

API 29 installation and startup remain blocked by the ADB disconnect. API 34 startup-screen rendering and configuration tests passed, but authenticated workflows did not. The Android compatibility requirement is incomplete. The local API benchmark is a serial development measurement, not a capacity or service-level test, and it does not measure React page rendering. The ZAP exercise was unauthenticated and passive; it does not cover active attacks, authenticated authorization, deployed infrastructure, or every security property. These are bounded verification results, not claims of complete compatibility, production performance, or complete security validation.

Evidence references: android-compatibility.md, web-performance.md, zap-findings.md, security-dependency-triage.md, evidence-index.md, TEST-193, TEST-194, and TEST-195.
