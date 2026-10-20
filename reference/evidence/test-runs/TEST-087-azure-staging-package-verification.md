---
id: TEST-087
type: test-run
title: Azure staging package local verification
status: executed
recordedAtUtc: 2026-10-17T15:33:07Z
testedCommit: c51e673691dca541530886bfe0139150848db80b
sourceBranch: deploy/azure-staging
evidenceLevel: locally-executed
---

# Azure staging package local verification

## Identity and limits

Starting source was `ac62139af203ad06b6b1a712426d8558d73982d8` on
`feature/web-analytics-layout`. The combined ZIP was built from clean commit
`c51e673691dca541530886bfe0139150848db80b` on `deploy/azure-staging`.
Its SHA-256 is
`0d3b8bf582e24b404c5f798cc21d04f3d34795bb1d040526b4c9738ee38ffa33`.
The local artifact is
`artifacts/azure-staging/c51e673691dca541530886bfe0139150848db80b/unipm-azure-staging.zip`.

The successful smoke used that actual ZIP with the route correction committed
as `f8d1fd5c5f91c730079262350d0898eab3d6e761`. The correction changes only
the smoke harness, not application code. The full Playwright run used the
isolated tracked frontend source at `c51e673`.

Earlier focused tests, Release build, and full backend/unit coverage ran on
`0a8ae6a677a92c1c4969eeaa6df41c62ccbfce7d` plus the working implementation
patch subsequently committed in `7a05a6d`, `0a3c0b2`, and `55503be`.
These are working-patch executions, not exact-final-head test runs. Subsequent
changes before packaging affected scripts and documentation only. No binary
equivalence with a different build is claimed.

Environment: Windows, Node 22.23.3, .NET SDK 10.0.300, native SQL Server 2019
with Full-Text Search and disposable databases at compatibility 150. No Azure
resource was contacted, created, changed, or deployed. No external AI provider
call or held-out evaluation was performed. Existing environment files were
not edited.

## Executed checks

| Check | Result |
|---|---|
| Focused photo storage/endpoint tests | 19 passed after one test compilation correction |
| Focused hosting/Foundation/Auth/seeder tests | 37 passed |
| Release solution build | Passed, 0 errors; existing advisory and nullable warnings retained |
| Full backend Release suite with both SQL integration settings enabled | 570 passed, 1 optional external-provider test skipped, 0 failed |
| EF pending-model check | Passed after supplying the required process-only design-time connection setting; no model differences |
| Web focused environment/HTTP tests | 23 passed |
| Web formatting | Passed after formatting three changed files |
| ESLint and TypeScript | Passed |
| OpenAPI contract and generated-client consistency | Passed |
| Negative OpenAPI contract tests | 7 passed |
| Web unit suite with coverage | 230 passed across 29 files |
| Coverage | Statements 81.46%, branches 73%, functions 79.02%, lines 83% |
| Isolated production frontend build and .NET publish/ZIP | Passed |
| Actual ZIP HTTPS/API/browser smoke | Passed |
| Full mocked Playwright suite, 2 workers, port 5189 | 54 passed, 1 skipped, 2 failed |
| Git diff whitespace check | Passed at this record's preparation |

Commands included:

```text
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~PackagedFrontendHostingTests|FullyQualifiedName~FoundationEndpointsTests|FullyQualifiedName~AuthEndpointsTests|FullyQualifiedName~DevelopmentUserSeederTests" -m:1 /nodeReuse:false
dotnet build UniPM.slnx -c Release --no-restore -m:1 /nodeReuse:false
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj -c Release --no-build --no-restore -m:1 /nodeReuse:false
dotnet ef migrations has-pending-model-changes --project server/server.csproj --configuration Release --no-build
npm run format:check
npm run lint
npm run typecheck
npm run api:check
node web/scripts/test-negative-openapi-contract.mjs
npm run test:coverage
pwsh -NoLogo -NoProfile -File scripts/package-azure-staging.ps1
node scripts/smoke-azure-package.mjs artifacts/azure-staging/c51e673691dca541530886bfe0139150848db80b/unipm-azure-staging.zip
npm run e2e -- --workers=2
git diff --check
```

