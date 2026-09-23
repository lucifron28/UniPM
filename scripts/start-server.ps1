[CmdletBinding()]
param(
    [ValidateRange(1, 65535)]
    [int]$Port = 5254,

    [ValidateNotNullOrEmpty()]
    [string]$WebOrigin = 'http://localhost:5173'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $repositoryRoot '.env'
$localSettings = @(
    'ConnectionStrings__DefaultConnection',
    'UNIPM_JWT_ISSUER',
    'UNIPM_JWT_AUDIENCE',
    'UNIPM_JWT_SIGNING_KEY',
    'UNIPM_JWT_ACCESS_TOKEN_MINUTES'
)

if (Test-Path -LiteralPath $envPath) {
    foreach ($line in [System.IO.File]::ReadLines($envPath)) {
        if ($line -notmatch '^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>.*)$') {
            continue
        }

        $name = $Matches['name']
        if ($localSettings -notcontains $name -or
            -not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name, 'Process'))) {
            continue
        }

        $value = $Matches['value'].Trim()
        if ($value.Length -ge 2 -and
            (($value[0] -eq '"' -and $value[-1] -eq '"') -or
             ($value[0] -eq "'" -and $value[-1] -eq "'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        [Environment]::SetEnvironmentVariable($name, $value, 'Process')
    }
}

if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__DefaultConnection)) {
    throw 'Set ConnectionStrings__DefaultConnection in the process or the ignored root .env before starting the API.'
}

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = "http://localhost:$Port"
$env:UNIPM_WEB_ORIGIN = $WebOrigin

Push-Location $repositoryRoot
try {
    & dotnet run --project .\server --no-launch-profile
    if ($LASTEXITCODE -ne 0) {
        throw "The API exited with code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
