<#
.SYNOPSIS
Initialize the pre-provisioned Azure staging database with fictional demo users and records.

.DESCRIPTION
Runs only after an operator has separately migrated the expected, empty staging database and
stopped the App Service and other database writers. The script performs a read-only target,
connection, permission, migration-history, and application-table preflight, then runs the existing
Development seed commands as short-lived local processes. It does not invoke the standalone
--migrate-database command or reset, host the API, provision Azure resources, or deploy.

The existing --seed-demo command calls EF Core MigrateAsync during context setup. With the exact
current migration history required by this script, that call is expected to be a no-op; the script
does not suppress it. The preflight is not a database lock, so stop application writers and do not
run another migration process while initialization is in progress.

The caller must provide ConnectionStrings__DefaultConnection and UNIPM_STAGING_DEMO_PASSWORD
as process environment variables. The SQL connection must use SQL authentication, explicit
encryption, and certificate validation. The demo password is checked against the existing
Identity password policy and is never written to the command line or output.

.PARAMETER ConfirmTarget
Must be exactly sql-unipm-staging.database.windows.net/unipm-staging-db. After the read-only
preflight, the script also requires the operator to type a phrase containing the detected
database collation.

.PARAMETER PublishedApiDll
Optional explicit path to UniPM.Api.dll. The accepted path is constrained to the current
commit's artifacts/azure-staging/<commit>/publish/UniPM.Api.dll, whose provenance manifest must
declare the same source commit and branch.

.EXAMPLE
$env:ConnectionStrings__DefaultConnection = '<SQL-authentication connection string>'
$env:UNIPM_STAGING_DEMO_PASSWORD = '<strong fictional demo password>'
./scripts/initialize-azure-staging-demo.ps1 -ConfirmTarget 'sql-unipm-staging.database.windows.net/unipm-staging-db'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ConfirmTarget,

    [Parameter()]
    [string]$PublishedApiDll
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$expectedServer = 'sql-unipm-staging.database.windows.net'
$expectedDatabase = 'unipm-staging-db'
$expectedTarget = "$expectedServer/$expectedDatabase"
$expectedMigrationIds = @(
    '20260623145921_InitialDomainSchema'
    '20260710170229_AddMaintenanceSearchDocuments'
    '20260711001300_EnforceDomainContracts'
    '20260711120000_AddMaintenanceFullTextSearch'
    '20260711123548_AddMaintenanceSearchDocumentEmbeddings'
    '20260713001356_AddIdentityAuthentication'
    '20260717044115_EnforceOneInspectionPerSchedule'
    '20260717153842_AddRefreshSessions'
    '20260725082333_AddReferenceDocumentFoundation'
    '20260727100331_AddPreventiveMaintenanceFormDomain'
    '20260907150158_AddWaterStationInspectionWorkItems'
    '20260917145243_SeparatePmExecutionFromAcknowledgement'
    '20260917172750_AddCanonicalPmCycleIdentity'
    '20260923160152_AssignScheduleOversight'
    '20260926064226_AddInspectionLocationVerification'
    '20260926165354_AddInspectionLocationQualityMetadata'
    '20260930123820_RetireMaintenanceHistoryRagStorage'
    '20261009020518_AddUniqueScheduleAssetCycle'
    '20261009041512_AddInspectionWmsReferralTracking'
    '20261009182058_AddScheduleEnrollmentDeferrals'
    '20261009234631_ReviewScheduleEnrollmentDeferrals'
    '20261010141251_AddInspectionPhotoEvidence'
)

function Get-ConnectionStringValue {
    param(
        [Parameter(Mandatory = $true)]
        [System.Data.Common.DbConnectionStringBuilder]$Builder,

        [Parameter(Mandatory = $true)]
        [string[]]$Aliases
    )

    $values = [System.Collections.Generic.List[string]]::new()
    foreach ($key in $Builder.Keys) {
        foreach ($alias in $Aliases) {
            if ([string]::Equals([string]$key, $alias, [StringComparison]::OrdinalIgnoreCase)) {
                $values.Add([string]$Builder[$key])
                break
            }
        }
    }

    if ($values.Count -eq 0) {
        return $null
    }

    if (@($values | Select-Object -Unique).Count -gt 1) {
        throw 'The SQL connection string contains conflicting aliases.'
    }

    return $values[0]
}

