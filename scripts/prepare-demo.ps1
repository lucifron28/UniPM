[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($env:UNIPM_DEV_USER_PASSWORD)) {
    throw "Set UNIPM_DEV_USER_PASSWORD in the current process before preparing the demo."
}

if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__DefaultConnection)) {
    throw "Set ConnectionStrings__DefaultConnection to the dedicated demo database before preparing the demo."
}

$env:ASPNETCORE_ENVIRONMENT = "Development"

function Invoke-DotNetStep {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Demo preparation stopped because a dotnet command failed with exit code $LASTEXITCODE."
    }
}

Push-Location $repositoryRoot
try {
    Invoke-DotNetStep @("run", "--project", "server", "--", "--migrate-database")
    Invoke-DotNetStep @("run", "--project", "server", "--", "--seed-development-users")
    Invoke-DotNetStep @("run", "--project", "server", "--", "--seed-demo")
    Invoke-DotNetStep @(
        "run",
        "--project",
        "tools/UniPM.DemoQrGenerator",
        "--",
        "--output",
        "reference/demo/qr")

    Write-Host "UniPM fictional demo data and QR files are ready."
}
finally {
    Pop-Location
}
