# Azure staging package and deployment guide

This guide prepares the UniPM web and API for a single-origin Azure App Service
staging deployment. Package preparation did not deploy the application.
On October 18, 2026, the owner separately approved and executed all 22
migrations against the existing Azure SQL staging database. The final schema
was verified by a read-only follow-up. No demo login was provisioned and no
App Service was deployed. See
[TEST-089](../evidence/test-runs/TEST-089-azure-sql-staging-migration.md).

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
values. It installs frontend dependencies in an isolated copy of committed
web source, so a running local Vite server cannot lock that install. The
recursive cleanup paths are constrained to the generated package
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

The locally verified candidate starts from
`ac62139af203ad06b6b1a712426d8558d73982d8` and was packaged at
`c51e673691dca541530886bfe0139150848db80b`. Its ZIP checksum is
`0d3b8bf582e24b404c5f798cc21d04f3d34795bb1d040526b4c9738ee38ffa33`.
See [TEST-087](../evidence/test-runs/TEST-087-azure-staging-package-verification.md)
for the actual ZIP smoke and
[TEST-088](../evidence/test-runs/TEST-088-azure-staging-browser-followup.md)
for the passing browser follow-up. Later evidence commits are not a new local
build. A CI package has its own source commit and checksum; verify that
manifest rather than assuming the bytes match the Windows artifact.

## Configure the existing staging resources

Provisioning and any later deployment require separate operator authorization.
The SQL logical server and database identified for staging are expected to
exist already. The storage account name has not been provided; use only the
account confirmed by the owner. Do not substitute an assumed account name.
On October 17, 2026, the owner confirmed creating `inspection-photos`.
Container existence is owner-reported; anonymous-access settings and managed
identity permissions have not been independently verified.

Set `HTTPS Only` for the App Service and configure its managed identity. Grant
that identity `Storage Blob Data Contributor` access scoped to the existing
`inspection-photos` container. Use account scope only if container scope cannot
be configured and the owner explicitly accepts the wider permission.
The container is fixed by the application. Verify its private access settings
in the portal. The application
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
2019 evidence alone does not prove this Azure SQL Database behavior. The
separately executed live Azure preflight reported `1`, and the complete
22-migration chain succeeded on this staging database. The retained reference
full-text index and removal of retired maintenance storage were verified.
This resolves the guard question for the tested target and source, not every
future Azure database. Historical migrations were unchanged.

The staging database is now migrated and empty. Do not rerun the original
empty-database migration wrapper. Demo initialization still requires its
separate authorization and fresh checks against the migrated schema.

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

## Fictional demo initialization, separate approval required

The live database already has all 22 migrations. Do not rerun them. TEST-089
records the last verified empty state; this preparation task did not reconnect
to Azure or initialize it. Obtain separate owner authorization before executing
this procedure. These are demonstration credentials, not institutional accounts.

Prerequisites:

- Trusted Windows workstation, Windows PowerShell 5.1, .NET 10 runtime,
  `sqlcmd.exe`, Git, and a clean `deploy/azure-staging` checkout.
- Fresh package built from the exact checked-out commit. Earlier packages do
  not contain the staging guards. See the packaging procedure above.
- App Service and all other database writers stopped. The public application's
  environment stays `Production`; only the bounded local seed children use
  `Development`. No HTTP server is started by initialization.
- A working encrypted SQL connection, certificate validation enabled, and
  narrowly scoped temporary access for the workstation's current public IP.
  A phone hotspot is suitable when dorm Wi-Fi changes its public IP. Recheck
  the current IP and adjust only that exact temporary rule if it changes.
  Never allow an entire network or all Azure services. The script does not
  change firewall rules.

The initializer accepts four independent process secrets by default:
`UNIPM_STAGING_ADMIN_PASSWORD`, `UNIPM_STAGING_GSD_PASSWORD`,
`UNIPM_STAGING_INSPECTOR_PASSWORD`, and `UNIPM_STAGING_SUPERVISOR_PASSWORD`.
Each must satisfy Identity's minimum 12 characters, uppercase, lowercase,
digit and non-alphanumeric requirements; duplicates are rejected.

