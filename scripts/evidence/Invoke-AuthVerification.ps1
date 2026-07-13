[CmdletBinding()]
param(
    [string]$OutputRoot = (Join-Path (Get-Location) 'artifacts/evidence')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http

$artifactRoot = $null
$overallExitCode = 0
$testedCommit = $null
$sourceBranch = $null
$worktreeClean = $false
$stageRecords = [System.Collections.Generic.List[object]]::new()
$checkRecords = [System.Collections.Generic.List[object]]::new()
$tokens = @{}
$savedEnvironment = @{}
$environmentNames = @(
    'MSSQL_SA_PASSWORD',
    'UNIPM_JWT_ISSUER',
    'UNIPM_JWT_AUDIENCE',
    'UNIPM_JWT_SIGNING_KEY',
    'UNIPM_JWT_ACCESS_TOKEN_MINUTES',
    'UNIPM_DEV_USER_PASSWORD',
    'UNIPM_MAINTENANCE_REVIEW_ENABLED',
    'UNIPM_SUMMARY_ENABLED',
    'UNIPM_EMBEDDINGS_ENABLED')

function Invoke-GitValue {
    param([Parameter(Mandatory)][string[]]$Arguments)
    $value = & git @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Git command failed: git $($Arguments -join ' ')"
    }

    return ($value | Out-String).Trim()
}

function Invoke-Stage {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Action
    )

    $started = [DateTimeOffset]::UtcNow
    try {
        & $Action
        $stageRecords.Add([ordered]@{
            name = $Name
            status = 'passed'
            startedAtUtc = $started.ToString('O')
            finishedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
            error = $null
        })
    }
    catch {
        $stageRecords.Add([ordered]@{
            name = $Name
            status = 'failed'
            startedAtUtc = $started.ToString('O')
            finishedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
            error = $_.Exception.Message
        })
        throw
    }
}

function Invoke-ApiRequest {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Uri,
        [string]$Body,
        [string]$AccessToken
    )

    $client = [System.Net.Http.HttpClient]::new()
    $request = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::new($Method),
        $Uri)
    try {
        if (-not [string]::IsNullOrWhiteSpace($AccessToken)) {
            $request.Headers.Authorization =
                [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $AccessToken)
        }
        if (-not [string]::IsNullOrWhiteSpace($Body)) {
            $request.Content = [System.Net.Http.StringContent]::new(
                $Body,
                [System.Text.Encoding]::UTF8,
                'application/json')
        }

        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            Content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        }
    }
    finally {
        $request.Dispose()
        $client.Dispose()
    }
}

function Add-Check {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][int]$ActualStatus,
        [Parameter(Mandatory)][int]$ExpectedStatus,
        [string]$Role,
        [hashtable]$Facts
    )

    if ($ActualStatus -ne $ExpectedStatus) {
        throw "$Name returned HTTP $ActualStatus; expected $ExpectedStatus."
    }

    $record = [ordered]@{
        name = $Name
        role = $Role
        httpStatus = $ActualStatus
    }
    if ($null -ne $Facts) {
        foreach ($key in $Facts.Keys) {
            $record[$key] = $Facts[$key]
        }
    }
    $checkRecords.Add($record)
}

function Get-TokenFingerprint {
    param([Parameter(Mandatory)][string]$Token)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Token))
        return ([BitConverter]::ToString($hash).Replace('-', '').ToLowerInvariant()).Substring(0, 12)
    }
    finally {
        $sha.Dispose()
    }
}

