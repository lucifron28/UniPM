Set-StrictMode -Version Latest

function Get-UniPmLanRepositoryRoot {
    (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
}

function Get-UniPmLanStatePath {
    Join-Path (Get-UniPmLanRepositoryRoot) 'artifacts\lan\active-session.json'
}

function Get-UniPmLanState {
    $path = Get-UniPmLanStatePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw 'No active UniPM LAN session was found. Start it with Start-UniPmLan.ps1 first.'
    }

    try {
        $state = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    }
    catch {
        throw 'The local UniPM LAN session state is unreadable. No processes were stopped.'
    }

    if ($state.SchemaVersion -ne 1 -or
        [string]::IsNullOrWhiteSpace([string]$state.SessionId) -or
        [string]::IsNullOrWhiteSpace([string]$state.LanAddress)) {
        throw 'The local UniPM LAN session state is incomplete. No processes were stopped.'
    }

    return $state
}

function Save-UniPmLanState {
    param([Parameter(Mandatory = $true)][object]$State)

    $path = Get-UniPmLanStatePath
    $directory = Split-Path -Parent $path
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $temporaryPath = "$path.$([Guid]::NewGuid().ToString('N')).tmp"
    $json = ConvertTo-Json -InputObject $State -Depth 8
    [System.IO.File]::WriteAllText($temporaryPath, $json, [System.Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporaryPath -Destination $path -Force
}

function Get-UniPmLanPrivateIPv4 {
    param([string]$LanAddress)

    $activeIndexes = @(Get-NetIPInterface -AddressFamily IPv4 -ErrorAction Stop |
        Where-Object {
            $_.ConnectionState -eq 'Connected'
        } |
        Select-Object -ExpandProperty InterfaceIndex -Unique)
    $privateProfileIndexes = @(Get-NetConnectionProfile -ErrorAction Stop |
        Where-Object { $_.NetworkCategory -eq 'Private' } |
        Select-Object -ExpandProperty InterfaceIndex -Unique)

    $candidates = @(Get-NetIPAddress -AddressFamily IPv4 -Type Unicast -ErrorAction Stop |
        Where-Object {
            $activeIndexes -contains $_.InterfaceIndex -and
            $privateProfileIndexes -contains $_.InterfaceIndex -and
            $_.AddressState -eq 'Preferred' -and
            (Test-UniPmLanRfc1918Address -Address ([string]$_.IPAddress))
        } |
        Sort-Object IPAddress -Unique)

    if (-not [string]::IsNullOrWhiteSpace($LanAddress)) {
        $selected = @($candidates | Where-Object { [string]::Equals(
            [string]$_.IPAddress,
            $LanAddress.Trim(),
            [StringComparison]::OrdinalIgnoreCase
        ) })
        if ($selected.Count -ne 1) {
            throw 'LanAddress must match an active RFC1918 IPv4 address on a Windows Private network profile.'
        }
    }
    else {
        if ($candidates.Count -eq 0) {
            throw 'No active RFC1918 IPv4 address on a Windows Private network profile was found. Connect to trusted Wi-Fi and set its Windows network profile to Private.'
        }
        if ($candidates.Count -gt 1) {
            $choices = ($candidates | ForEach-Object { [string]$_.IPAddress }) -join ', '
            throw "More than one active private IPv4 address was found: $choices. Pass -LanAddress with the trusted LAN address."
        }
        $selected = @($candidates[0])
    }

    $ip = [System.Net.IPAddress]::Parse([string]$selected[0].IPAddress)
    $prefixLength = [int]$selected[0].PrefixLength
    if ($prefixLength -lt 1 -or $prefixLength -gt 30) {
        throw 'The selected LAN interface has an unsuitable IPv4 subnet prefix.'
    }

    $addressBytes = $ip.GetAddressBytes()
    $networkBytes = New-Object byte[] 4
    $remainingBits = $prefixLength
    for ($index = 0; $index -lt 4; $index++) {
        $bits = [Math]::Max(0, [Math]::Min(8, $remainingBits))
        $mask = 0
        if ($bits -gt 0) {
            $mask = 256 - [int][Math]::Pow(2, 8 - $bits)
        }
        $networkBytes[$index] = [byte](([int]$addressBytes[$index]) -band $mask)
        $remainingBits -= $bits
    }

    [pscustomobject]@{
        Address      = [string]$selected[0].IPAddress
        PrefixLength = $prefixLength
        Subnet       = "$([System.Net.IPAddress]::new($networkBytes))/$prefixLength"
        InterfaceIndex = [int]$selected[0].InterfaceIndex
    }
}

function Test-UniPmLanRfc1918Address {
    param([Parameter(Mandatory = $true)][string]$Address)

    $parsed = $null
    if (-not [System.Net.IPAddress]::TryParse($Address, [ref]$parsed) -or
        $parsed.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetwork) {
        return $false
    }

    $bytes = $parsed.GetAddressBytes()
    return ($bytes[0] -eq 10) -or
        ($bytes[0] -eq 172 -and $bytes[1] -ge 16 -and $bytes[1] -le 31) -or
        ($bytes[0] -eq 192 -and $bytes[1] -eq 168)
}

function Get-UniPmLanConnectionString {
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $connectionString = [Environment]::GetEnvironmentVariable(
        'ConnectionStrings__DefaultConnection',
        'Process'
    )
    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        $dotenvPath = Join-Path $RepositoryRoot '.env'
        if (Test-Path -LiteralPath $dotenvPath -PathType Leaf) {
            foreach ($line in [System.IO.File]::ReadLines($dotenvPath)) {
                if ($line -notmatch '^\s*ConnectionStrings__DefaultConnection\s*=\s*(?<value>.*)$') {
                    continue
                }
                $connectionString = $Matches['value'].Trim()
                if ($connectionString.Length -ge 2 -and
                    (($connectionString[0] -eq '"' -and $connectionString[$connectionString.Length - 1] -eq '"') -or
                     ($connectionString[0] -eq "'" -and $connectionString[$connectionString.Length - 1] -eq "'"))) {
                    $connectionString = $connectionString.Substring(1, $connectionString.Length - 2)
                }
                break
            }
        }
    }

    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        throw 'Set a local ConnectionStrings__DefaultConnection in this PowerShell process or the ignored root .env file.'
    }

    return $connectionString
}

