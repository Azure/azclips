$ErrorActionPreference = 'Stop'
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'KeyVaultSecrets.Live.ps1'), [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw 'Live harness has syntax errors.' }
foreach ($definition in $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $false)) {
    . ([scriptblock]::Create($definition.Extent.Text))
}
$temp = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory $temp
$statePath = Join-Path $temp 'state.json'
$EvidenceDirectory = $temp
$utf8 = [Text.UTF8Encoding]::new($false)
$VaultName = 'fixture-vault'
$SubscriptionId = 'fixture-subscription'
$LegacyAz = 'must-not-run'
$results = [Collections.Generic.List[object]]::new()
$calls = [Collections.Generic.List[string]]::new()
$responses = [Collections.Generic.Queue[object]]::new()

function Invoke-Native([string]$Executable, [string[]]$Arguments) {
    $calls.Add($Arguments -join ' ')
    if ($responses.Count -eq 0) { throw 'Unexpected native invocation in offline harness test.' }
    return $responses.Dequeue()
}

function Expect-Rejection([scriptblock]$Action) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    if (-not $rejected) { throw 'Expected the harness to reject this result.' }
}

try {
    Assert-NativeResult 'Expected missing secret' @{ Code = 1; Error = '(SecretNotFound) test fixture' } '\(SecretNotFound\)'
    Expect-Rejection { Assert-NativeResult 'Wrong failure' @{ Code = 1; Error = '(Forbidden) test fixture' } '\(SecretNotFound\)' }
    Expect-Rejection { Assert-NativeResult 'Success is not failure' @{ Code = 0; Error = '(SecretNotFound)' } '\(SecretNotFound\)' }
    Expect-Rejection { Assert-NativeResult 'Network failure' @{ Code = 1; Error = 'connection timed out' } '\(Forbidden\)' }

    $runId = [Guid]::NewGuid().ToString('N')
    $state = @{
        schemaVersion = 2; runId = $runId; vault = $VaultName; subscription = $SubscriptionId
        cli = "clips-cli-$runId"; cliExtra = "clips-cli-extra-$runId"
        ps = "clips-ps-$runId"; psExtra = "clips-ps-extra-$runId"; cleanupNames = @()
    }
    Save-State
    $responses.Enqueue(@{ Code = 1; Error = '(Forbidden) fixture'; Output = '' })
    Expect-Rejection { Register-TestSecret $state.cli }
    if ($state.cleanupNames.Count) { throw 'An unverified secret was registered for cleanup.' }
    $responses.Enqueue(@{ Code = 1; Error = '(SecretNotFound) fixture'; Output = '' })
    Register-TestSecret $state.cli
    if ((Read-State).cleanupNames.Count -ne 1) { throw 'Ownership was not persisted before writing.' }
    $priorCalls = $calls.Count
    Expect-Rejection { Remove-RunSecrets -WhatIf }
    if ($calls.Count -ne $priorCalls -or (Read-State).cleanupNames.Count -ne 1) { throw 'Cleanup WhatIf changed remote state or forgot ownership.' }

    $state.vault = 'wrong-vault'; Save-State
    Expect-Rejection { Read-State }
    $state.vault = $VaultName
    $state.cleanupNames = @('someone-elses-secret'); Save-State
    $priorCalls = $calls.Count
    Expect-Rejection { Remove-RunSecrets }
    if ($calls.Count -ne $priorCalls) { throw 'Invalid state reached native cleanup.' }

    $state.cleanupNames = @($state.cli, $state.ps); Save-State
    $responses.Enqueue(@{ Code = 0; Error = ''; Output = '"fixture-id"' })
    $responses.Enqueue(@{ Code = 1; Error = '(SecretNotFound) fixture'; Output = '' })
    Remove-RunSecrets
    if ((Read-State).cleanupNames.Count) { throw 'Successful cleanup left owned secrets pending.' }
    if ($calls | Where-Object { $_ -match '\bpurge\b|someone-elses-secret' }) { throw 'Unsafe cleanup command.' }

    $state.cleanupNames = @($state.cli); Save-State
    $responses.Enqueue(@{ Code = 1; Error = '(Forbidden) fixture'; Output = '' })
    Expect-Rejection { Remove-RunSecrets }
    if ((Read-State).cleanupNames.Count -ne 1) { throw 'Failed cleanup lost its retry manifest.' }

    Remove-Item -LiteralPath $statePath
    $state = $null
    $completed = $false
    $Phase = 'Cli'
    $ResumeCliReads = $false
    $KeepTestSecrets = $false
    $Dotnet = 'must-not-run'
    $CliAssembly = 'fixture.dll'
    $ProfileName = 'fixture'
    $responses.Enqueue(@{ Code = 1; Error = '(SecretNotFound) fixture'; Output = '' })
    $responses.Enqueue(@{ Code = 1; Error = '(SecretNotFound) fixture'; Output = '' })
    $responses.Enqueue(@{ Code = 1; Error = 'injected set failure'; Output = '' })
    $responses.Enqueue(@{ Code = 0; Error = ''; Output = '"fixture-id"' })
    $responses.Enqueue(@{ Code = 1; Error = '(SecretNotFound) fixture'; Output = '' })
    $execution = @($ast.EndBlock.Statements | Where-Object { $_ -is [Management.Automation.Language.TryStatementAst] })[-1]
    Expect-Rejection { . ([scriptblock]::Create($execution.Extent.Text)) }
    if ((Read-State).cleanupNames.Count) { throw 'Partial run failure did not execute cleanup.' }
    if (@(Get-ChildItem $temp -Filter '*.txt').Count) { throw 'Completed cleanup retained synthetic value files.' }
    if ($responses.Count) { throw 'Some expected test invocations did not run.' }
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
    Import-TestModule
    $recovery = Get-Command -Module KeyVault -Name Undo-AzKeyVaultSecretRemoval
    if (-not $recovery.Parameters.ContainsKey('WhatIf') -or -not $recovery.Parameters.ContainsKey('Confirm')) {
        throw 'Recovery must support WhatIf and Confirm.'
    }
    $preview = @(Undo-AzKeyVaultSecretRemoval -VaultName fixture -Name test -Profile 'whatif-profile-does-not-exist' -WhatIf -ErrorAction Stop)
    if ($preview.Count) { throw 'Recovery WhatIf returned service output.' }
    Write-Host 'PASS: recovery cmdlet exported and WhatIf bypasses profile resolution and service execution'
    Write-Host 'PASS: offline failure-reason, ownership, target binding and cleanup tests'
}
finally { Remove-Item -LiteralPath $temp -Recurse -Force }