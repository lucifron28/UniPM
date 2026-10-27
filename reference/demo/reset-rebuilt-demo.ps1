param(
    [switch]$Initialize,
    [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$manifestPath = Join-Path $repoRoot 'artifacts/demo-rebuild/fixture-manifest.json'
$sqlPath = Join-Path $PSScriptRoot 'rebuilt-demo-reset.sql'
$connectionString = 'Server=localhost;Database=UniPMDb;Integrated Security=True;TrustServerCertificate=True'
$tables = @('Assets', 'PreventiveMaintenanceSchedules', 'PreventiveMaintenanceForms', 'InspectionRecords', 'ScheduleAssignmentReassignmentAudits')
function Read-Ids($connection, [string]$query) {
    $command = $connection.CreateCommand()
    $command.CommandText = $query
    $reader = $command.ExecuteReader()
    $values = [System.Collections.Generic.List[string]]::new()
    try { while ($reader.Read()) { $values.Add($reader.GetGuid(0).ToString()) } }
    finally { $reader.Dispose(); $command.Dispose() }
    return $values.ToArray()
}
function Read-Scalar($connection, [string]$query) {
    $command = $connection.CreateCommand()
    $command.CommandText = $query
    try { return $command.ExecuteScalar() }
    finally { $command.Dispose() }
}
function Capture-Manifest($connection, $baseIds) {
    $ids = [ordered]@{}
    foreach ($table in $tables) { $ids[$table] = @(Read-Ids $connection "SELECT Id FROM dbo.[$table] ORDER BY Id") }
    return @{ version = 1; server = 'localhost'; database = 'UniPMDb'; baseAssets = @($baseIds); tables = $ids }
}
$mustBeEmpty = @('InspectionPhotoEvidence', 'InspectionWmsReferrals', 'InspectionWmsReferralAudits', 'InspectionLocationAttempts', 'PreventiveMaintenanceAcknowledgements', 'ScheduleEnrollmentDeferrals')
$connection = [System.Data.SqlClient.SqlConnection]::new($connectionString)
$connection.Open()
try {
    if ($Initialize) {
        if (Test-Path -LiteralPath $manifestPath) { throw 'Manifest already exists. It was not overwritten.' }
        foreach ($table in $mustBeEmpty) { if ((Read-Scalar $connection "SELECT COUNT(*) FROM dbo.[$table]") -ne 0) { throw "$table contains records. No manifest was created." } }
        $nonSynthetic = Read-Scalar $connection "SELECT COUNT(*) FROM InspectionRecords WHERE Remarks IS NULL OR Remarks <> 'Synthetic demonstration inspection.'"
        $nonSyntheticEvents = Read-Scalar $connection "SELECT COUNT(*) FROM ScheduleAssignmentReassignmentAudits WHERE Reason <> 'Synthetic demonstration assignment; rebuilt locally'"
        $baseIds = @(Read-Ids $connection "SELECT Id FROM Assets WHERE AssetCode NOT LIKE '%-A2' AND AssetCode NOT LIKE '%-A3' ORDER BY Id")
        $assetCount = Read-Scalar $connection 'SELECT COUNT(*) FROM Assets'
        if ($nonSynthetic -ne 0 -or $nonSyntheticEvents -ne 0 -or $baseIds.Count -ne 45 -or $assetCount -ne 135) { throw 'Database does not match the rebuilt synthetic fixture. No manifest was created.' }
        $manifest = Capture-Manifest $connection $baseIds
        New-Item -ItemType Directory -Path (Split-Path $manifestPath) -Force | Out-Null
        $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
        Write-Output "Saved the local fixture manifest to $manifestPath."
        exit 0
    }
    if (-not (Test-Path -LiteralPath $manifestPath)) { throw 'Fixture manifest is missing. No records were changed.' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.version -ne 1 -or $manifest.server -ne 'localhost' -or $manifest.database -ne 'UniPMDb' -or @($manifest.baseAssets).Count -ne 45) { throw 'Invalid fixture manifest. No records were changed.' }
    $guard = [System.Text.StringBuilder]::new()
    [void]$guard.AppendLine('CREATE TABLE #BaseAssets (Id uniqueidentifier NOT NULL PRIMARY KEY);')
    $baseValues = @($manifest.baseAssets | ForEach-Object { "('$( [guid]::Parse($_).ToString() )')" })
    [void]$guard.AppendLine('INSERT INTO #BaseAssets(Id) VALUES ' + ($baseValues -join ',') + ';')
    foreach ($table in $mustBeEmpty) { [void]$guard.AppendLine("IF EXISTS (SELECT 1 FROM dbo.[$table] WITH (TABLOCKX,HOLDLOCK)) THROW 51011, 'Unrelated records exist in $table; reset stopped.', 1;") }
    $number = 0
    foreach ($table in $tables) {
        $number++
        $expected = @($manifest.tables.$table)
        [void]$guard.AppendLine("CREATE TABLE #Expected$number (Id uniqueidentifier NOT NULL PRIMARY KEY);")
        for ($offset = 0; $offset -lt $expected.Count; $offset += 500) {
            $last = [Math]::Min($offset + 499, $expected.Count - 1)
            $values = @($expected[$offset..$last] | ForEach-Object { "('$( [guid]::Parse($_).ToString() )')" })
            [void]$guard.AppendLine("INSERT INTO #Expected$number(Id) VALUES " + ($values -join ',') + ';')
        }
        [void]$guard.AppendLine("IF EXISTS (SELECT Id FROM dbo.[$table] WITH (TABLOCKX,HOLDLOCK) EXCEPT SELECT Id FROM #Expected$number) OR EXISTS (SELECT Id FROM #Expected$number EXCEPT SELECT Id FROM dbo.[$table] WITH (TABLOCKX,HOLDLOCK)) THROW 51010, 'Fixture or unrelated records changed in $table; reset stopped.', 1;")
    }
    $sql = Get-Content -LiteralPath $sqlPath -Raw
    if (-not $sql.Contains('-- MANIFEST_GUARD')) { throw 'Reset guard is missing.' }
    $sql = $sql.Replace('-- MANIFEST_GUARD', $guard.ToString())
    $snapshotQuery = ($tables | ForEach-Object { "SELECT '$($_)' AS FixtureTable, Id AS FixtureId FROM dbo.[$_]" }) -join ' UNION ALL '
    $ending = if ($CheckOnly) { ' ROLLBACK TRANSACTION;' } else { ' COMMIT TRANSACTION;' }
    $sql = $sql.Replace(' COMMIT TRANSACTION;', " $snapshotQuery;`n$ending")
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 180
    $command.CommandText = $sql
    $ids = [ordered]@{}
    foreach ($table in $tables) { $ids[$table] = [System.Collections.Generic.List[string]]::new() }
    try {
        $reader = $command.ExecuteReader()
        try {
            do {
                if ($reader.FieldCount -eq 2 -and $reader.GetName(0) -eq 'FixtureTable') {
                    while ($reader.Read()) { $ids[$reader.GetString(0)].Add($reader.GetGuid(1).ToString()) }
                } else { while ($reader.Read()) {} }
            } while ($reader.NextResult())
        } finally { $reader.Dispose() }
    } finally { $command.Dispose() }
    if ($CheckOnly) { Write-Output 'Reset check passed and was rolled back. No records changed.'; exit 0 }
    $updated = @{ version = 1; server = 'localhost'; database = 'UniPMDb'; baseAssets = @($manifest.baseAssets); tables = $ids }
    $temporary = "$manifestPath.new"
    $updated | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $temporary -Encoding UTF8
    Move-Item -LiteralPath $temporary -Destination $manifestPath -Force
    Write-Output 'Rebuilt the synthetic fixture and updated its local manifest.'
}
finally { $connection.Dispose() }

