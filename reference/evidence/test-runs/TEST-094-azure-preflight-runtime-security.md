---
id: TEST-094
type: test-run
title: Azure deployment preflight and runtime dependency security
status: executed
recordedAtUtc: 2026-10-18T04:00:23Z
testedCommit: 75ba43eb432c638be14f7344ad49959c56d63856
sourceBranch: deploy/azure-staging
evidenceLevel: locally-executed
---

# Azure deployment preflight and runtime dependency security

## Source and verification

The security patch at `75ba43eb432c638be14f7344ad49959c56d63856`
updates Axios from 1.18.1 to 1.20.0 and transitive Seroval from 1.5.5 to
1.6.9. Axios also raises its form-data dependency floor to 4.0.6. No API,
authentication, database, migration, seed or PM workflow implementation changed.

Using Node 22.23.3 on Windows, the executor ran:

```text
npm ci --ignore-scripts --no-audit --no-fund
npm run test:run -- src/api/http-client.test.ts src/api/problem-details.test.ts src/components/layout/app-shell-access.test.tsx src/routes/app/guard.test.ts
npm audit --omit=dev --package-lock-only --ignore-scripts --json
```

All commands exited 0. The focused batch passed 42 tests across four files.
jsdom reported its existing unimplemented scrollTo warnings; no test failed.
The production-only audit returned zero vulnerabilities. It does not establish
that development tooling is free of advisories. The earlier full lock audit
reported 14 development-only vulnerable package records, which this patch did
not broadly upgrade. The earlier NuGet audit found Pkcs 9.0.4 advisories; that
assembly was absent from the previously published API manifest and DLLs at
`a68acb517ef1c6b234120c773b2c1e94d6f4ef89`.

Raw outputs and command/exit metadata remain in ignored
`artifacts/azure-deployment-preflight/` and its `runtime-security/` directory.
The npm-ci output was reconstructed from captured output after the executor's
initial relative log path resolved outside the repository. The focused test
and audit logs were captured in the repository's artifact directory.

## Executed Azure preflight

Azure CLI 2.91.0 browser login succeeded. The selected subscription was
confirmed as the owner's existing Free Trial, with spending limit On. The
remaining credit balance was not verified. Read-only CLI queries established:

- The existing resource group and storage account are in Japan West; SQL is
  in Japan East. Existing resources were not recreated or relocated.
- `stunipmstaging` has HTTPS-only access, minimum TLS 1.2 and anonymous Blob
  access disabled. `inspection-photos` exists with public access `None`.
- .NET 10 Linux runtime configuration `DOTNETCORE|10.0` is advertised, and
  Japan East is advertised for Linux B1. These listings do not prove quota.
- SQL uses its free offer and was Paused at discovery. Two temporary
  administrator IP firewall rules remained. They were not changed.
- No SQL Entra administrator was configured. No runtime SQL identity or
  managed-identity Blob role assignment was created.

After explicit owner approval, this provisioning command was executed. The
subscription identifier and output projection are omitted below:

```powershell
az appservice plan create --name asp-unipm-staging --resource-group rg-unipm-staging --location japaneast --sku B1 --is-linux --number-of-workers 1 --subscription <verified-owner-subscription> --output json --only-show-errors
```

It exited 1. Azure reported current B1 VM limit 0, current usage 0, required
capacity 1. A read-only plan lookup returned ResourceNotFound. No app creation
or ZIP deployment followed. Read-only usage discovery reported Japan East F1
and B1 limits of zero; Japan West, Southeast Asia and East Asia reported total
regional VM limits of zero. No alternate region/tier was provisioned, and the
subscription was not upgraded. Microsoft documents that Free Trial subscriptions
are [ineligible for quota increases](https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/azure-subscription-service-limits).

## Preserved boundary and remaining verification

The owner reports successful live initialization with four users, 25 assets,
105 schedules, 77 inspections, 42 forms and 29 acknowledgements. These counts
were not independently queried in this preflight. No live SQL data connection,
migration, initialization or reset command ran. No firewall, SQL permission or
Blob RBAC change was performed.

The accompanying template/documentation correction explicitly disables the
schedule worker until scheduling effective-date behavior receives approval.
Its runtime default remains unchanged. No seeding credentials belong in app
settings. Production, HTTPS, private photo storage and disabled AI remain required.

Fresh final-head packaging is the next local check after this record is
committed. This record does not claim that package passed. Full suites,
Playwright, live HTTPS/authentication, app-to-SQL, managed-identity photo access,
restart persistence, GSD acceptance and physical-device testing were not run.
No deployment URL exists. Subscription capacity is the current cloud blocker;
changing the offer, tier or region requires a new owner decision. Nothing was
merged into main.
