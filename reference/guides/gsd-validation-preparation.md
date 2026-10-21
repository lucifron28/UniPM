# GSD Validation Preparation

This guide prepares a fresh local LAN fallback and a separately approved Azure
replacement database for GSD validation. It does not authorize running either
environment, changing firewall rules, migrating or seeding a database, or
modifying Azure resources. Those actions require an approved target and operator
window. The scripts under `scripts/lan/` are preparation tooling; they do not
reset or migrate databases.

## Local LAN fallback

The LAN helper uses the API at TCP 5254 and Vite at TCP 5173. It accepts only an
active RFC1918 IPv4 address on a Windows Private network profile. SQL remains
local to the laptop and is never opened to the LAN. Startup checks the chosen
database with `SELECT 1`, then checks API liveness/readiness and the Vite API
URL. It does not migrate, reset, or seed. The normal schedule-generation
worker remains enabled and may add its ordinary generated schedules during
runtime, so check baseline counts before startup and expect counts to evolve
with real work. DeepSeek and embedding calls are disabled in this offline
configuration; NLA reports use the deterministic parser and therefore have
reduced language coverage.

The helper requires the installed .NET 10 SDK, Node.js 22.x (the web package's
engine range), and the existing `web/node_modules/vite` installation. It does
not install SDKs, packages, or dependencies. `Start-UniPmLan.ps1` accepts only
the dedicated `UniPM_GSD_Validation` database name and requires the same exact
value for `-ConfirmDatabase`.

The repository root `.env` currently points to the existing local `UniPMDb`
fixture. Do not use that fixture for GSD validation. Create a dedicated empty
database, for example `UniPM_GSD_Validation`, through the approved local SQL
administration process. Keep the root `.env` unchanged. In the operator's
PowerShell process, select the dedicated database explicitly:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Server=.;Database=UniPM_GSD_Validation;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
```

Before migration, use a read-only SQL session with that connection to confirm
the new database identity and that it has no user tables:

```sql
SELECT DB_NAME() AS DatabaseName,
       COUNT_BIG(*) AS ExistingUserTables
