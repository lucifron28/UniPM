[CmdletBinding()]
param(
    [string]$DatabaseName = "UniPM_Nonfunctional_2026_11_12",
    [string]$BaseUrl = "http://127.0.0.1:5254",
    [string]$ZapHome = "",
    [string]$JavaHome = "C:\Program Files\Android\Android Studio\jbr",
    [string]$ReportTag = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
$artifactDirectory = Join-Path $repoRoot "artifacts\nonfunctional-2026-11\zap"
$planPath = Join-Path $PSScriptRoot "zap-passive-plan.yaml"
if ([string]::IsNullOrWhiteSpace($ReportTag)) { $ReportTag = Get-Date -Format "yyyy-MM-dd-HHmmss" }
if ($ReportTag -notmatch "^[A-Za-z0-9][A-Za-z0-9._-]*$") { throw "ReportTag may contain only letters, digits, dot, underscore, and hyphen." }
if (-not $ZapHome) { $ZapHome = Join-Path $repoRoot "artifacts\nonfunctional-2026-11\zap-install\ZAP_2.17.0" }
$ZapHome = (Resolve-Path -LiteralPath $ZapHome).Path
$zapCommand = Join-Path $ZapHome "zap.bat"
if (-not (Test-Path -LiteralPath $zapCommand)) { throw "ZAP not found at $zapCommand. Extract the verified official release first." }
if (-not (Test-Path -LiteralPath (Join-Path $JavaHome "bin\java.exe"))) {
    if (-not $env:JAVA_HOME -or -not (Test-Path -LiteralPath (Join-Path $env:JAVA_HOME "bin\java.exe"))) {
        throw "Java 17 or newer is required. Set JAVA_HOME or pass -JavaHome."
    }
    $JavaHome = $env:JAVA_HOME
}

$envNames = @(
    "JAVA_HOME", "PATH", "ASPNETCORE_ENVIRONMENT", "DOTNET_ENVIRONMENT", "ASPNETCORE_URLS",
    "ConnectionStrings__DefaultConnection", "UNIPM_JWT_ISSUER", "UNIPM_JWT_AUDIENCE",
    "UNIPM_JWT_SIGNING_KEY", "UNIPM_WEB_ORIGIN", "PreventiveMaintenanceScheduleGeneration__WorkerEnabled"
)
$oldEnvironment = @{}
foreach ($name in $envNames) { $oldEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, "Process") }
$apiProcess = $null
Push-Location $repoRoot
try {
    $env:JAVA_HOME = $JavaHome
    $env:PATH = (Join-Path $JavaHome "bin") + [IO.Path]::PathSeparator + $env:PATH
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:DOTNET_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = $BaseUrl
    $env:ConnectionStrings__DefaultConnection = "Server=lpc:localhost;Database=$DatabaseName;Integrated Security=True;Encrypt=False;TrustServerCertificate=True"
    $env:UNIPM_JWT_ISSUER = "UniPM-local-zap"
    $env:UNIPM_JWT_AUDIENCE = "UniPM-local-zap"
    $env:UNIPM_JWT_SIGNING_KEY = [Convert]::ToBase64String([guid]::NewGuid().ToByteArray()) + "passive-baseline-local-key"
    $env:UNIPM_WEB_ORIGIN = "http://localhost:5173"
    $env:PreventiveMaintenanceScheduleGeneration__WorkerEnabled = "false"

    $port = ([Uri]$BaseUrl).Port
    $alreadyListening = Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue
    if ($alreadyListening) { throw "Port $port is occupied; refusing to stop an unknown process." }
    $serverLog = Join-Path $artifactDirectory "api-server.stdout.log"
    $serverErrorLog = Join-Path $artifactDirectory "api-server.stderr.log"
    $zapLog = Join-Path $artifactDirectory "zap-automation.log"
    New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
    $apiProcess = Start-Process -FilePath "dotnet" -ArgumentList @("run", "--project", "server\server.csproj", "--configuration", "Release", "--no-build") -WorkingDirectory $repoRoot -PassThru -RedirectStandardOutput $serverLog -RedirectStandardError $serverErrorLog

    $ready = $false
    for ($attempt = 0; $attempt -lt 90; $attempt++) {
        if ($apiProcess.HasExited) { throw "API exited during startup; inspect local API server logs." }
        try {
            $response = Invoke-WebRequest -Uri "$($BaseUrl.TrimEnd('/'))/health/ready" -TimeoutSec 3 -UseBasicParsing
            if ($response.StatusCode -eq 200) { $ready = $true; break }
        } catch { Start-Sleep -Seconds 1 }
    }
    if (-not $ready) { throw "API readiness timed out; inspect local API server logs." }

    Push-Location $ZapHome
    try {
        & ".\zap.bat" -cmd -dir (Join-Path $artifactDirectory "zap-data") -autorun $planPath *> $zapLog
        $zapExit = $LASTEXITCODE
    }
    finally { Pop-Location }
    Get-Content $zapLog
    if ($zapExit -notin @(0, 2)) { throw "ZAP Automation Framework exited with code $zapExit." }
    foreach ($extension in @("html", "json")) {
        $report = Get-ChildItem -LiteralPath $artifactDirectory -Filter ("unipm-passive-baseline-*." + $extension) -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if (-not $report) { throw "ZAP did not generate the expected $extension report." }
        $reportName = "zap-report-$ReportTag.$extension"
        $destination = Join-Path $PSScriptRoot $reportName
        if (Test-Path -LiteralPath $destination) { throw "Refusing to overwrite existing report: $destination" }
        Copy-Item -LiteralPath $report.FullName -Destination $destination
    }
}
finally {
    if ($null -ne $apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
        try { $apiProcess.WaitForExit(10000) } catch { }
    }
    Pop-Location
    foreach ($name in $envNames) { [Environment]::SetEnvironmentVariable($name, $oldEnvironment[$name], "Process") }
}
Write-Host ("ZAP reports and logs: " + $artifactDirectory)
