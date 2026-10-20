[CmdletBinding()]
param(
    [string]$LanAddress,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$DatabaseName,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ConfirmDatabase
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'UniPmLan.Common.psm1') -Force

$root = Get-UniPmLanRepositoryRoot
$statePath = Get-UniPmLanStatePath
$serverProject = Join-Path $root 'server\server.csproj'
$viteEntry = Join-Path $root 'web\node_modules\vite\bin\vite.js'
$logDirectory = $null
$state = $null
$connectionString = $null
$stateCreatedByInvocation = $false

function Start-UniPmLanChild {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string]$ArgumentList,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][string]$StdoutPath,
        [Parameter(Mandatory = $true)][string]$StderrPath,
        [Parameter(Mandatory = $true)][hashtable]$EnvironmentValues
    )

    $environmentNames = @($EnvironmentValues.Keys)
    $environmentNames += @(
        'DEEPSEEK_API_KEY',
        'OPENAI_API_KEY',
        'UNIPM_DEV_USER_PASSWORD',
        'UNIPM_STAGING_DEMO_PASSWORD',
        'UNIPM_STAGING_ADMIN_PASSWORD',
        'UNIPM_STAGING_GSD_PASSWORD',
        'UNIPM_STAGING_INSPECTOR_PASSWORD',
        'UNIPM_STAGING_SUPERVISOR_PASSWORD',
        'UNIPM_GSD_VALIDATION_SEED_ENABLED',
        'UNIPM_GSD_VALIDATION_TARGET_CONFIRMATION',
        'UNIPM_GSD_VALIDATION_DATABASE_CONFIRMATION',
        'UNIPM_STAGING_DEMO_INITIALIZATION',
        'UNIPM_STAGING_ALLOW_SHARED_PASSWORD',
        'UNIPM_JWT_ISSUER',
        'UNIPM_JWT_AUDIENCE',
        'UNIPM_JWT_SIGNING_KEY',
        'UNIPM_JWT_ACCESS_TOKEN_MINUTES',
        'UNIPM_AUTH_REFRESH_TOKEN_DAYS',
        'UNIPM_WEB_ORIGIN',
        'Jwt__Issuer',
        'Jwt__Audience',
        'Jwt__SigningKey',
        'Jwt__AccessTokenMinutes',
        'AuthSession__WebOrigin',
        'NaturalLanguageAnalytics__V3DeepSeekEnabled',
        'Embeddings__Enabled',
        'PreventiveMaintenanceScheduleGeneration__WorkerEnabled',
        'AllowedHosts',
        'ASPNETCORE_ENVIRONMENT',
        'ASPNETCORE_URLS',
        'ConnectionStrings__DefaultConnection',
        'Logging__LogLevel__Default',
        'Logging__LogLevel__Microsoft.AspNetCore',
        'Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command',
        'VITE_API_BASE_URL'
    )
    $currentNames = @([Environment]::GetEnvironmentVariables('Process').Keys |
        ForEach-Object { [string]$_ } |
        Where-Object {
            $_.StartsWith('AZURE_', [StringComparison]::OrdinalIgnoreCase) -or
            $_.StartsWith('AWS_', [StringComparison]::OrdinalIgnoreCase) -or
            $_.StartsWith('VITE_', [StringComparison]::OrdinalIgnoreCase)
        })
    $environmentNames += $currentNames
    $environmentNames = @($environmentNames | Select-Object -Unique)
    $previousValues = @{}
    foreach ($name in $environmentNames) {
        $previousValues[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }

    try {
        foreach ($name in $environmentNames) {
            [Environment]::SetEnvironmentVariable($name, $null, 'Process')
        }
        foreach ($name in $EnvironmentValues.Keys) {
            [Environment]::SetEnvironmentVariable(
                [string]$name,
                [string]$EnvironmentValues[$name],
                'Process'
            )
        }

        $startParameters = @{
            FilePath = $FilePath
            ArgumentList = $ArgumentList
            WorkingDirectory = $WorkingDirectory
            WindowStyle = 'Hidden'
            RedirectStandardOutput = $StdoutPath
            RedirectStandardError = $StderrPath
            PassThru = $true
        }
        $process = Start-Process @startParameters
    }
    finally {
        foreach ($name in $environmentNames) {
            [Environment]::SetEnvironmentVariable($name, $previousValues[$name], 'Process')
        }
    }

    $deadline = (Get-Date).AddSeconds(5)
    do {
        $identity = Get-UniPmLanProcessIdentity -ProcessId $process.Id
        if ($null -ne $identity) {
            return $identity
        }
        Start-Sleep -Milliseconds 100
    } while ((Get-Date) -lt $deadline)

    try {
        if (-not $process.HasExited) {
            $process.Kill()
            $process.WaitForExit(5000) | Out-Null
        }
    }
    catch {
        # The Process handle came directly from this invocation's Start-Process call.
    }
    throw 'A LAN child process did not retain a verifiable process identity.'
}

