# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# az Tab Completion for PowerShell
# Usage: . <path-to>/az.completions.ps1
# Or add to your $PROFILE to auto-load on every session.

# Load command tree from pre-generated JSON file (built by build/build.ps1)
$global:_azCommandTree = $null

$_jsonFile = Join-Path $PSScriptRoot 'az.completions.json'
if (Test-Path $_jsonFile) {
    $global:_azCommandTree = Get-Content $_jsonFile -Raw | ConvertFrom-Json -AsHashtable
} else {
    # Fallback: try generating at load time via az --completions
    $_azExe = Join-Path $PSScriptRoot 'az.exe'
    if (-not (Test-Path $_azExe)) {
        $_azExe = (Get-Command az -ErrorAction SilentlyContinue)?.Source
    }
    $json = $null
    if ($_azExe) { $json = & $_azExe --completions 2>$null }
    if ($json) { $json = ($json | Where-Object { $_ -match '^\s*\{' }) -join '' }
    if ($json -and $json -ne '{}') {
        $global:_azCommandTree = $json | ConvertFrom-Json -AsHashtable
    }
}

if (-not $global:_azCommandTree -or $global:_azCommandTree.Count -eq 0) {
    Write-Warning "az completions data not found. Run 'build/build.ps1' to generate. Tab completion unavailable."
    $global:_azCommandTree = @{}
}

