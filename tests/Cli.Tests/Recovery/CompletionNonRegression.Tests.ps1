# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Run in a fresh PowerShell host. Both versions use the same fake CLI, config and
# fallback while real CompletionResult/TabExpansion2 preserve replacement metadata.
param([string]$Baseline = '83f27105bebeb1619b06b4871b9d970d186530d5')

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$previousHome = $env:AZCLIPS_HOME
$env:AZCLIPS_HOME = Join-Path $root ".test-artifacts\completion-nonregression-$([guid]::NewGuid().ToString('N'))"
$current = Get-Content -LiteralPath (Join-Path $root 'src\CLI\Completions\az.completions.ps1') -Raw
$baselineSource = (& git -C $root --no-pager show "${Baseline}:src/CLI/Completions/az.completions.ps1") -join "`n"
if ($LASTEXITCODE -ne 0) { throw 'Could not read the requested baseline from Git.' }

function global:_azCompatibilityExecutable {
    $global:LASTEXITCODE = 0
    $global:_azCompatibilityQueries.Add(($args -join '|'))
    if ($args[0] -ne '--completions') { throw 'Ordinary completion must never request history.' }
    if ($global:_azCompatibilityMode -eq 'old') { return 'ERROR: unsupported completion protocol' }
    if ($global:_azCompatibilityMode -eq 'disabled') { return '{}' }
    $tree = @{
        deployment = @{
            _subcommands = @('group create', 'group what-if')
            'group create' = @('--name', '--location', '--mode')
            'group what-if' = @('--name', '--mode')
        }
        vm = @{ _subcommands = @('show', 'list'); show = @('--name', '--ids'); list = @('--resource-group') }
        account = @{ _params = @('--subscription') }
    }
    if ($args.Count -gt 1) { $tree = $tree[$args[1]] }
    ConvertTo-Json -InputObject $tree -Depth 8 -Compress
}

function global:_azCompatibilityAlternate { _azCompatibilityExecutable @args }

function Convert-Matches($Matches) {
    @($Matches | ForEach-Object {
        [ordered]@{
            text = $_.CompletionText
            label = $_.ListItemText
            type = [string]$_.ResultType
            tooltip = $_.ToolTip
        }
    })
}

function Invoke-CompatibilitySuite([string]$Source) {
    $script:_azTabExpansion2Installed = $false
    function global:TabExpansion2 {
        param([string]$inputScript, [int]$cursorColumn = $inputScript.Length, $options)
        $global:_azCompatibilityFallbacks++
        $matches = [System.Collections.ObjectModel.Collection[System.Management.Automation.CompletionResult]]::new()
        $matches.Add([System.Management.Automation.CompletionResult]::new('fallback', 'fallback label', 'Text', 'fallback tooltip'))
        [System.Management.Automation.CommandCompletion]::new($matches, 0, $cursorColumn, 0)
    }
    . ([scriptblock]::Create($Source))
    function global:_azGetConfigStamp { $global:_azCompatibilityStamp }
    $global:_azCompatibilityQueries = [System.Collections.Generic.List[string]]::new()
    $global:_azCompatibilityFallbacks = 0
    $global:_azCompatibilityStamp = 'enabled'
    $global:_azCompatibilityMode = 'enabled'
    $snapshots = [System.Collections.Generic.List[object]]::new()
    $cases = @(
        @{ line = 'az ' }, @{ line = 'az dep' }, @{ line = 'az deployment ' },
        @{ line = 'az deployment group ' }, @{ line = 'az deployment group create --' },
        @{ line = 'az deployment group create --name value --' },
        @{ line = 'az deployment group create --name=value --' },
        @{ line = 'az deployment group create --NAME value --' },
        @{ line = 'az DEPLOYMENT GROUP create --M' }, @{ line = 'az vm sh' },
        @{ line = "az deployment`t" }, @{ line = "az deployment group create --name`t" },
        @{ line = 'az deployment group create --na --mode later'; cursor = 31 },
        @{ line = '.\az.exe vm ' }, @{ line = 'C:\offline\az.exe vm show --' }
    )
    foreach ($case in $cases) {
        $cursor = if ($case.ContainsKey('cursor')) { $case.cursor } else { $case.line.Length }
        $matches = @(_azGetCompletions -exePath '_azCompatibilityExecutable' -inputText $case.line -cursorPos $cursor)
        $completion = TabExpansion2 -inputScript $case.line -cursorColumn $cursor
        $snapshots.Add([ordered]@{
            line = $case.line
            cursor = $cursor
            matches = @(Convert-Matches $matches)
            native = @(Convert-Matches $completion.CompletionMatches)
            replacementIndex = $completion.ReplacementIndex
            replacementLength = $completion.ReplacementLength
            queries = $global:_azCompatibilityQueries.Count
            fallbacks = $global:_azCompatibilityFallbacks
        })
    }
    foreach ($mode in @('enabled', 'disabled', 'disabled', 'enabled', 'old', 'old')) {
        $global:_azCompatibilityStamp = $mode
        $global:_azCompatibilityMode = $mode
        $matches = @(_azGetCompletions -exePath '_azCompatibilityAlternate' -inputText 'az vm ' -cursorPos 6)
        $snapshots.Add([ordered]@{
            mode = $mode; matches = @(Convert-Matches $matches); queries = $global:_azCompatibilityQueries.Count
        })
    }
    $snapshots.Add([ordered]@{ queries = @($global:_azCompatibilityQueries); fallbacks = $global:_azCompatibilityFallbacks })
    return ConvertTo-Json -InputObject @($snapshots) -Depth 12 -Compress
}

function global:Get-Command {
    [CmdletBinding()]
    param([Parameter(Position = 0)][string]$Name, [string]$CommandType)
    if ($Name -in @('az', 'az.exe')) { return [pscustomobject]@{ Source = '_azCompatibilityExecutable' } }
    Microsoft.PowerShell.Core\Get-Command @PSBoundParameters
}

function global:Test-Path {
    param([Parameter(Position = 0)]$Path)
    if ($Path -in @('.\az.exe', 'C:\offline\az.exe')) { return $true }
    Microsoft.PowerShell.Management\Test-Path @PSBoundParameters
}

function global:Resolve-Path {
    param([Parameter(Position = 0)]$Path)
    if ($Path -in @('.\az.exe', 'C:\offline\az.exe')) {
        return [pscustomobject]@{ Path = '_azCompatibilityExecutable' }
    }
    Microsoft.PowerShell.Management\Resolve-Path @PSBoundParameters
}

try {
    $before = Invoke-CompatibilitySuite $baselineSource
    $after = Invoke-CompatibilitySuite $current
    if ($before -cne $after) {
        throw "PowerShell completion differs from $Baseline.`nBASELINE: $before`nCURRENT: $after"
    }
    Write-Host "PowerShell ordinary completion matches $Baseline (candidates, order, labels, ranges, whitespace, equals, cache, fallback, query counts)."
} finally {
    $env:AZCLIPS_HOME = $previousHome
    Remove-Item Function:\Get-Command
    Remove-Item Function:\Test-Path
    Remove-Item Function:\Resolve-Path
}
