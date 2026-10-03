[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$projectPath = 'server/server.csproj'
$assemblyPath = Join-Path $repositoryRoot 'server/bin/Release/net10.0/UniPM.Api.dll'
$previousMigration = '20260926165354_AddInspectionLocationQualityMetadata'
$retirementMigration = '20260930123820_RetireMaintenanceHistoryRagStorage'
$databaseName = 'UniPMRetirement_' + [Guid]::NewGuid().ToString('N')
$testedCommit = (& git -C $repositoryRoot rev-parse HEAD 2>$null | Out-String).Trim()
$sourceBranch = (& git -C $repositoryRoot branch --show-current 2>$null | Out-String).Trim()
$shortCommit = if ($testedCommit.Length -ge 8) { $testedCommit.Substring(0, 8) } else { 'unknown' }
$timestamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssZ')
$artifactDirectory = Join-Path $repositoryRoot "artifacts/rag-retirement/$timestamp-$shortCommit"
$summaryPath = Join-Path $artifactDirectory 'migration-verification.json'
$logPath = Join-Path $artifactDirectory 'migration-verification.log'
$connectionVariable = 'ConnectionStrings__DefaultConnection'
$previousConnectionString = [Environment]::GetEnvironmentVariable($connectionVariable, 'Process')
$databaseCreated = $false
$failure = $null
$currentStage = 'preflight'
$steps = [System.Collections.Generic.List[object]]::new()
$trackedTables = @(
    [pscustomobject]@{ Name = 'Assets'; Key = 'Id' },
    [pscustomobject]@{ Name = 'PreventiveMaintenanceSchedules'; Key = 'Id' },
    [pscustomobject]@{ Name = 'InspectionRecords'; Key = 'Id' },
    [pscustomobject]@{ Name = 'PreventiveMaintenanceForms'; Key = 'Id' },
    [pscustomobject]@{ Name = 'PreventiveMaintenanceAcknowledgements'; Key = 'Id' },
    [pscustomobject]@{ Name = 'InspectionLocationAttempts'; Key = 'Id' },
    [pscustomobject]@{ Name = 'ReferenceDocuments'; Key = 'Id' },
    [pscustomobject]@{ Name = 'ReferenceDocumentApplicabilities'; Key = 'Id' },
    [pscustomobject]@{ Name = 'ReferenceDocumentSections'; Key = 'Id' },
    [pscustomobject]@{ Name = 'ReferenceDocumentSectionEmbeddings'; Key = 'ReferenceDocumentSectionId' }
)
$run = [ordered]@{
    status = 'running'
    startedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    finishedAtUtc = $null
    testedCommit = $testedCommit
    sourceBranch = $sourceBranch
    helperIdentity = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    sqlServer = $null
    compatibilityLevel = $null
    databaseName = $databaseName
    previousMigration = $previousMigration
    retirementMigration = $retirementMigration
    releaseAssembly = 'server/bin/Release/net10.0/UniPM.Api.dll'
    artifactDirectory = "artifacts/rag-retirement/$timestamp-$shortCommit"
    steps = $steps
    preRetirementDerivedRows = $null
    baselineData = $null
    afterRetirementData = $null
    afterRollbackData = $null
    afterReapplyData = $null
    downRecreatedDerivedRows = $null
    checks = [ordered]@{}
    failedAt = $null
    failureType = $null
    sqlErrorNumber = $null
    failureMessage = $null
    databaseDropped = $false
    databaseIdAbsent = $false
    environmentRestored = $false
}

function Get-ConnectionString {
    param([Parameter(Mandatory)][string]$Database)

    return "Server=localhost;Database=$Database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;"
}

function Invoke-SqlScalar {
    param(
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string]$Query
    )

    $connection = [System.Data.SqlClient.SqlConnection]::new((Get-ConnectionString -Database $Database))
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = $Query
        $command.CommandTimeout = 180
        return $command.ExecuteScalar()
    }
    finally {
        $connection.Dispose()
    }
}

function Invoke-SqlNonQuery {
    param(
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string]$Query
    )

    $connection = [System.Data.SqlClient.SqlConnection]::new((Get-ConnectionString -Database $Database))
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = $Query
        $command.CommandTimeout = 180
        $null = $command.ExecuteNonQuery()
    }
    finally {
        $connection.Dispose()
    }
}

function Get-TableState {
    param([Parameter(Mandatory)][string]$Database)

    $state = [ordered]@{}
    foreach ($table in $trackedTables) {
        $query = @"
SELECT
    (SELECT COUNT_BIG(*) FROM dbo.[$($table.Name)]) AS [RowCount],
    COALESCE((
        SELECT STRING_AGG(CONVERT(nvarchar(36), [$($table.Key)]), N',')
            WITHIN GROUP (ORDER BY [$($table.Key)])
        FROM dbo.[$($table.Name)]
    ), N'') AS OrderedIds,
    CONVERT(varchar(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), COALESCE((
        SELECT * FROM dbo.[$($table.Name)]
        ORDER BY [$($table.Key)]
        FOR JSON PATH, INCLUDE_NULL_VALUES
    ), N'[]'))), 2) AS FullRowSha256;