function Invoke-LocalSeedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command,

        [Parameter(Mandatory = $true)]
        [string]$DotnetPath,

        [Parameter(Mandatory = $true)]
        [string]$ApiDll,

        [Parameter(Mandatory = $true)]
        [string]$ConnectionString,

        [Parameter(Mandatory = $true)]
        [string]$DemoPassword
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $DotnetPath
    $startInfo.Arguments = '"' + $ApiDll + '" ' + $Command
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    $childEnvironment = $startInfo.EnvironmentVariables
    $childEnvironment['ASPNETCORE_ENVIRONMENT'] = 'Development'
    $childEnvironment['DOTNET_ENVIRONMENT'] = 'Development'
    $childEnvironment['ConnectionStrings__DefaultConnection'] = $ConnectionString
    $childEnvironment['UNIPM_DEV_USER_PASSWORD'] = $DemoPassword
    $childEnvironment['UNIPM_DEV_SKIP_DEPARTMENT_HEAD_ACCOUNT'] = 'true'
    $childEnvironment['Embeddings__Enabled'] = 'false'
    $childEnvironment['NaturalLanguageAnalytics__Enabled'] = 'false'
    $childEnvironment['Observability__MetricsEnabled'] = 'false'
    $childEnvironment['PreventiveMaintenanceScheduleGeneration__WorkerEnabled'] = 'false'
    $childEnvironment.Remove('UNIPM_STAGING_DEMO_PASSWORD')
    $childEnvironment.Remove('SQLCMDPASSWORD')

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            throw 'The local seed command could not be started.'
        }

        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(180000)) {
            try {
                $process.Kill()
                $process.WaitForExit()
            }
            catch {
                # Keep child output private; the timeout remains the actionable failure.
            }

            throw "The local $Command command timed out and may have partially written data."
        }

        $null = $stdoutTask.GetAwaiter().GetResult()
        $null = $stderrTask.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) {
            throw "The local $Command command failed with exit code $($process.ExitCode)."
        }
    }
    finally {
        $process.Dispose()
    }
}

if (-not [string]::Equals($ConfirmTarget, $expectedTarget, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'ConfirmTarget must match the provisioned staging server and database exactly.'
}

foreach ($name in @(
        'WEBSITE_SITE_NAME',
        'WEBSITE_INSTANCE_ID',
        'WEBSITE_HOSTNAME',
        'WEBSITE_SLOT_NAME',
        'WEBSITE_RUN_FROM_PACKAGE'
    )) {
    if (-not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name, 'Process'))) {
        throw 'Do not run staging initialization inside an Azure App Service process.'
    }
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$branch = (& git -C $repositoryRoot branch --show-current 2>$null)
if ($LASTEXITCODE -ne 0 -or $branch.Trim() -ne 'deploy/azure-staging') {
    throw 'Run this initializer from the deploy/azure-staging branch.'
}

$trackedChanges = (& git -C $repositoryRoot status --porcelain --untracked-files=no 2>$null)
if ($LASTEXITCODE -ne 0 -or $trackedChanges) {
    throw 'Run this initializer from a clean tracked working tree so the published artifact matches the checked-out commit.'
}

$sourceCommit = (& git -C $repositoryRoot rev-parse HEAD 2>$null)
if ($LASTEXITCODE -ne 0 -or $sourceCommit.Trim() -notmatch '^[0-9a-fA-F]{40}$') {
    throw 'The current source commit could not be identified.'
}

$expectedApiDll = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts/azure-staging/$($sourceCommit.Trim())/publish/UniPM.Api.dll"))
if ([string]::IsNullOrWhiteSpace($PublishedApiDll)) {
    $resolvedApiDll = $expectedApiDll
}
else {
    $candidateApiDll = if ([IO.Path]::IsPathRooted($PublishedApiDll)) {
        [IO.Path]::GetFullPath($PublishedApiDll)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $repositoryRoot $PublishedApiDll))
    }

    if (-not [string]::Equals($candidateApiDll, $expectedApiDll, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'PublishedApiDll must be the publish output for the current checked-out commit.'
    }

    $resolvedApiDll = $candidateApiDll
}

if (-not (Test-Path -LiteralPath $resolvedApiDll -PathType Leaf)) {
    throw 'The current commit publish output is missing UniPM.Api.dll. Package the clean commit before seeding.'
}

$manifestPath = Join-Path ([IO.Path]::GetDirectoryName([IO.Path]::GetDirectoryName($resolvedApiDll))) 'provenance.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw 'The current commit publish provenance manifest is missing.'
}
try {
    $provenance = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
}
catch {
    throw 'The current commit publish provenance manifest is invalid.'
}
if (-not [string]::Equals([string]$provenance.sourceCommit, $sourceCommit.Trim(), [StringComparison]::OrdinalIgnoreCase) -or
    -not [string]::Equals([string]$provenance.sourceBranch, 'deploy/azure-staging', [StringComparison]::Ordinal)) {
    throw 'The publish provenance manifest does not declare the current commit and branch.'
}