function Get-ArtifactRelativePath {
    param([Parameter(Mandatory)][string]$Path)
    $baseUri = [System.Uri]::new("$($artifactRoot.TrimEnd('\'))\")
    $pathUri = [System.Uri]::new($Path)
    return [System.Uri]::UnescapeDataString(
        $baseUri.MakeRelativeUri($pathUri).ToString()).Replace('/', '\')
}

try {
    foreach ($name in $environmentNames) {
        $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }

    $testedCommit = Invoke-GitValue @('rev-parse', 'HEAD')
    $shortCommit = Invoke-GitValue @('rev-parse', '--short=12', 'HEAD')
    $sourceBranch = Invoke-GitValue @('branch', '--show-current')
    $status = Invoke-GitValue @('status', '--porcelain=v1', '--untracked-files=all')
    $worktreeClean = [string]::IsNullOrWhiteSpace($status)
    if (-not $worktreeClean) {
        throw 'Authentication verification requires a clean worktree.'
    }

    $timestamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmssZ')
    $artifactRoot = Join-Path (
        [System.IO.Path]::GetFullPath($OutputRoot)) (
        "$timestamp-$shortCommit-authentication")
    New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

    [Environment]::SetEnvironmentVariable(
        'MSSQL_SA_PASSWORD',
        "SqlEvidence!9$([Guid]::NewGuid().ToString('N'))",
        'Process')
    [Environment]::SetEnvironmentVariable('UNIPM_JWT_ISSUER', 'UniPM.AuthEvidence', 'Process')
    [Environment]::SetEnvironmentVariable('UNIPM_JWT_AUDIENCE', 'UniPM.AuthEvidence.Clients', 'Process')
    [Environment]::SetEnvironmentVariable(
        'UNIPM_JWT_SIGNING_KEY',
        "AuthEvidence-Key-$([Guid]::NewGuid().ToString('N'))-$([Guid]::NewGuid().ToString('N'))",
        'Process')
    [Environment]::SetEnvironmentVariable('UNIPM_JWT_ACCESS_TOKEN_MINUTES', '60', 'Process')
    [Environment]::SetEnvironmentVariable(
        'UNIPM_DEV_USER_PASSWORD',
        "AuthEvidence!9$([Guid]::NewGuid().ToString('N'))",
        'Process')
    [Environment]::SetEnvironmentVariable('UNIPM_MAINTENANCE_REVIEW_ENABLED', 'true', 'Process')
    [Environment]::SetEnvironmentVariable('UNIPM_SUMMARY_ENABLED', 'false', 'Process')
    [Environment]::SetEnvironmentVariable('UNIPM_EMBEDDINGS_ENABLED', 'false', 'Process')

    Invoke-Stage 'compose-config' {
        docker compose config --quiet
        if ($LASTEXITCODE -ne 0) { throw "Compose validation failed with exit code $LASTEXITCODE." }
    }
    Invoke-Stage 'stack-start' {
        docker compose down -v
        if ($LASTEXITCODE -ne 0) { throw "Fresh-volume reset failed with exit code $LASTEXITCODE." }
        docker compose up --build -d
        if ($LASTEXITCODE -ne 0) { throw "Stack startup failed with exit code $LASTEXITCODE." }
    }
    Invoke-Stage 'health-ready' {
        $ready = $false
        for ($attempt = 0; $attempt -lt 30; $attempt++) {
            try {
                $health = Invoke-ApiRequest -Method GET -Uri 'http://localhost:5000/health/ready'
                if ($health.StatusCode -eq 200) {
                    $ready = $true
                    break
                }
            }
            catch {
                # The API may not be accepting requests yet.
            }
            Start-Sleep -Seconds 2
        }
        if (-not $ready) { throw 'API readiness did not become healthy.' }
    }
    Invoke-Stage 'database-migrate' {
        docker compose exec -T unipm-api dotnet UniPM.Api.dll --migrate-database
        if ($LASTEXITCODE -ne 0) { throw "Database migration failed with exit code $LASTEXITCODE." }
    }
    Invoke-Stage 'synthetic-seed' {
        docker compose exec -T unipm-api dotnet UniPM.Api.dll --seed-synthetic
        if ($LASTEXITCODE -ne 0) { throw "Synthetic seed failed with exit code $LASTEXITCODE." }
    }
    Invoke-Stage 'development-user-seed' {
        docker compose exec -T unipm-api dotnet UniPM.Api.dll --seed-development-users
        if ($LASTEXITCODE -ne 0) { throw "Development user seed failed with exit code $LASTEXITCODE." }
    }
    Invoke-Stage 'login-and-me' {
        $password = [Environment]::GetEnvironmentVariable('UNIPM_DEV_USER_PASSWORD', 'Process')
        $definitions = @(
            @{ Role = 'Admin'; Email = 'admin@unipm.local' },
            @{ Role = 'GSD'; Email = 'gsd@unipm.local' },
            @{ Role = 'Inspector'; Email = 'inspector@unipm.local' },
            @{ Role = 'Supervisor'; Email = 'supervisor@unipm.local' },
            @{ Role = 'DepartmentHead'; Email = 'departmenthead@unipm.local' })
        foreach ($definition in $definitions) {
            $body = @{ email = $definition.Email; password = $password } | ConvertTo-Json -Compress
            $login = Invoke-ApiRequest -Method POST -Uri 'http://localhost:5000/api/v1/auth/login' -Body $body
            if ($login.StatusCode -ne 200) {
                throw "Login for role $($definition.Role) returned HTTP $($login.StatusCode)."
            }
            $payload = $login.Content | ConvertFrom-Json
            $tokens[$definition.Role] = $payload.accessToken
            Add-Check -Name 'login' -Role $definition.Role -ActualStatus $login.StatusCode -ExpectedStatus 200 -Facts @{
                expiresAtUtc = $payload.expiresAtUtc
                tokenFingerprint = Get-TokenFingerprint $payload.accessToken
            }
        }

        $me = Invoke-ApiRequest -Method GET -Uri 'http://localhost:5000/api/v1/auth/me' -AccessToken $tokens['GSD']
        $mePayload = $me.Content | ConvertFrom-Json
        Add-Check -Name 'current-user' -Role 'GSD' -ActualStatus $me.StatusCode -ExpectedStatus 200 -Facts @{
            returnedRoles = @($mePayload.roles)
        }
    }
    Invoke-Stage 'authorization-policies' {
        $assetBody = @{
            assetCode = "AUTH-$shortCommit"
            assetCategory = 'fire-extinguisher'
            building = 'Evidence Building'
            department = 'GSD'
            location = 'Evidence Room'
        } | ConvertTo-Json -Compress

        $anonymous = Invoke-ApiRequest -Method POST -Uri 'http://localhost:5000/api/v1/assets/' -Body $assetBody
        Add-Check -Name 'asset-create-anonymous' -ActualStatus $anonymous.StatusCode -ExpectedStatus 401

        $admin = Invoke-ApiRequest -Method POST -Uri 'http://localhost:5000/api/v1/assets/' -Body $assetBody -AccessToken $tokens['Admin']
        Add-Check -Name 'asset-create-admin-forbidden' -Role 'Admin' -ActualStatus $admin.StatusCode -ExpectedStatus 403

        $asset = Invoke-ApiRequest -Method POST -Uri 'http://localhost:5000/api/v1/assets/' -Body $assetBody -AccessToken $tokens['GSD']
        $assetPayload = $asset.Content | ConvertFrom-Json
        Add-Check -Name 'asset-create' -Role 'GSD' -ActualStatus $asset.StatusCode -ExpectedStatus 201

        $scheduleBody = @{
            assetId = $assetPayload.id
            scheduleDate = [DateTimeOffset]::UtcNow.AddDays(1).ToString('O')
            periodType = 'Quarter'
            quarter = 'Q1'
            year = 2026
        } | ConvertTo-Json -Compress
        $schedule = Invoke-ApiRequest -Method POST -Uri 'http://localhost:5000/api/v1/schedules/' -Body $scheduleBody -AccessToken $tokens['Supervisor']
        $schedulePayload = $schedule.Content | ConvertFrom-Json
        Add-Check -Name 'schedule-create' -Role 'Supervisor' -ActualStatus $schedule.StatusCode -ExpectedStatus 201

        $inspectorId = (($tokens['Inspector'] | ForEach-Object {
            $parts = $_.Split('.')
            $payloadPart = $parts[1].Replace('-', '+').Replace('_', '/')
            while ($payloadPart.Length % 4 -ne 0) { $payloadPart += '=' }
            ([System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payloadPart)) | ConvertFrom-Json).sub
        }))
        $inspectionBody = @{
            scheduleId = $schedulePayload.id
            inspectorUserId = $inspectorId
            dateInspected = [DateTimeOffset]::UtcNow.ToString('O')
            isOperational = $true
            remarks = 'Authentication evidence inspection'
        } | ConvertTo-Json -Compress
        $inspection = Invoke-ApiRequest -Method POST -Uri 'http://localhost:5000/api/v1/inspections/' -Body $inspectionBody -AccessToken $tokens['Inspector']
        Add-Check -Name 'inspection-submit' -Role 'Inspector' -ActualStatus $inspection.StatusCode -ExpectedStatus 201

        # Deterministic fictional FA-003 fixture ID, never a production record.
        $reviewBody = @{
            assetId = '7bfa4436-d0f1-5997-9310-6dafbc8183fe'
            findingText = 'hindi nagrerespond ang smoke detector'
            generateSummary = $false
        } | ConvertTo-Json -Compress
        $review = Invoke-ApiRequest -Method POST -Uri 'http://localhost:5000/api/v1/maintenance-review' -Body $reviewBody -AccessToken $tokens['DepartmentHead']
        $reviewPayload = $review.Content | ConvertFrom-Json
        Add-Check -Name 'maintenance-review' -Role 'DepartmentHead' -ActualStatus $review.StatusCode -ExpectedStatus 200 -Facts @{
            sourceCount = @($reviewPayload.sourceRecords).Count
            evidenceStatus = $reviewPayload.evidenceStatus
            summaryStatus = $reviewPayload.summaryStatus
        }
        if (@($reviewPayload.sourceRecords).Count -lt 1) {
            throw 'Authorized maintenance review returned no source records.'
        }
    }

    [ordered]@{
        testedCommit = $testedCommit
        sourceBranch = $sourceBranch
        worktreeClean = $worktreeClean
        checks = $checkRecords
    } | ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath (Join-Path $artifactRoot 'auth-verification.json') -Encoding utf8
}
catch {
    $overallExitCode = 1
    if ($null -eq $artifactRoot) {
        $artifactRoot = Join-Path ([System.IO.Path]::GetFullPath($OutputRoot)) 'authentication-failed'
        New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
    }
    Set-Content -LiteralPath (Join-Path $artifactRoot 'error.txt') -Value $_.Exception.Message -Encoding utf8
}
finally {
    try {
        docker compose down -v
        if ($LASTEXITCODE -ne 0) { $overallExitCode = 1 }
    }
    catch {
        $overallExitCode = 1
    }

    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
    }
    $tokens.Clear()

    if ($null -ne $artifactRoot) {
        $summary = [ordered]@{
            recordedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
            testedCommit = $testedCommit
            sourceBranch = $sourceBranch
            worktreeClean = $worktreeClean
            status = if ($overallExitCode -eq 0) { 'passed' } else { 'failed' }
            exitCode = $overallExitCode
            stages = $stageRecords
            checks = $checkRecords
            artifacts = @(Get-ChildItem -LiteralPath $artifactRoot -File |
                Where-Object Name -ne 'SHA256SUMS.txt' |
                ForEach-Object { Get-ArtifactRelativePath $_.FullName } |
                Sort-Object)
        }
        $summary | ConvertTo-Json -Depth 10 |
            Set-Content -LiteralPath (Join-Path $artifactRoot 'verification-summary.json') -Encoding utf8
        $hashLines = Get-ChildItem -LiteralPath $artifactRoot -File |
            Where-Object Name -ne 'SHA256SUMS.txt' |
            Sort-Object Name |
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
