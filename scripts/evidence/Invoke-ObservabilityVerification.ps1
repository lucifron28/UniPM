[CmdletBinding()]
param(
    [string]$OutputRoot = (Join-Path (Get-Location) 'artifacts/evidence'),

    [switch]$CleanupVolumes
)

$ErrorActionPreference = 'Stop'
$artifactRoot = $null
$stageRecords = [System.Collections.Generic.List[object]]::new()
$scriptErrors = [System.Collections.Generic.List[string]]::new()
$overallExitCode = 0
$stackStarted = $false

function Get-RepositoryValue {
    param([string[]]$Arguments)

    $value = & git @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Git command failed: git $($Arguments -join ' ')"
    }

    return ($value | Out-String).Trim()
}

function Get-RepositoryRoot {
    $root = Get-RepositoryValue @('rev-parse', '--show-toplevel')
    if (-not (Test-Path -LiteralPath (Join-Path $root 'UniPM.slnx'))) {
        throw "The current directory is not a UniPM repository: $root"
    }

    return $root
}

function Get-ArtifactRelativePath {
    param([string]$Path)

    $baseUri = [System.Uri]::new("$($artifactRoot.TrimEnd('\'))\")
    $pathUri = [System.Uri]::new($Path)
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($pathUri).ToString()).Replace('/', '\')
}

function Invoke-CapturedStage {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [Parameter(Mandatory)]
        [string]$LogPath
    )

    $started = [DateTimeOffset]::UtcNow
    $safeCommand = "$FilePath $($Arguments -join ' ')".Trim()
    Set-Content -LiteralPath $LogPath -Value @(
        "Command: $safeCommand"
        "StartedAtUtc: $($started.ToString('O'))"
        ''
    ) -Encoding utf8

    $exitCode = 1
    try {
        & $FilePath @Arguments 2>&1 |
            Out-File -LiteralPath $LogPath -Append -Encoding utf8
        $exitCode = $LASTEXITCODE
    }
    catch {
        Add-Content -LiteralPath $LogPath -Value $_.Exception.ToString()
    }

    $finished = [DateTimeOffset]::UtcNow
    $stageRecords.Add([pscustomobject][ordered]@{
        name = $Name
        command = $safeCommand
        log = Get-ArtifactRelativePath $LogPath
        startedAtUtc = $started.ToString('O')
        finishedAtUtc = $finished.ToString('O')
        exitCode = $exitCode
        status = if ($exitCode -eq 0) { 'passed' } else { 'failed' }
    })

    if ($exitCode -ne 0) {
        $script:overallExitCode = 1
    }

    return $exitCode
}

function Invoke-DockerCompose {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [Parameter(Mandatory)]
        [string]$LogPath
    )

    return Invoke-CapturedStage -Name ($Arguments -join ' ') -FilePath 'docker' -Arguments (@('compose') + $Arguments) -LogPath $LogPath
}

function Wait-ForHttp {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$Uri,

        [int]$ExpectedStatusCode = 200,

        [int]$MaxAttempts = 30,

        [int]$DelayMilliseconds = 1000
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 5
            if ([int]$response.StatusCode -eq $ExpectedStatusCode) {
                return [pscustomobject]@{
                    name = $Name
                    uri = $Uri
                    statusCode = [int]$response.StatusCode
                    attempts = $attempt
                    status = 'passed'
                }
            }
        }
        catch {
            if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq $ExpectedStatusCode) {
                return [pscustomobject]@{
                    name = $Name
                    uri = $Uri
                    statusCode = $ExpectedStatusCode
                    attempts = $attempt
                    status = 'passed'
                }
            }
        }

        Start-Sleep -Milliseconds $DelayMilliseconds
    }

    throw "$Name did not return HTTP $ExpectedStatusCode after $MaxAttempts attempts."
}

