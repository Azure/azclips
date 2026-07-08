# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# az Tab Completion for PowerShell (Dynamic)
# Usage: . <path-to>/az.completions.ps1
# Or add to your $PROFILE to auto-load on every session.
#
# Completions are resolved dynamically by invoking `az --completion-candidates [group] [subcommand]`
# at tab time. This ensures completions match whichever `az` binary is on PATH.
#
# Set $AZCLIPS_BINARY=1 to ensure we are using autocompletion for azclips (not azure-cli).
# Completions are cached for the current PowerShell session and invalidated if the resolved exe path changes (e.g., after an update)
# or if the completion.enabled setting changes in user config.
# To refresh manually, run:
#   _azClearCache

function global:_azIsAzclips {
    param([string]$exePath)
    
    # Environment variable override: set $env:AZCLIPS_BINARY = '1' for azclips, '0' for old az
    if ($env:AZCLIPS_BINARY -eq '1') { return $true }
    if ($env:AZCLIPS_BINARY -eq '0') { return $false }
    
    # Default: assume old az (unset env var means old az completions)
    return $false
}

function global:_azResolveExe {
    # 1. Same directory as this script (e.g. artifacts/Release/Core.Cli/)
    $local = Join-Path $PSScriptRoot 'az.exe'
    if (Test-Path $local) { return $local }
    $local = Join-Path $PSScriptRoot 'az'
    if (Test-Path $local) { return $local }

    # 2. Parent directory (e.g. script is in Completions/, az.exe is in parent)
    $parent = Split-Path $PSScriptRoot -Parent
    if ($parent) {
        $local = Join-Path $parent 'az.exe'
        if (Test-Path $local) { return $local }
        $local = Join-Path $parent 'az'
        if (Test-Path $local) { return $local }
    }

    # 3. Walk up to find repo root (look for global.json), then check artifacts/
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

# Session cache for candidate output (persists until exe path/config stamp/context changes)
$global:_azCompletionsCache = @{ candidates = $null; exe = $null; configStamp = $null; contextKey = $null }

function global:_azGetConfigStamp {
    $configPath = Join-Path $HOME '.azclips/config.json'
    if (-not (Test-Path $configPath)) {
        # Missing config means defaults apply (completion.enabled=true).
        return "$configPath|enabled:true|missing"
    }

    try {
        $item = Get-Item -LiteralPath $configPath -ErrorAction Stop
        return "$configPath|meta|$($item.Length)|$($item.LastWriteTimeUtc.Ticks)"
    } catch {
        return "$configPath|error"
    }
}

function global:_azQueryCandidates {
    param([string[]]$context)
    $exe = _azResolveExe
    if (-not $exe) { return @() }
    $configStamp = _azGetConfigStamp
    $contextKey = ($context -join '|')
    
    # Check cache validity: invalidate on exe path, config stamp, or context change
    $isCacheValid = ($null -ne $global:_azCompletionsCache.candidates) -and 
                    ($global:_azCompletionsCache.exe -eq $exe) -and
                    ($global:_azCompletionsCache.configStamp -eq $configStamp) -and
                    ($global:_azCompletionsCache.contextKey -eq $contextKey)
    
    if ($isCacheValid) {
        return @($global:_azCompletionsCache.candidates)
    }
    
    $args_ = @('--completion-candidates') + $context
    try {
        $candidates = @(& $exe @args_ 2>$null | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        $global:_azCompletionsCache.candidates = $candidates
        $global:_azCompletionsCache.exe = $exe
        $global:_azCompletionsCache.configStamp = $configStamp
        $global:_azCompletionsCache.contextKey = $contextKey
        return $candidates
    } catch {}
    return @()
}

function global:_azGetTopLevelGroups {
    return @(_azQueryCandidates | Sort-Object)
}

# Manual cache refresh function (use if needed)
function global:_azClearCache {
    $global:_azCompletionsCache = @{ candidates = $null; exe = $null; configStamp = $null; contextKey = $null }
    Write-Host "az completions cache cleared. Completions will be refreshed on next Tab press." -ForegroundColor Green
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
    $groupCandidates = @(_azQueryCandidates @($group))
    if (-not $groupCandidates -or $groupCandidates.Count -eq 0) { return @() }

    # --- Level 1: completing subcommand ---
    if (($completedArgs.Count -eq 1 -and $isCompletingNewToken) -or
        ($completedArgs.Count -eq 2 -and -not $isCompletingNewToken)) {
        $prefix = if ($completedArgs.Count -ge 2) { $completedArgs[1] } else { '' }
        return @($groupCandidates | Where-Object { $_ -like "$prefix*" } | ForEach-Object {
            $resultType = if ($_ -like '--*') { 'ParameterName' } else { 'ParameterValue' }
            $toolTip = if ($_ -like '--*') { $_ } else { "$group $_" }
            [System.Management.Automation.CompletionResult]::new($_, $_, $resultType, $toolTip)
        })
    }

    # --- Level 2: completing parameters ---
    $subcommand = if ($completedArgs.Count -ge 2 -and $completedArgs[1] -notlike '--*') { $completedArgs[1].ToLower() } else { $null }
    $paramStartIndex = if ($subcommand) { 2 } else { 1 }
    $paramList = if ($subcommand) { @(_azQueryCandidates @($group, $subcommand)) } else { $groupCandidates }
    $paramList = @($paramList | Where-Object { $_ -like '--*' })

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

                # For az/az.exe, do not delegate to built-in completion when dynamic completion is disabled
                # or returns no matches. This keeps Tab behavior aligned with completion.enabled=false.
                $emptyMatches = [System.Collections.ObjectModel.Collection[System.Management.Automation.CompletionResult]]::new()
                return [System.Management.Automation.CommandCompletion]::new($emptyMatches, 0, $cursor, 0)
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
