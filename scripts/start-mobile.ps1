[CmdletBinding()]
param(
    [string]$ApiBaseUrl,
    [string]$DeviceId
)

$ErrorActionPreference = 'Stop'
$apiBaseUrlWasProvided = -not [string]::IsNullOrWhiteSpace($ApiBaseUrl)
if ($apiBaseUrlWasProvided) {
    $ApiBaseUrl = $ApiBaseUrl.Trim()
}

$flutterCommand = Get-Command flutter -ErrorAction SilentlyContinue
if ($null -eq $flutterCommand) {
    throw 'Flutter was not found on PATH. Install Flutter and open a new PowerShell window.'
}

$mobileRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'mobile'
if (-not (Test-Path -LiteralPath (Join-Path $mobileRoot 'pubspec.yaml'))) {
    throw "Flutter project was not found at '$mobileRoot'."
}

$adbCommand = Get-Command adb -ErrorAction SilentlyContinue
if ($null -eq $adbCommand) {
    throw 'ADB was not found on PATH. Install Android platform-tools and open a new PowerShell window.'
}

$adbOutput = & $adbCommand.Source devices
if ($LASTEXITCODE -ne 0) {
    throw "ADB device discovery failed with exit code $LASTEXITCODE."
}

$connectedDeviceIds = @(
    foreach ($line in $adbOutput) {
        if ([string]$line -match '^\s*(\S+)\s+device(?:\s|$)') {
            $Matches[1]
        }
    }
)

$selectedDeviceId = $null
if (-not [string]::IsNullOrWhiteSpace($DeviceId)) {
    $selectedDeviceId = $DeviceId.Trim()
    if ($connectedDeviceIds -notcontains $selectedDeviceId) {
        throw "ADB did not report device '$selectedDeviceId' in the connected state. Run 'adb devices' to check its status."
    }
}
elseif ($connectedDeviceIds.Count -gt 0) {
    $selectedDeviceId = [string]$connectedDeviceIds[0]
}

if ([string]::IsNullOrWhiteSpace($selectedDeviceId)) {
    throw "No connected Android device was found. Start an emulator or connect a phone, then run 'adb devices' to confirm it is ready."
}

if (-not $apiBaseUrlWasProvided) {
    if ($selectedDeviceId.StartsWith('emulator-', [StringComparison]::OrdinalIgnoreCase)) {
        $ApiBaseUrl = 'http://10.0.2.2:5254/'
    }
    else {
        $ApiBaseUrl = 'http://127.0.0.1:5254/'
        & $adbCommand.Source -s $selectedDeviceId reverse tcp:5254 tcp:5254
        if ($LASTEXITCODE -ne 0) {
            throw "ADB reverse setup failed with exit code $LASTEXITCODE."
        }
        Write-Host "Forwarded API port 5254 to Android device $selectedDeviceId."
    }
}

$parsedApiBaseUrl = $null
if (-not [Uri]::TryCreate($ApiBaseUrl, [UriKind]::Absolute, [ref]$parsedApiBaseUrl) -or
    [string]::IsNullOrWhiteSpace($parsedApiBaseUrl.Host) -or
    @('http', 'https') -notcontains $parsedApiBaseUrl.Scheme.ToLowerInvariant() -or
    -not [string]::IsNullOrEmpty($parsedApiBaseUrl.UserInfo)) {
    throw 'ApiBaseUrl must be an absolute HTTP or HTTPS URL without embedded credentials.'
}

$apiUriBuilder = [System.UriBuilder]::new($parsedApiBaseUrl)
if (-not $apiUriBuilder.Path.EndsWith('/')) {
    $apiUriBuilder.Path += '/'
}
$ApiBaseUrl = $apiUriBuilder.Uri.AbsoluteUri

$flutterArguments = @('run')
if (-not $selectedDeviceId.StartsWith('emulator-', [StringComparison]::OrdinalIgnoreCase)) {
    $flutterArguments += '--no-enable-impeller'
    Write-Host 'Using Flutter compatibility rendering for this physical Android device.'
}
$flutterArguments += @("--dart-define=UNIPM_API_BASE_URL=$ApiBaseUrl", '-d', $selectedDeviceId)
Write-Host "Starting UniPM Mobile on Android device $selectedDeviceId."

Push-Location -LiteralPath $mobileRoot
try {
    & $flutterCommand.Source @flutterArguments
    $flutterExitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}

if ($flutterExitCode -ne 0) {
    throw "flutter run failed with exit code $flutterExitCode."
}