FROM sys.tables
WHERE is_ms_shipped = 0;
```

The expected target is `UniPM_GSD_Validation` with zero user tables. Stop for
manual review if either value differs. The LAN start command independently
rejects any database name other than this dedicated target.

The operator must verify the exact `DataSource/Database` target and confirm
that the dedicated database is new and empty before applying migrations. For
the local example above, the target string is `./UniPM_GSD_Validation`. Then
run the existing migration command. This example restores the previous process
environment even on failure:

```powershell
$names = @('ConnectionStrings__DefaultConnection', 'ASPNETCORE_ENVIRONMENT')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
  $env:ConnectionStrings__DefaultConnection = 'Server=.;Database=UniPM_GSD_Validation;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
  $env:ASPNETCORE_ENVIRONMENT = 'Development'
  $target = 'UniPM_GSD_Validation'
  $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($env:ConnectionStrings__DefaultConnection)
  $actualTarget = "$($builder.DataSource)/$($builder.InitialCatalog)"
  if ($actualTarget -cne "./$target" -or (Read-Host "Type APPLY to migrate the confirmed fresh target $actualTarget") -cne 'APPLY') { throw 'Target confirmation failed.' }
  dotnet run --project server/server.csproj -c Release --no-launch-profile -- --migrate-database
  if ($LASTEXITCODE -ne 0) { throw 'Migration command failed; stop for review.' }
}
finally {
  foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
```

This command is for a later approved operator run; it was not executed while
preparing this guide. Keep it bound to the explicitly selected fresh database.
After migration, the dataset and user-bootstrap commands must pass their current
migration-history and empty-target guards. The bootstrap creates the five
canonical roles and four fictional operational accounts (Admin, GSD, Inspector,
and Supervisor). Use process-only credentials; never put passwords in command
arguments, source files, logs, or committed configuration. Restore prior process
environment values when the operator process exits.

The dataset executor's separate opt-in commands are:

1. Bootstrap the four validation users with `--seed-gsd-validation-users`.
2. Add the validation data with `--seed-gsd-validation`.

Both require `UNIPM_GSD_VALIDATION_SEED_ENABLED=true`,
`UNIPM_STAGING_DEMO_INITIALIZATION=true`, and
`UNIPM_GSD_VALIDATION_TARGET_CONFIRMATION` set to exactly
`<SqlConnection.DataSource>/<SqlConnection.Database>` (case-insensitive, no
credentials), with `ASPNETCORE_ENVIRONMENT=Development`. For the local example,
the confirmation is `./UniPM_GSD_Validation`. Bootstrap also requires either
four distinct process-only passwords (`UNIPM_STAGING_ADMIN_PASSWORD`,
`UNIPM_STAGING_GSD_PASSWORD`, `UNIPM_STAGING_INSPECTOR_PASSWORD`, and
`UNIPM_STAGING_SUPERVISOR_PASSWORD`) or the explicitly enabled shared-password
mode (`UNIPM_STAGING_ALLOW_SHARED_PASSWORD=true` with
`UNIPM_STAGING_DEMO_PASSWORD`). Prefer four distinct passwords. Each must meet
the existing minimum policy: 12 characters with uppercase and lowercase
letters, a digit, and a symbol. Prompt for each with `Read-Host -AsSecureString`;
expose each only to the short-lived process
environment and restore previous values in `finally`.

```powershell
dotnet run --project server/server.csproj --no-launch-profile -- --seed-gsd-validation-users
dotnet run --project server/server.csproj --no-launch-profile -- --seed-gsd-validation
```

The first command creates the roles and four accounts; the second requires
those accounts and seeds the fixture. Neither applies migrations, and the
dataset command requires all other mapped tables to be empty. Do not host the
public staging service in Development. Never point either command at `UniPMDb`
or an existing staging/live database. See the
[validation dataset manifest](gsd-validation-dataset.md) for planned counts,
scenarios, and live batch identifiers; those counts are not acceptance evidence
until the operator verifies them.

For a later approved local run, the following pattern keeps the connection and
account values in this PowerShell process only, prompts without echoing the
passwords, and restores previous process values even if a command fails. Review
the printed target carefully; this example assumes the fresh local database
shown above and has not been executed.

```powershell
$names = @(
  'ConnectionStrings__DefaultConnection', 'ASPNETCORE_ENVIRONMENT',
  'UNIPM_STAGING_DEMO_INITIALIZATION', 'UNIPM_GSD_VALIDATION_SEED_ENABLED',
  'UNIPM_GSD_VALIDATION_TARGET_CONFIRMATION', 'UNIPM_STAGING_ADMIN_PASSWORD',
  'UNIPM_STAGING_GSD_PASSWORD', 'UNIPM_STAGING_INSPECTOR_PASSWORD',
  'UNIPM_STAGING_SUPERVISOR_PASSWORD', 'UNIPM_STAGING_ALLOW_SHARED_PASSWORD',
  'UNIPM_STAGING_DEMO_PASSWORD'
)
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
function Set-TemporarySeedPassword([string]$Name, [string]$Prompt) {
  $secure = Read-Host -Prompt $Prompt -AsSecureString
  $credential = [System.Management.Automation.PSCredential]::new('UniPM', $secure)
  [Environment]::SetEnvironmentVariable($Name, $credential.GetNetworkCredential().Password, 'Process')
  $credential = $null
  $secure.Dispose()
}
function Invoke-GsdSeed([string]$Argument) {
  & dotnet run --project server/server.csproj --no-launch-profile -- $Argument
  if ($LASTEXITCODE -ne 0) { throw "Operator command failed: $Argument" }
}
try {
  $env:ConnectionStrings__DefaultConnection = 'Server=.;Database=UniPM_GSD_Validation;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
  $env:ASPNETCORE_ENVIRONMENT = 'Development'
  $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($env:ConnectionStrings__DefaultConnection)
  $target = "$($builder.DataSource)/$($builder.InitialCatalog)"
  if ((Read-Host "Type the exact fresh target to confirm: $target") -cne $target) { throw 'Target confirmation failed.' }
  $env:UNIPM_GSD_VALIDATION_TARGET_CONFIRMATION = $target
  $env:UNIPM_STAGING_DEMO_INITIALIZATION = 'true'
  $env:UNIPM_GSD_VALIDATION_SEED_ENABLED = 'true'
  $env:UNIPM_STAGING_ALLOW_SHARED_PASSWORD = 'false'
  Set-TemporarySeedPassword 'UNIPM_STAGING_ADMIN_PASSWORD' 'Admin account password'
  Set-TemporarySeedPassword 'UNIPM_STAGING_GSD_PASSWORD' 'GSD account password'
  Set-TemporarySeedPassword 'UNIPM_STAGING_INSPECTOR_PASSWORD' 'Inspector account password'
  Set-TemporarySeedPassword 'UNIPM_STAGING_SUPERVISOR_PASSWORD' 'Supervisor account password'
  Invoke-GsdSeed '--seed-gsd-validation-users'
  Invoke-GsdSeed '--seed-gsd-validation'
}
finally {
  foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
}
```

Apply migrations separately after the same fresh-target check using
`dotnet run --project server/server.csproj -c Release --no-launch-profile -- --migrate-database`;
wrap the connection and environment setup in the same `try/finally` restoration
pattern. The account prompts and seed flags are not needed for migration.
No staging/live connection string belongs in this local example. For a later
Azure operator process, derive the confirmation from the explicitly selected
Azure connection target and follow the Azure replacement gates below; the local
launcher intentionally rejects remote SQL.

The planned validation fixture contains 200 assets, 500 schedules, 248
historical inspections, and 20 forms (10 Submitted and 10 Acknowledged), split
across fictional Fire Extinguishers / CCMS, Water Drinking Stations /
Administration, Fire Alarm Systems / Library, and Emergency Lights / CNAHS
batches. The fixture also plans 15 WMS referrals. These are planned counts
until the guarded seed completes and is reviewed. The first operator-created
case is `FE-CCMS-001`; it has no seeded finding. Normal schedule generation may
change counts as real work is performed.

### GSD acceptance exercise

After verifying the baseline in the manifest, use these four unfinished
batches; each has 13 intended live rows and no precompleted inspections for
those rows:

- Fire Extinguishers / CCMS / August 2026
- Water Drinking Stations / Administration / August 2026
- Fire Alarm Systems / Library / June 2026
- Emergency Lights / CNAHS / June 2026

1. Confirm the fresh baseline counts and the live-batch rows in the manifest.
2. Assign one batch to a Supervisor through the normal schedule workflow.
3. Assign an Inspector to that batch through the normal workflow.
4. Sign in as that Inspector and open each assigned asset by QR scan or search.
5. Record actual results for all rows and submit the whole PM form. Use
   `FE-CCMS-001` for the controlled non-operational case.
6. Record acknowledgement with the authorized recipient's fictional name,
   position, and signature. Do not use a real person's signature.
7. Record a WMS referral only for the qualifying acknowledged non-operational
   finding. Do not mark the asset repaired before repair is independently
   verified.
8. Review the resulting schedule, form, inspection, referral, dashboard, and
   supported NLA results; repeat for the remaining batches and keep local and
   Azure results separate.

For later postflight, run this read-only query against only the target under
review and compare it with the [manifest](gsd-validation-dataset.md):

```sql
SELECT 'Assets' AS [Table], COUNT_BIG(*) AS [Rows] FROM dbo.Assets
UNION ALL SELECT 'PreventiveMaintenanceSchedules', COUNT_BIG(*) FROM dbo.PreventiveMaintenanceSchedules
UNION ALL SELECT 'InspectionRecords', COUNT_BIG(*) FROM dbo.InspectionRecords
UNION ALL SELECT 'PreventiveMaintenanceForms', COUNT_BIG(*) FROM dbo.PreventiveMaintenanceForms
UNION ALL SELECT 'PreventiveMaintenanceAcknowledgements', COUNT_BIG(*) FROM dbo.PreventiveMaintenanceAcknowledgements
UNION ALL SELECT 'InspectionWmsReferrals', COUNT_BIG(*) FROM dbo.InspectionWmsReferrals
UNION ALL SELECT 'InspectionWmsReferralAudits', COUNT_BIG(*) FROM dbo.InspectionWmsReferralAudits;
```

Record the query date and exact database target. The runtime schedule worker and
GSD workflow can change counts after baseline verification.

After migrations and seed acceptance, start the local services from the repo
root, substituting the exact dedicated database name for both arguments:

```powershell
.\scripts\lan\Start-UniPmLan.ps1 -DatabaseName UniPM_GSD_Validation -ConfirmDatabase UniPM_GSD_Validation
.\scripts\lan\Test-UniPmLan.ps1
```

If more than one eligible private address exists, select the trusted Wi-Fi
address explicitly with `-LanAddress`. The launcher saves session identity and
sanitized logs under ignored `artifacts/lan/`; it does not save credentials or
the connection string. Run the readiness check from the laptop, then verify
from an Android phone on the same trusted Wi-Fi. Client isolation or Windows
Firewall may block access even when the laptop check passes.

If Windows Firewall blocks the phone, the optional helper adds only inbound
TCP 5173 and 5254 rules for the selected private subnet and Private profile. It
requires an elevated PowerShell window and a separate explicit confirmation:

```powershell
.\scripts\lan\Set-UniPmLanFirewall.ps1 -Action Add -ConfirmChange -Confirm
```

Remove those session-owned rules before or while stopping the session:

```powershell
.\scripts\lan\Set-UniPmLanFirewall.ps1 -Action Remove -ConfirmChange -Confirm
.\scripts\lan\Stop-UniPmLan.ps1
```

`Stop-UniPmLan.ps1` verifies saved PID, executable path, and process start time
before stopping the recorded process trees. It retains logs. The firewall helper
will remove rules only when their session identity, profile, ports, and subnet
still match the saved state. SQL ports are never included.

If Stop cannot verify a recorded process or firewall rule, it reports the
problem and keeps the session state for review; do not delete that state until
the owned process tree and firewall rules have been checked.

For Android debug runs, point the app at the selected laptop IP:

```powershell
flutter run --debug --dart-define=UNIPM_API_BASE_URL=http://<LAN-IP>:5254
```

This is a debug-only HTTP configuration; release builds continue to require
HTTPS. Restarting the Development API may invalidate local sign-in sessions;
authenticate again with local validation accounts.

### Offline mobile data boundary

The updated encrypted offline store uses a v2 key scoped to the normalized API
scheme, host, effective port, path, and user ID; query strings, fragments, and
embedded credentials are ignored. Separate API endpoints, paths, and users
have separate queues. Existing v1 snapshots were keyed only by user ID; their
original backend cannot be inferred, so the updated app leaves them untouched
and does not import them automatically.

Before upgrading or switching a device that may have v1 offline data, reconnect
with the original app version and original backend, then resolve and synchronize
every pending operation and conflict. If anything remains unresolved, keep that
device and app version on the original backend; use a separate clean device for
LAN testing. Do not assume those v1 drafts will appear in the updated app.
After resolution, sign out; clear app storage only if the owner approves it and
understands it deletes remaining local drafts and queued operations.

New v2 queues remain attached to their API endpoint if the laptop's LAN IP
changes. Such a change creates a different API-base scope; queued work is
retained with its old endpoint and is not transferred automatically. Keep the
LAN endpoint stable through a test session, or resolve the queue before changing
the IP. No LAN-to-Azure or Azure-to-LAN synchronization is provided.

## Separately approved Azure replacement

No Azure inventory, backup, database write, migration, seed, or app change was
performed while preparing this guide. The repository's current staging guide
describes Azure Container Apps and Azure SQL; older App Service deployment
artifacts also remain. Do not infer the live host from old deployment
artifacts. Confirm the actual active resources, writer replicas, database
server, and target database with a read-only inventory before planning a change.

The retained `scripts/initialize-azure-staging-demo.ps1` is an old staging
initializer pinned to an exact 22-migration empty-target guard. It is not a
reset or replacement workflow; do not rerun it against populated staging. For
future Azure validation, first perform a read-only resource and writer inventory
and record the exact subscription, app/revision, SQL server, database, and
migration history. Make and verify an approved backup of the existing database
for rollback, then provision a separately named empty Azure SQL validation
database; do not restore populated staging rows into the seed target. Confirm
the new server/database target and enforce explicit SQL encryption
(`Encrypt=True`, `TrustServerCertificate=False`). Before any approved cutover,
stop or deactivate every app revision and background writer, apply the approved
current migrations to the new database, run the guarded user and dataset seed
commands as a short-lived Development operator process, and review the
read-only counts and login/workflow checks. Preserve deployed Production auth,
host, and origin configuration; never run the public staging service in
Development. Only then may an approved operator change the app's database
configuration to the replacement. Retain the old database unchanged for
rollback. Keep the LAN helper pointed only at a local SQL instance; it refuses
remote SQL data sources.

The future read-only preflight should identify the Azure subscription, active
Container Apps app and revisions, ingress, configured SQL target, and database
status/schema without printing connection secrets. These example Azure CLI
queries only read resource metadata; substitute reviewed names and do not add
commands that reveal app secrets or connection-string values:

```powershell
az account show --query "{subscriptionId:id,tenantId:tenantId}" -o json
az containerapp list --query "[].{name:name,resourceGroup:resourceGroup,location:location}" -o table
az webapp list --query "[].{name:name,resourceGroup:resourceGroup,location:location,state:state}" -o table
az containerapp show --resource-group <RESOURCE-GROUP> --name <APP-NAME> --query "{id:id,state:properties.provisioningState,latestRevision:properties.latestRevisionName,ingress:properties.configuration.ingress}" -o json
az containerapp revision list --resource-group <RESOURCE-GROUP> --name <APP-NAME> --query "[?properties.active].{name:name,active:properties.active}" -o table
az sql server list --query "[].{name:name,resourceGroup:resourceGroup,location:location}" -o table
az sql db show --resource-group <RESOURCE-GROUP> --server <SQL-SERVER> --name <DATABASE> --query "{id:id,status:status,edition:edition,collation:collation,earliestRestoreDate:earliestRestoreDate}" -o json
```

Use the read-only database session for migration history and baseline counts;
do not pass a connection secret in a command argument:

```sql
SELECT MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
```

No Azure inventory or live resource access was performed for this guide. Before
an approved replacement, require an explicit typed confirmation of the full
`DataSource/Database` target for each guarded seed command. The local LAN
launcher refuses Azure targets by design and is not an Azure seeding or cutover
tool.

## Recovery from Azure to LAN

1. Confirm Azure is unavailable or unsuitable for the session; do not attempt
   an automatic database switch.
2. Start the prepared laptop services with the dedicated local database and
   verify both API health endpoints, the frontend, and the selected LAN API URL.
3. Connect GSD devices to the same trusted Wi-Fi and open the displayed web URL.
4. Launch the Android debug build with
   `UNIPM_API_BASE_URL=http://<LAN-IP>:5254` and sign in using local validation
   accounts.
5. Keep changes in the local database. Azure and local records never
   synchronize automatically.

Before changing a previously used phone from Azure to LAN, follow the offline
mobile data boundary above. Resolve v1 pending work through the original app and
backend before upgrading; v1 data is not automatically imported. For new v2
data, backend scopes remain isolated and endpoint changes retain pending work in
its previous scope. If pending work cannot be resolved, keep the device on its
original backend and use another device for LAN.

GSD acceptance and physical-device acceptance remain pending until the
authorized operator completes and records those checks.
