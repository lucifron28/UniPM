$ErrorActionPreference='Stop'
$fixtureName='UniPMDb_Test182_'+[guid]::NewGuid().ToString('N').Substring(0,12)
if ($fixtureName -notmatch '^UniPMDb_Test182_[a-f0-9]{12}$') { throw 'Invalid fixture name' }
$fixtureName | Set-Content artifacts/test-182/database.txt
$env:ConnectionStrings__DefaultConnection="Server=localhost;Database=$fixtureName;Integrated Security=True;TrustServerCertificate=True"
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:UNIPM_WEB_ORIGIN='http://localhost:5187'
$env:UNIPM_JWT_ISSUER='UniPM-Test182'
$env:UNIPM_JWT_AUDIENCE='UniPM-Test182'
$env:UNIPM_JWT_SIGNING_KEY=[guid]::NewGuid().ToString('N')+[guid]::NewGuid().ToString('N')
foreach ($setting in @('UNIPM_DEV_USER_PASSWORD','DEEPSEEK_API_KEY')) {
    $entry=Get-Content -LiteralPath .env | Where-Object { $_.StartsWith($setting+'=') } | Select-Object -First 1
    if (-not $entry) { throw "Missing required setting: $setting" }
    [Environment]::SetEnvironmentVariable($setting,($entry -split '=',2)[1].Trim().Trim('"').Trim("'"),'Process')
}
$env:NaturalLanguageAnalytics__V3DeepSeekEnabled='true'
$env:Logging__LogLevel__Default='Warning'
$env:TEST182_TRACE_DIRECTORY=(Resolve-Path artifacts/test-182/telemetry).Path
& dotnet artifacts/test-182/runtime/bin/Debug/net10.0/UniPM.Api.dll --migrate-database *> artifacts/test-182/migrate.log
if ($LASTEXITCODE -ne 0) { throw 'Fixture migrations failed; inspect ignored migration log' }
& dotnet artifacts/test-182/runtime/bin/Debug/net10.0/UniPM.Api.dll --seed-development-users *> artifacts/test-182/users.log
if ($LASTEXITCODE -ne 0) { throw 'Synthetic identity preparation failed' }
& sqlcmd -I -S localhost -E -d $fixtureName -b -Q "INSERT INTO Assets (Id,AssetCode,AssetCategory,Building,Department,Status,CreatedAt,UpdatedAt) VALUES ('18200000-0000-0000-0000-000000000001','TEST182-FE-01','fire-extinguisher','North Block','Synthetic Department','Active','2024-01-01T00:00:00+00:00','2024-01-01T00:00:00+00:00');" *> artifacts/test-182/fixture.log
if ($LASTEXITCODE -ne 0) { throw 'Synthetic catalog preparation failed' }
$service=Start-Process -FilePath 'dotnet' -ArgumentList @('artifacts/test-182/runtime/bin/Debug/net10.0/UniPM.Api.dll','--urls','http://localhost:5263') -WindowStyle Hidden -RedirectStandardOutput artifacts/test-182/api.stdout.log -RedirectStandardError artifacts/test-182/api.stderr.log -PassThru
$service.Id | Set-Content artifacts/test-182/api.pid
'Owned one-asset SQL fixture and interpretation API started.'