function Assert-UniPmLanLocalDatabase {
    param(
        [Parameter(Mandatory = $true)][string]$ConnectionString,
        [Parameter(Mandatory = $true)][string]$DatabaseName,
        [Parameter(Mandatory = $true)][string]$ConfirmDatabase
    )

    if (-not [string]::Equals($DatabaseName, 'UniPM_GSD_Validation', [StringComparison]::Ordinal)) {
        throw "DatabaseName must be the dedicated local validation database 'UniPM_GSD_Validation'."
    }
    if (-not [string]::Equals($DatabaseName, $ConfirmDatabase, [StringComparison]::Ordinal)) {
        throw 'ConfirmDatabase must exactly match DatabaseName.'
    }
    if ($DatabaseName -notmatch '^[A-Za-z0-9_.-]{1,128}$') {
        throw 'DatabaseName must be a simple SQL database name.'
    }

    try {
        $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($ConnectionString)
        $server = [string]$builder.DataSource
        $database = [string]$builder.InitialCatalog
    }
    catch {
        throw 'The local SQL connection setting is invalid; its value was not displayed.'
    }

    $serverValue = $server.Trim()
    if ($serverValue -match '^(?i)(tcp|lpc):') {
        $serverValue = $serverValue.Substring($serverValue.IndexOf(':') + 1)
    }
    $serverPart = $serverValue.Split(',')[0].Split('\')[0].Trim()
    $localNames = @('.', '(local)', '(localdb)', 'localhost', '127.0.0.1', '::1', '[::1]')
    $computerName = [Environment]::MachineName
    if (-not [string]::IsNullOrWhiteSpace($computerName)) {
        $localNames += $computerName
    }
    if ($localNames -notcontains $serverPart) {
        $builder.Clear()
        throw 'The LAN fallback accepts only a local SQL Server data source. Azure or other remote SQL targets are refused.'
    }
    if (-not [string]::Equals($database, $DatabaseName, [StringComparison]::OrdinalIgnoreCase)) {
        $builder.Clear()
        throw "The connection string database does not match the explicitly confirmed local database '$DatabaseName'."
    }
    if ([string]::IsNullOrWhiteSpace($database)) {
        $builder.Clear()
        throw 'The local SQL connection must name a dedicated database.'
    }

    $builder.Clear()
}

function Test-UniPmLanDatabaseConnection {
    param([Parameter(Mandatory = $true)][string]$ConnectionString)

    $connection = $null
    $command = $null
    try {
        $connection = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = 'SELECT 1'
        $result = $command.ExecuteScalar()
        if ([int]$result -ne 1) {
            throw 'Local database readiness query did not return 1.'
        }
    }
    catch {
        throw 'The local SQL readiness query failed. SQL client details were withheld; no database writes were attempted.'
    }
    finally {
        if ($null -ne $command) { $command.Dispose() }
        if ($null -ne $connection) { $connection.Dispose() }
    }
}

function Get-UniPmLanProcessIdentity {
    param([Parameter(Mandatory = $true)][int]$ProcessId)

    $rows = @(Get-CimInstance -ClassName Win32_Process -Filter "ProcessId = $ProcessId" -ErrorAction SilentlyContinue)
    if ($rows.Count -ne 1) {
        return $null
    }

    $row = $rows[0]
    if ([string]::IsNullOrWhiteSpace([string]$row.ExecutablePath) -or $null -eq $row.CreationDate) {
        return $null
    }
    try {
        if ($row.CreationDate -is [datetime]) {
            $startedUtc = $row.CreationDate.ToUniversalTime()
        }
        else {
            $startedUtc = [System.Management.ManagementDateTimeConverter]::ToDateTime(
                [string]$row.CreationDate
            ).ToUniversalTime()
        }
    }
    catch {
        return $null
    }

    [pscustomobject]@{
        ProcessId      = [int]$row.ProcessId
        ParentProcessId = [int]$row.ParentProcessId
        ExecutablePath = [string]$row.ExecutablePath
        StartedUtcTicks = [long]$startedUtc.Ticks
    }
}

function Test-UniPmLanIdentityMatch {
    param(
        [Parameter(Mandatory = $true)][object]$Actual,
        [Parameter(Mandatory = $true)][object]$Expected
    )

    return ([int]$Actual.ProcessId -eq [int]$Expected.ProcessId) -and
        ([long]$Actual.StartedUtcTicks -eq [long]$Expected.StartedUtcTicks) -and
        [string]::Equals(
            [string]$Actual.ExecutablePath,
            [string]$Expected.ExecutablePath,
            [StringComparison]::OrdinalIgnoreCase
        )
}

function Get-UniPmLanProcessTree {
    param([Parameter(Mandatory = $true)][object]$Identity)

    $rows = @(Get-CimInstance -ClassName Win32_Process -ErrorAction Stop |
        Select-Object ProcessId, ParentProcessId, ExecutablePath, CreationDate)
    $root = @($rows | Where-Object { [int]$_.ProcessId -eq [int]$Identity.ProcessId })
    if ($root.Count -ne 1) {
        throw 'The recorded process is no longer present. No process was stopped.'
    }

    $rootIdentity = Get-UniPmLanProcessIdentity -ProcessId ([int]$Identity.ProcessId)
    if ($null -eq $rootIdentity -or -not (Test-UniPmLanIdentityMatch -Actual $rootIdentity -Expected $Identity)) {
        throw 'The recorded process identity no longer matches. No process was stopped.'
    }

    $tree = New-Object System.Collections.ArrayList
    $null = $tree.Add([pscustomobject]@{
        ProcessId = [int]$Identity.ProcessId
        ParentProcessId = [int]$root[0].ParentProcessId
        ExecutablePath = [string]$root[0].ExecutablePath
        StartedUtcTicks = [long]$Identity.StartedUtcTicks
        Depth = 0
    })
    $knownDepth = @{}
    $knownDepth[[int]$Identity.ProcessId] = 0

    $changed = $true
    while ($changed) {
        $changed = $false
        foreach ($row in $rows) {
            $pidValue = [int]$row.ProcessId
            $parentValue = [int]$row.ParentProcessId
            if ($knownDepth.ContainsKey($pidValue) -or -not $knownDepth.ContainsKey($parentValue)) {
                continue
            }
            if ($null -eq $row.CreationDate -or [string]::IsNullOrWhiteSpace([string]$row.ExecutablePath)) {
                continue
            }
            try {
                if ($row.CreationDate -is [datetime]) {
                    $createdUtc = $row.CreationDate.ToUniversalTime()
                }
                else {
                    $createdUtc = [System.Management.ManagementDateTimeConverter]::ToDateTime(
                        [string]$row.CreationDate
                    ).ToUniversalTime()
                }
            }
            catch {
                continue
            }
            if ($createdUtc.Ticks -lt [long]$Identity.StartedUtcTicks) {
                continue
            }

            $depth = [int]$knownDepth[$parentValue] + 1
            $knownDepth[$pidValue] = $depth
            $null = $tree.Add([pscustomobject]@{
                ProcessId = $pidValue
                ParentProcessId = $parentValue
                ExecutablePath = [string]$row.ExecutablePath
                StartedUtcTicks = [long]$createdUtc.Ticks
                Depth = $depth
            })
            $changed = $true
        }
    }

    return @($tree | Sort-Object Depth -Descending)
}

function Stop-UniPmLanProcessTree {
    param([Parameter(Mandatory = $true)][object]$Identity)

    $tree = @(Get-UniPmLanProcessTree -Identity $Identity)
    $stopped = 0
    foreach ($entry in $tree) {
        $actual = Get-UniPmLanProcessIdentity -ProcessId ([int]$entry.ProcessId)
        if ($null -eq $actual) {
            continue
        }
        if (-not [string]::Equals(
            [string]$actual.ExecutablePath,
            [string]$entry.ExecutablePath,
            [StringComparison]::OrdinalIgnoreCase
        ) -or [long]$actual.StartedUtcTicks -ne [long]$entry.StartedUtcTicks) {
            continue
        }

        Stop-Process -Id ([int]$entry.ProcessId) -Force -ErrorAction Stop
        $stopped++
    }

    return $stopped
}

function Test-UniPmLanAdministrator {
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object System.Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-UniPmLanOwnedFirewallRule {
    param(
        [Parameter(Mandatory = $true)][object]$RuleRecord,
        [Parameter(Mandatory = $true)][object]$FirewallState
    )

    $rules = @(Get-NetFirewallRule -Name ([string]$RuleRecord.Name) -ErrorAction SilentlyContinue)
    if ($rules.Count -ne 1) {
        return $false
    }
    $rule = $rules[0]
    if ($rule.Group -ne [string]$FirewallState.Group -or
        $rule.Description -ne [string]$RuleRecord.Description -or
        $rule.Direction -ne 'Inbound' -or
        $rule.Action -ne 'Allow' -or
        $rule.Enabled -ne 'True' -or
        [string]$rule.Profile -ne 'Private') {
        return $false
    }

    $portFilter = Get-NetFirewallPortFilter -AssociatedNetFirewallRule $rule
    $addressFilter = Get-NetFirewallAddressFilter -AssociatedNetFirewallRule $rule
    $ports = @($portFilter.LocalPort | ForEach-Object { [string]$_ })
    $addresses = @($addressFilter.RemoteAddress | ForEach-Object { [string]$_ })
    return ([string]$portFilter.Protocol -eq 'TCP') -and
        ($ports.Count -eq 1) -and
        ([string]$ports[0] -eq [string]$RuleRecord.Port) -and
        ($addresses.Count -eq 1) -and
        ([string]$addresses[0] -eq [string]$FirewallState.Subnet)
}

function Add-UniPmLanFirewallRules {
    param([Parameter(Mandatory = $true)][object]$State)

    if (-not (Test-UniPmLanAdministrator)) {
        throw 'Run this operation from an elevated PowerShell window to change Windows Firewall.'
    }
    if ($null -ne $State.FirewallRules) {
        throw 'Firewall rules are already recorded for this LAN session.'
    }

    $network = Get-UniPmLanPrivateIPv4 -LanAddress ([string]$State.LanAddress)
    if ($network.PrefixLength -lt 16 -or $network.PrefixLength -gt 30) {
        throw 'Firewall rules require a local subnet between /16 and /30 to keep remote access narrow.'
    }
    if (-not [string]::Equals([string]$network.Subnet, [string]$State.LanSubnet, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The active LAN subnet changed after the service started. Restart the LAN session before configuring firewall rules.'
    }

    $group = 'UniPM GSD LAN Validation'
    $apiName = "UniPM-GSD-$($State.SessionId)-API"
    $webName = "UniPM-GSD-$($State.SessionId)-Web"
    $records = @(
        [pscustomobject]@{
            Name = $apiName
            DisplayName = "UniPM LAN API $($State.SessionId)"
            Description = "UniPM LAN helper session $($State.SessionId)"
            Port = 5254
        },
        [pscustomobject]@{
            Name = $webName
            DisplayName = "UniPM LAN Web $($State.SessionId)"
            Description = "UniPM LAN helper session $($State.SessionId)"
            Port = 5173
        }
    )
    foreach ($record in $records) {
        if (@(Get-NetFirewallRule -Name $record.Name -ErrorAction SilentlyContinue).Count -gt 0) {
            throw 'A firewall rule with this session name already exists; no rule was changed.'
        }
    }

    $created = New-Object System.Collections.ArrayList
    try {
        foreach ($record in $records) {
            $ruleParameters = @{
                Name = $record.Name
                DisplayName = $record.DisplayName
                Group = $group
                Description = $record.Description
                Direction = 'Inbound'
                Action = 'Allow'
                Profile = 'Private'
                Protocol = 'TCP'
                LocalPort = $record.Port
                RemoteAddress = $network.Subnet
            }
            New-NetFirewallRule @ruleParameters | Out-Null
            $null = $created.Add($record)
        }
    }
    catch {
        foreach ($record in $created) {
            $current = @(Get-NetFirewallRule -Name $record.Name -ErrorAction SilentlyContinue)
            if ($current.Count -eq 1 -and
                $current[0].Group -eq $group -and
                $current[0].Description -eq $record.Description) {
                Remove-NetFirewallRule -Name $record.Name -ErrorAction SilentlyContinue
            }
        }
        throw 'Firewall rule creation failed; any rules created by this operation were removed.'
    }

    $State.FirewallRules = [ordered]@{
        SessionId = [string]$State.SessionId
        Group = $group
        Subnet = [string]$network.Subnet
        Rules = $records
    }
    Save-UniPmLanState -State $State
}

function Remove-UniPmLanFirewallRules {
    param([Parameter(Mandatory = $true)][object]$State)

    if ($null -eq $State.FirewallRules) {
        return 0
    }
    if (-not (Test-UniPmLanAdministrator)) {
        throw 'Run Stop-UniPmLan.ps1 from an elevated PowerShell window to remove the session firewall rules.'
    }
    $firewallState = $State.FirewallRules
    if (-not [string]::Equals(
        [string]$firewallState.SessionId,
        [string]$State.SessionId,
        [StringComparison]::Ordinal
    )) {
        throw 'Firewall session identity does not match. No firewall rule was removed.'
    }

    $records = @($firewallState.Rules)
    foreach ($record in $records) {
        if (-not (Test-UniPmLanOwnedFirewallRule -RuleRecord $record -FirewallState $firewallState)) {
            throw "Firewall rule $($record.Name) no longer matches the recorded private-subnet rule. Review it manually; no firewall rules were removed."
        }
    }
    foreach ($record in $records) {
        Remove-NetFirewallRule -Name ([string]$record.Name) -ErrorAction Stop
    }

    $State.FirewallRules = $null
    Save-UniPmLanState -State $State
    return $records.Count
}

Export-ModuleMember -Function Get-UniPmLanRepositoryRoot, Get-UniPmLanStatePath, Get-UniPmLanState, Save-UniPmLanState, Get-UniPmLanPrivateIPv4, Test-UniPmLanRfc1918Address, Get-UniPmLanConnectionString, Assert-UniPmLanLocalDatabase, Test-UniPmLanDatabaseConnection, Get-UniPmLanProcessIdentity, Test-UniPmLanIdentityMatch, Get-UniPmLanProcessTree, Stop-UniPmLanProcessTree, Test-UniPmLanAdministrator, Add-UniPmLanFirewallRules, Remove-UniPmLanFirewallRules
