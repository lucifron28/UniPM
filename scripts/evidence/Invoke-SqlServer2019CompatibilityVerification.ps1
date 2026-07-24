[CmdletBinding()]
param(
    [string]$ApplicationConnectionString = $env:ConnectionStrings__DefaultConnection,
    [string]$TestConnectionString = $env:UNIPM_SQLSERVER2019_TEST_CONNECTION
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ApplicationConnectionString) -or [string]::IsNullOrWhiteSpace($TestConnectionString)) {
    throw 'Set ConnectionStrings__DefaultConnection and UNIPM_SQLSERVER2019_TEST_CONNECTION before running native SQL Server 2019 verification.'
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot | Split-Path -Parent
Set-Location $repositoryRoot
$artifactDirectory = Join-Path $repositoryRoot ("artifacts\evidence\sqlserver2019-{0}" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$applicationDatabaseName = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($ApplicationConnectionString).InitialCatalog

if ([string]::IsNullOrWhiteSpace($applicationDatabaseName)) {
    throw 'ConnectionStrings__DefaultConnection must specify an application database name.'
}

function Invoke-SqlScalar([string]$ConnectionString, [string]$Sql) {
    $connection = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = $Sql
        return $command.ExecuteScalar()
    }
    finally {
        $connection.Dispose()
    }
}

try {
    $majorVersion = [int](Invoke-SqlScalar $TestConnectionString "SELECT CONVERT(int, SERVERPROPERTY('ProductMajorVersion'));")
    $fullTextInstalled = [int](Invoke-SqlScalar $TestConnectionString "SELECT CONVERT(int, SERVERPROPERTY('IsFullTextInstalled'));")
    if ($majorVersion -ne 15) { throw "Expected SQL Server major version 15, found $majorVersion." }
    if ($fullTextInstalled -ne 1) { throw 'SQL Server Full-Text Search is not installed.' }

    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ConnectionStrings__DefaultConnection = $ApplicationConnectionString
    $env:UNIPM_SQLSERVER_TEST_CONNECTION = $TestConnectionString
    $env:UNIPM_SQLSERVER2019_TEST_CONNECTION = $TestConnectionString

    & dotnet restore .\UniPM.slnx *> (Join-Path $artifactDirectory 'restore.log')
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
    & dotnet build .\UniPM.slnx -c Release --no-restore *> (Join-Path $artifactDirectory 'build.log')
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

    foreach ($command in @('--migrate-database', '--seed-synthetic', '--seed-development-users', '--rebuild-maintenance-search-documents')) {
        & dotnet run --project server -- $command *> (Join-Path $artifactDirectory ("{0}.log" -f $command.TrimStart('-')))
        if ($LASTEXITCODE -ne 0) { throw "Maintenance command $command failed." }
    }

    $compatibilityLevel = [int](Invoke-SqlScalar $ApplicationConnectionString "SELECT compatibility_level FROM sys.databases WHERE name = N'$($applicationDatabaseName.Replace("'", "''"))';")
    if ($compatibilityLevel -ne 150) { throw "Expected $applicationDatabaseName compatibility level 150, found $compatibilityLevel." }

    [ordered]@{
        sqlServerMajorVersion = $majorVersion
        fullTextInstalled = $fullTextInstalled
        applicationDatabase = $applicationDatabaseName
        compatibilityLevel = $compatibilityLevel
        fullTextCatalogCount = [int](Invoke-SqlScalar $ApplicationConnectionString "SELECT COUNT(*) FROM sys.fulltext_catalogs WHERE name = N'UniPMMaintenanceRetrieval';")
        maintenanceSearchDocumentFullTextIndexCount = [int](Invoke-SqlScalar $ApplicationConnectionString "SELECT COUNT(*) FROM sys.fulltext_indexes AS indexTable INNER JOIN sys.tables AS tableInfo ON tableInfo.object_id = indexTable.object_id WHERE tableInfo.name = N'MaintenanceSearchDocuments' AND indexTable.is_enabled = 1;")
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artifactDirectory 'sql-server-probes.json') -Encoding utf8

    # SQL integration tests use their own explicit connection. Remove the application
    # connection so InMemory-hosted API tests do not register SQL Server as a second provider.
    Remove-Item Env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
    & dotnet test .\UniPM.slnx -c Release --no-build *> (Join-Path $artifactDirectory 'tests.log')
    if ($LASTEXITCODE -ne 0) { throw 'SQL-enabled test suite failed.' }

    Write-Host 'Native SQL Server 2019 compatibility verification completed. Review sanitized artifacts before creating evidence.'
}
catch {
    Write-Error $_
    exit 1
}