The owner explicitly approved a shared-password exception for this temporary
demo. Select `-AllowSharedDemoPassword` and provide only
`UNIPM_STAGING_DEMO_PASSWORD`. This is an explicit mode, never a fallback.
It cannot be combined with per-role secrets. A common password means knowledge
of one role's credential permits login to the other demo accounts. Use distinct
credentials for any later institutional deployment. No password value belongs
in this guide, source, evidence, chat, command arguments, or plaintext files.

After authorization, run the following in a trusted Windows PowerShell session.
Paste the command text, then enter credentials only at the hidden prompts.
No password literals enter command history. For independent passwords, change
`$useSharedDemoPassword` to `$false` before running; each role is prompted
separately. A password manager can generate the independent credentials.

```powershell
Set-Location C:\Users\Ron\Documents\projects\Capstone
$useSharedDemoPassword = $true # Explicit owner-approved temporary demo mode.
$credentialNames = @('UNIPM_STAGING_DEMO_PASSWORD',
    'UNIPM_STAGING_ADMIN_PASSWORD', 'UNIPM_STAGING_GSD_PASSWORD',
    'UNIPM_STAGING_INSPECTOR_PASSWORD', 'UNIPM_STAGING_SUPERVISOR_PASSWORD')
$temporaryNames = @('ConnectionStrings__DefaultConnection', 'SQLCMDPASSWORD') + $credentialNames
$savedEnvironment = @{}
$sqlSecret = $null
$connection = $null
function Read-ProcessSecret([string]$Name, [string]$Prompt) {
    $secret = Read-Host $Prompt -AsSecureString
    try {
        [Environment]::SetEnvironmentVariable($Name,
            [Net.NetworkCredential]::new('', $secret).Password, 'Process')
    }
    finally { $secret.Dispose() }
}
try {
    foreach ($name in $temporaryNames) {
        $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
    $sqlLogin = Read-Host 'Azure SQL login'
    $sqlSecret = Read-Host 'Azure SQL password' -AsSecureString
    $connection = [Data.SqlClient.SqlConnectionStringBuilder]::new()
    $connection.DataSource = 'tcp:sql-unipm-staging.database.windows.net,1433'
    $connection.InitialCatalog = 'unipm-staging-db'
    $connection.UserID = $sqlLogin
    $connection.Password = [Net.NetworkCredential]::new('', $sqlSecret).Password
    $connection.Encrypt = $true
    $connection.TrustServerCertificate = $false
    $connection.ConnectTimeout = 60
    [Environment]::SetEnvironmentVariable('ConnectionStrings__DefaultConnection',
        $connection.ConnectionString, 'Process')
    if ($useSharedDemoPassword) {
        Read-ProcessSecret 'UNIPM_STAGING_DEMO_PASSWORD' 'Shared demo account password'
    }
    else {
        foreach ($name in $credentialNames[1..4]) { Read-ProcessSecret $name $name }
    }
    & ./scripts/initialize-azure-staging-demo.ps1 `
        -ConfirmTarget 'sql-unipm-staging.database.windows.net/unipm-staging-db' `
        -AllowSharedDemoPassword:$useSharedDemoPassword
    # Independent read-only postflight, only after initialization succeeds.
    [Environment]::SetEnvironmentVariable('SQLCMDPASSWORD', $connection.Password, 'Process')
    & sqlcmd.exe -S $connection.DataSource -d $connection.InitialCatalog `
        -U $sqlLogin -N -x -b -l 60 -i database/azure-staging-demo-postflight.sql
    if ($LASTEXITCODE -ne 0) { throw 'Read-only demo postflight failed. Stop for review.' }
}
finally {
    foreach ($name in $temporaryNames) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
    }
    if ($null -ne $connection) { $connection.Clear() }
    if ($null -ne $sqlSecret) { $sqlSecret.Dispose() }
    $savedEnvironment.Clear()
    Remove-Item Function:\Read-ProcessSecret -ErrorAction SilentlyContinue
}
```

The initializer verifies the exact logical server/database, Azure SQL
EngineEdition 5, compatibility 150, available Full-Text Search, all 22 migration
IDs, all 22 application tables, empty tables, and read/write permissions. It
reports the collation for operator review and requires the exact `APP STOPPED`
confirmation phrase before any application writes. Do not confirm while another
writer is active. This check is not a concurrency lock.
Missing SQL target metadata or Full-Text availability is rejected explicitly,
including `NULL` values. `scripts/test-staging-sql-null-guards.ps1` verifies
these predicates with read-only expressions against local SQL Server only.

The account command precedes the demo-data command. The staging profile never
calls `MigrateAsync`, resets a database, deletes/replaces demo records, repairs
existing accounts, or updates an existing password. The legacy local development
profile retains its previous seeding/reseeding behavior. The four staging logins
are `admin@unipm.test`, `gsd@unipm.test`, `inspector@unipm.test` and
`supervisor@unipm.test`. The legacy DepartmentHead role remains for contract
compatibility, but no Department Head account is created.

Expected success is `UNIPM_STAGING_DEMO_VERIFIED`, four active accounts with
one intended role each, 25 assets, 105 schedules, 77 inspection rows, 42 forms,
and 29 acknowledgements. Campus facility names are publicly documented; equipment,
placements and maintenance records are synthetic. See the
[source-to-seed reference](mseuf-campus-staging-seed.md) for verified names,
inventory examples, identifier conventions and existing-local-data limitations.
The focused local seed and staging-guard result is recorded in
[TEST-092](../evidence/test-runs/TEST-092-mseuf-campus-staging-seed.md).
Existing synthetic signatures are
placeholder images, not real signatures or inspection photos. The data covers
pending work, Draft, Submitted, Acknowledged, on-time/late and operational/
non-operational examples, and the four CPMP categories. It does not establish
photo upload, physical geolocation, or real institutional acceptance. Reference
rows and inspection photos remain empty. No dataset expansion was needed.

On failure, stop. The two commands are separate processes; account creation can
partially commit before an error, and successful accounts may remain if demo
seeding fails. Neither command is an automatic recovery tool. Reinitialization
is deliberately rejected. Do not reset, delete rows, change passwords, rerun
migrations, or repeat the seed commands without diagnosis and new approval.
Use the read-only schema postflight from TEST-089 and
`database/azure-staging-demo-postflight.sql` to inspect migration IDs, expected
account/role membership and row counts. A failed count check is diagnostic, not
permission to alter data. Never select password hashes, tokens, or secrets for
an evidence log. The script withholds child process output on failure because
it can include sensitive configuration. Record only the sanitized failure and
which command started; seek reviewed recovery instructions.

After success or failure, remove temporary workstation firewall rules in the
portal and close the trusted session. The wrapper restores prior process
variables and clears its temporary secret references. The initializer restores
`SQLCMDPASSWORD`; it does not persist credentials. Keep seed variables out of
App Service configuration. Deployment, runtime role acceptance and actual Azure
initialization require their own verification and authorization.

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
and checksum, verify the portal runtime and host configuration, recheck the
database's current identity and migration history, validate managed identity
access to the confirmed Blob container, and perform an HTTPS smoke test of
login, secure refresh/logout, the PM workflows, photo access, and readiness.
Keep that evidence sanitized and distinguish local package checks from Azure
execution.

Official deployment references: [App Service ZIP deployment](https://learn.microsoft.com/en-us/azure/app-service/deploy-zip),
[run from package](https://learn.microsoft.com/en-us/azure/app-service/deploy-run-package),
[App Service HTTPS settings](https://learn.microsoft.com/en-us/azure/app-service/configure-common),
[App Service managed identity with Blob Storage](https://learn.microsoft.com/en-us/azure/app-service/scenario-secure-app-access-storage),
and [.NET App Service quickstart](https://learn.microsoft.com/en-us/azure/app-service/quickstart-dotnetcore).
