[CmdletBinding()]
param(
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$ExpectedCommitSha
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$testedCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $testedCommit -notmatch '^[0-9a-f]{40}$') {
    throw 'Could not determine the repository HEAD commit.'
}
if ($ExpectedCommitSha -and $testedCommit -ne $ExpectedCommitSha.ToLowerInvariant()) {
    throw 'Repository HEAD does not match ExpectedCommitSha.'
}
$sourceBranch = (& git -C $repoRoot rev-parse --abbrev-ref HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw 'Could not determine the source branch.'
}
if (@(& git -C $repoRoot status --porcelain --untracked-files=all).Count -gt 0) {
    throw 'The HTTP smoke requires a clean worktree at the tested commit.'
}

$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + $testedCommit.Substring(0, 7)
$artifactDirectory = Join-Path $repoRoot (Join-Path 'artifacts/rag-retirement' $runId)
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null

$serverDirectory = Join-Path $repoRoot 'server'
$apiDll = Join-Path $serverDirectory 'bin/Release/net10.0/UniPM.Api.dll'
$curlCommand = Get-Command 'curl.exe' -CommandType Application -ErrorAction SilentlyContinue
$sqlcmdCommand = Get-Command 'sqlcmd.exe' -CommandType Application -ErrorAction SilentlyContinue
$dotnetCommand = Get-Command 'dotnet.exe' -CommandType Application -ErrorAction SilentlyContinue
if (-not $curlCommand) { throw 'curl.exe was not found on PATH.' }
if (-not $sqlcmdCommand) { throw 'sqlcmd.exe was not found on PATH.' }
if (-not $dotnetCommand) { throw 'dotnet.exe was not found on PATH.' }
if (-not (Test-Path -LiteralPath $apiDll -PathType Leaf)) {
    throw 'The Release API assembly was not found at server/bin/Release/net10.0/UniPM.Api.dll.'
}

$script:CurlPath = $curlCommand.Source
$script:SqlcmdPath = $sqlcmdCommand.Source
$script:DotnetPath = $dotnetCommand.Source
$script:ArtifactDirectory = $artifactDirectory
$script:RepoRoot = $repoRoot
$script:DatabaseName = 'UniPMRuntime_' + [Guid]::NewGuid().ToString('N')
if ($script:DatabaseName -notmatch '^UniPMRuntime_[0-9a-f]{32}$') {
    throw 'Generated runtime database name failed the ownership check.'
}

$smokeResultPath = Join-Path $artifactDirectory 'http-smoke.json'
$cleanupResultPath = Join-Path $artifactDirectory 'cleanup.json'
$apiStdoutPath = Join-Path $artifactDirectory 'api.stdout.log'
$apiStderrPath = Join-Path $artifactDirectory 'api.stderr.log'
$migrationLogPath = Join-Path $artifactDirectory 'migration.log'
$bodyTempPaths = [System.Collections.Generic.List[string]]::new()
$environmentNames = @(
    'ASPNETCORE_ENVIRONMENT',
    'ASPNETCORE_URLS',
    'DOTNET_ENVIRONMENT',
    'ConnectionStrings__DefaultConnection',
    'Embeddings__Enabled',
    'Embeddings__ProviderKey',
    'Embeddings__BaseAddress',
    'Embeddings__Path',
    'Embeddings__Model',
    'Embeddings__ApiKey',
    'Embeddings__AllowRemoteProvider',
    'MaintenanceReview__Enabled',
    'Summary__Enabled',
    'Summary__ProviderKey',
    'Summary__BaseAddress',
    'Summary__ApiKey',
    'Jwt__Issuer',
    'Jwt__Audience',
    'Jwt__SigningKey',
    'Jwt__AccessTokenMinutes',
    'UNIPM_JWT_ISSUER',
    'UNIPM_JWT_AUDIENCE',
    'UNIPM_JWT_SIGNING_KEY',
    'UNIPM_JWT_ACCESS_TOKEN_MINUTES',
    'UNIPM_AUTH_REFRESH_TOKEN_DAYS',
    'UNIPM_WEB_ORIGIN'
)
$previousEnvironment = @{}
foreach ($name in $environmentNames) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

$databaseCreateAttempted = $false
$databaseCreatedByHarness = $false
$apiProcess = $null
$apiProcessStartTimeUtc = $null
$listener = $null
$databaseName = $script:DatabaseName
$smokeResultWritten = $false
$smokeExitCode = 0

