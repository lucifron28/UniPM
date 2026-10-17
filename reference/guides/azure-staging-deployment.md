# Azure staging package and deployment guide

This guide prepares the UniPM web and API for a single-origin Azure App Service
staging deployment. It does not record a deployment: no Azure resource was
created or changed, no Azure database was contacted, and no staging login was
provisioned during this work.

The intended host is a managed .NET 10 Linux App Service in Japan East. Confirm
the .NET 10 runtime and the desired App Service plan are available in the
subscription and region in the Azure portal before provisioning. The package
is framework-dependent and portable; the App Service supplies .NET 10. It
contains the API at the site root and the compiled React application under
`wwwroot`.

## Build the reviewed package

Package from a clean, committed `deploy/azure-staging` branch with Node.js 22,
npm, and the .NET 10 SDK installed:

```powershell
pwsh -NoLogo -NoProfile -File scripts/package-azure-staging.ps1
```

The script restores the web lockfile, type-checks the frontend, builds it with
an empty Vite environment directory and the same-origin API base `/`, then
publishes the API and copies the frontend into the publish root. It rejects
unexpected `VITE_*` process variables, source maps, local API URLs, development
settings, and environment or user-secret files. It does not read or print `.env`
values. The recursive cleanup paths are constrained to the generated package
directory.

The resulting files are under
`artifacts/azure-staging/<source-commit>/`:

- `unipm-azure-staging.zip` — deployable package; `UniPM.Api.dll` and its
  runtime files are at the ZIP root, with the SPA in `wwwroot/`.
- `unipm-azure-staging.zip.sha256` — SHA-256 checksum.
- `provenance.json` — source commit, tool versions, publish mode, API base, and
  package checksum.

The `Azure staging package` GitHub Actions workflow creates and uploads these
artifacts for `deploy/**` pushes or a manual dispatch. It only builds an
artifact; it has no Azure credentials and does not deploy it. Check the
checksum and source commit before using an artifact. Keep the package tied to
the exact reviewed commit.

The API information endpoint is `GET /api/info`. In a package, `GET /` serves
the React application. Unknown `/api`, health, metrics, OpenAPI, Swagger, and
asset-file paths do not fall through to the SPA. API calls use the app's own
origin, and the existing `/api/v1` contracts are unchanged.

## Configure the existing staging resources

Provisioning and any later deployment require separate operator authorization.
The SQL logical server and database identified for staging are expected to
exist already. The storage account name has not been provided; use only the
account confirmed by the owner. Do not substitute an assumed account name.

Set `HTTPS Only` for the App Service and configure its managed identity. Grant
that identity `Storage Blob Data Contributor` access scoped to the existing
`inspection-photos` container. Use account scope only if container scope cannot
be configured and the owner explicitly accepts the wider permission.
The container is fixed by the application. Confirm it exists in the portal;
create it there only if absent and separately authorized. The application
selects Azure Blob storage only when
`InspectionPhotoEvidence__StorageProvider=AzureBlob` and the configured
`InspectionPhotoEvidence__BlobServiceUri` is the HTTPS account-root URI. It
uses managed identity and never stores photo bytes in the public web root.

Use [`../templates/azure-staging-appsettings.env.example`](../templates/azure-staging-appsettings.env.example)
as a key/value checklist for App Service Configuration. Replace every
placeholder in the portal; do not commit a completed copy. In particular:

- Set `ASPNETCORE_ENVIRONMENT=Production` and `AllowedHosts` to the verified
  App Service hostname, without a scheme.
- Set the SQL connection string using the confirmed SQL server/database and a
  secret supplied through the portal. Require encryption and certificate
  validation. Do not put credentials in source control, command history, or
  evidence.
- Generate a high-entropy JWT signing key of at least 32 bytes and set issuer
  and audience values consistently with the client. `UNIPM_WEB_ORIGIN` must be
  the exact HTTPS origin, without a path. Production startup rejects a missing,
  HTTP, or localhost origin; refresh cookies remain secure outside Development.
