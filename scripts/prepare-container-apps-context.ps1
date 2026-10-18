[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

function Invoke-RepositoryGit {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $output = & git -C $repositoryRoot @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Could not verify the repository state."
    }
    return ($output -join "`n").Trim()
}

function Assert-SafeArchivePath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $normalized = $Path.Replace("\", "/")
    if ([string]::IsNullOrWhiteSpace($normalized) -or
        $normalized.StartsWith("/", [StringComparison]::Ordinal) -or
        $normalized -match "^[A-Za-z]:" -or
        $normalized -match "[\x00-\x1f]") {
        throw "The verified package contains an unsafe path."
    }

    $relativePath = $normalized.TrimEnd("/")
    $segments = $relativePath.Split("/")
    foreach ($segment in $segments) {
        if ([string]::IsNullOrWhiteSpace($segment) -or
            $segment -in @(".", "..") -or
            $segment -match '[:*?"<>|]' -or
            $segment.EndsWith(".", [StringComparison]::Ordinal) -or
            $segment.EndsWith(" ", [StringComparison]::Ordinal)) {
            throw "The verified package contains an unsafe path."
        }
    }

    $leaf = $segments[$segments.Length - 1]
    $extension = [System.IO.Path]::GetExtension($leaf)
    if ($leaf -match '^\.env(?:\..*)?$' -or
        $leaf -match '^appsettings\.(?!json$).+\.json$' -or
        $leaf -match '^usersecrets\.json$' -or
        $leaf -match '^(?:secrets?|credentials?)(?:\..*)?$' -or
        $leaf -match '^nuget\.config$' -or
        $extension -in @(".map", ".pdb", ".pfx", ".p12", ".pem", ".key", ".crt", ".cer", ".user", ".suo")) {
        throw "The verified package contains a forbidden settings, secret, map, symbol, or key file."
    }

    return $relativePath
}

function Assert-NoSensitiveSettings {
    param([Parameter()][object]$Value)

    if ($null -eq $Value) { return }
    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        foreach ($property in $Value.PSObject.Properties) {
            if ($property.Value -is [string] -and
                -not [string]::IsNullOrWhiteSpace($property.Value) -and
                $property.Name -match '(?i)(connection.?string|defaultconnection|password|\bpwd\b|secret|token|api.?key|signing.?key|account.?key|sas)') {
                throw "The published application settings contain a populated credential-like value."
            }
            Assert-NoSensitiveSettings -Value $property.Value
        }
    }
    elseif ($Value -is [System.Collections.IDictionary]) {
        foreach ($key in $Value.Keys) {
            $item = $Value[$key]
            if ($item -is [string] -and
                -not [string]::IsNullOrWhiteSpace($item) -and
                [string]$key -match '(?i)(connection.?string|defaultconnection|password|\bpwd\b|secret|token|api.?key|signing.?key|account.?key|sas)') {
                throw "The published application settings contain a populated credential-like value."
            }
            Assert-NoSensitiveSettings -Value $item
        }
    }
    elseif ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
        foreach ($item in $Value) { Assert-NoSensitiveSettings -Value $item }
    }
}

$branch = Invoke-RepositoryGit @("branch", "--show-current")
if ($branch -notin @("feature/container-apps-staging-preparation", "deploy/azure-staging")) {
    throw "Run this script only from the preparation or staging deployment branch."
}
if (Invoke-RepositoryGit @("status", "--porcelain")) {
    throw "Prepare a context only from a clean Git working tree."
}
$sourceCommit = Invoke-RepositoryGit @("rev-parse", "HEAD")
if ($sourceCommit -notmatch '^[0-9a-f]{40}$') {
    throw "The current Git commit could not be verified."
}

$packageDirectory = Join-Path $repositoryRoot "artifacts/azure-staging/$sourceCommit"
$zipPath = Join-Path $packageDirectory "unipm-azure-staging.zip"
$provenancePath = Join-Path $packageDirectory "provenance.json"
$checksumPath = "$zipPath.sha256"
foreach ($requiredPath in @($zipPath, $provenancePath, $checksumPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "The package for the current commit is incomplete."
    }
}

