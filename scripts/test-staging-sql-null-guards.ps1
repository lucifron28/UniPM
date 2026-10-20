<# Execute the checked-in guard predicates against NULL, wrong and expected values.
   Read-only local SQL Server expressions only. Never connects to Azure or seeds data. #>
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$preflight = Get-Content (Join-Path $PSScriptRoot 'initialize-azure-staging-demo.ps1') -Raw
$postflight = Get-Content (Join-Path $root 'database/azure-staging-demo-postflight.sql') -Raw
$cases = @(
    @{ Id = 51000; Source = 'DB_NAME()'; Expected = "N'unipm-staging-db'"; Wrong = "N'wrong'" },
    @{ Id = 51008; Source = "SERVERPROPERTY('ServerName')"; Expected = "N'sql-unipm-staging'"; Wrong = "N'wrong'" },
    @{ Id = 51001; Source = '@engineEdition'; Expected = '5'; Wrong = '0' },
    @{ Id = 51002; Source = '@compatibilityLevel'; Expected = '150'; Wrong = '140' },
    @{ Id = 51009; Source = "FULLTEXTSERVICEPROPERTY('IsFullTextInstalled')"; Expected = '1'; Wrong = '0' }
)
$expressions = [Collections.Generic.List[string]]::new()
foreach ($case in $cases) {
    $match = [regex]::Match($preflight, "(?m)^IF (?<predicate>[^\r\n]+?)\s+THROW $($case.Id),")
    if (-not $match.Success) { throw 'Could not locate an expected preflight guard.' }
    $predicate = $match.Groups['predicate'].Value.Replace('$expectedDatabase', 'unipm-staging-db')
    foreach ($value in @('NULL', $case.Wrong, $case.Expected)) {
        $rejection = if ($value -eq $case.Expected) { 0 } else { 1 }
        $condition = $predicate.Replace($case.Source, $value)
        $expressions.Add("IF (CASE WHEN $condition THEN 1 ELSE 0 END) <> $rejection THROW 51200, N'Preflight guard truth table failed.', 1;")
    }
}
# Verify the actual postflight target condition, including each missing target property.
$target = [regex]::Match($postflight, '(?ms)^IF (?<predicate>.*?)\s+THROW 51100,').Groups['predicate'].Value
$compatibility = [regex]::Match($postflight, '(?ms)^IF (?<predicate>ISNULL\(\(SELECT compatibility_level.*?)\s+THROW 51101,').Groups['predicate'].Value
if (-not $target -or -not $compatibility) { throw 'Could not locate expected postflight guards.' }
for ($index = -1; $index -lt 3; $index++) {
    $condition = $target.Replace('DB_NAME()', $(if ($index -eq 0) { 'NULL' } else { "N'unipm-staging-db'" }))
    $condition = $condition.Replace("SERVERPROPERTY('ServerName')", $(if ($index -eq 1) { 'NULL' } else { "N'sql-unipm-staging'" }))
    $condition = $condition.Replace("SERVERPROPERTY('EngineEdition')", $(if ($index -eq 2) { 'NULL' } else { '5' }))
    $rejection = if ($index -eq -1) { 0 } else { 1 }
    $expressions.Add("IF (CASE WHEN $condition THEN 1 ELSE 0 END) <> $rejection THROW 51201, N'Postflight target truth table failed.', 1;")
}
foreach ($value in @('NULL', '140', '150')) {
    $condition = $compatibility.Replace('(SELECT compatibility_level FROM sys.databases WHERE database_id = DB_ID())', $value)
    $rejection = if ($value -eq '150') { 0 } else { 1 }
    $expressions.Add("IF (CASE WHEN $condition THEN 1 ELSE 0 END) <> $rejection THROW 51202, N'Postflight compatibility truth table failed.', 1;")
}
$query = "SET NOCOUNT ON;`n" + ($expressions -join "`n") + "`nSELECT N'PASS: $($expressions.Count) SQL null-safety cases';"
$originalStartup = [Environment]::GetEnvironmentVariable('SQLCMDINI', 'Process')
try {
    [Environment]::SetEnvironmentVariable('SQLCMDINI', $null, 'Process')
    # Fixed local target and integrated authentication; only scalar predicates are evaluated.
    & sqlcmd.exe -S . -d master -E -N -C -b -h -1 -W -Q $query
    if ($LASTEXITCODE -ne 0) { throw 'Local SQL null-guard verification failed.' }
}
finally { [Environment]::SetEnvironmentVariable('SQLCMDINI', $originalStartup, 'Process') }