- Configure `InspectionPhotoEvidence__StorageProvider=AzureBlob` and the
  verified storage account root URI. Keep `Embeddings__Enabled=false`,
  `NaturalLanguageAnalytics__Enabled=false`, and
  `Observability__MetricsEnabled=false` for this PMIS validation staging host.
  The PMIS workflows and deterministic PM analytics do not require an AI
  provider.
- Keep `PreventiveMaintenanceScheduleGeneration__WorkerEnabled=true` only if
  the staging owner wants the existing schedule-generation worker active.

Use the startup command `dotnet UniPM.Api.dll`. Disable App Service build
automation with `SCM_DO_BUILD_DURING_DEPLOYMENT=false`, because the ZIP already
contains the build. Keep the JWT signing secret stable across restarts and
redeployments. Confirm the .NET 10 managed Linux runtime in the portal.

App Service terminates HTTPS in front of the application. Do not enable blanket
forwarded-header trust. Verify the secure refresh-cookie behavior with the
actual App Service HTTPS configuration before inviting users. Do not weaken
cookie security to make an HTTP test pass.

## Database readiness and demo users

The application does not apply migrations at startup. Before any migration,
read [the Azure SQL migration audit](./azure-sql-migration-audit.md) and run
its companion [read-only preflight](../../database/azure-staging-preflight.sql)
against the explicitly confirmed staging database. Stop unless the database
identity, empty-schema state, permissions, compatibility level, and migration
history match the documented preconditions.

The migration chain includes historical Full-Text Search installation guards
that require `SERVERPROPERTY('IsFullTextInstalled')` to be `1`. Local SQL Server
2019 evidence does not prove this Azure SQL Database behavior. The preflight
captures the property but has not been run against Azure. Resolve this
compatibility check with a reviewed target-specific plan before applying the
migrations; do not edit historical migrations or claim Azure migration
readiness from the local checks.

Once those preconditions pass and migration execution is separately approved,
stop the App Service and other writers. Supply the migration connection through
`ConnectionStrings__DefaultConnection` in a trusted workstation process. Run
the published package from its publish directory:

```powershell
dotnet ./UniPM.Api.dll --migrate-database
```

This explicit maintenance command does not start HTTP hosting. Capture its exit
code, rerun the read-only preflight, and require all 22 migration IDs through
`20261010141251_AddInspectionPhotoEvidence`. Stop on a partial migration or any
unexpected schema. Never run a destructive reset as recovery.

Allow only the administrator's current public IP for this operation. For runtime
SQL access, allow the App Service's documented outbound IP addresses. Keep
blanket Azure-service access disabled. Recheck outbound addresses after plan or
network changes, and remove temporary administrator firewall rules afterward.

After a separately approved migration and a fresh readiness check, the
maintainer can run
[`initialize-azure-staging-demo.ps1`](../../scripts/initialize-azure-staging-demo.ps1)
from a trusted workstation. It requires the exact target confirmation
`sql-unipm-staging.database.windows.net/unipm-staging-db`, a read-only
preflight, an empty target with the latest migration, and an interactive typed
confirmation. It runs the existing Development-only seed commands as bounded
local child processes and exits before starting a web host. It does not enable
Development mode in App Service, reset a database, or create a Department Head
login. Provide `ConnectionStrings__DefaultConnection` and
`UNIPM_STAGING_DEMO_PASSWORD` through a secure process environment; never put
their values in command arguments, source control, or this guide. The script's
optional `-PublishedApiDll` parameter defaults to the package's
`publish/UniPM.Api.dll` for the current full Git SHA.

The existing demo seeder internally calls `MigrateAsync`; the wrapper requires
the exact current migration history so that call is expected to do no work.
Keep all writers stopped throughout initialization. The four demo logins are
`admin@unipm.local`, `gsd@unipm.local`, `inspector@unipm.local`, and
`supervisor@unipm.local`. Their password comes only from the supplied secret.
The legacy DepartmentHead role remains for contract compatibility; no matching
login is created by this initializer.

The App Service health endpoints are `/health/live` and `/health/ready`. The
readiness endpoint checks SQL connectivity. It can succeed against an empty
database and does not prove that migrations or demo initialization completed.
Verify migration history separately. Startup does not migrate or seed data.

## Smoke tests and recovery