$provenance = Get-Content -LiteralPath $provenancePath -Raw | ConvertFrom-Json
if ($provenance.sourceCommit -ne $sourceCommit -or
    $provenance.sourceBranch -ne "deploy/azure-staging" -or
    $provenance.package -ne "unipm-azure-staging.zip" -or
    $provenance.sha256 -notmatch '^[0-9a-f]{64}$') {
    throw "Package provenance does not match the current staging commit."
}
$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$sidecar = (Get-Content -LiteralPath $checksumPath -Raw).Trim()
if ($zipHash -ne $provenance.sha256 -or
    $sidecar -notmatch ('^' + [regex]::Escape($zipHash) + '\s+unipm-azure-staging\.zip$')) {
    throw "The package checksum does not match its provenance and checksum file."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
$files = New-Object System.Collections.ArrayList
$seenPaths = @{}
$requiredFiles = @("UniPM.Api.dll", "UniPM.Api.deps.json", "UniPM.Api.runtimeconfig.json", "wwwroot/index.html")
try {
    foreach ($entry in $archive.Entries) {
        $relativePath = Assert-SafeArchivePath -Path $entry.FullName
        if ($entry.FullName.EndsWith("/", [StringComparison]::Ordinal) -or
            $entry.FullName.EndsWith("\", [StringComparison]::Ordinal)) { continue }
        if ($seenPaths.ContainsKey($relativePath)) {
            throw "The verified package contains duplicate paths."
        }
        $seenPaths[$relativePath] = $true
        [void]$files.Add([pscustomobject]@{ Entry = $entry; Path = $relativePath })
    }
    foreach ($requiredFile in $requiredFiles) {
        if (-not $seenPaths.ContainsKey($requiredFile)) {
            throw "The verified package is missing a required application file."
        }
    }

    $artifactRoot = Join-Path $repositoryRoot "artifacts"
    $contextParent = Join-Path $artifactRoot "container-apps-context"
    $contextRoot = Join-Path $contextParent $sourceCommit
    foreach ($directory in @($artifactRoot, $contextParent)) {
        if (Test-Path -LiteralPath $directory) {
            $item = Get-Item -LiteralPath $directory -Force
            if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Refusing to write a context through a reparse point."
            }
        }
    }
    if (Test-Path -LiteralPath $contextRoot) {
        throw "The context path already exists. Move it aside manually before retrying."
    }
    $contextFullPath = [System.IO.Path]::GetFullPath($contextRoot)
    $artifactPrefix = [System.IO.Path]::GetFullPath($artifactRoot).TrimEnd("\") + "\"
    if (-not $contextFullPath.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to write outside the ignored artifacts directory."
    }

    New-Item -ItemType Directory -Path (Join-Path $contextRoot "app") -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "container-apps/Dockerfile") -Destination (Join-Path $contextRoot "Dockerfile")
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "container-apps/.dockerignore") -Destination (Join-Path $contextRoot ".dockerignore")
    $appPath = Join-Path $contextRoot "app"
    $appPrefix = [System.IO.Path]::GetFullPath($appPath).TrimEnd("\") + "\"
    $manifestFiles = New-Object System.Collections.ArrayList
    foreach ($file in $files) {
        $destination = [System.IO.Path]::GetFullPath((Join-Path $appPath ($file.Path.Replace("/", "\"))))
        if (-not $destination.StartsWith($appPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to extract a package entry outside the app directory."
        }
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        $inputStream = $file.Entry.Open()
        try {
            $outputStream = [System.IO.File]::Open($destination, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
            try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose() }
        }
        finally { $inputStream.Dispose() }
        [void]$manifestFiles.Add([pscustomobject]@{
            path = $file.Path
            length = (Get-Item -LiteralPath $destination).Length
            sha256 = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
        })
    }

    $settingsPath = Join-Path $appPath "appsettings.json"
    if (Test-Path -LiteralPath $settingsPath -PathType Leaf) {
        try { $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json }
        catch { throw "The published application settings are not valid JSON." }
        Assert-NoSensitiveSettings -Value $settings
    }

    $contextManifest = [ordered]@{
        sourceCommit = $sourceCommit
        packageSha256 = $zipHash
        package = "unipm-azure-staging.zip"
        files = @($manifestFiles | Sort-Object path)
    }
    $contextManifest | ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath (Join-Path $contextRoot "publish-manifest.json") -Encoding UTF8
}
finally {
    $archive.Dispose()
}

Write-Output "Context: $contextRoot"
Write-Output "Source commit: $sourceCommit"
Write-Output "Package SHA-256: $zipHash"
Write-Output "Published files extracted: $($files.Count)"
