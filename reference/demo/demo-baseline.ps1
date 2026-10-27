param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Capture', 'Verify', 'Restore')][string]$Mode,
    [string]$TargetDatabase
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$stateRoot = Join-Path $repoRoot 'artifacts/demo-rebuild'
$baselinePath = Join-Path $stateRoot 'clean-baseline.json'
$fixturePath = Join-Path $stateRoot 'scenario-fixture-v2.json'
$master = [System.Data.SqlClient.SqlConnection]::new('Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True')
$master.Open()
function Read-Scalar($connection, [string]$sql) {
    $cmd = $connection.CreateCommand(); $cmd.CommandText = $sql; $cmd.CommandTimeout = 180
    try { return $cmd.ExecuteScalar() } finally { $cmd.Dispose() }
}
function Sql-Literal([string]$value) { return "N'" + $value.Replace("'", "''") + "'" }
function Read-Fingerprints([string]$database, $expected) {
    $db = [System.Data.SqlClient.SqlConnection]::new("Server=localhost;Database=$database;Integrated Security=True;TrustServerCertificate=True")
    $db.Open()
    try {
        $result = @{}
        foreach ($property in $expected.PSObject.Properties) {
            $table = $property.Name
            if ($table -notmatch '^[A-Za-z]+$') { throw 'Invalid fixture table.' }
            $key = switch ($table) {
                'InspectionPhotoEvidence' { 'InspectionId' }
                'InspectionWmsReferrals' { 'InspectionId' }
                'ScheduleEnrollmentDeferrals' { 'AssetId, PmCycle' }
                default { 'Id' }
            }
            $json = [string](Read-Scalar $db "SELECT (SELECT * FROM dbo.[$table] ORDER BY $key FOR JSON PATH, INCLUDE_NULL_VALUES)")
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $result[$table] = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($json))).Replace('-', '') }
            finally { $sha.Dispose() }
        }
        return $result
    } finally { $db.Dispose() }
}
function Assert-Fingerprints([string]$database, $manifest) {
    $actual = Read-Fingerprints $database $manifest.snapshot
    foreach ($property in $manifest.snapshot.PSObject.Properties) {
        if ($actual[$property.Name] -ne $property.Value) { throw "Baseline mismatch in $database/$($property.Name)." }
    }
}
function Backup-Database([string]$database, [string]$label) {
    $directory = [string](Read-Scalar $master "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath'))")
    if (-not $directory) { throw 'SQL Server backup path unavailable.' }
    $path = Join-Path $directory ($database + '_' + $label + '_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff') + '.bak')
    $literal = Sql-Literal $path
    [void](Read-Scalar $master "BACKUP DATABASE [$database] TO DISK=$literal WITH COPY_ONLY,CHECKSUM; RESTORE VERIFYONLY FROM DISK=$literal WITH CHECKSUM;")
    return $path
}
function Read-BackupId([string]$path) {
    [void](Read-Scalar $master "RESTORE VERIFYONLY FROM DISK=$(Sql-Literal $path) WITH CHECKSUM")
    $cmd = $master.CreateCommand(); $cmd.CommandText = "RESTORE HEADERONLY FROM DISK=$(Sql-Literal $path)"; $cmd.CommandTimeout = 180
    try {
        $reader = $cmd.ExecuteReader()
        try {
            if (-not $reader.Read() -or -not [bool]$reader['HasBackupChecksums']) { throw 'Checksummed backup header is missing.' }
            return [string]$reader['BackupSetGUID']
        } finally { $reader.Dispose() }
    } finally { $cmd.Dispose() }
}
function Restore-Database([string]$database, [string]$backup) {
    $exists = [int](Read-Scalar $master "SELECT COUNT(*) FROM sys.databases WHERE name=$(Sql-Literal $database)")
    $dataDirectory = [string](Read-Scalar $master "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultDataPath'))")
    $logDirectory = [string](Read-Scalar $master "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultLogPath'))")
    if (-not $dataDirectory -or -not $logDirectory) { throw 'SQL Server data/log directories unavailable.' }
    $cmd = $master.CreateCommand(); $cmd.CommandText = "RESTORE FILELISTONLY FROM DISK=$(Sql-Literal $backup)"; $cmd.CommandTimeout = 180
    $moves = [System.Collections.Generic.List[string]]::new()
    try {
        $reader = $cmd.ExecuteReader()
        try {
            $index = 0
            while ($reader.Read()) {
                $index++
                $logical = [string]$reader['LogicalName']; $type = [string]$reader['Type']
                if ($type -notin @('D', 'L')) { throw 'Only ordinary SQL data and log files are supported.' }
                $directory = if ($type -eq 'L') { $logDirectory } else { $dataDirectory }
                $extension = if ($type -eq 'L') { '.ldf' } else { '.mdf' }
                $path = Join-Path $directory ($database + '_baseline_' + $index + $extension)
                $moves.Add("MOVE $(Sql-Literal $logical) TO $(Sql-Literal $path)")
            }
        } finally { $reader.Dispose() }
    } finally { $cmd.Dispose() }
    if ($moves.Count -eq 0) { throw 'Backup has no data files.' }
    try {
        if ($exists) { [void](Read-Scalar $master "ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE") }
        $replace = if ($exists) { ', REPLACE' } else { '' }
        [void](Read-Scalar $master ("RESTORE DATABASE [$database] FROM DISK=$(Sql-Literal $backup) WITH " + ($moves -join ', ') + ", CHECKSUM, RECOVERY$replace"))
    } finally {
        if ([int](Read-Scalar $master "SELECT COUNT(*) FROM sys.databases WHERE name=$(Sql-Literal $database)") -gt 0) {
            [void](Read-Scalar $master "ALTER DATABASE [$database] SET MULTI_USER")
        }
    }
}
try {
    if ($Mode -eq 'Capture') {
        if ($TargetDatabase) { throw 'Capture always reads the known local UniPMDb fixture.' }
        if (Test-Path -LiteralPath $baselinePath) { throw 'A clean baseline already exists. It was not replaced.' }
        $fixture = Get-Content -LiteralPath $fixturePath -Raw | ConvertFrom-Json
        if ($fixture.version -ne 2 -or @($fixture.baseAssets).Count -ne 153) { throw 'Scenario fixture manifest is invalid.' }
        Assert-Fingerprints 'UniPMDb' $fixture
        $backup = Backup-Database 'UniPMDb' 'clean_demo_baseline'
        Assert-Fingerprints 'UniPMDb' $fixture
        $photo = Join-Path $fixture.photoDirectory 'a46cc8d993104499827b000000000002.jpg'
        if ((Get-FileHash -LiteralPath $photo).Hash -ne (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'synthetic-inspection.jpg')).Hash) { throw 'Synthetic photo does not match the fixture.' }
        # SQL Server owns its backup directory; verify through SQL rather than
        # requiring the interactive Windows account to read protected files.
        $record = @{ version = 1; backupPath = $backup; backupSetId = (Read-BackupId $backup); fixture = $fixture }
        $record | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $baselinePath -Encoding UTF8
        Write-Output 'Captured and checksum-verified a clean local demonstration baseline.'
        exit 0
    }
    $baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
    if ($baseline.version -ne 1 -or (Read-BackupId $baseline.backupPath) -ne $baseline.backupSetId) { throw 'Baseline backup is invalid or changed.' }
    if ($Mode -eq 'Restore') {
        if ($TargetDatabase -ne 'UniPMDb' -and $TargetDatabase -notmatch '^UniPMDb_DemoCheck_[a-f0-9]{12}$') { throw 'Restore requires explicit UniPMDb or an owned disposable demonstration database name.' }
        if ([int](Read-Scalar $master "SELECT COUNT(*) FROM sys.databases WHERE name=$(Sql-Literal $TargetDatabase)") -gt 0) {
            [void](Backup-Database $TargetDatabase 'before_baseline_restore')
        }
        Restore-Database $TargetDatabase $baseline.backupPath
        Assert-Fingerprints $TargetDatabase $baseline.fixture
        if ($TargetDatabase -eq 'UniPMDb') {
            $photoDirectory = $baseline.fixture.photoDirectory
            New-Item -ItemType Directory -Path $photoDirectory -Force | Out-Null
            Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'synthetic-inspection.jpg') -Destination (Join-Path $photoDirectory 'a46cc8d993104499827b000000000002.jpg') -Force
            $baseline.fixture | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $fixturePath -Encoding UTF8
        }
        Write-Output "Restored $TargetDatabase and verified every fixture-table fingerprint."
        exit 0
    }
    if ($TargetDatabase) { throw 'Verify creates and drops its own isolated database.' }
    $checkDb = 'UniPMDb_DemoCheck_' + [guid]::NewGuid().ToString('N').Substring(0, 12)
    try {
        Restore-Database $checkDb $baseline.backupPath
        Assert-Fingerprints $checkDb $baseline.fixture
        # Simulate a practice-session edit, then exercise real restoration.
        [void](Read-Scalar $master "UPDATE [$checkDb].dbo.Assets SET Location=N'Synthetic practice edit' WHERE Id=(SELECT TOP 1 Id FROM [$checkDb].dbo.Assets ORDER BY Id)")
        $changed = $false
        try { Assert-Fingerprints $checkDb $baseline.fixture } catch { $changed = $true }
        if (-not $changed) { throw 'Practice edit was not detected.' }
        Restore-Database $checkDb $baseline.backupPath
        Assert-Fingerprints $checkDb $baseline.fixture
        Write-Output 'PASS: clean backup restored, practice edit detected, second restore recovered all eleven fixture fingerprints.'
    } finally {
        if ([int](Read-Scalar $master "SELECT COUNT(*) FROM sys.databases WHERE name=$(Sql-Literal $checkDb)") -gt 0) {
            [void](Read-Scalar $master "ALTER DATABASE [$checkDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$checkDb]")
        }
    }
} finally { $master.Dispose() }
