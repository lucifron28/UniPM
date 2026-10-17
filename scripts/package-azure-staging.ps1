[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$originalApiBaseUrl = [Environment]::GetEnvironmentVariable("VITE_API_BASE_URL", "Process")
$hadApiBaseUrl = $null -ne $originalApiBaseUrl

function Invoke-CheckedNative {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Command failed with exit code $LASTEXITCODE."
    }
}

function Get-CheckedVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $version = (& $Command @Arguments).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($version)) {
        throw "Could not read the $Command version."
    }
    return $version
}

function Assert-SafeContainedDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ParentPath,

        [Parameter(Mandatory = $true)]
        [string]$ChildPath,

        [Parameter()]
        [switch]$CheckNestedReparsePoints
    )

    $separatorCharacters = [char[]]@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $parent = [System.IO.Path]::GetFullPath($ParentPath).TrimEnd($separatorCharacters)
    $child = [System.IO.Path]::GetFullPath($ChildPath).TrimEnd($separatorCharacters)
    $prefix = $parent + [System.IO.Path]::DirectorySeparatorChar
    if (-not $child.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing recursive deletion outside the expected parent directory."
    }

    $current = $child
    while ($true) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Refusing recursive deletion through a reparse point."
            }
        }
        if ($current.Equals($parent, [StringComparison]::OrdinalIgnoreCase)) { break }
        $current = [System.IO.Path]::GetDirectoryName($current)
        if ([string]::IsNullOrWhiteSpace($current)) {
            throw "Could not verify the recursive deletion path boundary."
        }
    }

    if ($CheckNestedReparsePoints -and (Test-Path -LiteralPath $child -PathType Container)) {
        $nestedReparsePoint = Get-ChildItem -LiteralPath $child -Force -Recurse |
            Where-Object {
                ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
            } |
            Select-Object -First 1
        if ($nestedReparsePoint) {
            throw "Refusing recursive deletion of a directory containing a reparse point."
        }
    }
}

