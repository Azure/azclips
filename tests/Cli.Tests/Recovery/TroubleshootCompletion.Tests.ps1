# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Standalone shell tests, without Pester, compilation, or invoking an az executable.
# Run in a fresh host: pwsh -NoProfile -File tests\Cli.Tests\Recovery\TroubleshootCompletion.Tests.ps1
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\..\..\src\CLI\Completions\az.completions.ps1')

$global:_azTestTreeQueries = 0
$global:_azTestValueQueries = 0
$global:_azTestRequests = [System.Collections.Generic.List[object]]::new()
$global:_azTestValueExitCode = 0
$global:_azTestValuePayload = '[]'
$firstId = 'abcdef0123456789abcdef0123456789'
$secondId = '0123456789abcdef0123456789abcdef'
$label = "$firstId (2026-09-21T02:00:00.0000000+00:00)"

function global:_azGetConfigStamp { 'completion-test-config' }

function global:_azCompletionTestExecutable {
    $global:LASTEXITCODE = 0
    if ($args[0] -eq '--completions') {
        $global:_azTestTreeQueries++
        return '{"troubleshoot":{"_params":["--id","--list"]},"vm":{"_params":["--name"]}}'
    }
    if ($args[0] -eq '--completion-values') {
        $global:_azTestValueQueries++
        $global:_azTestRequests.Add([string[]]$args)
        $global:LASTEXITCODE = $global:_azTestValueExitCode
        return $global:_azTestValuePayload
    }
    throw 'Unexpected executable invocation.'
}

function Invoke-TestCompletion([string]$Line) {
    @(_azGetCompletions -exePath '_azCompletionTestExecutable' -inputText $Line -cursorPos $Line.Length)
}

function Assert-Texts([object[]]$Results, [string[]]$Expected, [string]$Because) {
    $actual = @($Results | ForEach-Object { $_.CompletionText })
    if ($actual.Count -ne $Expected.Count -or ($actual -join "`0") -cne ($Expected -join "`0")) {
        throw "$Because Expected '$($Expected -join ', ')'; got '$($actual -join ', ')'."
    }
}

function Set-IndexPayload([string[]]$Ids) {
    $entries = @($Ids | ForEach-Object {
        $text = "$_ (2026-09-21T02:00:00.0000000+00:00)"
        @{ value = $_; label = $text; description = $text }
    })
    $global:_azTestValuePayload = ConvertTo-Json -InputObject $entries -Compress
    $global:_azTestValueExitCode = 0
}

foreach ($request in 1..2) {
    Assert-Texts @(Invoke-TestCompletion 'az ') @('troubleshoot', 'vm') 'Root must survive cache reuse.'
    Assert-Texts @(Invoke-TestCompletion 'az troubleshoot ') @('--id', '--list') 'Flags must survive cache reuse.'
}
if ($global:_azTestTreeQueries -ne 1) { throw 'Static command-tree caching changed.' }

Set-IndexPayload @($firstId)
$first = @(Invoke-TestCompletion 'az troubleshoot --id ')
Assert-Texts $first @($firstId) 'Separate --id value completion failed.'
if ($first[0].ListItemText -cne $label -or $first[0].ToolTip -cne $label) {
    throw 'Structured ID/timestamp labels were not preserved.'
}
$request = $global:_azTestRequests[-1]
if (($request -join '|') -cne '--completion-values|troubleshoot|--id||--json') {
    throw 'Wrong hidden values protocol.'
}

Set-IndexPayload @($secondId)
Assert-Texts @(Invoke-TestCompletion 'az troubleshoot --id ') @($secondId) 'History values were cached.'
if ($global:_azTestValueQueries -ne 2 -or $global:_azTestTreeQueries -ne 1) {
    throw 'History requests must bypass the command-tree cache.'
}

Set-IndexPayload @($firstId, $secondId)
Assert-Texts @(Invoke-TestCompletion 'az troubleshoot --id ABC') @($firstId) 'Prefix matching must ignore case.'
if ($global:_azTestRequests[-1][3] -cne 'ABC') { throw 'The ID prefix was not passed to the endpoint.' }
Assert-Texts @(Invoke-TestCompletion 'az troubleshoot --id=ABC') @("--id=$firstId") 'Inline ID completion must preserve the flag.'
Assert-Texts @(Invoke-TestCompletion 'az troubleshoot --id=') @("--id=$firstId", "--id=$secondId") 'Empty inline ID completion failed.'
Assert-Texts @(Invoke-TestCompletion 'az TROUBLESHOOT --ID ABC') @($firstId) 'Command and flag matching must ignore case.'
Assert-Texts @(Invoke-TestCompletion "az troubleshoot --id`t") @($firstId, $secondId) 'Whitespace must start an ID value.'
Assert-Texts @(Invoke-TestCompletion "az troubleshoot --id=$firstId ") @('--list') 'Inline flags must be recognized as used.'