function Get-SafeEnvironmentMetadata {
    $dockerVersion = $null
    try {
        $dockerVersion = (& docker version --format '{{.Client.Version}}' 2>$null | Out-String).Trim()
    }
    catch {
        $dockerVersion = $null
    }

    return [ordered]@{
        recordedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        operatingSystem = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
        powershell = $PSVersionTable.PSVersion.ToString()
        dotnet = ((& dotnet --version 2>$null | Out-String).Trim())
        git = (Get-RepositoryValue @('--version'))
        docker = $dockerVersion
        testedCommit = (Get-RepositoryValue @('rev-parse', 'HEAD'))
        sourceBranch = (Get-RepositoryValue @('branch', '--show-current'))
        worktreeClean = (@(& git status --porcelain --untracked-files=all 2>$null).Count -eq 0)
        metricsEnabled = $true
        sqlServerConfigurationPresent = -not [string]::IsNullOrWhiteSpace($env:UNIPM_SQLSERVER_TEST_CONNECTION)
    }
}

try {
    $repositoryRoot = Get-RepositoryRoot
    Set-Location -LiteralPath $repositoryRoot

    $worktreeEntries = @(& git status --porcelain --untracked-files=all 2>$null)
    if ($worktreeEntries.Count -ne 0) {
        throw 'The worktree must be clean before an observability verification run.'
    }

    $shortSha = Get-RepositoryValue @('rev-parse', '--short=12', 'HEAD')
    $timestamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmssZ')
    $artifactRoot = Join-Path $OutputRoot "$timestamp-$shortSha-observability"
    New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null

    $environment = Get-SafeEnvironmentMetadata
    $environment | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $artifactRoot 'environment.json') -Encoding utf8

    $composeConfigExitCode = Invoke-DockerCompose -Arguments @('--profile', 'observability', 'config', '--quiet') -LogPath (Join-Path $artifactRoot 'compose-config.log')
    if ($composeConfigExitCode -eq 0) {
        $composeUpExitCode = Invoke-DockerCompose -Arguments @('--profile', 'observability', 'up', '--build', '-d') -LogPath (Join-Path $artifactRoot 'compose-up.log')
        if ($composeUpExitCode -ne 0) {
            throw 'The observability Compose profile failed to start.'
        }
        $stackStarted = $true

        Wait-ForHttp -Name 'API root' -Uri 'http://localhost:5000/' | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artifactRoot 'api-root.json') -Encoding utf8
        Wait-ForHttp -Name 'API liveness' -Uri 'http://localhost:5000/health/live' | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artifactRoot 'api-liveness.json') -Encoding utf8
        Wait-ForHttp -Name 'API metrics' -Uri 'http://localhost:5000/metrics' | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artifactRoot 'api-metrics.json') -Encoding utf8

        $metricsResponse = Invoke-WebRequest -Uri 'http://localhost:5000/metrics' -UseBasicParsing
        $metricsResponse.Content -split "`n" |
            Where-Object { $_ -match '^(# HELP|# TYPE|unipm_|http_server_request_duration|dotnet_)' } |
            Select-Object -First 250 |
            Set-Content -LiteralPath (Join-Path $artifactRoot 'api-metrics-sample.txt') -Encoding utf8

        Wait-ForHttp -Name 'Prometheus readiness' -Uri 'http://localhost:9090/-/ready' | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artifactRoot 'prometheus-ready.txt') -Encoding utf8
        $targets = Invoke-RestMethod -Uri 'http://localhost:9090/api/v1/targets'
        $targets.data.activeTargets |
            Where-Object { $_.labels.job -eq 'unipm-api' } |
            Select-Object -Property labels, health, lastError |
            ConvertTo-Json -Depth 6 |
            Set-Content -LiteralPath (Join-Path $artifactRoot 'prometheus-targets.json') -Encoding utf8
        if (-not ($targets.data.activeTargets | Where-Object { $_.labels.job -eq 'unipm-api' -and $_.health -eq 'up' })) {
            throw 'The unipm-api Prometheus target is not up.'
        }

        Wait-ForHttp -Name 'Grafana health' -Uri 'http://localhost:3000/api/health' | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artifactRoot 'grafana-health.json') -Encoding utf8

        $grafanaUser = if ([string]::IsNullOrWhiteSpace($env:UNIPM_GRAFANA_ADMIN_USER)) { 'admin' } else { $env:UNIPM_GRAFANA_ADMIN_USER }
        $grafanaPassword = if ([string]::IsNullOrWhiteSpace($env:UNIPM_GRAFANA_ADMIN_PASSWORD)) { 'ChangeThisLocalGrafanaPassword123!' } else { $env:UNIPM_GRAFANA_ADMIN_PASSWORD }
        $grafanaToken = [Convert]::ToBase64String(
            [System.Text.Encoding]::UTF8.GetBytes("$grafanaUser`:$grafanaPassword"))
        $grafanaHeaders = @{ Authorization = "Basic $grafanaToken" }
        $dataSource = Invoke-RestMethod -Uri 'http://localhost:3000/api/datasources/uid/unipm-prometheus' -Headers $grafanaHeaders
        [ordered]@{
            uid = $dataSource.uid
            name = $dataSource.name
            type = $dataSource.type
        } |
            ConvertTo-Json |
            Set-Content -LiteralPath (Join-Path $artifactRoot 'grafana-datasource.json') -Encoding utf8
        $dashboard = Invoke-RestMethod -Uri 'http://localhost:3000/api/dashboards/uid/unipm-system-health' -Headers $grafanaHeaders
        [ordered]@{
            uid = $dashboard.dashboard.uid
            title = $dashboard.dashboard.title
            version = $dashboard.dashboard.version
        } |
            ConvertTo-Json |
            Set-Content -LiteralPath (Join-Path $artifactRoot 'grafana-dashboard.json') -Encoding utf8
        $stageRecords.Add([pscustomobject][ordered]@{ name = 'stack-verification'; status = 'passed' })
    }
    else {
        throw 'Docker Compose configuration validation failed.'
    }
}
catch {
    $overallExitCode = 1
    $scriptErrors.Add($_.Exception.Message)
}
finally {
    if ($stackStarted) {
        $downArguments = @('--profile', 'observability', 'down')
        if ($CleanupVolumes) {
            $downArguments += '--volumes'
        }
        try {
            Invoke-DockerCompose -Arguments $downArguments -LogPath (Join-Path $artifactRoot 'compose-down.log') | Out-Null
        }
        catch {
            $scriptErrors.Add("Unable to stop the observability stack: $($_.Exception.Message)")
            $overallExitCode = 1
        }
    }

    if ($null -ne $artifactRoot) {
        $summary = [ordered]@{
            status = if ($overallExitCode -eq 0) { 'passed' } else { 'failed' }
            exitCode = $overallExitCode
            testedCommit = (Get-RepositoryValue @('rev-parse', 'HEAD'))
            sourceBranch = (Get-RepositoryValue @('branch', '--show-current'))
            worktreeClean = (@(& git status --porcelain --untracked-files=all 2>$null).Count -eq 0)
            artifactDirectory = (Get-ArtifactRelativePath $artifactRoot)
            stages = $stageRecords
            errors = $scriptErrors
            skippedVerification = @(
                'SQL Server validation is outside this observability script.'
                'IIS deployment and production monitoring were not verified.'
                'Long-term retention, alert delivery, and real user traffic were not verified.'
            )
        }
        $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $artifactRoot 'verification-summary.json') -Encoding utf8

        $hashLines = Get-ChildItem -LiteralPath $artifactRoot -File |
            Where-Object { $_.Name -ne 'SHA256SUMS.txt' } |
            Sort-Object FullName |
            ForEach-Object {
                $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                "$hash  $(Get-ArtifactRelativePath $_.FullName)"
            }
        $hashLines | Set-Content -LiteralPath (Join-Path $artifactRoot 'SHA256SUMS.txt') -Encoding utf8
    }
}

exit $overallExitCode
