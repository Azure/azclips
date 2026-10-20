[CmdletBinding()]
param(
    [ValidateSet('All', 'Cli', 'PowerShell', 'Delete', 'Cleanup')][string]$Phase = 'All',
    [Parameter(Mandatory)][string]$VaultName,
    [Parameter(Mandatory)][string]$SubscriptionId,
    [Parameter(Mandatory)][Alias('Profile')][string]$ProfileName,
    [Parameter(Mandatory)][string]$CliAssembly,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [string]$Dotnet = 'dotnet',
    [string]$LegacyAz = 'az',
    [switch]$ResumeCliReads,
    [switch]$KeepTestSecrets
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$null = New-Item -ItemType Directory -Path $EvidenceDirectory -Force
$statePath = Join-Path $EvidenceDirectory 'state.json'
$results = [Collections.Generic.List[object]]::new()
$utf8 = [Text.UTF8Encoding]::new($false)
$state = $null
$completed = $false

function Save-State {
    [IO.File]::WriteAllText($statePath, ($state | ConvertTo-Json -Depth 10), $utf8)
}

function Read-State {
    $loaded = Get-Content $statePath -Raw | ConvertFrom-Json -AsHashtable
    if ($loaded.schemaVersion -ne 2 -or $loaded.vault -cne $VaultName -or
        $loaded.subscription -cne $SubscriptionId -or $loaded.runId -notmatch '^[a-f0-9]{32}$') {
        throw 'Test state does not match this vault/subscription or supported harness version.'
    }
    $expected = @{
        cli = "clips-cli-$($loaded.runId)"; cliExtra = "clips-cli-extra-$($loaded.runId)"
        ps = "clips-ps-$($loaded.runId)"; psExtra = "clips-ps-extra-$($loaded.runId)"
    }
    foreach ($key in $expected.Keys) {
        if ($loaded[$key] -cne $expected[$key]) { throw 'Test state contains an unexpected secret name.' }
    }
    $allowed = @($expected.Values) + "$($expected.ps)-whatif"
    foreach ($name in $loaded.cleanupNames) {
        if ($name -cnotin $allowed) { throw 'Cleanup manifest contains a secret not owned by this run.' }
    }
    return $loaded
}

function Assert-NativeResult([string]$Name, $Result, [string]$ExpectedError) {
    if ($ExpectedError) {
        Assert-Check "$Name fails for the expected reason" ($Result.Code -ne 0 -and $Result.Error -match $ExpectedError)
    }
    else {
        Assert-Check "$Name succeeds" ($Result.Code -eq 0)
    }
}

function Assert-Check([string]$Name, [bool]$Condition) {
    if (-not $Condition) { throw "FAIL: $Name" }
    $results.Add(@{ name = $Name; result = 'pass'; time = [DateTimeOffset]::UtcNow.ToString('O') })
    Write-Host "PASS: $Name"
}

function Invoke-Native([string]$Executable, [string[]]$Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new($Executable)
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(120000)) {
            $process.Kill($true)
            throw 'Native test command timed out after 120 seconds.'
        }
        return @{ Code = $process.ExitCode; Output = $stdout.GetAwaiter().GetResult(); Error = $stderr.GetAwaiter().GetResult() }
    }
    finally { $process.Dispose() }
}

function Invoke-Clips([string]$Name, [string[]]$Arguments, [string]$ExpectedError) {
    $result = Invoke-Native $Dotnet (@($CliAssembly, 'keyvault', 'secret') + $Arguments + @('--vault-name', $VaultName, '--profile', $ProfileName, '--output', 'json'))
    $evidenceOutput = if ($Arguments -contains '--as-plain-text') { '[Plaintext output compared in memory; not retained.]' } else { $result.Output }
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory "$Name.stdout"), $evidenceOutput, $utf8)
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory "$Name.stderr"), $result.Error, $utf8)
    Assert-NativeResult $Name $result $ExpectedError
    if ($ExpectedError) {
        return $result
    }
    if ($Arguments -contains '--as-plain-text') {
        if ($result.Output.EndsWith([Environment]::NewLine)) {
            return $result.Output.Substring(0, $result.Output.Length - [Environment]::NewLine.Length)
        }
        return $result.Output
    }
    return ConvertFrom-Json -InputObject $result.Output -AsHashtable -NoEnumerate
}