$queries = $global:_azTestValueQueries
Assert-Texts @(Invoke-TestCompletion 'az troubleshoot --i') @('--id') 'Partial flag names must use static completion.'
Assert-Texts @(Invoke-TestCompletion 'az vm --id ') @('--name') 'Unrelated commands must keep their existing completion.'
Assert-Texts @(Invoke-TestCompletion 'az troubleshoot --id $(unsafe)') @() 'Unsafe prefixes must not be queried.'
if ($global:_azTestValueQueries -ne $queries) { throw 'An unrelated or unsafe context queried history.' }

$global:_azTestValueExitCode = 1
Assert-Texts @(Invoke-TestCompletion 'az troubleshoot --id ') @() 'Failed queries must not expose IDs or stale data.'
$global:_azTestValueExitCode = 0
foreach ($payload in @(
    'ERROR: unrecognized arguments --completion-values',
    'error: unsupported arguments',
    '{"error":"unsupported completion protocol"}',
    '["not-an-ID"]',
    '[]'
)) {
    $global:_azTestValuePayload = $payload
    Assert-Texts @(Invoke-TestCompletion 'az troubleshoot --id ') @() 'Non-azclips errors must not become candidates.'
}
if ($global:_azCompletionsCache.notAzclips.ContainsKey('_azCompletionTestExecutable')) {
    throw 'An unsupported values endpoint must not disable unrelated completions.'
}

$global:_azTestValuePayload = ConvertTo-Json -InputObject @(
    @{ value = $firstId; label = $label; description = $label },
    @{ value = $firstId.ToUpperInvariant(); label = $label; description = $label },
    @{ value = '00000000000000000000000000000000'; label = 'empty'; description = 'empty' },
    @{ value = "$secondId`n"; label = 'unsafe'; description = 'unsafe' },
    @{ value = '$(unsafe)'; label = 'unsafe'; description = 'unsafe' },
    @{ error = 'unsupported protocol' }
) -Compress
Assert-Texts @(Invoke-TestCompletion 'az troubleshoot --id ') @($firstId) 'Unsafe or duplicate response values must be excluded.'

$global:_azCompletionsCache.notAzclips['_azCompletionTestExecutable'] = $true
$queries = $global:_azTestValueQueries
Assert-Texts @(Invoke-TestCompletion 'az troubleshoot --id ') @() 'Known non-azclips executables must be skipped.'
if ($global:_azTestValueQueries -ne $queries) { throw 'Known non-azclips executable was invoked.' }
$global:_azCompletionsCache.notAzclips.Remove('_azCompletionTestExecutable')

# Exercise the real replacement span without resolving or executing the user's az.
function global:Get-Command {
    [CmdletBinding()]
    param([Parameter(Position = 0)][string]$Name, [string]$CommandType)
    if ($Name -eq 'az') { return [pscustomobject]@{ Source = '_azCompletionTestExecutable' } }
    Microsoft.PowerShell.Core\Get-Command @PSBoundParameters
}
try {
    Set-IndexPayload @($firstId)
    foreach ($line in @('az troubleshoot --id ABC', 'az troubleshoot --id=ABC')) {
        $completion = TabExpansion2 -inputScript $line -cursorColumn $line.Length
        if ($completion.CompletionMatches.Count -ne 1) { throw 'TabExpansion2 lost ID completion.' }
        $applied = $line.Remove($completion.ReplacementIndex, $completion.ReplacementLength).
            Insert($completion.ReplacementIndex, $completion.CompletionMatches[0].CompletionText)
        $expected = if ($line.Contains('--id=')) { "az troubleshoot --id=$firstId" } else { "az troubleshoot --id $firstId" }
        if ($applied -cne $expected) { throw 'ID completion produced an invalid command replacement.' }
    }
} finally {
    Remove-Item Function:\Get-Command
}

Write-Host 'Troubleshoot PowerShell completion tests passed.'
