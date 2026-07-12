[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$OutputRoot = (Join-Path (Get-Location) 'artifacts/evidence'),

    [switch]$RunSqlServerTests,

    [string[]]$BenchmarkChannels = @('none'),

    [switch]$KeepBenchmarkDatabase
)

$ErrorActionPreference = 'Stop'
$artifactRoot = $null
$stageRecords = [System.Collections.Generic.List[object]]::new()
$scriptErrors = [System.Collections.Generic.List[string]]::new()
$overallExitCode = 0
$worktreeClean = $false
$benchmarkChannelArgument = $null

$normalizedChannels = @($BenchmarkChannels |
    ForEach-Object { $_ -split ',' } |
    ForEach-Object { $_.Trim().ToLowerInvariant() } |
    Select-Object -Unique)
if ($normalizedChannels.Count -eq 0) {
    throw 'At least one benchmark channel must be selected.'
}
$unsupportedChannels = @($normalizedChannels | Where-Object { $_ -notin @('none', 'lexical', 'semantic', 'fused') })
if ($unsupportedChannels.Count -gt 0) {
    throw "Unsupported benchmark channel(s): $($unsupportedChannels -join ', ')"
}
if ($normalizedChannels.Count -gt 1 -and $normalizedChannels -contains 'none') {
    throw 'BenchmarkChannels none cannot be combined with another channel.'
}
$BenchmarkChannels = $normalizedChannels
$benchmarkChannelArgument = $BenchmarkChannels -join ','

function Get-RepositoryValue {
    param([string[]]$Arguments)

    $value = & git @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Git command failed: git $($Arguments -join ' ')"
    }

    return ($value | Out-String).Trim()
}

function Get-OptionalCommandValue {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )

    try {
        $value = & $FilePath @Arguments 2>$null
        if ($LASTEXITCODE -eq 0) {
            return ($value | Out-String).Trim()
        }
    }
    catch {
        return $null
    }

    return $null
}

function Get-ArtifactRelativePath {
    param([string]$Path)

    $baseUri = [System.Uri]::new("$($artifactRoot.TrimEnd('\'))\")
    $pathUri = [System.Uri]::new($Path)
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($pathUri).ToString()).Replace('/', '\')
}

function Invoke-Stage {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [Parameter(Mandatory)]
        [string]$LogPath,

        [string]$Description = ''
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
        & $FilePath @Arguments *>> $LogPath
        $exitCode = $LASTEXITCODE
    }
    catch {
        Add-Content -LiteralPath $LogPath -Value $_.Exception.ToString()
        $exitCode = 1
    }

    $finished = [DateTimeOffset]::UtcNow
    $record = [ordered]@{
        name = $Name
        description = $Description
        command = $safeCommand
        log = Get-ArtifactRelativePath $LogPath
        startedAtUtc = $started.ToString('O')
        finishedAtUtc = $finished.ToString('O')
        exitCode = $exitCode
        status = if ($exitCode -eq 0) { 'passed' } else { 'failed' }
    }
    $stageRecords.Add([pscustomobject]$record)

    if ($exitCode -ne 0) {
        $script:overallExitCode = 1
    }

    return [pscustomobject]$record
}

function Add-SkippedStage {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$Reason
    )

    $stageRecords.Add([pscustomobject][ordered]@{
        name = $Name
        status = 'skipped'
        exitCode = $null
        reason = $Reason
    })
}

function Get-TrxCounters {
    param([string]$TrxPath)

    if (-not (Test-Path -LiteralPath $TrxPath)) {
        return $null
    }

    try {
        [xml]$trx = Get-Content -Raw -LiteralPath $TrxPath
        $counters = $trx.TestRun.ResultSummary.Counters
        if ($null -eq $counters) {
            return $null
        }

        $results = @($trx.TestRun.Results.UnitTestResult)
        $skippedResults = @($results | Where-Object {
            $_.outcome -in @('NotExecuted', 'Skipped')
        }).Count

        return [ordered]@{
            total = [int]$counters.total
            executed = [int]$counters.executed
            passed = [int]$counters.passed
            failed = [int]$counters.failed
            error = [int]$counters.error
            skipped = $skippedResults
            notExecutedCounter = [int]$counters.notExecuted
            inconclusive = [int]$counters.inconclusive
        }
    }
    catch {
        $script:scriptErrors.Add("Unable to parse TRX '$TrxPath': $($_.Exception.Message)")
        return $null
    }
}

function Assert-SemanticConfiguration {
    $missing = [System.Collections.Generic.List[string]]::new()
    if ($env:UNIPM_EMBEDDINGS_ENABLED -ne 'true') { $missing.Add('UNIPM_EMBEDDINGS_ENABLED=true') }
    foreach ($name in @(
        'UNIPM_EMBEDDINGS_PROVIDER_KEY',
        'UNIPM_EMBEDDINGS_BASE_ADDRESS',
        'UNIPM_EMBEDDINGS_MODEL',
        'UNIPM_EMBEDDINGS_DIMENSIONS'
    )) {
        if ([string]::IsNullOrWhiteSpace((Get-Item -Path "Env:$name" -ErrorAction SilentlyContinue).Value)) {
            $missing.Add($name)
        }
    }

    if ($missing.Count -gt 0) {
        throw "Semantic benchmark configuration is incomplete. Missing: $($missing -join ', ')"
    }
}

