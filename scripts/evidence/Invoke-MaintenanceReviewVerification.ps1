[CmdletBinding()]
param(
    [string]$OutputRoot = (Join-Path (Get-Location) 'artifacts/evidence'),
    [switch]$RemoveVolumes
)

$ErrorActionPreference = 'Stop'
$artifactRoot = $null
$stageRecords = [System.Collections.Generic.List[object]]::new()
$overallExitCode = 0
$worktreeClean = $false
$testedCommit = $null
$sourceBranch = $null

Add-Type -AssemblyName System.Net.Http

function Invoke-JsonPost {
    param(
        [Parameter(Mandatory)][string]$Uri,
        [Parameter(Mandatory)][string]$Body
    )

    $client = New-Object System.Net.Http.HttpClient
    $content = New-Object System.Net.Http.StringContent(
        $Body,
        [System.Text.Encoding]::UTF8,
        'application/json')
    try {
        $response = $client.PostAsync($Uri, $content).GetAwaiter().GetResult()
        [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            Content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        }
    }
    finally {
        $content.Dispose()
        $client.Dispose()
    }
}

function Get-RepositoryValue {
    param([string[]]$Arguments)
    $value = & git @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Git command failed: git $($Arguments -join ' ')"
    }
    return ($value | Out-String).Trim()
}

function Get-ArtifactRelativePath {
    param([string]$Path)
    $baseUri = [System.Uri]::new("$($artifactRoot.TrimEnd('\'))\")
    $pathUri = [System.Uri]::new($Path)
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($pathUri).ToString()).Replace('/', '\')
}

function Invoke-Stage {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Action
    )

    $started = [DateTimeOffset]::UtcNow
    $status = 'passed'
    $errorMessage = $null
    try {
        & $Action
    }
    catch {
        $status = 'failed'
        $errorMessage = $_.Exception.Message
        $script:overallExitCode = 1
    }
    $finished = [DateTimeOffset]::UtcNow
    $stageRecords.Add([pscustomobject][ordered]@{
        name = $Name
        status = $status
        startedAtUtc = $started.ToString('O')
        finishedAtUtc = $finished.ToString('O')
        error = $errorMessage
    })
    if ($status -eq 'failed') {
        throw $errorMessage
    }
}

