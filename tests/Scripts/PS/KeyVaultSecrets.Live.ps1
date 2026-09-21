[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Cli', 'PowerShell', 'Delete')][string]$Phase,
    [Parameter(Mandatory)][string]$VaultName,
    [Parameter(Mandatory)][string]$SubscriptionId,
    [Parameter(Mandatory)][string]$Profile,
    [Parameter(Mandatory)][string]$CliAssembly,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [string]$Dotnet = '/usr/local/share/dotnet/dotnet',
    [string]$LegacyAz = '/opt/homebrew/bin/az',
    [switch]$ResumeCliReads
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$null = New-Item -ItemType Directory -Path $EvidenceDirectory -Force
$statePath = Join-Path $EvidenceDirectory 'state.json'
$results = [Collections.Generic.List[object]]::new()
$utf8 = [Text.UTF8Encoding]::new($false)

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

function Invoke-Clips([string]$Name, [string[]]$Arguments, [switch]$ExpectFailure) {
    $result = Invoke-Native $Dotnet (@($CliAssembly, 'keyvault', 'secret') + $Arguments + @('--vault-name', $VaultName, '--profile', $Profile, '--output', 'json'))
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory "$Name.stdout"), $result.Output, $utf8)
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory "$Name.stderr"), $result.Error, $utf8)
    if ($ExpectFailure) {
        Assert-Check "$Name returns a nonzero exit code" ($result.Code -ne 0)
        return $result
    }
    Assert-Check "$Name succeeds" ($result.Code -eq 0)
    if ($Arguments -contains '--as-plain-text') {
        if ($result.Output.EndsWith([Environment]::NewLine)) {
            return $result.Output.Substring(0, $result.Output.Length - [Environment]::NewLine.Length)
        }
        return $result.Output
    }
    return ConvertFrom-Json -InputObject $result.Output -AsHashtable -NoEnumerate
}

function Invoke-Legacy([string]$Name, [string[]]$Arguments, [switch]$ExpectFailure) {
    $result = Invoke-Native $LegacyAz (@('keyvault', 'secret') + $Arguments + @('--vault-name', $VaultName, '--subscription', $SubscriptionId, '--only-show-errors', '-o', 'json'))
    if ($ExpectFailure) {
        Assert-Check "$Name returns a nonzero exit code" ($result.Code -ne 0)
        return
    }
    Assert-Check "$Name succeeds" ($result.Code -eq 0)
    return ConvertFrom-Json -InputObject $result.Output -AsHashtable -NoEnumerate
}

function Read-Fixture([string]$Name) { [IO.File]::ReadAllText((Join-Path $EvidenceDirectory "$Name.txt"), $utf8) }

function Assert-Metadata([string]$Name, $Metadata) {
    $json = ConvertTo-Json -InputObject $Metadata -Depth 20 -Compress
    Assert-Check "$Name has no value property or fixture data" ($json -notmatch '"value"\s*:' -and $json -notmatch 'clips-live-')
}

function Import-TestModule {
    Import-Module (Join-Path $repoRoot 'src/PS/Accounts/Accounts.psd1') -ErrorAction Stop
    Import-Module (Join-Path $repoRoot 'src/PS/KeyVault/KeyVault.psd1') -ErrorAction Stop
    Assert-Check 'PowerShell module exports four commands' (@(Get-Command -Module KeyVault).Count -eq 4)
}