$sourceMigrationDirectory = Join-Path $repositoryRoot 'server/Migrations'
$sourceMigrationIds = @(
    Get-ChildItem -LiteralPath $sourceMigrationDirectory -Filter '*.cs' -File |
        Where-Object { $_.Name -notmatch '\.Designer\.cs$' -and $_.Name -notmatch 'ModelSnapshot' } |
        Select-Object -ExpandProperty BaseName |
        Sort-Object
)
$sortedExpectedMigrationIds = @($expectedMigrationIds | Sort-Object)
if ($sourceMigrationIds.Count -ne $sortedExpectedMigrationIds.Count -or
    [string]::Join('|', $sourceMigrationIds) -cne [string]::Join('|', $sortedExpectedMigrationIds)) {
    throw 'The checked-out source migration list differs from the initializer preflight chain; review it before updating the initializer.'
}

$dotnet = Get-Command dotnet.exe -CommandType Application -ErrorAction SilentlyContinue
$sqlcmd = Get-Command sqlcmd.exe -CommandType Application -ErrorAction SilentlyContinue
if ($null -eq $dotnet -or $null -eq $sqlcmd) {
    throw 'The dotnet runtime and sqlcmd client are required.'
}
if (-not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable('SQLCMDINI', 'Process'))) {
    throw 'Unset SQLCMDINI before running the staging initializer so no startup script runs during preflight.'
}

$connectionString = [Environment]::GetEnvironmentVariable('ConnectionStrings__DefaultConnection', 'Process')
$demoPassword = [Environment]::GetEnvironmentVariable('UNIPM_STAGING_DEMO_PASSWORD', 'Process')
if ([string]::IsNullOrWhiteSpace($connectionString) -or [string]::IsNullOrWhiteSpace($demoPassword)) {
    throw 'Set ConnectionStrings__DefaultConnection and UNIPM_STAGING_DEMO_PASSWORD in the current process environment.'
}

if ($demoPassword.Length -lt 12 -or
    $demoPassword -notmatch '\p{Lu}' -or
    $demoPassword -notmatch '\p{Ll}' -or
    $demoPassword -notmatch '\p{Nd}' -or
    $demoPassword -notmatch '[^\p{L}\p{Nd}]') {
    throw 'UNIPM_STAGING_DEMO_PASSWORD does not satisfy the configured Identity password policy.'
}

$builder = [System.Data.Common.DbConnectionStringBuilder]::new()
try {
    $builder.ConnectionString = $connectionString
}
catch {
    throw 'ConnectionStrings__DefaultConnection is not a valid SQL connection string.'
}

$server = Get-ConnectionStringValue -Builder $builder -Aliases @('Server', 'Data Source', 'Addr', 'Address', 'Network Address')
$database = Get-ConnectionStringValue -Builder $builder -Aliases @('Initial Catalog', 'Database')
$sqlLogin = Get-ConnectionStringValue -Builder $builder -Aliases @('User ID', 'UID', 'User')
$sqlPassword = Get-ConnectionStringValue -Builder $builder -Aliases @('Password', 'PWD')
$encrypt = Get-ConnectionStringValue -Builder $builder -Aliases @('Encrypt')
$trustServerCertificate = Get-ConnectionStringValue -Builder $builder -Aliases @('TrustServerCertificate')
$integratedSecurity = Get-ConnectionStringValue -Builder $builder -Aliases @('Integrated Security', 'Trusted_Connection')
$authentication = Get-ConnectionStringValue -Builder $builder -Aliases @('Authentication')

