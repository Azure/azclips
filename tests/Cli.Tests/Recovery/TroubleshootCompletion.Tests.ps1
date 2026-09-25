# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Standalone shell tests, without Pester, compilation, or invoking an az executable.
# Run in a fresh host: pwsh -NoProfile -File tests\Cli.Tests\Recovery\TroubleshootCompletion.Tests.ps1
$ErrorActionPreference = 'Stop'
$previousHome = $env:AZCLIPS_HOME
$env:AZCLIPS_HOME = Join-Path (Get-Location) ".test-artifacts\troubleshoot-completion-$([guid]::NewGuid().ToString('N'))"
trap { $env:AZCLIPS_HOME = $previousHome; throw }
. (Join-Path $PSScriptRoot '..\..\..\src\CLI\Completions\az.completions.ps1')

$global:_azTestTreeQueries = 0
$global:_azTestValueQueries = 0
$global:_azTestRequests = [System.Collections.Generic.List[object]]::new()
$global:_azTestValueExitCode = 0
$global:_azTestValuePayload = '[]'
$firstId = '42-2026-0924-0000-0002'
$secondId = '2-2026-0924-0000-0001'
$label = "$firstId (2026-09-21T02:00:00.0000000+00:00)"

function global:_azGetConfigStamp { 'completion-test-config' }

function global:_azCompletionTestExecutable {
    $global:LASTEXITCODE = 0
    if ($args[0] -eq '--completions') {
        $global:_azTestTreeQueries++
        return '{"troubleshoot":{"_params":["--id","--list","--style"],"_subcommands":["show"],"show":["--id"]},"vm":{"_params":["--name"]}}'
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
    Assert-Texts @(Invoke-TestCompletion 'az troubleshoot ') @('show', '--id', '--list', '--style') 'Flags must survive cache reuse.'
}
if ($global:_azTestTreeQueries -ne 1) { throw 'Static command-tree caching changed.' }

Set-IndexPayload @($firstId)
$first = @(Invoke-TestCompletion 'az troubleshoot --id ')
Assert-Texts $first @($firstId) 'Separate --id value completion failed.'
if ($first[0].ListItemText -cne $label -or $first[0].ToolTip -cne $label) {
    throw 'Structured ID/timestamp labels were not preserved.'
}
$request = $global:_azTestRequests[-1]
if (($request -join '|') -cne '--completion-values|troubleshoot|--id|--json') {
    throw 'Wrong hidden values protocol.'
}

Set-IndexPayload @($secondId)
Assert-Texts @(Invoke-TestCompletion 'az troubleshoot --id ') @($secondId) 'History values were cached.'
if ($global:_azTestValueQueries -ne 2 -or $global:_azTestTreeQueries -ne 1) {
    throw 'History requests must bypass the command-tree cache.'
}

Set-IndexPayload @($firstId, $secondId)
Assert-Texts @(Invoke-TestCompletion 'az troubleshoot --id 42') @($firstId) 'Native prefix matching failed.'
Assert-Texts @(Invoke-TestCompletion 'az troubleshoot --id *0924*') @($firstId, $secondId) 'Native wildcard matching must not be prefiltered.'
Assert-Texts @(Invoke-TestCompletion 'az troubleshoot --id=42') @("--id=$firstId") 'Inline ID completion must preserve the flag.'
Assert-Texts @(Invoke-TestCompletion 'az troubleshoot --id=') @("--id=$firstId", "--id=$secondId") 'Empty inline ID completion failed.'
Assert-Texts @(Invoke-TestCompletion 'az TROUBLESHOOT --ID 42') @($firstId) 'Command and flag matching must ignore case.'
Assert-Texts @(Invoke-TestCompletion 'az troubleshoot show --id 42') @($firstId) 'Show must use the same ID candidates.'
Assert-Texts @(Invoke-TestCompletion 'az troubleshoot show --id=42') @("--id=$firstId") 'Show inline ID completion failed.'
Assert-Texts @(Invoke-TestCompletion "az troubleshoot --id`t") @($firstId, $secondId) 'Whitespace must start an ID value.'
Assert-Texts @(Invoke-TestCompletion "az troubleshoot --id=$firstId ") @('show', '--list', '--style') 'Inline flags must be recognized as used for troubleshoot only.'

$queries = $global:_azTestValueQueries
Assert-Texts @(Invoke-TestCompletion 'az troubleshoot --i') @('--id') 'Partial flag names must use static completion.'
Assert-Texts @(Invoke-TestCompletion 'az vm --id ') @('--name') 'Unrelated commands must keep their existing completion.'
Assert-Texts @(Invoke-TestCompletion 'az troubleshoot unknown --id ') @() 'An unknown subcommand is not an ID value slot.'
if ($global:_azTestValueQueries -ne $queries) { throw 'An unrelated context queried history.' }
Assert-Texts @(Invoke-TestCompletion 'az troubleshoot --id $(unsafe)') @() 'A fragment must never execute shell input.'

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
    @{ value = $firstId; label = $label; description = $label },
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
    foreach ($line in @(
        'az troubleshoot --id 42',
        'az troubleshoot --id=42',
        'az troubleshoot show --id 42',
        'az troubleshoot show --id=42',
        "az troubleshoot --id`t42",
        'az troubleshoot --style oneshot --id 42',
        'az troubleshoot --id 42 --style oneshot'
    )) {
        $fragmentStart = $line.IndexOf('42')
        $cursor = $fragmentStart + 2
        $expected = $line.Remove($fragmentStart, 2).Insert($fragmentStart, $firstId)
        foreach ($completion in @(
            (TabExpansion2 -inputScript $line -cursorColumn $cursor),
            [System.Management.Automation.CommandCompletion]::CompleteInput($line, $cursor, $null)
        )) {
            if ($completion.CompletionMatches.Count -ne 1) { throw "Native completion lost ID candidates for '$line'." }
            $applied = $line.Remove($completion.ReplacementIndex, $completion.ReplacementLength).
                Insert($completion.ReplacementIndex, $completion.CompletionMatches[0].CompletionText)
            if ($applied -cne $expected) { throw "ID completion produced an invalid replacement: '$applied'." }
        }
    }
} finally {
    Remove-Item Function:\Get-Command
}

Write-Host 'Troubleshoot PowerShell completion tests passed.'
$env:AZCLIPS_HOME = $previousHome
