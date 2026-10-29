$ErrorActionPreference = 'Stop'
Set-Location (Resolve-Path (Join-Path $PSScriptRoot '../../..'))
$artifactDirectory = Join-Path $PWD 'artifacts/test-188'
New-Item -ItemType Directory -Force $artifactDirectory | Out-Null
if (Test-Path (Join-Path $artifactDirectory 'database.txt')) { throw 'Fixture already initialized; do not reuse a measurement database.' }
if (Get-NetTCPConnection -LocalPort 5268 -State Listen -ErrorAction SilentlyContinue) { throw 'Evaluation port is already in use.' }
$fixtureName = 'UniPMDb_Test188_' + [guid]::NewGuid().ToString('N').Substring(0,12)
if ($fixtureName -notmatch '^UniPMDb_Test188_[a-f0-9]{12}$') { throw 'Invalid owned fixture name.' }
$fixtureName | Set-Content (Join-Path $artifactDirectory 'database.txt')
$env:ConnectionStrings__DefaultConnection = "Server=localhost;Database=$fixtureName;Integrated Security=True;TrustServerCertificate=True"
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:UNIPM_WEB_ORIGIN = 'http://localhost:5187'
$env:UNIPM_JWT_ISSUER = 'UniPM-Test188'
$env:UNIPM_JWT_AUDIENCE = 'UniPM-Test188'
$env:UNIPM_JWT_SIGNING_KEY = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
foreach ($setting in @('UNIPM_DEV_USER_PASSWORD','DEEPSEEK_API_KEY')) {
    $entry = Get-Content -LiteralPath .env | Where-Object { $_.StartsWith($setting+'=') } | Select-Object -First 1
    if (-not $entry) { throw "Required backend setting is unavailable: $setting" }
    [Environment]::SetEnvironmentVariable($setting, ($entry -split '=',2)[1].Trim().Trim('"').Trim("'"), 'Process')
}
$env:NaturalLanguageAnalytics__V3DeepSeekEnabled = 'true'
$env:Logging__LogLevel__Default = 'Warning'
$assembly = 'artifacts/guard-build/bin/server/debug/UniPM.Api.dll'
& dotnet $assembly --migrate-database *> (Join-Path $artifactDirectory 'migrate.log')
if ($LASTEXITCODE -ne 0) { throw 'Fixture migration failed; inspect ignored log.' }
& dotnet $assembly --seed-development-users *> (Join-Path $artifactDirectory 'users.log')
if ($LASTEXITCODE -ne 0) { throw 'Synthetic identity preparation failed.' }
& sqlcmd -I -S localhost -E -d $fixtureName -b -Q "INSERT INTO Assets (Id,AssetCode,AssetCategory,Building,Department,Status,CreatedAt,UpdatedAt) VALUES ('18800000-0000-0000-0000-000000000001','TEST188-FE-01','fire-extinguisher','North Block','Synthetic Department','Active','2024-01-01T00:00:00+00:00','2024-01-01T00:00:00+00:00');" *> (Join-Path $artifactDirectory 'fixture.log')
if ($LASTEXITCODE -ne 0) { throw 'Synthetic catalog preparation failed.' }
$service = Start-Process -FilePath dotnet -ArgumentList @($assembly,'--urls','http://localhost:5268') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $artifactDirectory 'api.stdout.log') -RedirectStandardError (Join-Path $artifactDirectory 'api.stderr.log') -PassThru
$service.Id | Set-Content (Join-Path $artifactDirectory 'api.pid')
for ($attempt = 0; $attempt -lt 40; $attempt++) {
    Start-Sleep -Seconds 1
    if ($service.HasExited) { throw 'Owned API exited during preflight.' }
    $count = & sqlcmd -S localhost -E -d $fixtureName -b -h -1 -Q 'SET NOCOUNT ON; SELECT COUNT(*) FROM PreventiveMaintenanceSchedules;'
    if ($LASTEXITCODE -eq 0 -and ($count -join '').Trim() -eq '1') {
        'Owned synthetic fixture ready. No evaluation questions have been sent.'
        exit 0
    }
}
throw 'Owned fixture was not ready before the preflight timeout.'