try {
    $repoRoot = Get-RepositoryValue @('rev-parse', '--show-toplevel')
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'UniPM.slnx')) -or
        -not (Test-Path -LiteralPath (Join-Path $repoRoot '.git'))) {
        throw 'The current directory is not the UniPM repository root.'
    }

    Set-Location -LiteralPath $repoRoot
    $testedCommit = Get-RepositoryValue @('rev-parse', 'HEAD')
    $shortCommit = Get-RepositoryValue @('rev-parse', '--short=12', 'HEAD')
    $sourceBranch = Get-RepositoryValue @('branch', '--show-current')
    $recordedAtUtc = [DateTimeOffset]::UtcNow

    $resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
    New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null
    $directoryName = "$($recordedAtUtc.ToString('yyyyMMdd-HHmmssZ'))-$shortCommit"
    $artifactRoot = Join-Path $resolvedOutputRoot $directoryName
    New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
    $testResultsRoot = Join-Path $artifactRoot 'test-results'
    New-Item -ItemType Directory -Force -Path $testResultsRoot | Out-Null
    $worktreeEntries = @(& git status --porcelain --untracked-files=all 2>$null)
    $worktreeClean = $worktreeEntries.Count -eq 0

    $dockerVersion = Get-OptionalCommandValue 'docker' @('version', '--format', '{{.Server.Version}}')
    $environment = [ordered]@{
        recordedAtUtc = $recordedAtUtc.ToString('O')
        testedCommit = $testedCommit
        sourceBranch = $sourceBranch
        configuration = $Configuration
        operatingSystem = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
        powershellVersion = $PSVersionTable.PSVersion.ToString()
        dotnetSdkVersion = Get-OptionalCommandValue 'dotnet' @('--version')
        gitVersion = Get-OptionalCommandValue 'git' @('--version')
        dockerVersion = $dockerVersion
        sqlServerConfigurationPresent = -not [string]::IsNullOrWhiteSpace($env:UNIPM_SQLSERVER_TEST_CONNECTION)
        embeddingConfigurationPresent = $env:UNIPM_EMBEDDINGS_ENABLED -eq 'true'
        embeddingProviderKey = $env:UNIPM_EMBEDDINGS_PROVIDER_KEY
        embeddingModelKey = $env:UNIPM_EMBEDDINGS_MODEL
        embeddingDimensions = $env:UNIPM_EMBEDDINGS_DIMENSIONS
        selectedBenchmarkChannels = $benchmarkChannelArgument
        worktreeClean = $worktreeClean
    }
    $environment | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $artifactRoot 'environment.json') -Encoding utf8

    if (-not $worktreeClean) {
        throw "The worktree is not clean; refusing to claim that HEAD was tested. Found $($worktreeEntries.Count) tracked or untracked entries."
    }

    $restoreStage = Invoke-Stage -Name 'restore' -FilePath 'dotnet' -Arguments @('restore', '.\UniPM.slnx') -LogPath (Join-Path $artifactRoot 'restore.log') -Description 'Restore solution dependencies'
    $restoreSucceeded = $restoreStage.exitCode -eq 0

    if ($restoreSucceeded) {
        $buildStage = Invoke-Stage -Name 'build' -FilePath 'dotnet' -Arguments @('build', '.\UniPM.slnx', '--configuration', $Configuration, '--no-restore') -LogPath (Join-Path $artifactRoot 'build.log') -Description 'Build the solution'
        $buildSucceeded = $buildStage.exitCode -eq 0
    }
    else {
        Add-SkippedStage -Name 'build' -Reason 'Restore did not complete successfully.'
        $buildSucceeded = $false
    }

    if ($buildSucceeded) {
        $trxPath = Join-Path $testResultsRoot 'backend-tests.trx'
        Invoke-Stage -Name 'tests' -FilePath 'dotnet' -Arguments @(
            'test', '.\UniPM.slnx', '--configuration', $Configuration, '--no-build',
            '--logger', 'trx;LogFileName=backend-tests.trx', '--results-directory', $testResultsRoot
        ) -LogPath (Join-Path $artifactRoot 'tests-console.log') -Description 'Run the complete backend test suite' | Out-Null
        $testCounters = Get-TrxCounters $trxPath
    }
    else {
        Add-SkippedStage -Name 'tests' -Reason 'Build did not complete successfully; running --no-build could use stale binaries.'
    }

    if ($RunSqlServerTests) {
        if (-not $buildSucceeded) {
            Add-SkippedStage -Name 'sqlserver-tests' -Reason 'Build did not complete successfully; running --no-build could use stale binaries.'
        }
        elseif ([string]::IsNullOrWhiteSpace($env:UNIPM_SQLSERVER_TEST_CONNECTION)) {
            $script:overallExitCode = 1
            $stageRecords.Add([pscustomobject][ordered]@{
                name = 'sqlserver-tests'
                status = 'failed'
                exitCode = 2
                reason = 'UNIPM_SQLSERVER_TEST_CONNECTION is required when -RunSqlServerTests is used.'
            })
        }
        else {
            $sqlTrxPath = Join-Path $testResultsRoot 'sqlserver-tests.trx'
            Invoke-Stage -Name 'sqlserver-tests' -FilePath 'dotnet' -Arguments @(
                'test', '.\UniPM.slnx', '--configuration', $Configuration, '--no-build',
                '--filter', 'FullyQualifiedName~SqlServer',
                '--logger', 'trx;LogFileName=sqlserver-tests.trx', '--results-directory', $testResultsRoot
            ) -LogPath (Join-Path $artifactRoot 'sqlserver-tests.log') -Description 'Run tests using the repository SqlServerFact scope' | Out-Null
            $sqlTestCounters = Get-TrxCounters $sqlTrxPath
        }
    }
    else {
        Add-SkippedStage -Name 'sqlserver-tests' -Reason 'Not requested.'
    }

    $benchmarkRequested = -not ($BenchmarkChannels.Count -eq 1 -and $BenchmarkChannels[0] -eq 'none')
    if (-not $benchmarkRequested) {
        Add-SkippedStage -Name 'retrieval-benchmark' -Reason 'BenchmarkChannels was none.'
    }
    elseif (-not $restoreSucceeded -or -not $buildSucceeded) {
        $script:overallExitCode = 1
        Add-SkippedStage -Name 'retrieval-benchmark' -Reason 'Restore or build did not complete successfully; running --no-build could use stale binaries.'
    }
    elseif ([string]::IsNullOrWhiteSpace($env:UNIPM_SQLSERVER_TEST_CONNECTION)) {
        $script:overallExitCode = 1
        Add-SkippedStage -Name 'retrieval-benchmark' -Reason 'UNIPM_SQLSERVER_TEST_CONNECTION is required for the retrieval benchmark.'
    }
    else {
        $benchmarkConfigurationValid = $true
        if ($BenchmarkChannels -contains 'semantic' -or $BenchmarkChannels -contains 'fused') {
            try {
                Assert-SemanticConfiguration
            }
            catch {
                $benchmarkConfigurationValid = $false
                $script:overallExitCode = 1
                Add-SkippedStage -Name 'retrieval-benchmark' -Reason $_.Exception.Message
            }
        }

        if ($benchmarkConfigurationValid) {
            $benchmarkRoot = Join-Path $artifactRoot 'benchmark'
            New-Item -ItemType Directory -Force -Path $benchmarkRoot | Out-Null
            $previousKeepDatabase = $env:UNIPM_BENCHMARK_KEEP_DATABASE
            try {
                if ($KeepBenchmarkDatabase) {
                    $env:UNIPM_BENCHMARK_KEEP_DATABASE = 'true'
                }
                else {
                    Remove-Item Env:UNIPM_BENCHMARK_KEEP_DATABASE -ErrorAction SilentlyContinue
                }

                Invoke-Stage -Name 'retrieval-benchmark' -FilePath 'dotnet' -Arguments @(
                    'run', '--project', '.\tools\UniPM.RetrievalBenchmark', '--configuration', $Configuration, '--no-build', '--',
                    '--channels', $benchmarkChannelArgument, '--output', $benchmarkRoot
                ) -LogPath (Join-Path $artifactRoot 'benchmark.log') -Description 'Run the selected retrieval benchmark channel(s)' | Out-Null
            }
            finally {
                if ($null -eq $previousKeepDatabase) {
                    Remove-Item Env:UNIPM_BENCHMARK_KEEP_DATABASE -ErrorAction SilentlyContinue
                }
                else {
                    $env:UNIPM_BENCHMARK_KEEP_DATABASE = $previousKeepDatabase
                }
            }
        }
    }
}
catch {
    $script:overallExitCode = 1
    $script:scriptErrors.Add($_.Exception.ToString())
}
finally {
    if ($null -ne $artifactRoot) {
        $summary = [ordered]@{
            recordedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
            testedCommit = $testedCommit
            sourceBranch = $sourceBranch
            configuration = $Configuration
            worktreeClean = $worktreeClean
            status = if ($overallExitCode -eq 0) { 'passed' } else { 'failed' }
            exitCode = $overallExitCode
            stages = $stageRecords
            testCounts = $testCounters
            sqlServerTestCounts = $sqlTestCounters
            scriptErrors = $scriptErrors
            artifacts = @(Get-ChildItem -LiteralPath $artifactRoot -Recurse -File |
                Where-Object { $_.Name -ne 'SHA256SUMS.txt' } |
                ForEach-Object { Get-ArtifactRelativePath $_.FullName } |
                Sort-Object)
        }
        $summary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $artifactRoot 'verification-summary.json') -Encoding utf8

        $summary.artifacts = @(Get-ChildItem -LiteralPath $artifactRoot -Recurse -File |
            Where-Object { $_.Name -ne 'SHA256SUMS.txt' } |
            ForEach-Object { Get-ArtifactRelativePath $_.FullName } |
            Sort-Object)
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