Push-Location $repositoryRoot
try {
    $nodeVersion = Get-CheckedVersion "node" @("--version")
    if ($nodeVersion -notmatch '^v22\.') {
        throw "Packaging requires Node.js 22; the current Node.js version is $nodeVersion."
    }
    $nodeCommand = Get-Command node -ErrorAction Stop
    $nodeDirectory = Split-Path -Parent $nodeCommand.Source
    $npmCommand = if ($IsWindows) {
        Join-Path $nodeDirectory "npm.cmd"
    }
    else {
        "npm"
    }
    if ($IsWindows -and -not (Test-Path -LiteralPath $npmCommand -PathType Leaf)) {
        throw "The Node.js 22 installation does not include its matching npm.cmd."
    }
    $npmVersion = Get-CheckedVersion $npmCommand @("--version")
    $dotnetVersion = Get-CheckedVersion "dotnet" @("--version")
    if ($dotnetVersion -notmatch '^10\.') {
        throw "Packaging requires the .NET 10 SDK; the current version is $dotnetVersion."
    }

    $branch = (& git branch --show-current).Trim()
    if ($LASTEXITCODE -ne 0 -or $branch -ne "deploy/azure-staging") {
        throw "Run this package script from the deploy/azure-staging branch."
    }

    $workingTree = & git status --porcelain
    if ($LASTEXITCODE -ne 0) {
        throw "Could not read the Git working tree status."
    }
    if ($workingTree) {
        throw "Package only from a clean Git working tree."
    }

    $sourceCommit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sourceCommit)) {
        throw "Could not identify the source commit."
    }

    $unexpectedViteVariables = Get-ChildItem Env: |
        Where-Object {
            $_.Name.StartsWith("VITE_", [StringComparison]::OrdinalIgnoreCase)
            -and $_.Name -ne "VITE_API_BASE_URL"
        }
    if ($unexpectedViteVariables) {
        throw "Unset additional VITE_* process variables before packaging."
    }

    $artifactRoot = Join-Path $repositoryRoot "artifacts/azure-staging"
    $artifactDirectory = Join-Path $artifactRoot $sourceCommit
    $webRootSource = Join-Path $repositoryRoot "web"
    $nodeModulesDirectory = Join-Path $webRootSource "node_modules"
    $frontendBuild = Join-Path $webRootSource "dist"
    if (Test-Path -LiteralPath $artifactDirectory) {
        throw "The artifact directory already exists. Move or remove that ignored output before retrying."
    }

    $publishDirectory = Join-Path $artifactDirectory "publish"
    $emptyViteEnvironment = Join-Path $artifactDirectory "empty-vite-environment"
    $zipPath = Join-Path $artifactDirectory "unipm-azure-staging.zip"
    Assert-SafeContainedDirectory -ParentPath $artifactRoot -ChildPath $artifactDirectory
    Assert-SafeContainedDirectory -ParentPath $artifactDirectory -ChildPath $publishDirectory
    Assert-SafeContainedDirectory -ParentPath $artifactDirectory -ChildPath $emptyViteEnvironment
    Assert-SafeContainedDirectory -ParentPath $webRootSource -ChildPath $nodeModulesDirectory
    Assert-SafeContainedDirectory -ParentPath $webRootSource -ChildPath $frontendBuild
    New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $emptyViteEnvironment -Force | Out-Null

    $env:VITE_API_BASE_URL = "/"
    Push-Location (Join-Path $repositoryRoot "web")
    try {
        Invoke-CheckedNative $npmCommand @("ci", "--no-audit", "--no-fund")
        Invoke-CheckedNative $npmCommand @("run", "typecheck")
        Invoke-CheckedNative "node" @(
            "scripts/build-azure-staging.mjs",
            $emptyViteEnvironment)
    }
    finally {
        Pop-Location
    }

    Invoke-CheckedNative "dotnet" @(
        "publish",
        "server/server.csproj",
        "--configuration",
        "Release",
        "--output",
        $publishDirectory,
        "--self-contained",
        "false",
        "/p:UseAppHost=false",
        "/p:DebugType=None",
        "/p:DebugSymbols=false"
    )

    $webRoot = Join-Path $publishDirectory "wwwroot"
    $frontendIndex = Join-Path $frontendBuild "index.html"
    if (-not (Test-Path -LiteralPath $frontendIndex -PathType Leaf)) {
        throw "The Vite build did not produce web/dist/index.html."
    }

    New-Item -ItemType Directory -Path $webRoot -Force | Out-Null
    Copy-Item -Path (Join-Path $frontendBuild "*") -Destination $webRoot -Recurse -Force

    $developmentSettings = Join-Path $publishDirectory "appsettings.Development.json"
    if (Test-Path -LiteralPath $developmentSettings -PathType Leaf) {
        Remove-Item -LiteralPath $developmentSettings -Force
    }

    $localPhotoDirectory = Join-Path $publishDirectory "App_Data/InspectionPhotos"
    if (Test-Path -LiteralPath $localPhotoDirectory) {
        Assert-SafeContainedDirectory `
            -ParentPath $publishDirectory `
            -ChildPath $localPhotoDirectory `
            -CheckNestedReparsePoints
        Remove-Item -LiteralPath $localPhotoDirectory -Recurse -Force
    }

    if (-not (Test-Path -LiteralPath (Join-Path $publishDirectory "UniPM.Api.dll") -PathType Leaf)) {
        throw "The .NET publish output does not contain UniPM.Api.dll at its root."
    }

    $packageFiles = Get-ChildItem -LiteralPath $publishDirectory -File -Recurse -Force
    if ($packageFiles | Where-Object {
        $_.Extension -eq ".map"
        -or $_.Extension -eq ".pdb"
        -or $_.Name -match '^\.env(?:\.|$)'
        -or $_.Name -match '^appsettings\.Development\.json$'
        -or $_.Name -match '^usersecrets\.json$'
    }) {
        throw "The publish output contains a source map, development settings, environment file, or user-secrets file."
    }

    if ($packageFiles | Where-Object {
        $_.FullName.StartsWith($webRoot, [StringComparison]::OrdinalIgnoreCase)
        -and $_.Extension -eq ".map"
    }) {
        throw "The public frontend contains source maps."
    }

    $localApiReference = Get-ChildItem -LiteralPath $webRoot -File -Recurse -Force |
        Where-Object {
            Select-String -LiteralPath $_.FullName -Pattern '(?:localhost|127\.0\.0\.1):5000' -Quiet
        }
    if ($localApiReference) {
        throw "The frontend package contains the local API URL."
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $publishDirectory,
        $zipPath,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false)

    $checksum = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $zipName = Split-Path -Leaf $zipPath
    Set-Content -LiteralPath "$zipPath.sha256" -Value "$checksum  $zipName" -Encoding utf8

    $provenance = [ordered]@{
        sourceBranch = $branch
        sourceCommit = $sourceCommit
        createdUtc = [DateTime]::UtcNow.ToString("o")
        nodeVersion = $nodeVersion
        npmVersion = $npmVersion
        dotnetSdkVersion = $dotnetVersion
        frontendApiBase = "/"
        deploymentRuntime = ".NET 10 managed App Service runtime"
        publishMode = "framework-dependent portable"
        package = $zipName
        sha256 = $checksum
    }
    $provenance | ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath (Join-Path $artifactDirectory "provenance.json") -Encoding utf8

    Assert-SafeContainedDirectory `
        -ParentPath $artifactDirectory `
        -ChildPath $emptyViteEnvironment `
        -CheckNestedReparsePoints
    Remove-Item -LiteralPath $emptyViteEnvironment -Recurse -Force

    Write-Output "Package: $zipPath"
    Write-Output "SHA-256: $checksum"
    Write-Output "Provenance: $(Join-Path $artifactDirectory 'provenance.json')"
}
finally {
    if ($hadApiBaseUrl) {
        [Environment]::SetEnvironmentVariable("VITE_API_BASE_URL", $originalApiBaseUrl, "Process")
    }
    else {
        [Environment]::SetEnvironmentVariable("VITE_API_BASE_URL", $null, "Process")
    }
    Pop-Location
}
