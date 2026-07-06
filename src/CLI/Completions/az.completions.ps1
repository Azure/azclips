# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# az Tab Completion for PowerShell (Dynamic)
# Usage: . <path-to>/az.completions.ps1
# Or add to your $PROFILE to auto-load on every session.
#
# Completions are resolved dynamically by invoking `az --completions [group] [subcommand]`
# at tab time. This ensures completions match whichever `az` binary is on PATH.

# Per-binary cache: path -> $true/$false
$global:_azIsAzclipsCache = @{}

function global:_azIsAzclips {
    param([string]$exePath)
    # Resolve which binary to check
    if (-not $exePath) { return $false }
    $resolved = Get-Command $exePath -ErrorAction SilentlyContinue
    if ($resolved) { $exePath = $resolved.Source }
    $key = $exePath.ToLower()
    if ($global:_azIsAzclipsCache.ContainsKey($key)) { return $global:_azIsAzclipsCache[$key] }
    $result = $false
    try {
        $ver = & $exePath --version 2>&1 | Out-String
        # System Azure CLI outputs 'azure-cli X.Y.Z'; azclips does not.
        # If --version failed or returned empty, treat as not azclips.
        if ($ver) {
            $result = ($ver -notmatch 'azure-cli')
        }
    } catch {
        # --version threw → not azclips
    }
    $global:_azIsAzclipsCache[$key] = $result
    return $result
}

function global:_azResolveExe {
    # 1. Same directory as this script (e.g. artifacts/Release/Core.Cli/)
    $local = Join-Path $PSScriptRoot 'az.exe'
    if (Test-Path $local) { return $local }
    $local = Join-Path $PSScriptRoot 'az'
    if (Test-Path $local) { return $local }

    # 2. Walk up to find repo root (look for global.json), then check artifacts/
    $dir = $PSScriptRoot
    while ($dir) {
        if (Test-Path (Join-Path $dir 'global.json')) {
            foreach ($cfg in 'Release', 'Debug') {
                $candidate = Join-Path $dir "artifacts/$cfg/Core.Cli/az.exe"
                if (Test-Path $candidate) { return $candidate }
                $candidate = Join-Path $dir "artifacts/$cfg/Core.Cli/az"
                if (Test-Path $candidate) { return $candidate }
            }
            break
        }
        $parent = Split-Path $dir -Parent
        if ($parent -eq $dir) { break }
        $dir = $parent
    }

    # 3. Fall back to PATH
    $cmd = Get-Command az -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

function global:_azQueryCompletions {
    param([string[]]$context)
    $exe = _azResolveExe
    if (-not $exe) { return $null }
    $args_ = @('--completions') + $context
    try {
        $json = & $exe @args_ 2>$null
        if ($json) {
            $joined = ($json | Where-Object { $_ -match '^\s*[\{\[]' }) -join ''
            if ($joined) { return $joined | ConvertFrom-Json -AsHashtable }
        }
    } catch {}
    return $null
}

function global:_azGetTopLevelGroups {
    # Always query `az --completions` at tab time (no caching).
    $tree = _azQueryCompletions
    if ($tree -and $tree.Count -gt 0) {
        return @($tree.Keys | Sort-Object)
    }
    return @()
}

function global:_azGetGroupDef {
    param([string]$group)
    # Always query `az --completions <group>` at tab time (no caching).
    $tree = _azQueryCompletions @($group)
    if ($tree -and $tree.Count -gt 0) {
        return $tree
    }
    return $null
}

# Core completion logic — returns CompletionResult[] for a given input line and cursor
function global:_azGetCompletions {
    param([string]$inputText, [int]$cursorPos)

    $textUpToCursor = $inputText.Substring(0, $cursorPos)
    $allTokens = @($textUpToCursor -split '\s+' | Where-Object { $_ -ne '' })
    $completedArgs = @($allTokens | Select-Object -Skip 1)
    $isCompletingNewToken = $textUpToCursor.EndsWith(' ')

    $topLevelGroups = _azGetTopLevelGroups
    if (-not $topLevelGroups -or $topLevelGroups.Count -eq 0) { return @() }

    # --- Level 0: completing command group ---
    if ($completedArgs.Count -eq 0 -or ($completedArgs.Count -eq 1 -and -not $isCompletingNewToken)) {
        $prefix = if ($completedArgs.Count -ge 1) { $completedArgs[0] } else { '' }
        return @($topLevelGroups | Where-Object { $_ -like "$prefix*" } | ForEach-Object {
            [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
        })
    }

    $group = $completedArgs[0].ToLower()
    $groupDef = _azGetGroupDef $group
    if (-not $groupDef) { return @() }

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

# Register native argument completer for both az and az.exe
foreach ($_cmd in @('az', 'az.exe')) {
    Register-ArgumentCompleter -Native -CommandName $_cmd -ScriptBlock {
        param($wordToComplete, $commandAst, $cursorPosition)
        $line = $commandAst.ToString()
        _azGetCompletions $line $cursorPosition
    }
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

        if ($line -match '^\s*((\S*[\\/])?(az(\.exe)?))\s') {
            $azCmd = $Matches[1]   # full command: e.g. "artifacts\...\az.exe" or "az"
            $azPath = $Matches[2]  # path prefix: e.g. "artifacts\...\" or $null

            # Check the actual binary is azclips, not system azure-cli
            $isAzclips = _azIsAzclips $azCmd
            if ($isAzclips) {
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

Write-Host "az tab completion registered (dynamic). Press Tab after 'az ' to see suggestions." -ForegroundColor Green
