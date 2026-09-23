[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string]$ApiBaseUrl = 'http://localhost:5254',

    [ValidateRange(1, 65535)]
    [int]$Port = 5173
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$webDirectory = Join-Path $repositoryRoot 'web'

$parsedUrl = $null
if (-not [Uri]::TryCreate($ApiBaseUrl, [UriKind]::Absolute, [ref]$parsedUrl) -or
    $parsedUrl.Scheme -notin @('http', 'https') -or
    -not [string]::IsNullOrEmpty($parsedUrl.UserInfo)) {
    throw 'ApiBaseUrl must be an absolute HTTP or HTTPS URL without credentials.'
}

if (-not (Test-Path -LiteralPath (Join-Path $webDirectory 'node_modules'))) {
    throw 'Web dependencies are missing. Run npm ci from web/ once, then rerun this script.'
}

$env:VITE_API_BASE_URL = $ApiBaseUrl.TrimEnd('/')

Push-Location $webDirectory
try {
    & npm.cmd run dev -- --host localhost --port $Port
    if ($LASTEXITCODE -ne 0) {
        throw "The web server exited with code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
