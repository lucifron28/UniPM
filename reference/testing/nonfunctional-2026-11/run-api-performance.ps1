[CmdletBinding()]
param(
    [string]$DatabaseName = "UniPM_Nonfunctional_2026_11_12",
    [string]$BaseUrl = "http://127.0.0.1:5254",
    [ValidateRange(20, 500)]
    [int]$SampleCount = 30,
    [switch]$ResetDatabase
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$artifactRoot = Join-Path $repoRoot "artifacts\nonfunctional-2026-11\web-performance"
$runDirectory = Join-Path $artifactRoot (Get-Date -Format "yyyyMMdd-HHmmss")
$adminConnectionString = "Server=lpc:localhost;Database=master;Integrated Security=True;Encrypt=False;TrustServerCertificate=True"
$databaseConnectionString = "Server=lpc:localhost;Database=$DatabaseName;Integrated Security=True;Encrypt=False;TrustServerCertificate=True"

if ($DatabaseName -notmatch "^UniPM_Nonfunctional_[A-Za-z0-9_]+$") {
    throw "DatabaseName must start with UniPM_Nonfunctional_ and contain only letters, digits, or underscores."
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw "dotnet is not on PATH." }
if (-not (Get-Command node -ErrorAction SilentlyContinue)) { throw "node is not on PATH." }
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

$envNames = @(
    "ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT", "ASPNETCORE_URLS",
    "ConnectionStrings__DefaultConnection", "UNIPM_JWT_ISSUER",
    "UNIPM_JWT_AUDIENCE", "UNIPM_JWT_SIGNING_KEY", "UNIPM_WEB_ORIGIN",
    "UNIPM_DEV_USER_PASSWORD", "PreventiveMaintenanceScheduleGeneration__WorkerEnabled"
)
$oldEnvironment = @{}
foreach ($name in $envNames) { $oldEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, "Process") }
$apiProcess = $null

try {
    $master = [System.Data.SqlClient.SqlConnection]::new($adminConnectionString)
    $master.Open()
    try {
        $exists = $master.CreateCommand()
        $exists.CommandText = "SELECT DB_ID(@name)"
        [void]$exists.Parameters.Add("@name", [System.Data.SqlDbType]::NVarChar, 128)
        $exists.Parameters["@name"].Value = $DatabaseName
        $databaseId = $exists.ExecuteScalar()
        if ($null -ne $databaseId -and $databaseId -ne [DBNull]::Value) {
            if (-not $ResetDatabase) {
                throw "The isolated test database already exists. Use -ResetDatabase only when it contains no data you need."
            }
            $drop = $master.CreateCommand()
            $drop.CommandText = "ALTER DATABASE [$DatabaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$DatabaseName];"
            [void]$drop.ExecuteNonQuery()
        }
        $create = $master.CreateCommand()
        $create.CommandText = "CREATE DATABASE [$DatabaseName]; ALTER DATABASE [$DatabaseName] SET COMPATIBILITY_LEVEL = 150;"
        [void]$create.ExecuteNonQuery()
    }
    finally {
        $master.Close()
        $master.Dispose()
    }

    $randomBytes = New-Object byte[] 48
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($randomBytes) } finally { $rng.Dispose() }
    $testPassword = [Convert]::ToBase64String($randomBytes)

    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:DOTNET_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = $BaseUrl
    $env:ConnectionStrings__DefaultConnection = $databaseConnectionString
    $env:UNIPM_JWT_ISSUER = "UniPM-local-nonfunctional-test"
    $env:UNIPM_JWT_AUDIENCE = "UniPM-local-nonfunctional-test"
    $env:UNIPM_JWT_SIGNING_KEY = $testPassword + "local-test-key"
    $env:UNIPM_WEB_ORIGIN = "http://localhost:5173"
    $env:UNIPM_DEV_USER_PASSWORD = $testPassword
    $env:PreventiveMaintenanceScheduleGeneration__WorkerEnabled = "false"

    Push-Location $repoRoot
    try {
        & dotnet build server\server.csproj --configuration Release *> (Join-Path $runDirectory "server-build.log")
        if ($LASTEXITCODE -ne 0) { throw "Release server build failed; inspect server-build.log." }

        $commands = @(
            @{ Argument = "--migrate-database"; Log = "database-migrate.log" },
            @{ Argument = "--seed-development-users"; Log = "seed-development-users.log" },
            @{ Argument = "--seed-synthetic"; Log = "seed-synthetic.log" },
            @{ Argument = "--seed-demo"; Log = "seed-demo.log" }
        )
        foreach ($step in $commands) {
            & dotnet run --project server\server.csproj --configuration Release --no-build -- $step.Argument *> (Join-Path $runDirectory $step.Log)
            if ($LASTEXITCODE -ne 0) { throw "Maintenance command failed; inspect $($step.Log)." }
        }

        $port = ([Uri]$BaseUrl).Port
        $listener = Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue
        if ($listener) { throw "Port $port is occupied; refusing to stop an unknown process." }

        $apiProcess = Start-Process -FilePath "dotnet" -ArgumentList @("run", "--project", "server\server.csproj", "--configuration", "Release", "--no-build") -WorkingDirectory $repoRoot -PassThru -RedirectStandardOutput (Join-Path $runDirectory "api-server.stdout.log") -RedirectStandardError (Join-Path $runDirectory "api-server.stderr.log")
        $ready = $false
        for ($attempt = 0; $attempt -lt 90; $attempt++) {
            if ($apiProcess.HasExited) { throw "API exited during startup; inspect api-server logs." }
            try {
                $response = Invoke-WebRequest -Uri "$($BaseUrl.TrimEnd('/'))/health/ready" -TimeoutSec 3 -UseBasicParsing
                if ($response.StatusCode -eq 200) { $ready = $true; break }
            } catch { Start-Sleep -Seconds 1 }
        }
        if (-not $ready) { throw "API readiness timed out; inspect api-server logs." }

        $env:UNIPM_API_BASE_URL = "$($BaseUrl.TrimEnd('/'))/"
        $env:UNIPM_TEST_EMAIL = "gsd@unipm.local"
        $env:UNIPM_TEST_PASSWORD = $testPassword
        $env:UNIPM_SAMPLE_COUNT = "$SampleCount"
        $env:UNIPM_OUTPUT_DIRECTORY = $runDirectory
        & node (Join-Path $PSScriptRoot "measure-api.mjs") *> (Join-Path $runDirectory "measurement-run.log")
        $measurementExit = $LASTEXITCODE
        Get-Content (Join-Path $runDirectory "measurement-run.log")
        if ($measurementExit -ne 0) { throw "Measurement process failed; inspect measurement-run.log." }
    }
    finally {
        Pop-Location
    }
}
finally {
    if ($null -ne $apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
        try { $apiProcess.WaitForExit(10000) } catch { }
    }
    foreach ($name in $envNames) {
        [Environment]::SetEnvironmentVariable($name, $oldEnvironment[$name], "Process")
    }
    Remove-Item Env:UNIPM_API_BASE_URL, Env:UNIPM_TEST_EMAIL, Env:UNIPM_TEST_PASSWORD, Env:UNIPM_SAMPLE_COUNT, Env:UNIPM_OUTPUT_DIRECTORY -ErrorAction SilentlyContinue
}
Write-Host ("Raw measurements: " + $runDirectory)