# Core completion logic — returns CompletionResult[] for a given input line and cursor
function global:_azGetCompletions {
    param([string]$inputText, [int]$cursorPos)

    $tree = $global:_azCommandTree
    if (-not $tree -or $tree.Count -eq 0) { return @() }

    $textUpToCursor = $inputText.Substring(0, $cursorPos)
    $allTokens = @($textUpToCursor -split '\s+' | Where-Object { $_ -ne '' })
    $completedArgs = @($allTokens | Select-Object -Skip 1)
    $isCompletingNewToken = $textUpToCursor.EndsWith(' ')

    $topLevelGroups = @($tree.Keys | Sort-Object)

    # --- Level 0: completing command group ---
    if ($completedArgs.Count -eq 0 -or ($completedArgs.Count -eq 1 -and -not $isCompletingNewToken)) {
        $prefix = if ($completedArgs.Count -ge 1) { $completedArgs[0] } else { '' }
        return @($topLevelGroups | Where-Object { $_ -like "$prefix*" } | ForEach-Object {
            [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
        })
    }

    $group = $completedArgs[0].ToLower()
    if (-not $tree.ContainsKey($group)) { return @() }

    $groupDef = $tree[$group]
    $subcommands = if ($groupDef.ContainsKey('_subcommands')) { $groupDef['_subcommands'] } else { @() }

    # --- Level 1: completing subcommand ---
    $hasSubcommand = $false
    $subcommand = $null

    if ($completedArgs.Count -ge 2) {
        $candidate = $completedArgs[1].ToLower()
        if ($subcommands -contains $candidate) {
            $hasSubcommand = $true
            $subcommand = $candidate
        }
    }

    if ((-not $hasSubcommand) -and ($completedArgs.Count -eq 1 -and $isCompletingNewToken) -or
        ($completedArgs.Count -eq 2 -and -not $isCompletingNewToken -and -not $hasSubcommand)) {
        $prefix = if ($completedArgs.Count -ge 2) { $completedArgs[1] } else { '' }
        $results = @()

        $results += @($subcommands | Where-Object { $_ -like "$prefix*" } | ForEach-Object {
            [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', "$group $_")
        })

        if ($groupDef.ContainsKey('_params')) {
            $results += @($groupDef['_params'] | Where-Object { $_ -like "$prefix*" } | ForEach-Object {
                [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterName', $_)
            })
        }
        return $results
    }

    # --- Level 2: completing parameters ---
    $paramStartIndex = if ($hasSubcommand) { 2 } else { 1 }
    $paramList = @()
    if ($hasSubcommand -and $groupDef.ContainsKey($subcommand)) {
        $paramList = $groupDef[$subcommand]
    }
    elseif (-not $hasSubcommand -and $groupDef.ContainsKey('_params')) {
        $paramList = $groupDef['_params']
    }

    if ($paramList.Count -eq 0) { return @() }

    $allParamTokens = @($completedArgs[$paramStartIndex..($completedArgs.Count - 1)])
    $usedParams = @($allParamTokens | Where-Object { $_ -like '--*' })

    $wordToComplete = if (-not $isCompletingNewToken -and $completedArgs.Count -gt 0) { $completedArgs[-1] } else { '' }
    return @($paramList | Where-Object {
        $_ -like "$wordToComplete*" -and $_ -notin $usedParams
    } | ForEach-Object {
        [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterName', $_)
    })
}

# Register native argument completer (fallback for scripts that invoke completions directly)
Register-ArgumentCompleter -Native -CommandName az -ScriptBlock {
    param($wordToComplete, $commandAst, $cursorPosition)
    $line = $commandAst.ToString()
    _azGetCompletions $line $cursorPosition
}

# Override TabExpansion2 so PSReadLine's MenuComplete works for az
# including the empty-word case (e.g. "az " + Tab).
# For non-az commands, delegates to the default implementation.
function global:TabExpansion2 {
    [CmdletBinding(DefaultParameterSetName = 'ScriptInputSet')]
    [OutputType([System.Management.Automation.CommandCompletion])]
    param(
        [Parameter(ParameterSetName = 'ScriptInputSet', Mandatory, Position = 0)]
        [AllowEmptyString()]
        [string]$inputScript,

        [Parameter(ParameterSetName = 'ScriptInputSet', Position = 1)]
        [int]$cursorColumn = $inputScript.Length,

        [Parameter(ParameterSetName = 'AstInputSet', Mandatory, Position = 0)]
        [System.Management.Automation.Language.Ast]$ast,

        [Parameter(ParameterSetName = 'AstInputSet', Mandatory, Position = 1)]
        [System.Management.Automation.Language.Token[]]$tokens,

        [Parameter(ParameterSetName = 'AstInputSet', Mandatory, Position = 2)]
        [System.Management.Automation.Language.IScriptPosition]$positionOfCursor,

        [Parameter(ParameterSetName = 'ScriptInputSet', Position = 2)]
        [Parameter(ParameterSetName = 'AstInputSet', Position = 3)]
        [System.Collections.Hashtable]$options
    )

    end {
        $line = if ($PSCmdlet.ParameterSetName -eq 'ScriptInputSet') { $inputScript } else { $ast.Extent.Text }
        $cursor = if ($PSCmdlet.ParameterSetName -eq 'ScriptInputSet') { $cursorColumn } else { $positionOfCursor.Offset }

        if ($line -match '^\s*az(\s|$)') {
            $results = @(_azGetCompletions $line $cursor)
            if ($results.Count -gt 0) {
                $textUpToCursor = $line.Substring(0, [Math]::Min($cursor, $line.Length))
                $lastSpace = $textUpToCursor.LastIndexOf(' ')
                $replIndex = $lastSpace + 1
                $replLength = $cursor - $replIndex

                $matches = [System.Collections.ObjectModel.Collection[System.Management.Automation.CompletionResult]]::new()
                foreach ($r in $results) { $matches.Add($r) }
                return [System.Management.Automation.CommandCompletion]::new($matches, 0, $replIndex, $replLength)
            }
        }

        # Default: delegate to built-in completion
        if ($PSCmdlet.ParameterSetName -eq 'ScriptInputSet') {
            return [System.Management.Automation.CommandCompletion]::CompleteInput($inputScript, $cursorColumn, $options)
        } else {
            return [System.Management.Automation.CommandCompletion]::CompleteInput($ast, $tokens, $positionOfCursor, $options)
        }
    }
}

# Bind Tab to MenuComplete for dropdown-style display
Set-PSReadLineKeyHandler -Key Tab -Function MenuComplete

Write-Host "az tab completion registered. Press Tab after 'az ' to see suggestions." -ForegroundColor Green
