[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Add', 'Remove')]
    [string]$Action,

    [Parameter(Mandatory = $true)]
    [switch]$ConfirmChange
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'UniPmLan.Common.psm1') -Force

$state = Get-UniPmLanState
if (-not $ConfirmChange) {
    throw 'Pass -ConfirmChange to explicitly approve this firewall operation.'
}

if ($Action -eq 'Add') {
    $network = Get-UniPmLanPrivateIPv4 -LanAddress ([string]$state.LanAddress)
    if ($network.PrefixLength -lt 16 -or $network.PrefixLength -gt 30) {
        throw 'Firewall rules require an active /16 through /30 private subnet.'
    }
    if ($PSCmdlet.ShouldProcess(
        "$($network.Subnet), TCP 5173 and 5254",
        'Add UniPM LAN access rules limited to the Private profile'
    )) {
        Add-UniPmLanFirewallRules -State $state
        Write-Output "Added two reversible Private-profile rules for TCP 5173 and 5254 from $($network.Subnet)."
        Write-Output 'SQL Server ports remain closed to other devices.'
    }
}
else {
    if ($null -eq $state.FirewallRules) {
        Write-Output 'No firewall rules are recorded for this LAN session.'
    }
    elseif ($PSCmdlet.ShouldProcess(
        [string]$state.FirewallRules.SessionId,
        'Remove recorded UniPM LAN firewall rules'
    )) {
        $count = Remove-UniPmLanFirewallRules -State $state
        Write-Output "Removed $count recorded firewall rule(s)."
    }
}