The web commands ran from the web directory, except the negative-contract
script shown from the repository root. Playwright ran from the package's
isolated `frontend-source` directory with Node 22 and a dedicated Vite port.
Connection values, JWT secrets, demo passwords, and cookie/token contents are
deliberately excluded.

## ZIP smoke result

The smoke extracted the ZIP, explicitly applied all migrations to an owned
SQL Server 2019 database, ran bounded offline Development seed commands,
and then started the package in Production. The HTTP server used an ephemeral
local HTTPS certificate and synthetic hostname resolved only by the clients.
No machine DNS, hosts file, or trusted certificate store was modified.

Verified React root and actual nested `/app` routes, immutable hashed assets,
uncached HTML, API information, healthy liveness/readiness after migration,
non-HTML API 404s, and no public OpenAPI. All four fictional demo accounts
logged in. Refresh cookies were Secure and HttpOnly, rotation succeeded,
logout revoked refresh, anonymous schedule reads returned 401, operational
roles returned 200, and Admin returned 403. Chromium loaded the ZIP UI with
no localhost requests. Filesystem photo storage was the local substitute;
this does not verify Azure Blob persistence or managed identity.

The owned database, API process, and certificate were removed in `finally`.
A subsequent local catalog query found no `UniPM_AzurePackageSmoke_` databases.
The normal application database was not modified.

## Retained failures

- Initial photo test compilation rejected collection-expression arguments;
  explicit byte arrays corrected the test only.
- Hosting test setup initially configured startup JWT values too late; early
  host settings corrected it. The subsequent header assertion incorrectly
  depended on Cache-Control order; parsed directives now verify the policy.
- The first EF command stopped before model comparison because its design-time
  factory required a connection setting. The approved rerun passed.
- Package attempt 1 exposed invalid PowerShell continuation in predicates.
  Attempt 2 hit a locked native module in the existing local Vite process.
  Isolated source/dependency installation fixed packaging. Missing local
  dependency files were restored without replacing existing/locked files.
- The initial ZIP smoke requested `/assets`, a reserved static prefix rather
  than UniPM's `/app/assets` route. The corrected harness passed against the
  same unchanged ZIP.
- Full Playwright failed two first-navigation checks before UI assertions:
  `app-shell.spec.ts:94` and `assets.spec.ts:294` exceeded 30 seconds in
  `page.goto`, then reported `net::ERR_ABORTED`. No cause is established.
  These failures are not claimed as a demonstrated UI defect or a proven
  environment-only issue. No retry has yet established a passing full suite.

Raw logs are retained under ignored `artifacts/azure-staging/qa/`, including
`backend-focused-cache-policy.log`, `backend-full.log`, `web-coverage.log`,
`ef-model-check-rerun.log`, `package-isolated.log`, `package-smoke-rerun.log`,
and `web-playwright.log`. Successful smoke details are in
`artifacts/azure-staging/smoke-7b5e40f1-348b-4115-985b-6531cb556a28/result.json`.

## Not verified and remaining deployment requirements

- A passing full Playwright run and exact-head GitHub CI are still required.
  The branch has not been pushed during this verification task.
- The owner reported creation of `inspection-photos` on October 17, 2026.
  Its account name remains a placeholder. Private access and container-scoped
  managed identity permissions have not been independently checked.
- Azure SQL preflight, actual Full-Text Search guard behavior, migrations,
  permissions, compatibility, and collation remain unverified on Azure.
- App Service runtime/plan availability, HTTPS/proxy behavior, outbound SQL
  firewall rules, deployment, restart durability, and physical mobile access
  remain unverified.
- Inherited Pkcs 9.0.4 NU1902/NU1903 advisories and the existing nullable
  warning require separate review. No dependency remediation is claimed.
- SQL and Blob are not atomic; best-effort cleanup cannot guarantee no orphan
  after process or network failure.
- GSD scheduling-effective-date approval, staging acceptance, and real-device
  testing remain pending. No PM workflow or CPMP rule was changed.

Status: local package smoke passed; verification incomplete. UNMERGED and
NOT DEPLOYED.
