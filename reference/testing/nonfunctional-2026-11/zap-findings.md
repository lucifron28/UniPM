# OWASP ZAP passive baseline findings

## Scan identity and scope

- ZAP 2.17.0, Automation Framework
- Report generated: 2026-11-12T16:03:31.047Z (2026-11-13 00:03:31.047 Asia/Manila)
- Source commit: 53afe5708ca640b6fe7618589cbf3be1a1de6ae3
- Target: local ASP.NET Core API at http://127.0.0.1:5254
- Authentication: unauthenticated. The scanner did not receive the development account or access token.
- Scope: loopback API root, OpenAPI document and asset, schedule, and inspection registry routes.
- Jobs in the plan: five bounded spider jobs, passiveScan-wait, HTML report, JSON report.
- Active scanner: not present in the plan. No active attacks or third-party/production targets were used.

The scan log records successful plan completion. A spider job discovered the API document's route surface, so this is broader than only the three performance endpoints. The checked-in plan is the exact scan configuration.

## Finding classification

| ZAP alert | Risk / confidence | Instances | Review and verification |
|---|---|---:|---|
| X-Content-Type-Options Header Missing (10021) | Low / Medium | 1 in ZAP report | VERIFIED. The report flagged the local root. A separate local HTTP check found the header absent on GET /, /health/live, /health/ready, and /openapi/v1.json; all four returned HTTP 200 with their expected content types. The four-route check is preserved in zap-header-verification.json. This is a response-hardening gap. |
| Authentication Request Identified (10111) | Informational / Low | 2 | ROUTES REVIEWED. ZAP reported the OpenAPI-described POST operations /api/v1/auth/login and /api/v1/admin/accounts. This is an informational route-identification alert, not a confirmed vulnerability. No authenticated session was established. |
| Information Disclosure - Sensitive Information in URL (10024) | Informational / Medium | 4 | CONTEXT REVIEWED. The report names inspectorUserId, supervisorUserId, and selectedUserId query parameters on schedule assignment read routes. These are opaque synthetic user identifiers used as filters. No password or session token appeared in the reported URL parameters. URL log retention remains an application deployment consideration; this test did not inspect production logs. |

Risk and confidence labels above are the ZAP report classifications. The two informational signatures are retained as tool findings and are not presented as confirmed security vulnerabilities.

## Header remediation follow-up

The verified response-hardening gap was corrected in `server/Program.cs` at commit `31da92eb73fa7df8dafaaf378624390ed034010b`: the API now adds `X-Content-Type-Options: nosniff` to responses. This is middleware-only and does not alter business behavior, route contracts, or NLA logic.

The focused `FoundationEndpointsTests.Responses_include_the_content_type_nosniff_header` regression passed (1/1) across `/`, `/api/info`, `/health/live`, `/health/ready`, and `/not-found`. The Release backend build passed with 0 warnings and 0 errors. The local development test host logged that readiness was unhealthy because no database connection string was configured; that expected condition did not fail the header assertion.

The authorized passive baseline was repeated on 2026-11-13 at 01:13:32 Asia/Manila (`2026-11-12T17:13:32.349620Z`) against the same loopback API scope. The actual [after-fix HTML report](zap-report-after-header-fix-2026-11-13.html) and [JSON report](zap-report-after-header-fix-2026-11-13.json) are retained. The X-Content-Type-Options alert was absent; the same two informational alert types remained (2 Authentication Request Identified instances and 4 Information Disclosure - Sensitive Information in URL instances). This verifies the response-header change within this unauthenticated passive scan’s coverage. It does not establish complete security or authenticated authorization behavior.

The original [before-fix report](zap-report-2026-11-13.html), [JSON report](zap-report-2026-11-13.json), and four-route [header observations](zap-header-verification.json) are preserved unchanged for comparison.

## Actual reports

- [ZAP HTML report](zap-report-2026-11-13.html)
- [ZAP JSON report](zap-report-2026-11-13.json)
- [After-fix ZAP HTML report](zap-report-after-header-fix-2026-11-13.html)
- [After-fix ZAP JSON report](zap-report-after-header-fix-2026-11-13.json)
- [Header regression test log](security-header-regression-test.log)
- [npm dependency triage](security-dependency-triage.md)
- [Passive-only Automation Framework plan](zap-passive-plan.yaml)
- Raw automation and API logs are linked from the [evidence index](evidence-index.md).

## Reproduction and coverage boundary

ZAP 2.17.0 official cross-platform archive SHA-256: 94c8f767b1c2e94f0db66b3ae56514d5e3f5a728ee1b6c798e0c8fe2d61fbff0. The archive URL and official release record are listed in the main README.

After the local synthetic API database exists and the verified archive is extracted to artifacts/nonfunctional-2026-11/zap-install/ZAP_2.17.0, run:

    powershell -NoProfile -ExecutionPolicy Bypass -File .\reference\testing\nonfunctional-2026-11\run-zap-baseline.ps1 -DatabaseName UniPM_Nonfunctional_2026_11_12

The [ZAP baseline documentation](https://www.zaproxy.org/docs/docker/baseline-scan/) describes spidering followed by passive scanning; [ZAP passive scanning](https://www.zaproxy.org/docs/desktop/addons/passive-scanner/) analyzes traffic without modifying requests or responses. This run was unauthenticated and passive. It does not cover authenticated role authorization, active injection testing, business-logic attacks, mobile API workflows, deployed infrastructure, or third-party services. The results are security evidence for this bounded local baseline only, not a claim that UniPM is secure or has complete security coverage.