try {
    $repoRoot = Get-RepositoryValue @('rev-parse', '--show-toplevel')
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'UniPM.slnx')) -or -not (Test-Path -LiteralPath (Join-Path $repoRoot '.git'))) {
        throw 'The current directory is not the UniPM repository root.'
    }
    Set-Location -LiteralPath $repoRoot
    $testedCommit = Get-RepositoryValue @('rev-parse', 'HEAD')
    $sourceBranch = Get-RepositoryValue @('branch', '--show-current')
    $worktreeClean = @(& git status --porcelain --untracked-files=all 2>$null).Count -eq 0
    if (-not $worktreeClean) {
        throw 'The worktree is not clean; refusing to claim endpoint verification.'
    }

    $recordedAtUtc = [DateTimeOffset]::UtcNow
    $shortCommit = Get-RepositoryValue @('rev-parse', '--short=12', 'HEAD')
    $resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
    New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null
    $artifactRoot = Join-Path $resolvedOutputRoot "$($recordedAtUtc.ToString('yyyyMMdd-HHmmssZ'))-$shortCommit-maintenance-review"
    New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:UNIPM_MAINTENANCE_REVIEW_ENABLED = 'true'
    $env:UNIPM_SUMMARY_ENABLED = 'false'
    $env:UNIPM_EMBEDDINGS_ENABLED = 'false'
    $apiPort = if ([string]::IsNullOrWhiteSpace($env:UNIPM_API_PORT)) { '5000' } else { $env:UNIPM_API_PORT }
    $apiBase = "http://localhost:$apiPort"

    Invoke-Stage 'compose-config' {
        docker compose --profile observability config --quiet
        if ($LASTEXITCODE -ne 0) { throw "Docker Compose config failed with exit code $LASTEXITCODE." }
    }
    Invoke-Stage 'stack-start' {
        docker compose up --build -d unipm-api
        if ($LASTEXITCODE -ne 0) { throw "Docker Compose stack start failed with exit code $LASTEXITCODE." }
    }
    try {
        Invoke-Stage 'health-poll' {
            $ready = $false
            for ($attempt = 1; $attempt -le 30; $attempt++) {
                try {
                    $readiness = Invoke-RestMethod -Uri "$apiBase/health/ready" -TimeoutSec 3
                    if ($null -ne $readiness) {
                        $ready = $true
                        break
                    }
                }
                catch {
                    Start-Sleep -Seconds 2
                }
            }
            if (-not $ready) {
                throw 'The API did not become live within the bounded polling window.'
            }
        }
        Invoke-Stage 'seed' {
            docker compose exec -T unipm-api dotnet UniPM.Api.dll --seed-synthetic
            if ($LASTEXITCODE -ne 0) { throw "Synthetic seed failed with exit code $LASTEXITCODE." }
        }
        Invoke-Stage 'rebuild-search-documents' {
            docker compose exec -T unipm-api dotnet UniPM.Api.dll --rebuild-maintenance-search-documents
            if ($LASTEXITCODE -ne 0) { throw "Search-document rebuild failed with exit code $LASTEXITCODE." }
        }
        Invoke-Stage 'review-request' {
            $inspections = Invoke-RestMethod -Uri "$apiBase/api/v1/inspections" -Method Get
            $inspection = @($inspections) | Select-Object -First 1
            if ($null -eq $inspection) {
                throw 'No synthetic inspection was returned by the API.'
            }
            $asset = Invoke-RestMethod -Uri "$apiBase/api/v1/assets/$($inspection.assetId)" -Method Get
            $finding = switch ($asset.assetCategory) {
                'fire-extinguisher' { 'mahina ang pressure' }
                'fire-alarm' { 'hindi nagrerespond' }
                'emergency-light' { 'hindi umiilaw' }
                default { 'barado ang filter' }
            }
            $body = @{ assetId = $asset.id; findingText = $finding; generateSummary = $true } |
                ConvertTo-Json -Compress
            $response = $null
            $payload = $null
            for ($attempt = 1; $attempt -le 15; $attempt++) {
                try {
                    $candidateResponse = Invoke-JsonPost -Uri "$apiBase/api/v1/maintenance-review" -Body $body
                    $candidatePayload = $candidateResponse.Content | ConvertFrom-Json
                    if ([int]$candidateResponse.StatusCode -eq 200 -and @($candidatePayload.sourceRecords).Count -gt 0) {
                        $response = $candidateResponse
                        $payload = $candidatePayload
                        break
                    }
                }
                catch {
                    if ($attempt -eq 15) {
                        throw
                    }
                }

                Start-Sleep -Seconds 2
            }
            if ($null -eq $response -or $null -eq $payload) {
                throw 'Maintenance review did not return selected source records within the bounded Full-Text readiness retry window.'
            }
            if ($payload.summaryStatus -ne 'disabled' -or $null -ne $payload.summary) {
                throw 'Source-only review did not return the disabled summary contract.'
            }
            if (@($payload.sourceRecords).Count -eq 0) {
                throw 'Source-only review returned no selected source records.'
            }
            if ($null -eq $payload.retrievalStatus -or $payload.retrievalStatus.isDegraded -ne $true) {
                throw 'Review response did not report the expected degraded retrieval metadata while embeddings were disabled.'
            }
            if ($response.Content -match '(?i)ApiKey|VectorJson|prompt|tokenMap|Authorization') {
                throw 'Review response contained a forbidden provider or prompt field.'
            }
            Set-Content -LiteralPath (Join-Path $artifactRoot 'maintenance-review-response.json') -Value $response.Content -Encoding utf8
        }
    }
    finally {
        if ($RemoveVolumes) {
            docker compose down -v
        }
        else {
            docker compose down
        }
    }
}
catch {
    $overallExitCode = 1
    if ($null -eq $artifactRoot) {
        $artifactRoot = Join-Path ([System.IO.Path]::GetFullPath($OutputRoot)) 'maintenance-review-failed'
        New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
    }
    Set-Content -LiteralPath (Join-Path $artifactRoot 'error.txt') -Value $_.Exception.Message -Encoding utf8
}
finally {
    if ($null -ne $artifactRoot) {
        $summary = [ordered]@{
            recordedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
            testedCommit = $testedCommit
            sourceBranch = $sourceBranch
            worktreeClean = $worktreeClean
            status = if ($overallExitCode -eq 0) { 'passed' } else { 'failed' }
            exitCode = $overallExitCode
            summaryProviderExecuted = $false
            realEmbeddingProviderExecuted = $false
            stages = $stageRecords
            artifacts = @(Get-ChildItem -LiteralPath $artifactRoot -Recurse -File |
                Where-Object { $_.Name -ne 'SHA256SUMS.txt' } |
                ForEach-Object { Get-ArtifactRelativePath $_.FullName } |
                Sort-Object)
        }
        $summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $artifactRoot 'verification-summary.json') -Encoding utf8
        $hashLines = Get-ChildItem -LiteralPath $artifactRoot -Recurse -File |
            Where-Object { $_.Name -ne 'SHA256SUMS.txt' } |
            Sort-Object FullName |
            ForEach-Object {
                $relative = Get-ArtifactRelativePath $_.FullName
                $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
                "$hash  $relative"
            }
        Set-Content -LiteralPath (Join-Path $artifactRoot 'SHA256SUMS.txt') -Value $hashLines -Encoding utf8
        Write-Output "Evidence artifacts: $artifactRoot"
        Write-Output "Verification status: $(if ($overallExitCode -eq 0) { 'passed' } else { 'failed' })"
    }
}

exit $overallExitCode