$smokeResult = [ordered]@{
    testedCommit = $testedCommit
    sourceBranch = $sourceBranch
    environment = 'Development'
    databaseEngineMajorVersion = $null
    databaseCompatibilityLevel = $null
    fullTextSearchInstalled = $false
    noAi = [ordered]@{
        embeddingsEnabled = $false
        providerCredentialsSet = $false
        legacyMaintenanceReviewFlagSet = $true
        legacySummaryFlagSet = $true
        legacyFlagsAreProbeOnly = $true
    }
    responses = [ordered]@{
        apiRoot = [ordered]@{ path = '/'; statusCode = $null; name = $null; status = $null }
        liveHealth = [ordered]@{ path = '/health/live'; statusCode = $null; body = $null }
        readyHealth = [ordered]@{ path = '/health/ready'; statusCode = $null; body = $null }
        maintenanceReviewGet = [ordered]@{ path = '/api/v1/maintenance-review'; statusCode = $null }
        maintenanceReviewPost = [ordered]@{ path = '/api/v1/maintenance-review'; statusCode = $null }
        openApi = [ordered]@{
            path = '/openapi/v1.json'
            statusCode = $null
            maintenanceReviewPathCount = $null
            maintenanceReviewOperationCount = $null
        }
        schedules = [ordered]@{
            path = '/api/v1/schedules/'
            statusCode = $null
            bodyIsEmptyJsonArray = $false
            itemCount = $null
        }
    }
    success = $false
    failure = $null
}

$cleanupResult = [ordered]@{
    testedCommit = $testedCommit
    ownedDatabaseName = $databaseName
    databaseCreateAttempted = $false
    databaseCreatedByHarness = $false
    databaseDropExitCode = $null
    remainingOwnedDatabases = $null
    apiProcessId = $null
    apiProcessStartTimeUtc = $null
    processStopRequested = $false
    processIdentityVerifiedBeforeStop = $false
    processStopped = $true
    cleanupConfirmed = $false
    cleanupFailure = $null
}

function Write-JsonFile {
    param([string]$Path, $Value)
    $json = ConvertTo-Json -InputObject $Value -Depth 8
    [IO.File]::WriteAllText($Path, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
}

function Get-SafeFailure {
    param($ErrorRecord)
    $message = [string]$ErrorRecord.Exception.Message
    $message = $message -replace 'https?://[^\s''"]+', '[url]'
    $message = $message -replace '(?i)\b(?:Server|Data Source)\s*=\s*[^;\r\n]*(?:;[^\r\n]*)?', '[connection details]'
    $message = $message -replace '(?i)\b(?:ApiKey|SigningKey|Password|Pwd|Authorization)\s*[:=]\s*[^;\s,]+', '[credential]'
    return $ErrorRecord.Exception.GetType().Name + ': ' + $message.Trim()
}

function Invoke-Sql {
    param([string]$Query)
    $sqlOutput = @(& $script:SqlcmdPath -S . -E -C -I -d master -b -h -1 -W -Q $Query 2>&1)
    return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = $sqlOutput }
}

function Get-SqlInt {
    param([string]$Query)
    $response = Invoke-Sql -Query $Query
    if ($response.ExitCode -ne 0) { throw 'SQL metadata query failed.' }
    $values = @(
        $response.Output |
            ForEach-Object { ([string]$_).Trim() } |
            Where-Object { $_ -match '^\d+$' }
    )
    if ($values.Count -ne 1) { throw 'SQL metadata query returned an unexpected result.' }
    return [int]$values[0]
}

function Invoke-Http {
    param([string]$Method, [string]$Path, [string]$JsonBody)
    $bodyPath = Join-Path $script:ArtifactDirectory ([Guid]::NewGuid().ToString('N') + '.body')
    $script:BodyTempPaths.Add($bodyPath)
    $curlArgs = @(
        '--silent', '--show-error', '--output', $bodyPath,
        '--write-out', '%{http_code}', '--max-time', '8',
        '--request', $Method, '--header', 'Accept: application/json, text/plain'
    )
    if ($PSBoundParameters.ContainsKey('JsonBody')) {
        $curlArgs += @('--header', 'Content-Type: application/json', '--data-binary', $JsonBody)
    }
    $curlArgs += $script:BaseUrl + $Path
    $curlOutput = @(& $script:CurlPath @curlArgs 2>&1)
    $curlExitCode = $LASTEXITCODE
    $statusLines = @($curlOutput | ForEach-Object { [string]$_ } | Where-Object { $_ -match '^\d{3}$' })
    if ($curlExitCode -ne 0 -or $statusLines.Count -ne 1) {
        throw "HTTP request failed for $Method $Path (curl exit $curlExitCode)."
    }
    $body = if (Test-Path -LiteralPath $bodyPath -PathType Leaf) { [IO.File]::ReadAllText($bodyPath) } else { '' }
    return [pscustomobject]@{ StatusCode = [int]$statusLines[0]; Body = $body }
}

