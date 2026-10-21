[CmdletBinding()]
param(
    [string]$DeviceId
)

$ErrorActionPreference = 'Stop'
$apiBaseUrl = 'https://unipm-staging.yellowdesert-fc1789ae.japaneast.azurecontainerapps.io/'

Write-Host "Using the deployed UniPM backend: $apiBaseUrl"
& (Join-Path $PSScriptRoot 'start-mobile.ps1') -ApiBaseUrl $apiBaseUrl -DeviceId $DeviceId