function Invoke-Legacy([string]$Name, [string[]]$Arguments, [string]$ExpectedError) {
    $result = Invoke-Native $LegacyAz (@('keyvault', 'secret') + $Arguments + @('--vault-name', $VaultName, '--subscription', $SubscriptionId, '--only-show-errors', '-o', 'json'))
    Assert-NativeResult $Name $result $ExpectedError
    if ($ExpectedError) { return }
    return ConvertFrom-Json -InputObject $result.Output -AsHashtable -NoEnumerate
}

function Register-TestSecret([string]$Name) {
    $allowed = @($state.cli, $state.cliExtra, $state.ps, $state.psExtra, "$($state.ps)-whatif")
    if ($Name -cnotin $allowed) { throw 'Refusing to register a secret outside this run.' }
    if ($Name -cin $state.cleanupNames) { return }
    Invoke-Legacy "$Name does not already exist" @('show', '--name', $Name, '--query', 'id') -ExpectedError '\(SecretNotFound\)'
    $state.cleanupNames = @($state.cleanupNames) + $Name
    Save-State
}

function Remove-RunSecrets {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    if ($null -eq $state) { return }
    $validated = Read-State
    $remaining = @()
    foreach ($name in @($validated.cleanupNames)) {
        try {
            if (-not $PSCmdlet.ShouldProcess("$VaultName/$name", 'Soft-delete run-owned test secret')) {
                $remaining += $name
                continue
            }
            $common = @('--vault-name', $VaultName, '--subscription', $SubscriptionId, '--only-show-errors', '-o', 'json')
            $deleted = Invoke-Native $LegacyAz (@('keyvault', 'secret', 'delete', '--name', $name, '--query', 'id') + $common)
            if ($deleted.Code -ne 0 -and $deleted.Error -notmatch '\(SecretNotFound\)') { throw 'Test secret cleanup failed.' }
        }
        catch {
            $remaining += $name
            Write-Warning "Cleanup incomplete for $name. Rerun with -Phase Cleanup and the same evidence directory."
        }
    }
    $state.cleanupNames = $remaining
    Save-State
    if ($remaining.Count) { throw 'Some test secrets remain. Their names are recorded in state.json; nothing was purged.' }
    foreach ($fixture in @('cli-v1', 'cli-v2', 'ps-v1', 'ps-v2')) {
        $file = Join-Path $EvidenceDirectory "$fixture.txt"
        if (Test-Path $file) { Remove-Item -LiteralPath $file -Force }
    }
    Assert-Check 'Run-owned secrets cleaned up without purge' $true
}

function Read-Fixture([string]$Name) { [IO.File]::ReadAllText((Join-Path $EvidenceDirectory "$Name.txt"), $utf8) }

function Assert-Metadata([string]$Name, $Metadata) {
    $json = ConvertTo-Json -InputObject $Metadata -Depth 20 -Compress
    Assert-Check "$Name has no value property or fixture data" ($json -notmatch '"value"\s*:' -and $json -notmatch 'clips-live-')
}

function Import-TestModule {
    Import-Module (Join-Path $repoRoot 'src/PS/Accounts/Accounts.psd1') -ErrorAction Stop
    Import-Module (Join-Path $repoRoot 'src/PS/KeyVault/KeyVault.psd1') -ErrorAction Stop
    $expectedCmdlets = @('Set-AzKeyVaultSecret', 'Get-AzKeyVaultSecret', 'Update-AzKeyVaultSecret', 'Remove-AzKeyVaultSecret', 'Undo-AzKeyVaultSecretRemoval')
    $actualCmdlets = @(Get-Command -Module KeyVault -Name '*AzKeyVaultSecret*').Name
    Assert-Check 'PowerShell module exports the exact secret commands' ((($actualCmdlets | Sort-Object) -join ',') -ceq (($expectedCmdlets | Sort-Object) -join ','))
}