"@
        $connection = [System.Data.SqlClient.SqlConnection]::new((Get-ConnectionString -Database $Database))
        try {
            $connection.Open()
            $command = $connection.CreateCommand()
            $command.CommandText = $query
            $command.CommandTimeout = 180
            $reader = $command.ExecuteReader([System.Data.CommandBehavior]::SingleRow)
            try {
                if (-not $reader.Read()) {
                    throw "Could not read state for dbo.$($table.Name)."
                }

                $state[$table.Name] = [ordered]@{
                    count = [long]$reader.GetInt64(0)
                    orderedIds = if ($reader.IsDBNull(1)) { '' } else { $reader.GetString(1) }
                    fullRowSha256 = if ($reader.IsDBNull(2)) { '' } else { $reader.GetString(2) }
                }
            }
            finally {
                $reader.Dispose()
            }
        }
        finally {
            $connection.Dispose()
        }
    }

    return $state
}

function Assert-DataStateEqual {
    param(
        [Parameter(Mandatory)]$Expected,
        [Parameter(Mandatory)]$Actual,
        [Parameter(Mandatory)][string]$Stage
    )

    $expectedJson = ConvertTo-Json -InputObject $Expected -Depth 8 -Compress
    $actualJson = ConvertTo-Json -InputObject $Actual -Depth 8 -Compress
    if ($expectedJson -cne $actualJson) {
        throw "PM or reference table counts, ordered IDs, or full-row hashes changed during $Stage."
    }
}