function Wait-UniPmLanEndpoint {
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [Parameter(Mandatory = $true)][string]$Label,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds,
        [Parameter()][string]$LogPath,
        [Parameter()][object]$Identity
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        if ($null -ne $Identity) {
            $current = Get-UniPmLanProcessIdentity -ProcessId ([int]$Identity.ProcessId)
            if ($null -eq $current -or -not (Test-UniPmLanIdentityMatch -Actual $current -Expected $Identity)) {
                throw "$Label exited before its health check passed. Review the local log file: $LogPath"
            }
        }
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 3
            if ([int]$response.StatusCode -eq 200) {
                return $response
            }
        }
        catch {
            # Retry while the local process starts; do not print client details or configuration values.
        }
        Start-Sleep -Milliseconds 750
    } while ((Get-Date) -lt $deadline)

    throw "$Label did not become ready at $Url. Review the local log file: $LogPath"
}

try {
    if (Test-Path -LiteralPath $statePath -PathType Leaf) {
        throw 'An active-session file already exists. Run Stop-UniPmLan.ps1 and inspect stale state before starting another session.'
    }
    if (-not (Test-Path -LiteralPath $serverProject -PathType Leaf)) {
        throw 'The API project file was not found.'
    }
    if (-not (Test-Path -LiteralPath $viteEntry -PathType Leaf)) {
        throw 'Vite is not installed in web/node_modules. Install the locked web dependencies before starting the LAN host.'
    }

    $dotnet = Get-Command dotnet -ErrorAction Stop
    $node = Get-Command node -ErrorAction Stop
    $operatorIdentity = Get-UniPmLanProcessIdentity -ProcessId $PID
    if ($null -eq $operatorIdentity) {
        throw 'Windows process identity inspection is unavailable. No child processes were started.'
    }
    $sdkList = @(& $dotnet.Source --list-sdks 2>$null)
    if ($LASTEXITCODE -ne 0 -or -not ($sdkList | Where-Object { ([string]$_) -match '^10\.' })) {
        throw 'The .NET 10 SDK is required to start the local API.'
    }
    $nodeVersion = [string](& $node.Source --version 2>$null)
    if ($LASTEXITCODE -ne 0 -or $nodeVersion -notmatch '^v(?<major>\d+)\.') {
        throw 'A working Node.js installation is required to start the web frontend.'
    }
    $nodeMajor = [int]$Matches['major']
    if ($nodeMajor -lt 22 -or $nodeMajor -ge 23) {
        throw 'Node.js 22.x is required by the web package.'
    }

    $network = Get-UniPmLanPrivateIPv4 -LanAddress $LanAddress
    $LanAddress = [string]$network.Address
    $apiUrl = "http://$($LanAddress):5254"
    $webUrl = "http://$($LanAddress):5173"
    $webOrigin = $webUrl

    $connectionString = Get-UniPmLanConnectionString -RepositoryRoot $root
    Assert-UniPmLanLocalDatabase -ConnectionString $connectionString -DatabaseName $DatabaseName -ConfirmDatabase $ConfirmDatabase
    Test-UniPmLanDatabaseConnection -ConnectionString $connectionString

    $sessionId = [Guid]::NewGuid().ToString('N')
    $logDirectory = Join-Path $root "artifacts\lan\$sessionId"
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    $apiStdout = Join-Path $logDirectory 'api.stdout.log'
    $apiStderr = Join-Path $logDirectory 'api.stderr.log'
    $webStdout = Join-Path $logDirectory 'web.stdout.log'
    $webStderr = Join-Path $logDirectory 'web.stderr.log'

    $state = [ordered]@{
        SchemaVersion = 1
        SessionId = $sessionId
        StartedUtc = [DateTime]::UtcNow.ToString('o')
        RepositoryRoot = $root
        DatabaseName = $DatabaseName
        LanAddress = $LanAddress
        LanSubnet = [string]$network.Subnet
        ApiPort = 5254
        WebPort = 5173
        ApiUrl = $apiUrl
        WebUrl = $webUrl
        ViteApiBaseUrl = $apiUrl
        LogDirectory = $logDirectory
        ApiRoot = $null
        WebRoot = $null
        FirewallRules = $null
    }
    Save-UniPmLanState -State $state
    $stateCreatedByInvocation = $true

    $apiEnvironment = @{
        ConnectionStrings__DefaultConnection = $connectionString
        ASPNETCORE_ENVIRONMENT = 'Development'
        ASPNETCORE_URLS = $apiUrl
        AllowedHosts = "$LanAddress;localhost;127.0.0.1"
        UNIPM_WEB_ORIGIN = $webOrigin
        AuthSession__WebOrigin = $webOrigin
        NaturalLanguageAnalytics__V3DeepSeekEnabled = 'false'
        Embeddings__Enabled = 'false'
        Logging__LogLevel__Default = 'Warning'
        'Logging__LogLevel__Microsoft.AspNetCore' = 'Warning'
        'Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command' = 'Warning'
    }
    $apiArguments = 'run --project "' + $serverProject + '" --no-launch-profile'
    $state.ApiRoot = Start-UniPmLanChild -FilePath $dotnet.Source -ArgumentList $apiArguments -WorkingDirectory $root -StdoutPath $apiStdout -StderrPath $apiStderr -EnvironmentValues $apiEnvironment
    Save-UniPmLanState -State $state

    $nodeEnvironment = @{ VITE_API_BASE_URL = $apiUrl }
    $viteArguments = '"' + $viteEntry + '" --host ' + $LanAddress + ' --port 5173 --strictPort'
    $state.WebRoot = Start-UniPmLanChild -FilePath $node.Source -ArgumentList $viteArguments -WorkingDirectory (Join-Path $root 'web') -StdoutPath $webStdout -StderrPath $webStderr -EnvironmentValues $nodeEnvironment
    Save-UniPmLanState -State $state

    $null = Wait-UniPmLanEndpoint -Url "$apiUrl/health/live" -Label 'API liveness' -TimeoutSeconds 45 -LogPath $apiStderr -Identity $state.ApiRoot
    $null = Wait-UniPmLanEndpoint -Url "$apiUrl/health/ready" -Label 'API readiness' -TimeoutSeconds 60 -LogPath $apiStderr -Identity $state.ApiRoot
    $null = Wait-UniPmLanEndpoint -Url "$webUrl/" -Label 'Vite frontend' -TimeoutSeconds 45 -LogPath $webStderr -Identity $state.WebRoot
    $envModule = Wait-UniPmLanEndpoint -Url "$webUrl/src/config/env.ts" -Label 'Vite API configuration' -TimeoutSeconds 15 -LogPath $webStderr -Identity $state.WebRoot
    if (-not ([string]$envModule.Content).Contains($apiUrl)) {
        throw 'The Vite-served environment module does not contain the selected LAN API URL.'
    }

    Write-Output 'UniPM LAN services are ready.'
    Write-Output "Web: $webUrl"
    Write-Output "API: $apiUrl"
    Write-Output "Database: $DatabaseName (local SQL only)"
    Write-Output 'DeepSeek and embedding providers are disabled; offline NLA uses the deterministic parser.'
    Write-Output "Local logs: $logDirectory"
    Write-Output 'Use Test-UniPmLan.ps1 for a readiness check and Stop-UniPmLan.ps1 when finished.'
}
catch {
    $originalMessage = $_.Exception.Message
    $cleanupFailed = $false
    if ($null -ne $state) {
        foreach ($identity in @($state.ApiRoot, $state.WebRoot)) {
            if ($null -eq $identity) { continue }
            try {
                $null = Stop-UniPmLanProcessTree -Identity $identity
            }
            catch {
                $cleanupFailed = $true
            }
        }
    }
    if (-not $cleanupFailed -and $stateCreatedByInvocation -and
        (Test-Path -LiteralPath $statePath -PathType Leaf)) {
        try {
            $remainingState = Get-UniPmLanState
            if ([string]::Equals(
                [string]$remainingState.SessionId,
                [string]$state.SessionId,
                [StringComparison]::Ordinal
            )) {
                Remove-Item -LiteralPath $statePath -Force
            }
            else {
                $cleanupFailed = $true
            }
        }
        catch {
            $cleanupFailed = $true
        }
    }
    if ($cleanupFailed) {
        throw "$originalMessage Cleanup could not verify every owned process; inspect $statePath before retrying."
    }
    throw $originalMessage
}
finally {
    $connectionString = $null
}
