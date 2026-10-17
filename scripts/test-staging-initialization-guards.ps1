<# Local, credential-free verification. Parses the initializer but never runs its SQL/seed path. #>
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'initialize-azure-staging-demo.ps1'
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw 'Initializer PowerShell syntax failed.' }
$credentialFunction = $ast.Find({ param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
    $node.Name -eq 'Get-StagingCredentialEnvironment'
}, $true)
. ([scriptblock]::Create($credentialFunction.Extent.Text))
$names = @('UNIPM_STAGING_DEMO_PASSWORD', 'UNIPM_STAGING_ADMIN_PASSWORD',
    'UNIPM_STAGING_GSD_PASSWORD', 'UNIPM_STAGING_INSPECTOR_PASSWORD', 'UNIPM_STAGING_SUPERVISOR_PASSWORD')
$saved = @{}
$checks = 0
function Expect-Rejection {
    param([scriptblock]$Action)
    $rejected = $false
    try { $null = & $Action } catch { $rejected = $true }
    if (-not $rejected) { throw 'An unsafe credential state was accepted.' }
}
try {
    foreach ($name in $names) {
        $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
    Expect-Rejection { Get-StagingCredentialEnvironment $false }; $checks++
    [Environment]::SetEnvironmentVariable($names[0], ('Aa1!' + [Guid]::NewGuid().ToString('N')), 'Process')
    Expect-Rejection { Get-StagingCredentialEnvironment $false }; $checks++
    $shared = Get-StagingCredentialEnvironment $true
    if ($shared.Count -ne 1) { throw 'Shared mode returned an unexpected credential count.' }; $checks++
    foreach ($name in $names[1..4]) {
        [Environment]::SetEnvironmentVariable($name, ('Aa1!' + [Guid]::NewGuid().ToString('N')), 'Process')
    }
    Expect-Rejection { Get-StagingCredentialEnvironment $true }; $checks++
    $distinct = Get-StagingCredentialEnvironment $false
    if ($distinct.Count -ne 4) { throw 'Distinct mode returned an unexpected credential count.' }; $checks++
    [Environment]::SetEnvironmentVariable($names[1], $distinct[$names[2]], 'Process')
    Expect-Rejection { Get-StagingCredentialEnvironment $false }; $checks++
    [Environment]::SetEnvironmentVariable($names[1], 'invalid', 'Process')
    Expect-Rejection { Get-StagingCredentialEnvironment $false }; $checks++
    Expect-Rejection { & $scriptPath -ConfirmTarget 'wrong-target' }; $checks++
    $text = $ast.Extent.Text
    if ($text -notmatch "UNIPM_STAGING_DEMO_INITIALIZATION.*= 'true'" -or
        $text -notmatch 'UNIPM_STAGING_ALLOW_SHARED_PASSWORD' -or
        $text -notmatch 'Expected exactly 22 application tables' -or
        $text -notmatch 'APP STOPPED' -or
        $text -notmatch 'TrustServerCertificate' -or
        $text -notmatch '\$childEnvironment.Clear\(\)' -or
        $text -match "-Command '--(reset|migrate)") { throw 'Initializer safety contract changed.' }; $checks++
    if ($text.IndexOf("-Command '--seed-development-users'") -gt $text.IndexOf("-Command '--seed-demo'")) {
        throw 'Seed command order changed.'
    }; $checks++
    Write-Output "PASS: Windows PowerShell syntax and $checks local credential/safety checks. No SQL or seed process invoked."
}
finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
    if ($null -ne (Get-Variable shared -ErrorAction SilentlyContinue)) { $shared.Clear() }
    if ($null -ne (Get-Variable distinct -ErrorAction SilentlyContinue)) { $distinct.Clear() }
    $saved.Clear()
}
