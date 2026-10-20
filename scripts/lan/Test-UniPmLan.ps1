[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'UniPmLan.Common.psm1') -Force

$state = Get-UniPmLanState
$expectedApiUrl = "http://$([string]$state.LanAddress):$([int]$state.ApiPort)"
$expectedWebUrl = "http://$([string]$state.LanAddress):$([int]$state.WebPort)"

if (-not (Test-UniPmLanRfc1918Address -Address ([string]$state.LanAddress)) -or
    -not [string]::Equals([string]$state.ApiUrl, $expectedApiUrl, [StringComparison]::Ordinal) -or
    -not [string]::Equals([string]$state.WebUrl, $expectedWebUrl, [StringComparison]::Ordinal) -or
    -not [string]::Equals([string]$state.ViteApiBaseUrl, $expectedApiUrl, [StringComparison]::Ordinal) -or
    [int]$state.ApiPort -ne 5254 -or
    [int]$state.WebPort -ne 5173) {
    throw 'The saved LAN URLs do not match the selected private IP and expected ports.'
}

$network = Get-UniPmLanPrivateIPv4 -LanAddress ([string]$state.LanAddress)
$apiIdentity = Get-UniPmLanProcessIdentity -ProcessId ([int]$state.ApiRoot.ProcessId)
$webIdentity = Get-UniPmLanProcessIdentity -ProcessId ([int]$state.WebRoot.ProcessId)
if ($null -eq $apiIdentity -or -not (Test-UniPmLanIdentityMatch -Actual $apiIdentity -Expected $state.ApiRoot)) {
    throw 'The recorded API process is no longer running with its original identity.'
}
if ($null -eq $webIdentity -or -not (Test-UniPmLanIdentityMatch -Actual $webIdentity -Expected $state.WebRoot)) {
    throw 'The recorded web process is no longer running with its original identity.'
}

function Get-UniPmLanHttpResponse {
    param([Parameter(Mandatory = $true)][string]$Url)

    try {
        return Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 8
    }
    catch {
        throw "Readiness request failed for $Url. Check the saved local logs and Windows Firewall."
    }
}

$live = Get-UniPmLanHttpResponse -Url "$expectedApiUrl/health/live"
if ([int]$live.StatusCode -ne 200) {
    throw 'The API liveness endpoint did not return HTTP 200.'
}
$ready = Get-UniPmLanHttpResponse -Url "$expectedApiUrl/health/ready"
if ([int]$ready.StatusCode -ne 200) {
    throw 'The API readiness endpoint did not return HTTP 200.'
}
$web = Get-UniPmLanHttpResponse -Url "$expectedWebUrl/"
if ([int]$web.StatusCode -ne 200) {
    throw 'The frontend root did not return HTTP 200.'
}
$envModule = Get-UniPmLanHttpResponse -Url "$expectedWebUrl/src/config/env.ts"
if (-not ([string]$envModule.Content).Contains($expectedApiUrl)) {
    throw 'The Vite-served environment module does not point at the selected LAN API URL.'
}

Write-Output 'UniPM LAN readiness passed from this laptop.'
Write-Output "Selected private LAN address: $($network.Address) ($($network.Subnet))"
Write-Output "Web: $expectedWebUrl"
Write-Output "API: $expectedApiUrl"
Write-Output 'API /health/live, API /health/ready, frontend root, and Vite API URL all passed.'
Write-Warning 'This check runs on the laptop. Verify access from an Android phone on the same trusted Wi-Fi; Windows Firewall or Wi-Fi client isolation may block it. Never open SQL Server to the LAN.'