try {
    if ($Phase -eq 'Cli') {
        if ($ResumeCliReads) {
            $state = Get-Content $statePath -Raw | ConvertFrom-Json -AsHashtable
        }
        else {
        if (Test-Path $statePath) { throw 'Use a new evidence directory for a new live run.' }
        $runId = [Guid]::NewGuid().ToString('N').Substring(0, 8)
        $state = @{ cli = "clips-cli-$runId"; cliExtra = "clips-cli-extra-$runId"; ps = "clips-ps-$runId"; psExtra = "clips-ps-extra-$runId" }
        foreach ($fixture in @('cli-v1', 'cli-v2', 'ps-v1', 'ps-v2')) {
            $value = 'clips-live-' + [Guid]::NewGuid().ToString('N') + "`"-utf8-" + [char]0x00e9 + "`n"
            [IO.File]::WriteAllText((Join-Path $EvidenceDirectory "$fixture.txt"), $value, $utf8)
        }
        [IO.File]::WriteAllText($statePath, ($state | ConvertTo-Json), $utf8)
        $created = Invoke-Clips 'cli-set-v1' @('set', '--name', $state.cli, '--file', (Join-Path $EvidenceDirectory 'cli-v1.txt'), '--content-type', 'text/plain', '--tags', 'phase=live', '--debug')
        Assert-Metadata 'CLI set' $created
        $state.cliVersion1 = ([string]$created.id).Split('/')[-1]
        $null = Invoke-Clips 'cli-set-extra' @('set', '--name', $state.cliExtra, '--file', (Join-Path $EvidenceDirectory 'cli-v1.txt'))
        $second = Invoke-Clips 'cli-set-v2' @('set', '--name', $state.cli, '--file', (Join-Path $EvidenceDirectory 'cli-v2.txt'))
        $state.cliVersion2 = ([string]$second.id).Split('/')[-1]
        Assert-Check 'CLI set creates distinct versions' ($state.cliVersion1 -ne $state.cliVersion2)
        [IO.File]::WriteAllText($statePath, ($state | ConvertTo-Json), $utf8)
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
        $denied = Invoke-Clips 'cli-disabled-read' @('show', '--name', $state.cli, '--as-plain-text') -ExpectFailure
        Assert-Check 'Denied read does not leak fixture data' ($denied.Error -notmatch 'clips-live-')
        $null = Invoke-Clips 'cli-enable' @('set-attributes', '--name', $state.cli, '--enabled', 'true', '--expires', '2027-01-01T00:00:00Z')
        $verified = Invoke-Legacy 'Azure CLI verifies update' @('show', '--name', $state.cli)
        Assert-Check 'Update preserves secret value and sets expiry' ($verified.value -ceq (Read-Fixture 'cli-v2') -and ([DateTimeOffset]$verified.attributes.expires).ToUnixTimeSeconds() -eq 1798761600)
        $rejected = Invoke-Clips 'cli-delete-unconfirmed' @('delete', '--name', $state.cli) -ExpectFailure
        $null = Invoke-Legacy 'Unconfirmed delete leaves secret present' @('show', '--name', $state.cli, '--query', 'id')
        foreach ($log in Get-ChildItem $EvidenceDirectory -Filter '*.stderr') {
            Assert-Check "$($log.Name) excludes fixture data" ([IO.File]::ReadAllText($log.FullName) -notmatch 'clips-live-')
        }
    }
    elseif ($Phase -eq 'PowerShell') {
        $state = Get-Content $statePath -Raw | ConvertFrom-Json -AsHashtable
        Import-TestModule
        $secure1 = ConvertTo-SecureString (Read-Fixture 'ps-v1') -AsPlainText -Force
        $secure2 = ConvertTo-SecureString (Read-Fixture 'ps-v2') -AsPlainText -Force
        try {
            $common = @{ VaultName = $VaultName; Profile = $Profile; ErrorAction = 'Stop' }
            $created = Set-AzKeyVaultSecret @common -Name $state.ps -SecretValue $secure1 -Tag @{ phase = 'live' } -Confirm:$false -Debug 5> (Join-Path $EvidenceDirectory 'ps-set-debug.log')
            Assert-Metadata 'PowerShell set' $created
            $state.psVersion1 = $created.Id.Split('/')[-1]
            $null = Set-AzKeyVaultSecret @common -Name $state.psExtra -SecretValue $secure1 -Confirm:$false
            $second = Set-AzKeyVaultSecret @common -Name $state.ps -SecretValue $secure2 -Confirm:$false
            $state.psVersion2 = $second.Id.Split('/')[-1]
            Assert-Check 'PowerShell set creates distinct versions' ($state.psVersion1 -ne $state.psVersion2)
            [IO.File]::WriteAllText($statePath, ($state | ConvertTo-Json), $utf8)
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
            Invoke-Legacy 'PowerShell WhatIf does not create a secret' @('show', '--name', "$($state.ps)-whatif", '--query', 'id') -ExpectFailure
            foreach ($log in Get-ChildItem $EvidenceDirectory -Filter 'ps-*-debug.log') {
                Assert-Check "$($log.Name) excludes fixture data" ([IO.File]::ReadAllText($log.FullName) -notmatch 'clips-live-')
            }
        }
        finally { $secure1.Dispose(); $secure2.Dispose() }
    }
    else {
        $state = Get-Content $statePath -Raw | ConvertFrom-Json -AsHashtable
        Import-TestModule
        foreach ($name in @($state.cli, $state.cliExtra)) {
            $deleted = Invoke-Clips "delete-$name" @('delete', '--name', $name, '--yes', '--debug')
            Assert-Metadata "Deleted $name" $deleted
            Assert-Check "$name returns recovery metadata" (-not [string]::IsNullOrEmpty($deleted.recoveryId))
        }
        foreach ($name in @($state.ps, $state.psExtra)) {
            $deleted = Remove-AzKeyVaultSecret -VaultName $VaultName -Profile $Profile -Name $name -Confirm:$false -ErrorAction Stop
            Assert-Check "$name returns recovery metadata" (-not [string]::IsNullOrEmpty($deleted.RecoveryId))
        }
        foreach ($name in @($state.cli, $state.cliExtra, $state.ps, $state.psExtra)) {
            Invoke-Legacy "$name is absent from active secrets" @('show', '--name', $name, '--query', 'id') -ExpectFailure
            $deleted = Invoke-Legacy "$name is soft-deleted, not purged" @('show-deleted', '--name', $name)
            Assert-Check "$name retains recovery ID" (-not [string]::IsNullOrEmpty($deleted.recoveryId))
        }
    }
}
catch {
    $results.Add(@{ result = 'failed'; phase = $Phase; type = $_.Exception.GetType().FullName })
    throw
}
finally {
    [IO.File]::WriteAllText((Join-Path $EvidenceDirectory "$Phase-results.json"), ($results | ConvertTo-Json -Depth 10), $utf8)
}