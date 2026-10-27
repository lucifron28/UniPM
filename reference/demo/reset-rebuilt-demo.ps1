param(
    [switch]$Initialize,
    [switch]$CheckOnly,
    [datetime]$SeedDate = (Get-Date),
    [string]$PhotoDirectory
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$manifestPath = Join-Path $repoRoot 'artifacts/demo-rebuild/scenario-fixture-v2.json'
$tables = [ordered]@{
    Assets = 'Id'; PreventiveMaintenanceSchedules = 'Id'; PreventiveMaintenanceForms = 'Id'
    InspectionRecords = 'Id'; ScheduleAssignmentReassignmentAudits = 'Id'
    PreventiveMaintenanceAcknowledgements = 'Id'; InspectionWmsReferrals = 'InspectionId'
    InspectionWmsReferralAudits = 'Id'; InspectionPhotoEvidence = 'InspectionId'
    InspectionLocationAttempts = 'Id'; ScheduleEnrollmentDeferrals = 'AssetId, PmCycle'
}
$connection = [System.Data.SqlClient.SqlConnection]::new('Server=localhost;Database=UniPMDb;Integrated Security=True;TrustServerCertificate=True')
$connection.Open()
$transaction = $null
function Invoke-Scalar([string]$sql) {
    $command = $connection.CreateCommand()
    $command.Transaction = $transaction
    $command.CommandText = $sql
    $command.CommandTimeout = 180
    try { return $command.ExecuteScalar() } finally { $command.Dispose() }
}
function Read-Snapshot {
    $snapshot = [ordered]@{}
    foreach ($table in $tables.Keys) {
        $json = [string](Invoke-Scalar "SELECT (SELECT * FROM dbo.[$table] WITH (TABLOCKX,HOLDLOCK) ORDER BY $($tables[$table]) FOR JSON PATH, INCLUDE_NULL_VALUES)")
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try { $snapshot[$table] = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($json))).Replace('-', '') }
        finally { $sha.Dispose() }
    }
    return $snapshot
}
try {
    if ($Initialize) {
        if (Test-Path -LiteralPath $manifestPath) { throw 'Scenario manifest already exists; it was not overwritten.' }
        $transaction = $connection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
        $snapshot = Read-Snapshot
        $safe = Invoke-Scalar @'
SELECT CASE WHEN (SELECT COUNT(*) FROM Assets)=153
 AND (SELECT COUNT(*) FROM PreventiveMaintenanceSchedules)=846
 AND NOT EXISTS(SELECT 1 FROM InspectionRecords WHERE Remarks IS NULL OR Remarks NOT IN ('Synthetic demonstration inspection.','Synthetic demonstration inspection. Acknowledged seed example.'))
 AND NOT EXISTS(SELECT 1 FROM PreventiveMaintenanceAcknowledgements WHERE SignatoryName<>'Synthetic department representative')
 AND NOT EXISTS(SELECT 1 FROM InspectionWmsReferrals)
 AND NOT EXISTS(SELECT 1 FROM InspectionWmsReferralAudits)
 AND NOT EXISTS(SELECT 1 FROM InspectionPhotoEvidence)
 AND NOT EXISTS(SELECT 1 FROM InspectionLocationAttempts)
 AND NOT EXISTS(SELECT 1 FROM ScheduleEnrollmentDeferrals)
 AND NOT EXISTS(SELECT 1 FROM ScheduleAssignmentReassignmentAudits WHERE Reason<>'Synthetic demonstration assignment; rebuilt locally')
 THEN 1 ELSE 0 END
'@
        if ($safe -ne 1) { throw 'Database is not the inspected synthetic fixture. No records were adopted.' }
        $assetJson = [string](Invoke-Scalar 'SELECT (SELECT Id FROM Assets ORDER BY Id FOR JSON PATH)')
        $manifest = @{ version = 2; baseAssets = @($assetJson | ConvertFrom-Json | ForEach-Object { $_.Id }); snapshot = $snapshot }
        $transaction.Rollback(); $transaction.Dispose(); $transaction = $null
        New-Item -ItemType Directory -Path (Split-Path $manifestPath) -Force | Out-Null
        $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
        Write-Output 'Captured the existing synthetic fixture. Database records were not changed.'
        exit 0
    }
    if (-not (Test-Path -LiteralPath $manifestPath)) { throw 'Scenario manifest is missing. Initialize only the inspected synthetic fixture first.' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.version -ne 2 -or @($manifest.baseAssets).Count -ne 153) { throw 'Invalid scenario manifest.' }
    if (-not $PhotoDirectory) {
        if ($env:InspectionPhotoEvidence__StorageProvider -and $env:InspectionPhotoEvidence__StorageProvider -ne 'FileSystem') { throw 'This fixture requires local FileSystem photo storage.' }
        $PhotoDirectory = $env:InspectionPhotoEvidence__StorageDirectory
        if (-not $PhotoDirectory) { $PhotoDirectory = 'App_Data/InspectionPhotos' }
        if (-not [IO.Path]::IsPathRooted($PhotoDirectory)) { $PhotoDirectory = Join-Path (Join-Path $repoRoot 'server') $PhotoDirectory }
    }
    $PhotoDirectory = [IO.Path]::GetFullPath($PhotoDirectory)
    if (-not $CheckOnly) {
        $backupDir = [string](Invoke-Scalar "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath'))")
        if (-not $backupDir) { throw 'SQL Server backup directory is unavailable.' }
        $backup = (Join-Path $backupDir ('UniPMDb_before_scenarios_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff') + '.bak')).Replace("'", "''")
        [void](Invoke-Scalar "BACKUP DATABASE [UniPMDb] TO DISK=N'$backup' WITH COPY_ONLY,CHECKSUM; RESTORE VERIFYONLY FROM DISK=N'$backup' WITH CHECKSUM;")
        Write-Output 'Created and verified a copy-only database backup.'
    }
    $transaction = $connection.BeginTransaction([System.Data.IsolationLevel]::Serializable)
    $current = Read-Snapshot
    foreach ($table in $tables.Keys) {
        if ($current[$table] -ne $manifest.snapshot.$table) { throw "Records changed in $table. Reset stopped to protect demonstration or user changes." }
    }
    $baseValues = @($manifest.baseAssets | ForEach-Object { "('$( [guid]::Parse($_) )')" }) -join ','
    $photoFile = Join-Path $PSScriptRoot 'synthetic-inspection.jpg'
    $sql = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'rebuilt-demo-reset.sql') -Raw
    $sql = $sql.Replace('-- BASE_ASSETS', "INSERT INTO #BaseAssets VALUES $baseValues;")
    $sql = $sql.Replace('-- PHOTO_LENGTH', ([string](Get-Item -LiteralPath $photoFile).Length) + ',')
    $command = $connection.CreateCommand()
    $command.Transaction = $transaction
    $command.CommandTimeout = 180
    $command.CommandText = $sql
    [void]$command.Parameters.AddWithValue('@SeedDate', $SeedDate.ToString('yyyy-MM-dd'))
    try { [void]$command.ExecuteNonQuery() } finally { $command.Dispose() }
    $snapshot = Read-Snapshot
    if ($CheckOnly) {
        $transaction.Rollback(); $transaction.Dispose(); $transaction = $null
        Write-Output 'Scenario reset check passed and rolled back. No database or photo files changed.'
        exit 0
    }
    # Stage before commit. Failed replacement leaves an old manifest that refuses the next reset.
    $updated = @{ version = 2; baseAssets = @($manifest.baseAssets); snapshot = $snapshot; seedDate = $SeedDate.ToString('yyyy-MM-dd'); photoDirectory = $PhotoDirectory }
    $temporary = "$manifestPath.new"
    $updated | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $temporary -Encoding UTF8
    New-Item -ItemType Directory -Path $PhotoDirectory -Force | Out-Null
    Copy-Item -LiteralPath $photoFile -Destination (Join-Path $PhotoDirectory 'a46cc8d993104499827b000000000002.jpg') -Force
    $transaction.Commit(); $transaction.Dispose(); $transaction = $null
    Move-Item -LiteralPath $temporary -Destination $manifestPath -Force
    Write-Output "Populated scenario fixture v2 for $($SeedDate.ToString('yyyy-MM-dd')). Manifest updated."
}
finally {
    if ($transaction) { $transaction.Rollback(); $transaction.Dispose() }
    $connection.Dispose()
}