On Windows with native SQL Server 2019 and Full-Text Search, run the local
package smoke procedure with Node 22 after packaging:

```powershell
node scripts/smoke-azure-package.mjs artifacts/azure-staging/<source-commit>/unipm-azure-staging.zip
```

It verifies the ZIP checksum, extracts the actual ZIP, creates an owned
disposable database, applies migrations, seeds four fictional roles, and starts
the package in Production with an ephemeral local HTTPS certificate. It checks
routes, cookie rotation/logout, schedule authorization, and Chromium navigation.
It removes its database, process, and certificate even after a failed check.
Its filesystem storage substitute does not test Azure Blob durability.
The synthetic hostname resolves only inside the smoke clients; no machine DNS,
hosts file, or trusted certificate store is changed. Review the generated
`artifacts/azure-staging/smoke-*/result.json` and the verification record.

Against the later HTTPS staging host, check `/`,
a direct nested React URL, `/api/info`, a hashed static asset, `/health/live`,
and `/health/ready`. An unknown `/api/v1` path must return 404 without HTML.
Confirm no browser API request targets localhost. Sign in as each demo role,
refresh the session, inspect the Secure and HttpOnly refresh cookie, log out,
and confirm role restrictions. Verify photo upload/read/replacement and access
after an App Service restart using the actual private Blob container. Local
filesystem tests cannot establish Azure photo durability or identity access.

Flutter will use this same HTTPS API origin through its existing API-base
configuration. No mobile code or authentication contract changes are included.
Physical-device connectivity and workflow acceptance remain pending.

Retain the previous approved ZIP, checksum, configuration version, and a SQL
backup or restore point before later deployment. App rollback means restoring
that ZIP with compatible configuration. Do not automatically run EF `Down`:
some historical migrations drop data, and RAG rollback recreates empty derived
tables. Restore or recover the database only through a reviewed backup plan.
Blob objects and SQL references are not one transaction. Review orphaned photo
objects before deletion; a process crash or unavailable storage can leave an
orphan even though request cleanup is best-effort.

## Cost controls and known risks

Confirm the App Service plan price in the subscription before provisioning.
A paid plan can keep charging when its app is stopped. Remove the paid plan
when it is no longer needed, subject to owner approval. Keep SQL free-offer
overage billing disabled and review serverless auto-pause and quota behavior.
Blob capacity, requests, and data transfer can incur charges. Set budget alerts
and remove only owner-approved temporary resources after testing.
[App Service plan billing](https://learn.microsoft.com/en-us/azure/app-service/overview-hosting-plans),
[Azure SQL free offer](https://learn.microsoft.com/en-us/azure/azure-sql/database/free-offer)

The current EF SQL Server dependency chain resolves
`System.Security.Cryptography.Pkcs` 9.0.4 and emits NU1902/NU1903 advisories.
This predates the Blob adapter. Review and remediate the dependency before
public deployment; passing tests do not resolve a security advisory.
[Moderate advisory](https://github.com/advisories/GHSA-2fmv-rv2c-59x7),
[high advisory](https://github.com/advisories/GHSA-846f-f764-76mx)

## Evidence still required

The package workflow has not been run against Azure and the App Service has not
been deployed. Before opening staging to GSD, record the exact package commit
and checksum, verify the portal runtime and host configuration, complete the
Azure SQL Full-Text Search and migration preflight, validate managed identity
access to the confirmed Blob container, and perform an HTTPS smoke test of
login, secure refresh/logout, the PM workflows, photo access, and readiness.
Keep that evidence sanitized and distinguish local package checks from Azure
execution.

Official deployment references: [App Service ZIP deployment](https://learn.microsoft.com/en-us/azure/app-service/deploy-zip),
[run from package](https://learn.microsoft.com/en-us/azure/app-service/deploy-run-package),
[App Service HTTPS settings](https://learn.microsoft.com/en-us/azure/app-service/configure-common),
[App Service managed identity with Blob Storage](https://learn.microsoft.com/en-us/azure/app-service/scenario-secure-app-access-storage),
and [.NET App Service quickstart](https://learn.microsoft.com/en-us/azure/app-service/quickstart-dotnetcore).