function Get-OwnedProcessState {
    param([int]$ProcessId, [DateTime]$StartTimeUtc)
    $current = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if (-not $current) { return 'gone' }
    try { $started = $current.StartTime.ToUniversalTime() } catch { return 'unverified' }
    if ($current.ProcessName -ne 'dotnet' -or $started -ne $StartTimeUtc) { return 'different-process' }
    return 'owned-running'
}

function Sanitize-Log {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return }
    $content = [IO.File]::ReadAllText($Path)
    $content = $content -replace [regex]::Escape($script:RepoRoot), '[workspace]'
    $content = $content -replace 'https?://127\.0\.0\.1:\d+', '[loopback]'
    $content = $content -replace '(?im)^\s*.*(?:Server|Data Source)\s*=\s*.*$', '[connection details redacted]'
    $content = $content -replace '(?i)\b(ApiKey|SigningKey|Password|Pwd|Authorization)\s*[:=]\s*[^;\s,]+', '$1=[redacted]'
    [IO.File]::WriteAllText($Path, $content, [Text.UTF8Encoding]::new($false))
}

try {
    $databaseCountQuery = "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name=N'$databaseName';"
    $majorVersion = Get-SqlInt -Query "SET NOCOUNT ON; SELECT CONVERT(int, SERVERPROPERTY('ProductMajorVersion'));"
    $smokeResult.databaseEngineMajorVersion = $majorVersion
    if ($majorVersion -ne 15) { throw 'The selected local SQL Server instance is not SQL Server 2019.' }
    $fullTextInstalled = Get-SqlInt -Query "SET NOCOUNT ON; SELECT CONVERT(int, FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'));"
    $smokeResult.fullTextSearchInstalled = ($fullTextInstalled -eq 1)
    if ($fullTextInstalled -ne 1) { throw 'Full-Text Search is not installed on the selected SQL Server instance.' }
    if ((Get-SqlInt -Query $databaseCountQuery) -ne 0) {
        throw 'The generated runtime database name already exists.'
    }

    $databaseCreateAttempted = $true
    $cleanupResult.databaseCreateAttempted = $true
    $createResult = Invoke-Sql -Query "CREATE DATABASE [$databaseName];"
    if ($createResult.ExitCode -ne 0) { throw 'Owned runtime database creation failed.' }
    $databaseCreatedByHarness = $true
    $cleanupResult.databaseCreatedByHarness = $true
    if ((Get-SqlInt -Query $databaseCountQuery) -ne 1) {
        throw 'Owned runtime database creation could not be confirmed.'
    }

    $compatibilityResult = Invoke-Sql -Query "ALTER DATABASE [$databaseName] SET COMPATIBILITY_LEVEL = 150;"
    if ($compatibilityResult.ExitCode -ne 0) { throw 'Owned database compatibility setup failed.' }
    $compatibilityLevel = Get-SqlInt -Query "SET NOCOUNT ON; SELECT compatibility_level FROM sys.databases WHERE name=N'$databaseName';"
    $smokeResult.databaseCompatibilityLevel = $compatibilityLevel
    if ($compatibilityLevel -ne 150) { throw 'Owned runtime database compatibility level is not 150.' }

    $connectionString = "Server=.;Database=$databaseName;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;"
    [Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Development', 'Process')
    [Environment]::SetEnvironmentVariable('DOTNET_ENVIRONMENT', 'Development', 'Process')
    [Environment]::SetEnvironmentVariable('ConnectionStrings__DefaultConnection', $connectionString, 'Process')
    [Environment]::SetEnvironmentVariable('Embeddings__Enabled', 'false', 'Process')
    [Environment]::SetEnvironmentVariable('Embeddings__ProviderKey', '', 'Process')
    [Environment]::SetEnvironmentVariable('Embeddings__BaseAddress', '', 'Process')
    [Environment]::SetEnvironmentVariable('Embeddings__Path', '', 'Process')
    [Environment]::SetEnvironmentVariable('Embeddings__Model', '', 'Process')
    [Environment]::SetEnvironmentVariable('Embeddings__ApiKey', '', 'Process')
    [Environment]::SetEnvironmentVariable('Embeddings__AllowRemoteProvider', 'false', 'Process')
    [Environment]::SetEnvironmentVariable('MaintenanceReview__Enabled', 'true', 'Process')
    [Environment]::SetEnvironmentVariable('Summary__Enabled', 'true', 'Process')
    [Environment]::SetEnvironmentVariable('Summary__ProviderKey', '', 'Process')
    [Environment]::SetEnvironmentVariable('Summary__BaseAddress', '', 'Process')
    [Environment]::SetEnvironmentVariable('Summary__ApiKey', '', 'Process')
    foreach ($name in @(
        'ASPNETCORE_URLS', 'Jwt__Issuer', 'Jwt__Audience', 'Jwt__SigningKey',
        'Jwt__AccessTokenMinutes', 'UNIPM_JWT_ISSUER', 'UNIPM_JWT_AUDIENCE',
        'UNIPM_JWT_SIGNING_KEY', 'UNIPM_JWT_ACCESS_TOKEN_MINUTES',
        'UNIPM_AUTH_REFRESH_TOKEN_DAYS', 'UNIPM_WEB_ORIGIN'
    )) {
        [Environment]::SetEnvironmentVariable($name, '', 'Process')
    }
    $credentialNames = @(
        'Embeddings__ProviderKey', 'Embeddings__BaseAddress', 'Embeddings__Model', 'Embeddings__ApiKey',
        'Summary__ProviderKey', 'Summary__BaseAddress', 'Summary__ApiKey'
    )
    $providerCredentialsSet = @(
        $credentialNames | Where-Object {
            -not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_, 'Process'))
        }
    ).Count -gt 0
    $smokeResult.noAi.providerCredentialsSet = $providerCredentialsSet
    if ($providerCredentialsSet -or [Environment]::GetEnvironmentVariable('Embeddings__Enabled', 'Process') -ne 'false') {
        throw 'No-AI process configuration was not applied.'
    }
    $legacyFlagsSet = (
        [Environment]::GetEnvironmentVariable('MaintenanceReview__Enabled', 'Process') -eq 'true' -and
        [Environment]::GetEnvironmentVariable('Summary__Enabled', 'Process') -eq 'true'
    )
    $smokeResult.noAi.legacyMaintenanceReviewFlagSet = $legacyFlagsSet
    $smokeResult.noAi.legacySummaryFlagSet = $legacyFlagsSet
    if (-not $legacyFlagsSet) { throw 'Retired configuration probe flags were not applied.' }

    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = ([Net.IPEndPoint]$listener.LocalEndpoint).Port
    $listener.Stop()
    $listener = $null
    $script:BaseUrl = 'http://127.0.0.1:' + $port
    [Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', $script:BaseUrl, 'Process')

    Push-Location $serverDirectory
    try {
        $migrationOutput = @(& $script:DotnetPath $apiDll '--migrate-database' 2>&1)
        $migrationExitCode = $LASTEXITCODE
        $migrationOutput | Set-Content -LiteralPath $migrationLogPath -Encoding UTF8
    }
    finally {
        Pop-Location
    }
    if ($migrationExitCode -ne 0) { throw 'Database migrations failed.' }

    $apiProcess = Start-Process -FilePath $script:DotnetPath -ArgumentList @('bin/Release/net10.0/UniPM.Api.dll') -WorkingDirectory $serverDirectory -PassThru -WindowStyle Hidden -RedirectStandardOutput $apiStdoutPath -RedirectStandardError $apiStderrPath
    $apiProcessStartTimeUtc = $apiProcess.StartTime.ToUniversalTime()
    $cleanupResult.apiProcessId = $apiProcess.Id
    $cleanupResult.apiProcessStartTimeUtc = $apiProcessStartTimeUtc.ToString('o')

    $rootResponse = $null
    $startupDeadline = [DateTime]::UtcNow.AddSeconds(30)
    while ([DateTime]::UtcNow -lt $startupDeadline -and -not $rootResponse) {
        $apiProcess.Refresh()
        if ($apiProcess.HasExited) { throw 'API process exited before serving its root route.' }
        try {
            $candidate = Invoke-Http -Method 'GET' -Path '/'
            if ($candidate.StatusCode -eq 200) { $rootResponse = $candidate }
        }
        catch {
            Start-Sleep -Milliseconds 300
        }
    }
    if (-not $rootResponse) { throw 'API root did not become available within 30 seconds.' }
    $root = ConvertFrom-Json -InputObject $rootResponse.Body -ErrorAction Stop
    $smokeResult.responses.apiRoot.statusCode = $rootResponse.StatusCode
    $smokeResult.responses.apiRoot.name = [string]$root.name
    $smokeResult.responses.apiRoot.status = [string]$root.status

    $live = Invoke-Http -Method 'GET' -Path '/health/live'
    $ready = Invoke-Http -Method 'GET' -Path '/health/ready'
    $reviewGet = Invoke-Http -Method 'GET' -Path '/api/v1/maintenance-review'
    $reviewPost = Invoke-Http -Method 'POST' -Path '/api/v1/maintenance-review' -JsonBody '{}'
    $openApiResponse = Invoke-Http -Method 'GET' -Path '/openapi/v1.json'
    $schedules = Invoke-Http -Method 'GET' -Path '/api/v1/schedules/'

    $smokeResult.responses.liveHealth.statusCode = $live.StatusCode
    $smokeResult.responses.liveHealth.body = $live.Body.Trim()
    $smokeResult.responses.readyHealth.statusCode = $ready.StatusCode
    $smokeResult.responses.readyHealth.body = $ready.Body.Trim()
    $smokeResult.responses.maintenanceReviewGet.statusCode = $reviewGet.StatusCode
    $smokeResult.responses.maintenanceReviewPost.statusCode = $reviewPost.StatusCode

    $openApi = ConvertFrom-Json -InputObject $openApiResponse.Body -ErrorAction Stop
    if ($null -eq $openApi.paths) { throw 'OpenAPI document did not contain a paths object.' }
    $reviewPaths = @($openApi.paths.PSObject.Properties | Where-Object { $_.Name -match '(?i)maintenance-review' })
    $operationNames = @('get', 'put', 'post', 'delete', 'options', 'head', 'patch', 'trace')
    $reviewOperationCount = 0
    foreach ($reviewPath in $reviewPaths) {
        foreach ($property in $reviewPath.Value.PSObject.Properties) {
            if ($operationNames -contains $property.Name.ToLowerInvariant()) { $reviewOperationCount++ }
        }
    }
    $smokeResult.responses.openApi.statusCode = $openApiResponse.StatusCode
    $smokeResult.responses.openApi.maintenanceReviewPathCount = $reviewPaths.Count
    $smokeResult.responses.openApi.maintenanceReviewOperationCount = $reviewOperationCount

    $emptyJsonArray = $schedules.Body.Trim() -match '^\[\s*\]$'
    $smokeResult.responses.schedules.statusCode = $schedules.StatusCode
    $smokeResult.responses.schedules.bodyIsEmptyJsonArray = $emptyJsonArray
    if ($emptyJsonArray) { $smokeResult.responses.schedules.itemCount = 0 }

    $smokeResult.success = (
        $rootResponse.StatusCode -eq 200 -and $root.name -eq 'UniPM API' -and $root.status -eq 'running' -and
        $live.StatusCode -eq 200 -and $live.Body.Trim() -eq 'Healthy' -and
        $ready.StatusCode -eq 200 -and $ready.Body.Trim() -eq 'Healthy' -and
        $reviewGet.StatusCode -eq 404 -and $reviewPost.StatusCode -eq 404 -and
        $openApiResponse.StatusCode -eq 200 -and $reviewPaths.Count -eq 0 -and $reviewOperationCount -eq 0 -and
        $schedules.StatusCode -eq 200 -and $emptyJsonArray -and
        $smokeResult.databaseCompatibilityLevel -eq 150 -and $fullTextInstalled -eq 1 -and
        -not $providerCredentialsSet -and $legacyFlagsSet
    )
    if (-not $smokeResult.success) {
        $smokeExitCode = 1
        $smokeResult.failure = 'One or more no-AI HTTP smoke assertions failed.'
    }
    Write-JsonFile -Path $smokeResultPath -Value $smokeResult
    $smokeResultWritten = $true
}
catch {
    $smokeExitCode = 1
    $smokeResult.success = $false
    $smokeResult.failure = Get-SafeFailure -ErrorRecord $_
    if (-not $smokeResultWritten) {
        try {
            Write-JsonFile -Path $smokeResultPath -Value $smokeResult
            $smokeResultWritten = $true
        }
        catch {
            $smokeResult.failure = 'Smoke result could not be written before cleanup.'
        }
    }
}
finally {
    if (-not $smokeResultWritten) {
        $smokeResult.success = $false
        if (-not $smokeResult.failure) { $smokeResult.failure = 'Smoke result was incomplete before cleanup.' }
        try { Write-JsonFile -Path $smokeResultPath -Value $smokeResult; $smokeResultWritten = $true }
        catch { $cleanupResult.cleanupFailure = 'Smoke result could not be written before cleanup.' }
    }
    if ($listener) { $listener.Stop() }

    if ($apiProcess) {
        $processState = Get-OwnedProcessState -ProcessId $apiProcess.Id -StartTimeUtc $apiProcessStartTimeUtc
        if ($processState -eq 'owned-running') {
            $cleanupResult.processStopRequested = $true
            $cleanupResult.processIdentityVerifiedBeforeStop = $true
            try {
                Stop-Process -Id $apiProcess.Id -Force -ErrorAction Stop
                $apiProcess.WaitForExit(10000) | Out-Null
                $cleanupResult.processStopped = (
                    (Get-OwnedProcessState -ProcessId $apiProcess.Id -StartTimeUtc $apiProcessStartTimeUtc) -ne 'owned-running'
                )
            }
            catch {
                $cleanupResult.processStopped = $false
            }
        }
        else {
            $cleanupResult.processStopped = ($processState -eq 'gone' -or $processState -eq 'different-process')
        }
        $apiProcess.Dispose()
    }

    $databaseCleanupConfirmed = -not $databaseCreateAttempted
    if ($databaseCreateAttempted -and -not $cleanupResult.processStopped) {
        $cleanupResult.cleanupFailure = 'The owned API process could not be confirmed stopped; the database was left untouched.'
        $databaseCleanupConfirmed = $false
    }
    elseif ($databaseCreateAttempted) {
        try {
            $countBeforeDrop = Get-SqlInt -Query "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name=N'$databaseName';"
            if ($countBeforeDrop -eq 1 -and $databaseCreatedByHarness) {
                $drop = Invoke-Sql -Query "ALTER DATABASE [$databaseName] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$databaseName];"
                $cleanupResult.databaseDropExitCode = $drop.ExitCode
            }
            elseif ($countBeforeDrop -gt 0 -and -not $databaseCreatedByHarness) {
                $cleanupResult.cleanupFailure = 'Generated-name database ownership was not confirmed; the database was left untouched.'
            }
            $remaining = Get-SqlInt -Query "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name=N'$databaseName';"
            $cleanupResult.remainingOwnedDatabases = $remaining
            $databaseCleanupConfirmed = (
                $remaining -eq 0 -and
                ($countBeforeDrop -eq 0 -or -not $databaseCreatedByHarness -or $cleanupResult.databaseDropExitCode -eq 0)
            )
        }
        catch {
            $cleanupResult.cleanupFailure = Get-SafeFailure -ErrorRecord $_
            $databaseCleanupConfirmed = $false
        }
    }

    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
    foreach ($path in $bodyTempPaths) {
        if (Test-Path -LiteralPath $path -PathType Leaf) { Remove-Item -LiteralPath $path -Force }
    }
    Sanitize-Log -Path $apiStdoutPath
    Sanitize-Log -Path $apiStderrPath
    Sanitize-Log -Path $migrationLogPath

    $cleanupResult.cleanupConfirmed = $cleanupResult.processStopped -and $databaseCleanupConfirmed
    try { Write-JsonFile -Path $cleanupResultPath -Value $cleanupResult }
    catch {
        $cleanupResult.cleanupConfirmed = $false
        $cleanupResult.cleanupFailure = 'Cleanup metadata could not be written.'
    }
}

Write-Output (ConvertTo-Json -InputObject $smokeResult -Depth 8)
Write-Output (ConvertTo-Json -InputObject $cleanupResult -Depth 8)
if ($smokeExitCode -ne 0 -or -not $cleanupResult.cleanupConfirmed) { exit 1 }
