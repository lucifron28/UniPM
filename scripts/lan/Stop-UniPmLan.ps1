[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'UniPmLan.Common.psm1') -Force

$state = Get-UniPmLanState
$problems = New-Object System.Collections.ArrayList
if ($null -ne $state.FirewallRules) {
    try {
        $removedRules = Remove-UniPmLanFirewallRules -State $state
        Write-Output "Removed $removedRules session-owned Private-profile firewall rule(s)."
    }
    catch {
        $null = $problems.Add("Firewall rules: $($_.Exception.Message)")
    }
}
foreach ($name in @('ApiRoot', 'WebRoot')) {
    $identity = $state.$name
    if ($null -eq $identity) {
        continue
    }
    try {
        $count = Stop-UniPmLanProcessTree -Identity $identity
        Write-Output "Stopped $count process(es) owned by the recorded $name process."
    }
    catch {
        $null = $problems.Add("${name}: $($_.Exception.Message)")
    }
}

if ($problems.Count -gt 0) {
    Write-Output 'Some session resources could not be verified or removed:'
    foreach ($problem in $problems) {
        Write-Output "- $problem"
    }
    throw "The session state was retained at $(Get-UniPmLanStatePath). Review it before retrying; no unverified process or firewall rule was changed."
}

$statePath = Get-UniPmLanStatePath
Remove-Item -LiteralPath $statePath -Force
Write-Output 'UniPM LAN processes were stopped and the active-session file was removed.'
Write-Output "Local logs were retained at $($state.LogDirectory)."