$normalizedServer = ([string]$server).Trim() -replace '(?i)^tcp:', ''
if ($normalizedServer -match ',1433$') {
    $normalizedServer = $normalizedServer.Substring(0, $normalizedServer.Length - 5)
}
if (-not [string]::Equals($normalizedServer, $expectedServer, [StringComparison]::OrdinalIgnoreCase) -or
    -not [string]::Equals([string]$database, $expectedDatabase, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'ConnectionStrings__DefaultConnection does not target the provisioned staging server and database.'
}

if ([string]::IsNullOrWhiteSpace($sqlLogin) -or [string]::IsNullOrWhiteSpace($sqlPassword)) {
    throw 'The staging connection must use SQL authentication with a login and password.'
}
if (-not [string]::IsNullOrWhiteSpace($integratedSecurity) -and
    $integratedSecurity -notin @('false', 'False', '0', 'no', 'No')) {
    throw 'Integrated authentication is not allowed for the staging initializer.'
}
if (-not [string]::IsNullOrWhiteSpace($authentication) -and
    $authentication -notin @('SqlPassword', 'SQLPassword')) {
    throw 'The staging connection must use SQL password authentication.'
}
if ($encrypt -notin @('true', 'True', 'yes', 'Yes', '1', 'Mandatory', 'Strict')) {
    throw 'The SQL connection must explicitly enable encryption.'
}
if ($trustServerCertificate -notin @('false', 'False', 'no', 'No', '0')) {
    throw 'The SQL connection must explicitly require server-certificate validation.'
}

$expectedIdValues = ($expectedMigrationIds | ForEach-Object { "(N'$_')" }) -join ",`n    "
$preflightSql = @"
SET NOCOUNT ON;

DECLARE @engineEdition int = TRY_CONVERT(int, SERVERPROPERTY('EngineEdition'));
DECLARE @compatibilityLevel int = (
    SELECT compatibility_level FROM sys.databases WHERE database_id = DB_ID()
);
DECLARE @collation sysname = CONVERT(sysname, DATABASEPROPERTYEX(DB_NAME(), 'Collation'));

IF DB_NAME() <> N'$expectedDatabase' THROW 51000, N'Unexpected database.', 1;
IF @engineEdition <> 5 THROW 51001, N'Expected Azure SQL Database.', 1;
IF @compatibilityLevel <> 150 THROW 51002, N'Expected compatibility level 150.', 1;
IF @collation IS NULL OR @collation = N'' THROW 51003, N'Database collation is unavailable.', 1;

IF ISNULL(IS_ROLEMEMBER(N'db_owner'), 0) <> 1
   AND (ISNULL(IS_ROLEMEMBER(N'db_datareader'), 0) <> 1
        OR ISNULL(IS_ROLEMEMBER(N'db_datawriter'), 0) <> 1)
    THROW 51004, N'The current principal needs db_datareader and db_datawriter or db_owner.', 1;

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    THROW 51005, N'The EF migration history is missing.', 1;

DECLARE @expectedMigrations TABLE (MigrationId nvarchar(150) NOT NULL PRIMARY KEY);
INSERT INTO @expectedMigrations (MigrationId)
VALUES
    $expectedIdValues;

IF (SELECT COUNT_BIG(*) FROM dbo.__EFMigrationsHistory) <> (SELECT COUNT_BIG(*) FROM @expectedMigrations)
   OR EXISTS (SELECT MigrationId FROM @expectedMigrations EXCEPT SELECT MigrationId FROM dbo.__EFMigrationsHistory)
   OR EXISTS (SELECT MigrationId FROM dbo.__EFMigrationsHistory EXCEPT SELECT MigrationId FROM @expectedMigrations)
    THROW 51006, N'The migration history is not the exact expected 22-migration chain.', 1;

DECLARE @applicationDataEmpty bit = 1;
DECLARE @tableName nvarchar(517);
DECLARE @hasRows bit;
DECLARE @tableSql nvarchar(max);
DECLARE emptyTables CURSOR LOCAL FAST_FORWARD FOR
    SELECT QUOTENAME(schema_name(t.schema_id)) + N'.' + QUOTENAME(t.name)
    FROM sys.tables AS t
    WHERE t.is_ms_shipped = 0
      AND NOT (t.schema_id = SCHEMA_ID(N'dbo') AND t.name = N'__EFMigrationsHistory');

OPEN emptyTables;
FETCH NEXT FROM emptyTables INTO @tableName;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @hasRows = 0;
    SET @tableSql = N'IF EXISTS (SELECT TOP (1) 1 FROM ' + @tableName + N') SET @hasRows = 1;';
    EXEC sys.sp_executesql @tableSql, N'@hasRows bit OUTPUT', @hasRows = @hasRows OUTPUT;
    IF @hasRows = 1 SET @applicationDataEmpty = 0;
    FETCH NEXT FROM emptyTables INTO @tableName;
END;
CLOSE emptyTables;
DEALLOCATE emptyTables;

IF @applicationDataEmpty <> 1 THROW 51007, N'Application tables contain existing rows.', 1;

SELECT CONCAT(
    N'UNIPM_STAGING_PREFLIGHT_OK|',
    DB_NAME(), N'|',
    CONVERT(nvarchar(10), @engineEdition), N'|',
    CONVERT(nvarchar(10), @compatibilityLevel), N'|',
    @collation, N'|22|SEED_READ_WRITE'
);
"@

$sqlFile = Join-Path ([IO.Path]::GetTempPath()) ("unipm-staging-preflight-$([Guid]::NewGuid().ToString('N')).sql")
$originalSqlcmdPassword = [Environment]::GetEnvironmentVariable('SQLCMDPASSWORD', 'Process')
$hadSqlcmdPassword = $null -ne $originalSqlcmdPassword
$seedCommandsStarted = $false
try {
    [IO.File]::WriteAllText($sqlFile, $preflightSql, [Text.UTF8Encoding]::new($false))
    [Environment]::SetEnvironmentVariable('SQLCMDPASSWORD', $sqlPassword, 'Process')

    $sqlcmdOutput = @(& $sqlcmd.Source `
        -S $expectedServer `
        -d $expectedDatabase `
        -U $sqlLogin `
        -N `
        -x `
        -b `
        -r 1 `
        -h -1 `
        -s '|' `
        -W `
        -w 1024 `
        -l 30 `
        -t 120 `
        -i $sqlFile 2>&1)
    $sqlcmdExitCode = $LASTEXITCODE
    if ($sqlcmdExitCode -ne 0) {
        throw 'Read-only SQL preflight failed; SQL client details were withheld. No seed commands were started.'
    }

    $preflightLine = $sqlcmdOutput |
        ForEach-Object { ([string]$_).Trim() } |
        Where-Object { $_ -like 'UNIPM_STAGING_PREFLIGHT_OK|*' } |
        Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($preflightLine)) {
        throw 'The read-only SQL preflight returned no success marker. No seed commands were started.'
    }

    $preflightFields = $preflightLine -split '\|'
    if ($preflightFields.Count -ne 7 -or
        $preflightFields[1] -ne $expectedDatabase -or
        $preflightFields[2] -ne '5' -or
        $preflightFields[3] -ne '150' -or
        $preflightFields[5] -ne '22' -or
        $preflightFields[6] -ne 'SEED_READ_WRITE') {
        throw 'The read-only SQL preflight returned unexpected target details. No seed commands were started.'
    }

    $collation = $preflightFields[4]
    $confirmation = "APP STOPPED $expectedDatabase collation $collation"
    Write-Output "Read-only preflight passed for $expectedTarget (Azure SQL Database, compatibility 150, collation $collation, exact 22 migrations, empty application tables, and seed read/write permission)."
    Write-Output 'The migrations do not pin a required collation; review the reported value before continuing.'
    Write-Output 'Stop the App Service and all other database writers before running the seed commands; this preflight does not lock the database.'
    $typedConfirmation = Read-Host "After stopping database writers, type '$confirmation' to run the two local seed commands"
    if (-not [string]::Equals($typedConfirmation, $confirmation, [StringComparison]::Ordinal)) {
        throw 'The typed confirmation did not match. No seed commands were started.'
    }

    Write-Output 'Running the local Development-user seed command.'
    $seedCommandsStarted = $true
    Invoke-LocalSeedCommand `
        -Command '--seed-development-users' `
        -DotnetPath $dotnet.Source `
        -ApiDll $resolvedApiDll `
        -ConnectionString $connectionString `
        -DemoPassword $demoPassword

    Write-Output 'Running the local fictional demo-data seed command.'
    Invoke-LocalSeedCommand `
        -Command '--seed-demo' `
        -DotnetPath $dotnet.Source `
        -ApiDll $resolvedApiDll `
        -ConnectionString $connectionString `
        -DemoPassword $demoPassword

    Write-Output 'Staging initialization completed. No API host, standalone migration command, or reset command was started. The existing --seed-demo MigrateAsync context check was expected to be a no-op after preflight.'
}
catch {
    if ($seedCommandsStarted) {
        throw 'A local seed command did not complete. It may have partially written data; do not rerun or reset automatically. Stop for manual review.'
    }

    throw $_.Exception.Message
}
finally {
    if ($hadSqlcmdPassword) {
        [Environment]::SetEnvironmentVariable('SQLCMDPASSWORD', $originalSqlcmdPassword, 'Process')
    }
    else {
        [Environment]::SetEnvironmentVariable('SQLCMDPASSWORD', $null, 'Process')
    }

    if (Test-Path -LiteralPath $sqlFile -PathType Leaf) {
        Remove-Item -LiteralPath $sqlFile -Force
    }
}