try {
    if ($Phase -in @('All', 'Cli')) {
        if ($ResumeCliReads) {
            $state = Read-State
        }
        else {
        if (Test-Path $statePath) { throw 'Use a new evidence directory for a new live run.' }
        $runId = [Guid]::NewGuid().ToString('N')
        $state = @{
            schemaVersion = 2; runId = $runId; vault = $VaultName; subscription = $SubscriptionId; cleanupNames = @()
            cli = "clips-cli-$runId"; cliExtra = "clips-cli-extra-$runId"; ps = "clips-ps-$runId"; psExtra = "clips-ps-extra-$runId"
        }
        Save-State
        foreach ($fixture in @('cli-v1', 'cli-v2', 'ps-v1', 'ps-v2')) {
            $value = 'clips-live-' + [Guid]::NewGuid().ToString('N') + "`"-utf8-" + [char]0x00e9 + "`n"
            [IO.File]::WriteAllText((Join-Path $EvidenceDirectory "$fixture.txt"), $value, $utf8)
        }
        Register-TestSecret $state.cli
        Register-TestSecret $state.cliExtra
        $created = Invoke-Clips 'cli-set-v1' @('set', '--name', $state.cli, '--file', (Join-Path $EvidenceDirectory 'cli-v1.txt'), '--content-type', 'text/plain', '--tags', 'phase=live', '--debug')
        Assert-Metadata 'CLI set' $created
        $state.cliVersion1 = ([string]$created.id).Split('/')[-1]
        $null = Invoke-Clips 'cli-set-extra' @('set', '--name', $state.cliExtra, '--file', (Join-Path $EvidenceDirectory 'cli-v1.txt'))
        $second = Invoke-Clips 'cli-set-v2' @('set', '--name', $state.cli, '--file', (Join-Path $EvidenceDirectory 'cli-v2.txt'))
        $state.cliVersion2 = ([string]$second.id).Split('/')[-1]
        Assert-Check 'CLI set creates distinct versions' ($state.cliVersion1 -ne $state.cliVersion2)
        Save-State
        }
        $shown = Invoke-Clips 'cli-show-metadata' @('show', '--name', $state.cli, '--debug')
        Assert-Metadata 'CLI show' $shown
        $plain = Invoke-Clips 'cli-show-plaintext' @('show', '--name', $state.cli, '--as-plain-text', '--debug')
        Assert-Check 'CLI latest plaintext preserves UTF-8 and newline' ($plain -ceq (Read-Fixture 'cli-v2'))
        $old = Invoke-Clips 'cli-show-version1' @('show', '--name', $state.cli, '--version', $state.cliVersion1, '--as-plain-text')
        Assert-Check 'CLI explicit version retrieves the original value' ($old -ceq (Read-Fixture 'cli-v1'))
        $independent = Invoke-Legacy 'Azure CLI verifies CLI secret' @('show', '--name', $state.cli)
        Assert-Check 'Independent Azure CLI agrees with the CLI value' ($independent.value -ceq (Read-Fixture 'cli-v2'))
        $list = Invoke-Clips 'cli-list-pages' @('list', '--maxresults', '1', '--debug')
        Assert-Check 'CLI list follows multiple pages' (@($list).Count -ge 2)
        Assert-Metadata 'CLI list' $list
        $versions = Invoke-Clips 'cli-version-pages' @('list-versions', '--name', $state.cli, '--maxresults', '1', '--debug')
        Assert-Check 'CLI version list returns both pages' (@($versions).Count -eq 2)
        $disabled = Invoke-Clips 'cli-disable' @('set-attributes', '--name', $state.cli, '--enabled', 'false', '--tags', 'phase=updated')
        Assert-Check 'CLI update preserves explicit false' ($disabled.attributes.enabled -eq $false)
        $verified = Invoke-Legacy 'Azure CLI verifies disabled metadata' @('list-versions', '--name', $state.cli)
        Assert-Check 'Service records disabled latest version' (@($verified | Where-Object { $_.id.EndsWith($state.cliVersion2) -and $_.attributes.enabled -eq $false }).Count -eq 1)
        $denied = Invoke-Clips 'cli-disabled-read' @('show', '--name', $state.cli, '--as-plain-text') -ExpectedError 'Access denied\.'
        Invoke-Legacy 'Service denies disabled secret read' @('show', '--name', $state.cli, '--query', 'id') -ExpectedError '\(Forbidden\)'
        Assert-Check 'Denied read does not leak fixture data' ($denied.Error -notmatch 'clips-live-')
        $null = Invoke-Clips 'cli-enable' @('set-attributes', '--name', $state.cli, '--enabled', 'true', '--expires', '2027-01-01T00:00:00Z')
        $verified = Invoke-Legacy 'Azure CLI verifies update' @('show', '--name', $state.cli)
        Assert-Check 'Update preserves secret value and sets expiry' ($verified.value -ceq (Read-Fixture 'cli-v2') -and ([DateTimeOffset]$verified.attributes.expires).ToUnixTimeSeconds() -eq 1798761600)
        $null = Invoke-Clips 'cli-delete-unconfirmed' @('delete', '--name', $state.cli) -ExpectedError 'Deletion affects all secret versions\. Confirm with --yes'
        $null = Invoke-Legacy 'Unconfirmed delete leaves secret present' @('show', '--name', $state.cli, '--query', 'id')
        foreach ($log in Get-ChildItem $EvidenceDirectory -Filter '*.stderr') {
            Assert-Check "$($log.Name) excludes fixture data" ([IO.File]::ReadAllText($log.FullName) -notmatch 'clips-live-')
        }
    }
    if ($Phase -in @('All', 'PowerShell')) {
        $state = Read-State
        Import-TestModule
        Register-TestSecret $state.ps
        Register-TestSecret $state.psExtra
        Register-TestSecret "$($state.ps)-whatif"
        $secure1 = ConvertTo-SecureString (Read-Fixture 'ps-v1') -AsPlainText -Force
        $secure2 = ConvertTo-SecureString (Read-Fixture 'ps-v2') -AsPlainText -Force
        try {
            $common = @{ VaultName = $VaultName; Profile = $ProfileName; ErrorAction = 'Stop' }
            $created = Set-AzKeyVaultSecret @common -Name $state.ps -SecretValue $secure1 -Tag @{ phase = 'live' } -Confirm:$false -Debug 5> (Join-Path $EvidenceDirectory 'ps-set-debug.log')
            Assert-Metadata 'PowerShell set' $created
            $state.psVersion1 = $created.Id.Split('/')[-1]
            $null = Set-AzKeyVaultSecret @common -Name $state.psExtra -SecretValue $secure1 -Confirm:$false
            $second = Set-AzKeyVaultSecret @common -Name $state.ps -SecretValue $secure2 -Confirm:$false
            $state.psVersion2 = $second.Id.Split('/')[-1]
            Assert-Check 'PowerShell set creates distinct versions' ($state.psVersion1 -ne $state.psVersion2)
            Save-State
            $shown = Get-AzKeyVaultSecret @common -Name $state.ps
            Assert-Metadata 'PowerShell show' $shown
            $plain = Get-AzKeyVaultSecret @common -Name $state.ps -AsPlainText -Debug 5> (Join-Path $EvidenceDirectory 'ps-show-debug.log')
            Assert-Check 'PowerShell plaintext preserves UTF-8 and newline' ($plain -ceq (Read-Fixture 'ps-v2'))
            $old = Get-AzKeyVaultSecret @common -Name $state.ps -Version $state.psVersion1 -AsPlainText
            Assert-Check 'PowerShell explicit version matches original' ($old -ceq (Read-Fixture 'ps-v1'))
            $verified = Invoke-Legacy 'Azure CLI verifies PowerShell secret' @('show', '--name', $state.ps)
            Assert-Check 'Independent Azure CLI agrees with PowerShell value' ($verified.value -ceq (Read-Fixture 'ps-v2'))
            $list = @(Get-AzKeyVaultSecret @common -MaxResults 1)
            Assert-Check 'PowerShell list follows pages' ($list.Count -ge 4)
            Assert-Metadata 'PowerShell list' $list
            $versions = @(Get-AzKeyVaultSecret @common -Name $state.ps -IncludeVersions -MaxResults 1)
            Assert-Check 'PowerShell version list returns both versions' ($versions.Count -eq 2)
            $updated = Update-AzKeyVaultSecret @common -Name $state.ps -Enabled $false -Tag @{} -Confirm:$false
            Assert-Check 'PowerShell update preserves explicit false and clears tags' ($updated.Attributes.Enabled -eq $false -and $updated.Tags.Count -eq 0)
            $null = Update-AzKeyVaultSecret @common -Name $state.ps -Enabled $true -Confirm:$false
            Set-AzKeyVaultSecret @common -Name "$($state.ps)-whatif" -SecretValue $secure1 -WhatIf
            Remove-AzKeyVaultSecret @common -Name $state.ps -WhatIf
            $null = Invoke-Legacy 'PowerShell WhatIf preserves existing secret' @('show', '--name', $state.ps, '--query', 'id')
            Invoke-Legacy 'PowerShell WhatIf does not create a secret' @('show', '--name', "$($state.ps)-whatif", '--query', 'id') -ExpectedError '\(SecretNotFound\)'
            foreach ($log in Get-ChildItem $EvidenceDirectory -Filter 'ps-*-debug.log') {
                Assert-Check "$($log.Name) excludes fixture data" ([IO.File]::ReadAllText($log.FullName) -notmatch 'clips-live-')
            }
        }
        finally { $secure1.Dispose(); $secure2.Dispose() }
    }
    if ($Phase -eq 'Delete' -or ($Phase -eq 'All' -and -not $KeepTestSecrets)) {
        $state = Read-State
        Import-TestModule
        foreach ($name in @($state.cli, $state.cliExtra)) {
            $deleted = Invoke-Clips "delete-$name" @('delete', '--name', $name, '--yes', '--debug')
            Assert-Metadata "Deleted $name" $deleted
            Assert-Check "$name returns recovery metadata" (-not [string]::IsNullOrEmpty($deleted.recoveryId))
        }
        foreach ($name in @($state.ps, $state.psExtra)) {
            $deleted = Remove-AzKeyVaultSecret -VaultName $VaultName -Profile $ProfileName -Name $name -Confirm:$false -ErrorAction Stop
            Assert-Check "$name returns recovery metadata" (-not [string]::IsNullOrEmpty($deleted.RecoveryId))
        }
        foreach ($name in @($state.cli, $state.cliExtra, $state.ps, $state.psExtra)) {
            Invoke-Legacy "$name is absent from active secrets" @('show', '--name', $name, '--query', 'id') -ExpectedError '\(SecretNotFound\)'
            $deleted = Invoke-Legacy "$name is soft-deleted, not purged" @('show-deleted', '--name', $name)
            Assert-Check "$name retains recovery ID" (-not [string]::IsNullOrEmpty($deleted.recoveryId))
        }
    }
    if ($Phase -eq 'Cleanup') { $state = Read-State }
    $completed = $true
}
catch {
    $results.Add(@{ result = 'failed'; phase = $Phase; type = $_.Exception.GetType().FullName })
    throw
}
finally {
    try {
        if ($state -and (-not $completed -or $Phase -in @('Delete', 'Cleanup') -or ($Phase -eq 'All' -and -not $KeepTestSecrets))) {
            Remove-RunSecrets
        }
    }
    finally {
        $stamp = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffffff')
        [IO.File]::WriteAllText((Join-Path $EvidenceDirectory "$Phase-$stamp-results.json"), (ConvertTo-Json -InputObject @($results) -Depth 10), $utf8)
    }
}