function Assert-StorageState {
    param(
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][bool]$Retired
    )

    $legacyTableCount = [int](Invoke-SqlScalar -Database $Database -Query @'
SELECT COUNT(*)
FROM sys.tables AS tables
INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
WHERE schemas.name = N'dbo'
  AND tables.name IN (N'MaintenanceSearchDocuments', N'MaintenanceSearchDocumentEmbeddings');
'@)
    $legacyCatalogCount = [int](Invoke-SqlScalar -Database $Database -Query "SELECT COUNT(*) FROM sys.fulltext_catalogs WHERE name = N'UniPMMaintenanceRetrieval';")
    $legacyIndexCount = [int](Invoke-SqlScalar -Database $Database -Query @'
SELECT COUNT(*)
FROM sys.fulltext_indexes AS fullTextIndex
INNER JOIN sys.tables AS tables ON tables.object_id = fullTextIndex.object_id
INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
INNER JOIN sys.fulltext_catalogs AS catalogs ON catalogs.fulltext_catalog_id = fullTextIndex.fulltext_catalog_id
WHERE schemas.name = N'dbo'
  AND tables.name = N'MaintenanceSearchDocuments'
  AND catalogs.name = N'UniPMMaintenanceRetrieval';
'@)
    $expectedLegacyCount = if ($Retired) { 0 } else { 2 }
    $expectedIndexCount = if ($Retired) { 0 } else { 1 }
    if ($legacyTableCount -ne $expectedLegacyCount -or $legacyCatalogCount -ne [int](-not $Retired) -or $legacyIndexCount -ne $expectedIndexCount) {
        throw 'Maintenance retrieval tables, catalog, or full-text index are in an unexpected state.'
    }

    $referenceTableCount = [int](Invoke-SqlScalar -Database $Database -Query @'
SELECT COUNT(*)
FROM sys.tables AS tables
INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
WHERE schemas.name = N'dbo'
  AND tables.name IN (N'ReferenceDocuments', N'ReferenceDocumentApplicabilities', N'ReferenceDocumentSections', N'ReferenceDocumentSectionEmbeddings');
'@)
    $referenceCatalogCount = [int](Invoke-SqlScalar -Database $Database -Query "SELECT COUNT(*) FROM sys.fulltext_catalogs WHERE name = N'UniPMReferenceRetrieval';")
    $referenceIndexColumnCount = [int](Invoke-SqlScalar -Database $Database -Query @'
SELECT COUNT(*)
FROM sys.fulltext_indexes AS fullTextIndex
INNER JOIN sys.tables AS tables ON tables.object_id = fullTextIndex.object_id
INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
INNER JOIN sys.fulltext_catalogs AS catalogs ON catalogs.fulltext_catalog_id = fullTextIndex.fulltext_catalog_id
INNER JOIN sys.fulltext_index_columns AS indexColumns ON indexColumns.object_id = fullTextIndex.object_id
WHERE schemas.name = N'dbo'
  AND tables.name = N'ReferenceDocumentSections'
  AND catalogs.name = N'UniPMReferenceRetrieval'
  AND fullTextIndex.is_enabled = 1;
'@)
    if ($referenceTableCount -ne 4 -or $referenceCatalogCount -ne 1 -or $referenceIndexColumnCount -ne 2) {
        throw 'Reference-document tables, catalog, or enabled Full-Text columns are missing.'
    }

}

function Invoke-EfMigrationUpdate {
    param(
        [Parameter(Mandatory)][string]$Stage,
        [Parameter(Mandatory)][string]$Migration
    )

    $script:currentStage = $Stage
    $startedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    $arguments = @(
        'ef', 'database', 'update', $Migration,
        '--project', $projectPath,
        '--startup-project', $projectPath,
        '--configuration', 'Release',
        '--no-build'
    )
    $stepLogPath = Join-Path $artifactDirectory "$Stage.log"
    $stepOutput = @()
    $exitCode = 1
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $locationPushed = $false
    try {
        Push-Location $repositoryRoot
        $locationPushed = $true
        $stepOutput = @(& dotnet @arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    catch {
        $stepOutput = @($_.Exception.GetType().FullName)
        $exitCode = 1
    }
    finally {
        if ($locationPushed) {
            Pop-Location
        }
        $ErrorActionPreference = $previousErrorActionPreference
    }

    $finishedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    $safeOutput = $stepOutput | Out-String
    $safeOutput = $safeOutput -replace '(?i)(Server|Data Source)\s*=\s*[^;,\r\n]+', '$1=[redacted]'
    $safeOutput = $safeOutput -replace '(?i)(Database|Initial Catalog)\s*=\s*[^;,\r\n]+', '$1=[owned disposable database]'
    $safeOutput = $safeOutput -replace '(?i)(Password|Pwd)\s*=\s*[^;,\r\n]+', '$1=[redacted]'
    $safeOutput = $safeOutput.Replace($databaseName, '[owned disposable database]')
    $safeOutput = $safeOutput -replace '(?i)localhost', '[local SQL Server]'
    @(
        "Stage: $Stage"
        "Migration: $Migration"
        "Started at UTC: $startedAtUtc"
        "Finished at UTC: $finishedAtUtc"
        "Exit code: $exitCode"
        ''
        $safeOutput.TrimEnd()
    ) | Set-Content -LiteralPath $stepLogPath -Encoding utf8
    $steps.Add([pscustomobject][ordered]@{
        name = $Stage
        command = 'dotnet ef database update [migration] --project server/server.csproj --startup-project server/server.csproj --configuration Release --no-build'
        migration = $Migration
        log = "artifacts/rag-retirement/$timestamp-$shortCommit/$Stage.log"
        startedAtUtc = $startedAtUtc
        finishedAtUtc = $finishedAtUtc
        exitCode = $exitCode
        status = if ($exitCode -eq 0) { 'passed' } else { 'failed' }
    })
    if ($exitCode -ne 0) {
        throw "$Stage failed with exit code $exitCode."
    }
}

function Assert-FixtureRelationships {
    param([Parameter(Mandatory)][string]$Database)

    Invoke-SqlNonQuery -Database $Database -Query @'
IF NOT EXISTS (
    SELECT 1
    FROM dbo.InspectionRecords AS inspection
    INNER JOIN dbo.Assets AS asset ON asset.Id = inspection.AssetId
    INNER JOIN dbo.PreventiveMaintenanceSchedules AS schedule ON schedule.Id = inspection.ScheduleId
    INNER JOIN dbo.PreventiveMaintenanceForms AS form ON form.Id = inspection.PreventiveMaintenanceFormId
    INNER JOIN dbo.InspectionLocationAttempts AS attempt ON attempt.Id = inspection.LocationAttemptId
    WHERE inspection.Id = '00000000-0000-4000-8000-000000000103'
      AND asset.Id = '00000000-0000-4000-8000-000000000101'
      AND schedule.Id = '00000000-0000-4000-8000-000000000102'
      AND form.Id = '00000000-0000-4000-8000-000000000104'
      AND attempt.Id = '00000000-0000-4000-8000-000000000106')
    THROW 51100, 'Synthetic PM fixture relationships are incomplete.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.PreventiveMaintenanceAcknowledgements AS acknowledgement
    INNER JOIN dbo.PreventiveMaintenanceForms AS form ON form.Id = acknowledgement.FormId
    WHERE acknowledgement.Id = '00000000-0000-4000-8000-000000000105'
      AND acknowledgement.FormId = '00000000-0000-4000-8000-000000000104'
      AND form.Status = N'Acknowledged')
    THROW 51101, 'Synthetic acknowledgement relationship is incomplete.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM dbo.ReferenceDocumentApplicabilities AS applicability
    INNER JOIN dbo.ReferenceDocuments AS document ON document.Id = applicability.ReferenceDocumentId
    INNER JOIN dbo.ReferenceDocumentSections AS section ON section.ReferenceDocumentId = document.Id
    INNER JOIN dbo.ReferenceDocumentSectionEmbeddings AS embedding ON embedding.ReferenceDocumentSectionId = section.Id
    WHERE document.Id = '00000000-0000-4000-8000-000000000108'
      AND applicability.Id = '00000000-0000-4000-8000-000000000109'
      AND section.Id = '00000000-0000-4000-8000-000000000110'
      AND document.IsSynthetic = 1)
    THROW 51102, 'Synthetic reference fixture relationships are incomplete.', 1;
'@
}

New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null

try {
    $currentStage = 'preflight'
    if ($databaseName -notmatch '^UniPMRetirement_[0-9a-f]{32}$') {
        throw 'Generated disposable database name failed strict validation.'
    }
    if ($testedCommit -notmatch '^[0-9a-f]{40}$' -or $sourceBranch -ne 'refactor/retire-maintenance-history-rag') {
        throw 'Run this verifier from a committed refactor/retire-maintenance-history-rag checkout.'
    }
    $worktreeState = (& git -C $repositoryRoot status --porcelain 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $worktreeState.Length -ne 0) {
        throw 'The worktree must be clean so the tested commit is exact.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $projectPath) -PathType Leaf)) {
        throw 'Expected server/server.csproj was not found.'
    }
    if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
        throw 'Expected Release assembly server/bin/Release/net10.0/UniPM.Api.dll was not found.'
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'The dotnet command is unavailable.'
    }

    $serverFacts = [string](Invoke-SqlScalar -Database 'master' -Query @'
SELECT CONCAT(
    CONVERT(int, SERVERPROPERTY('ProductMajorVersion')),
    N'|',
    ISNULL(TRY_CONVERT(int, SERVERPROPERTY('IsFullTextInstalled')), 0));
'@)
    $serverFactParts = $serverFacts.Split('|')
    if ($serverFactParts.Count -ne 2 -or [int]$serverFactParts[0] -ne 15 -or [int]$serverFactParts[1] -ne 1) {
        throw 'The local SQL Server must be major version 15 with Full-Text Search installed.'
    }
    $run.sqlServer = [ordered]@{
        productMajorVersion = [int]$serverFactParts[0]
        fullTextInstalled = $true
        authentication = 'Windows integrated'
        host = 'localhost'
    }
    $run.checks.sqlServer2019 = $true
    $run.checks.fullTextInstalled = $true

    $currentStage = 'create-owned-disposable-database'
    Invoke-SqlNonQuery -Database 'master' -Query "CREATE DATABASE [$databaseName];"
    $databaseCreated = $true
    Invoke-SqlNonQuery -Database 'master' -Query "ALTER DATABASE [$databaseName] SET COMPATIBILITY_LEVEL = 150;"
    [Environment]::SetEnvironmentVariable($connectionVariable, (Get-ConnectionString -Database $databaseName), 'Process')
    $compatibilityLevel = [int](Invoke-SqlScalar -Database $databaseName -Query 'SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME();')
    if ($compatibilityLevel -ne 150) {
        throw 'The disposable database did not retain compatibility level 150.'
    }
    $run.compatibilityLevel = $compatibilityLevel
    $run.checks.compatibilityLevel150 = $true
    $steps.Add([pscustomobject][ordered]@{
        name = $currentStage
        databaseName = $databaseName
        compatibilityLevel = $compatibilityLevel
        status = 'passed'
    })

    Invoke-EfMigrationUpdate -Stage 'apply-previous-migration' -Migration $previousMigration
    $currentStage = 'seed-synthetic-baseline-fixture'
    $fixtureSql = @'
DECLARE @FixtureCreatedAt datetimeoffset = '2026-08-01T10:00:00+08:00';
DECLARE @ScheduleDate datetimeoffset = '2026-08-31T00:00:00+08:00';
DECLARE @InspectionStartedAt datetimeoffset = '2026-08-31T09:00:00+08:00';
DECLARE @CompletionTime datetimeoffset = '2026-08-31T12:00:00+08:00';
DECLARE @SubmittedAt datetimeoffset = '2026-08-31T13:00:00+08:00';
DECLARE @AcknowledgedAt datetimeoffset = '2026-08-31T14:00:00+08:00';
DECLARE @UserId uniqueidentifier = '00000000-0000-4000-8000-000000000107';
DECLARE @AssetId uniqueidentifier = '00000000-0000-4000-8000-000000000101';
DECLARE @ScheduleId uniqueidentifier = '00000000-0000-4000-8000-000000000102';
DECLARE @InspectionId uniqueidentifier = '00000000-0000-4000-8000-000000000103';
DECLARE @FormId uniqueidentifier = '00000000-0000-4000-8000-000000000104';
DECLARE @AcknowledgementId uniqueidentifier = '00000000-0000-4000-8000-000000000105';
DECLARE @LocationAttemptId uniqueidentifier = '00000000-0000-4000-8000-000000000106';
DECLARE @ReferenceDocumentId uniqueidentifier = '00000000-0000-4000-8000-000000000108';
DECLARE @ApplicabilityId uniqueidentifier = '00000000-0000-4000-8000-000000000109';
DECLARE @SectionId uniqueidentifier = '00000000-0000-4000-8000-000000000110';

IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260926165354_AddInspectionLocationQualityMetadata')
   OR EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260930123820_RetireMaintenanceHistoryRagStorage')
    THROW 51110, 'Database is not at the required pre-retirement migration.', 1;

IF EXISTS (SELECT 1 FROM dbo.Assets)
   OR EXISTS (SELECT 1 FROM dbo.PreventiveMaintenanceSchedules)
   OR EXISTS (SELECT 1 FROM dbo.InspectionRecords)
   OR EXISTS (SELECT 1 FROM dbo.PreventiveMaintenanceForms)
   OR EXISTS (SELECT 1 FROM dbo.PreventiveMaintenanceAcknowledgements)
   OR EXISTS (SELECT 1 FROM dbo.InspectionLocationAttempts)
   OR EXISTS (SELECT 1 FROM dbo.ReferenceDocuments)
   OR EXISTS (SELECT 1 FROM dbo.ReferenceDocumentApplicabilities)
   OR EXISTS (SELECT 1 FROM dbo.ReferenceDocumentSections)
   OR EXISTS (SELECT 1 FROM dbo.ReferenceDocumentSectionEmbeddings)
    THROW 51111, 'A migrated PM or reference table was not empty before fixture insertion.', 1;

INSERT INTO dbo.AspNetUsers
    (Id, DisplayName, IsActive, UserName, NormalizedUserName, Email, NormalizedEmail,
     EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber,
     PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount)
VALUES
    (@UserId, N'Synthetic Migration Verifier', 1,
     N'synthetic-rag-retirement-verifier', N'SYNTHETIC-RAG-RETIREMENT-VERIFIER',
     N'synthetic-verifier@example.invalid', N'SYNTHETIC-VERIFIER@EXAMPLE.INVALID',
     0, NULL, N'synthetic-security-stamp', N'synthetic-concurrency-stamp',
     NULL, 0, 0, NULL, 0, 0);

INSERT INTO dbo.Assets
    (Id, AssetCode, AssetCategory, DescriptionEmbedding, Building, Department, Location,
     QrCodeValue, VerificationLatitude, VerificationLongitude, VerificationRadiusMeters,
     Status, CreatedAt, UpdatedAt)
VALUES
    (@AssetId, N'SYN-RETIRE-001', N'fire-extinguisher', NULL,
     N'Synthetic Verification Building', N'GSD', N'Synthetic Room 001',
     N'SYN-RETIRE-QR-001', NULL, NULL, NULL,
     N'Active', @FixtureCreatedAt, @CompletionTime);

INSERT INTO dbo.PreventiveMaintenanceSchedules
    (Id, AssetId, ScheduleDate, PmCycle, PeriodType, Status, Quarter, Semester,
     Year, AcademicYear, AssignedToUserId, AssignedSupervisorUserId,
     CompletedAt, CreatedAt, UpdatedAt)
VALUES
    (@ScheduleId, @AssetId, @ScheduleDate, N'2026-08', N'Quarter', N'Completed',
     N'Q3', NULL, 2026, N'2026-2027', @UserId, NULL,
     @CompletionTime, @FixtureCreatedAt, @CompletionTime);

INSERT INTO dbo.InspectionLocationAttempts
    (Id, AssetId, ScheduleId, ActorUserId, CapturedAt, DevicePositionTimestamp,
     MeasuredLatitude, MeasuredLongitude, AccuracyMeters, HasAccuracy, IsMocked,
     AccuracyMode, AcquisitionDurationMs, ExpectedLatitude, ExpectedLongitude,
     ExpectedRadiusMeters, DistanceMeters, Outcome)
VALUES
    (@LocationAttemptId, @AssetId, @ScheduleId, @UserId, @CompletionTime, @CompletionTime,
     0.001, 0.001, 4.5, 1, 0, N'Precise', 500,
     0.001, 0.001, 20.0, 0.0, N'Inside');

INSERT INTO dbo.PreventiveMaintenanceForms
    (Id, FileNumber, AssetCategory, Building, Department, PmCycle, PeriodType, Quarter,
     Semester, Year, AcademicYear, Status, CreatedByUserId, SubmittedByUserId,
     SubmittedAt, FieldWorkCompletedAt, CreatedAt, UpdatedAt)
VALUES
    (@FormId, N'SYN-RETIRE-2026-001', N'fire-extinguisher',
     N'Synthetic Verification Building', N'GSD', N'2026-08', N'Quarter', N'Q3',
     NULL, 2026, N'2026-2027', N'Acknowledged', @UserId, @UserId,
     @SubmittedAt, @CompletionTime, @FixtureCreatedAt, @AcknowledgedAt);

INSERT INTO dbo.InspectionRecords
    (Id, ScheduleId, PreventiveMaintenanceFormId, AssetId, InspectorUserId,
     LocationAttemptId, DateInspected, StartedAt, CompletedAt, DateAccomplished,
     IsOperational, Remarks, ActionsRecommendations, WaterReplaceCarbonFilter,
     WaterReplaceSedimentFilter, WaterCheckUvLight, RemarksEmbedding, CreatedAt, UpdatedAt)
VALUES
    (@InspectionId, @ScheduleId, @FormId, @AssetId, @UserId,
     @LocationAttemptId, @InspectionStartedAt, @InspectionStartedAt, @CompletionTime, NULL,
     1, N'Synthetic inspection passed.', NULL, NULL, NULL, NULL, NULL,
     @InspectionStartedAt, @CompletionTime);

INSERT INTO dbo.PreventiveMaintenanceAcknowledgements
    (Id, FormId, SignatoryName, SignatoryPosition, SignatureData, SignatureContentType,
     SignatureChecksum, CapturedByUserId, AcknowledgedAt)
VALUES
    (@AcknowledgementId, @FormId, N'Synthetic Signatory', N'Synthetic Department Head',
     N'c3ludGhldGljLXNpZ25hdHVyZQ==', N'image/png', REPLICATE(N'a', 64),
     @UserId, @AcknowledgedAt);

INSERT INTO dbo.ReferenceDocuments
    (Id, SourceType, SourceKey, Title, PublisherAuthority, Revision, LifecycleStatus,
     EffectiveDate, SupersededByDocumentId, ImportedAt, ContentChecksum,
     IsSynthetic, SyntheticFixtureKey)
VALUES
    (@ReferenceDocumentId, N'Institutional', N'SYNTHETIC-RETIREMENT-001',
     N'Synthetic Migration Verification Procedure', N'Synthetic Publisher', N'TEST-1',
     N'Active', NULL, NULL, @FixtureCreatedAt, REPLICATE(N'0', 64),
     1, N'rag-retirement-migration-verification');

INSERT INTO dbo.ReferenceDocumentApplicabilities
    (Id, ReferenceDocumentId, AssetCategory, Manufacturer, ModelSeries, EquipmentFamily, ScopeLabel)
VALUES
    (@ApplicabilityId, @ReferenceDocumentId, N'fire-extinguisher', NULL, NULL, NULL,
     N'Synthetic retirement verification fixture');

INSERT INTO dbo.ReferenceDocumentSections
    (Id, ReferenceDocumentId, Sequence, Heading, SourceLocator, PageStart, PageEnd,
     SectionText, SectionHash, CreatedAt, UpdatedAt)
VALUES
    (@SectionId, @ReferenceDocumentId, 0, N'Synthetic inspection checks',
     N'synthetic:fixture/section-001', 1, 1,
     N'Synthetic fixture text used only to verify preserved reference data.',
     REPLICATE(N'1', 64), @FixtureCreatedAt, @CompletionTime);

INSERT INTO dbo.ReferenceDocumentSectionEmbeddings
    (ReferenceDocumentSectionId, ProviderKey, ModelKey, EmbeddingProfile, Dimensions,
     VectorJson, SectionHash, GeneratedAt)
VALUES
    (@SectionId, N'synthetic', N'synthetic', N'synthetic-verification-v1', 2,
     N'[1,0]', REPLICATE(N'1', 64), @CompletionTime);

INSERT INTO dbo.MaintenanceSearchDocuments
    (InspectionId, AssetCategory, AssetCode, AssetId, AssetUpdatedAt, Building,
     DateInspected, Department, IsOperational, IssueKeysJson, LexiconVersion,
     Location, ProjectionVersion, ScheduleId, SearchText, SourceCreatedAt, SourceUpdatedAt)
VALUES
    (@InspectionId, N'fire-extinguisher', N'SYN-RETIRE-001', @AssetId, @CompletionTime,
     N'Synthetic Verification Building', @InspectionStartedAt, N'GSD', 1, N'[]',
     N'synthetic-v1', N'Synthetic Room 001', N'synthetic-v1', @ScheduleId,
     N'Synthetic migration retirement fixture.', @InspectionStartedAt, @CompletionTime);

INSERT INTO dbo.MaintenanceSearchDocumentEmbeddings
    (InspectionId, Dimensions, EmbeddingProfile, GeneratedAt, ModelKey, ProviderKey,
     SourceHash, VectorJson)
VALUES
    (@InspectionId, 2, N'synthetic-verification-v1', @CompletionTime,
     N'synthetic', N'synthetic', REPLICATE(N'2', 64), N'[1,0]');
'@
    Invoke-SqlNonQuery -Database $databaseName -Query $fixtureSql
    Assert-FixtureRelationships -Database $databaseName
    Assert-StorageState -Database $databaseName -Retired $false

    $legacyDocuments = [int](Invoke-SqlScalar -Database $databaseName -Query 'SELECT COUNT(*) FROM dbo.MaintenanceSearchDocuments;')
    $legacyEmbeddings = [int](Invoke-SqlScalar -Database $databaseName -Query 'SELECT COUNT(*) FROM dbo.MaintenanceSearchDocumentEmbeddings;')
    if ($legacyDocuments -ne 1 -or $legacyEmbeddings -ne 1) {
        throw 'The synthetic derived-storage rows were not created before retirement.'
    }
    $run.preRetirementDerivedRows = [ordered]@{ documents = $legacyDocuments; embeddings = $legacyEmbeddings }
    $run.checks.baselineMigrationAppliedDirectlyByEf = $true
    $run.checks.syntheticPmReferenceRelationshipsValid = $true
    $run.checks.populatedRetiredDerivedStorage = $true

    $baselineData = Get-TableState -Database $databaseName
    foreach ($table in $trackedTables) {
        if ([long]$baselineData[$table.Name].count -lt 1) {
            throw "Expected representative synthetic data in dbo.$($table.Name)."
        }
    }
    $run.baselineData = $baselineData

    Invoke-EfMigrationUpdate -Stage 'apply-retirement-migration' -Migration $retirementMigration
    $currentStage = 'verify-retirement-up'
    Assert-StorageState -Database $databaseName -Retired $true
    $afterRetirementData = Get-TableState -Database $databaseName
    Assert-DataStateEqual -Expected $baselineData -Actual $afterRetirementData -Stage 'retirement Up'
    $run.afterRetirementData = $afterRetirementData
    $run.checks.retiredTablesCatalogAndIndexAbsent = $true
    $run.checks.referenceFtsAndSchemaRetained = $true
    $run.checks.pmAndReferenceRowsUnchangedAfterUp = $true

    Invoke-EfMigrationUpdate -Stage 'rollback-retirement-migration' -Migration $previousMigration
    $currentStage = 'verify-retirement-down'
    Assert-StorageState -Database $databaseName -Retired $false
    $afterRollbackData = Get-TableState -Database $databaseName
    Assert-DataStateEqual -Expected $baselineData -Actual $afterRollbackData -Stage 'retirement Down'
    $run.afterRollbackData = $afterRollbackData
    $run.downRecreatedDerivedRows = [ordered]@{
        documents = [int](Invoke-SqlScalar -Database $databaseName -Query 'SELECT COUNT(*) FROM dbo.MaintenanceSearchDocuments;')
        embeddings = [int](Invoke-SqlScalar -Database $databaseName -Query 'SELECT COUNT(*) FROM dbo.MaintenanceSearchDocumentEmbeddings;')
    }
    if ($run.downRecreatedDerivedRows.documents -ne 0 -or $run.downRecreatedDerivedRows.embeddings -ne 0) {
        throw 'Retirement Down did not recreate empty maintenance-derived tables.'
    }
    $run.checks.rollbackRecreatedEmptyDerivedSchema = $true
    $run.checks.pmAndReferenceRowsUnchangedAfterDown = $true

    Invoke-EfMigrationUpdate -Stage 'reapply-retirement-migration' -Migration $retirementMigration
    $currentStage = 'verify-retirement-reapply'
    Assert-StorageState -Database $databaseName -Retired $true
    $afterReapplyData = Get-TableState -Database $databaseName
    Assert-DataStateEqual -Expected $baselineData -Actual $afterReapplyData -Stage 'retirement reapply'
    $run.afterReapplyData = $afterReapplyData
    $run.checks.reapplyRemovedDerivedSchema = $true
    $run.checks.pmAndReferenceRowsUnchangedAfterReapply = $true

    $run.status = 'passed'
    $run.checks.upDownUpPassed = $true
}
catch {
    $failure = $_
    $run.status = 'failed'
    $run.failedAt = $currentStage
    $run.failureType = $_.Exception.GetType().FullName
    $diagnosticException = $_.Exception
    $sqlException = $diagnosticException
    while ($null -ne $sqlException -and $sqlException -isnot [System.Data.SqlClient.SqlException]) {
        $sqlException = $sqlException.InnerException
    }
    if ($null -ne $sqlException) {
        $run.sqlErrorNumber = $sqlException.Number
        $diagnosticMessage = $sqlException.Message
    }
    else {
        $diagnosticMessage = $diagnosticException.Message
    }
    $diagnosticMessage = $diagnosticMessage -replace '(?i)(Server|Data Source|Database|Initial Catalog|Password|Pwd)\s*=\s*[^;,\r\n]+', '$1=[redacted]'
    $diagnosticMessage = $diagnosticMessage.Replace($databaseName, '[owned-db]').Replace('localhost', '[local-server]')
    $run.failureMessage = $diagnosticMessage
}
finally {
    [Environment]::SetEnvironmentVariable($connectionVariable, $previousConnectionString, 'Process')
    $restoredConnectionString = [Environment]::GetEnvironmentVariable($connectionVariable, 'Process')
    $run.environmentRestored = ([string]::IsNullOrEmpty($restoredConnectionString) -and [string]::IsNullOrEmpty($previousConnectionString)) -or $restoredConnectionString -ceq $previousConnectionString

    if ($databaseCreated -and $databaseName -match '^UniPMRetirement_[0-9a-f]{32}$') {
        try {
            [System.Data.SqlClient.SqlConnection]::ClearAllPools()
            Invoke-SqlNonQuery -Database 'master' -Query "ALTER DATABASE [$databaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$databaseName];"
            $remainingDatabaseId = Invoke-SqlScalar -Database 'master' -Query "SELECT DB_ID(N'$databaseName');"
            $run.databaseIdAbsent = $null -eq $remainingDatabaseId -or [System.Convert]::IsDBNull($remainingDatabaseId)
            $run.databaseDropped = $run.databaseIdAbsent
            if (-not $run.databaseIdAbsent) {
                throw 'Owned disposable database still exists after the drop command.'
            }
        }
        catch {
            $run.databaseDropped = $false
            if ($null -eq $failure) {
                $failure = $_
                $run.status = 'failed'
                $run.failedAt = 'cleanup-disposable-database'
                $run.failureType = $_.Exception.GetType().FullName
                $diagnosticException = $_.Exception
                $sqlException = $diagnosticException
                while ($null -ne $sqlException -and $sqlException -isnot [System.Data.SqlClient.SqlException]) {
                    $sqlException = $sqlException.InnerException
                }
                if ($null -ne $sqlException) {
                    $run.sqlErrorNumber = $sqlException.Number
                    $diagnosticMessage = $sqlException.Message
                }
                else {
                    $diagnosticMessage = $diagnosticException.Message
                }
                $diagnosticMessage = $diagnosticMessage -replace '(?i)(Server|Data Source|Database|Initial Catalog|Password|Pwd)\s*=\s*[^;,\r\n]+', '$1=[redacted]'
                $diagnosticMessage = $diagnosticMessage.Replace($databaseName, '[owned-db]').Replace('localhost', '[local-server]')
                $run.failureMessage = $diagnosticMessage
            }
        }
    }

    if (-not $run.environmentRestored -or ($databaseCreated -and -not $run.databaseDropped)) {
        $run.status = 'failed'
    }
    $run.finishedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    $serverMajorVersion = if ($null -eq $run.sqlServer) { 'unavailable' } else { $run.sqlServer.productMajorVersion }
    $run | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $summaryPath -Encoding utf8
    @(
        "Status: $($run.status)"
        "Started at UTC: $($run.startedAtUtc)"
        "Finished at UTC: $($run.finishedAtUtc)"
        "Tested commit: $testedCommit"
        "Source branch: $sourceBranch"
        "Helper identity: $($run.helperIdentity)"
        "SQL Server major version: $serverMajorVersion"
        "Compatibility level: $($run.compatibilityLevel)"
        "Disposable database dropped: $($run.databaseDropped)"
        "Disposable database ID absent after drop: $($run.databaseIdAbsent)"
        "Process environment restored: $($run.environmentRestored)"
        "SQL error number: $($run.sqlErrorNumber)"
        "Failure: $($run.failureMessage)"
        "Summary: artifacts/rag-retirement/$timestamp-$shortCommit/migration-verification.json"
    ) | Set-Content -LiteralPath $logPath -Encoding utf8
}

if ($null -ne $failure -or $run.status -ne 'passed') {
    throw "RAG retirement migration verification failed at '$($run.failedAt)'. Review $($run.artifactDirectory)/migration-verification.json."
}

Write-Output "RAG retirement migration Up -> Down -> Up passed. Artifacts: $($run.artifactDirectory)"
