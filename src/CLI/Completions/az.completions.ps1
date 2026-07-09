# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# az Tab Completion for PowerShell (Dynamic)
# Usage: . <path-to>/az.completions.ps1
# Or add to your $PROFILE to auto-load on every session.
#
# Completions are resolved dynamically by invoking `az --completions [group] [subcommand]`
# at tab time. This ensures completions match whichever `az` binary is on PATH.
#
# Detection: The script auto-detects azclips by calling `az --completions`.
# If the binary returns valid JSON, it is treated as azclips; otherwise completions are skipped.
# The result is cached per session so detection only runs once.
# Completions are cached for the current PowerShell session and invalidated if the resolved exe path changes (e.g., after an update)
# or if the completion.enabled setting changes in user config.
# To refresh manually, run:
#   _azClearCache

# Session cache for completions tree (persists until exe path/config stamp changes)
# isAzclips: $null = unknown, $true = azclips detected, $false = not azclips
# notAzclips: hashtable of exe paths confirmed not to be azclips (per-exe detection)
$global:_azCompletionsCache = @{ tree = $null; exe = $null; configStamp = $null; isAzclips = $null; notAzclips = @{} }

function global:_azGetConfigStamp {
    # Prefer semantic invalidation based on completion.enabled.
    # Fall back to file metadata if the config cannot be parsed.
    $configPath = Join-Path $HOME '.azclips/config.json'
    if (-not (Test-Path $configPath)) {
        # Missing config means defaults apply (completion.enabled=true).
        return "$configPath|enabled:true|missing"
    }

    try {
        $raw = Get-Content -LiteralPath $configPath -Raw -ErrorAction Stop
        if ([string]::IsNullOrWhiteSpace($raw)) {
            return "$configPath|enabled:true|empty"
        }

        $doc = $raw | ConvertFrom-Json -AsHashtable -ErrorAction Stop
        $enabled = $true

        if ($doc -is [System.Collections.Hashtable]) {
            if ($doc.ContainsKey('completion')) {
                $completionNode = $doc['completion']
                if ($completionNode -is [System.Collections.Hashtable] -and $completionNode.ContainsKey('enabled')) {
                    $enabled = [System.Convert]::ToBoolean($completionNode['enabled'])
                } elseif ($completionNode -and $completionNode.PSObject.Properties['enabled']) {
                    $enabled = [System.Convert]::ToBoolean($completionNode.enabled)
                }
            } elseif ($doc.ContainsKey('completion.enabled')) {
                # Legacy flat key support.
                $enabled = [System.Convert]::ToBoolean($doc['completion.enabled'])
            }
        }

        return "$configPath|enabled:$enabled"
    } catch {
        try {
            $item = Get-Item -LiteralPath $configPath -ErrorAction Stop
            return "$configPath|fallback-meta|$($item.Length)|$($item.LastWriteTimeUtc.Ticks)"
        } catch {
            return "$configPath|error"
        }
    }
}

function global:_azQueryCompletions {
    param([string]$exePath, [string[]]$context)
    if (-not $exePath) { return $null }
    # Skip exes already known to not be azclips
    if ($global:_azCompletionsCache.notAzclips.ContainsKey($exePath)) { return $null }
    $configStamp = _azGetConfigStamp
    
    # Check cache validity: invalidate on exe path or config stamp change
    $isCacheValid = ($null -ne $global:_azCompletionsCache.tree) -and 
                    ($global:_azCompletionsCache.exe -eq $exePath) -and
                    ($global:_azCompletionsCache.configStamp -eq $configStamp)
    
    if ($isCacheValid) {
        # Use cached tree, navigate to requested context
        $tree = $global:_azCompletionsCache.tree
        foreach ($part in $context) {
            if ($tree -is [System.Collections.Hashtable] -and $tree.ContainsKey($part)) {
                $tree = $tree[$part]
            } else {
                $isCacheValid = $false
                break
            }
        }
        if ($isCacheValid) { return $tree }
    }
    
    # Cache miss or exe changed: query fresh
    $args_ = @('--completions') + $context
    try {
        $json = & $exePath @args_ 2>$null
        if ($json) {
            $joined = ($json | Where-Object { $_ -match '^\s*[\{\[]' }) -join ''
            if ($joined) {
                $result = $joined | ConvertFrom-Json -AsHashtable
                $global:_azCompletionsCache.isAzclips = $true
                
                # Only cache root-level query (no context) to minimize memory
                if ($context.Count -eq 0) {
                    $global:_azCompletionsCache.tree = $result
                    $global:_azCompletionsCache.exe = $exePath
                    $global:_azCompletionsCache.configStamp = $configStamp
                }
                return $result
            }
        }
    } catch {}
    # Output was not valid JSON → not azclips; remember per exe so other binaries aren't blocked
    $global:_azCompletionsCache.notAzclips[$exePath] = $true
    return $null
}

function global:_azGetTopLevelGroups {
    param([string]$exePath)
    $tree = _azQueryCompletions -exePath $exePath
    if ($tree -and $tree.Count -gt 0) {
        return @($tree.Keys | Sort-Object)
    }
    return @()
}

function global:_azGetGroupDef {
    param([string]$exePath, [string]$group)
    $tree = _azQueryCompletions -exePath $exePath -context @($group)
    if ($tree -and $tree.Count -gt 0) {
        return $tree
    }
    return $null
}

# Manual cache refresh function (use if needed)
function global:_azClearCache {
    $global:_azCompletionsCache = @{ tree = $null; exe = $null; configStamp = $null; isAzclips = $null; notAzclips = @{} }
    Write-Host "az completions cache cleared. Completions will be refreshed on next Tab press." -ForegroundColor Green
}

# Core completion logic — returns CompletionResult[] for a given input line and cursor
function global:_azGetCompletions {
    param([string]$exePath, [string]$inputText, [int]$cursorPos)

    $textUpToCursor = $inputText.Substring(0, $cursorPos)
    $allTokens = @($textUpToCursor -split '\s+' | Where-Object { $_ -ne '' })
    $completedArgs = @($allTokens | Select-Object -Skip 1)
    $isCompletingNewToken = $textUpToCursor.EndsWith(' ')

    $topLevelGroups = _azGetTopLevelGroups -exePath $exePath
    if (-not $topLevelGroups -or $topLevelGroups.Count -eq 0) { return @() }

    # --- Level 0: completing command group ---
    if ($completedArgs.Count -eq 0 -or ($completedArgs.Count -eq 1 -and -not $isCompletingNewToken)) {
        $prefix = if ($completedArgs.Count -ge 1) { $completedArgs[0] } else { '' }
        return @($topLevelGroups | Where-Object { $_ -like "$prefix*" } | ForEach-Object {
            [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
        })
    }

    $group = $completedArgs[0].ToLower()
    $groupDef = _azGetGroupDef -exePath $exePath -group $group
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
        $cmd = Get-Command ($commandAst.GetCommandName()) -ErrorAction SilentlyContinue
        $exe = if ($cmd) { $cmd.Source } else { $null }
        if ($exe) { _azGetCompletions -exePath $exe -inputText $line -cursorPos $cursorPosition }
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

        if ($line -match '^\s*((\S*[\\/])?(az(\.exe)?))(\s|$)') {
            $azCmd = $Matches[1]   # e.g. "artifacts\...\az.exe" or "az"

            # Resolve the actual exe path from what the user typed
            $resolvedExe = $null
            if ($azCmd -match '[\\/]') {
                # Path-qualified: resolve directly, ensure .exe suffix for invocability
                foreach ($candidate in $azCmd, "$azCmd.exe") {
                    if (Test-Path $candidate) {
                        $resolvedExe = (Resolve-Path $candidate).Path
                        if ($resolvedExe -and -not $resolvedExe.EndsWith('.exe', [System.StringComparison]::OrdinalIgnoreCase)) {
                            if (Test-Path "$resolvedExe.exe") { $resolvedExe = "$resolvedExe.exe" }
                        }
                        break
                    }
                }
            } else {
                # Bare command: resolve via PATH (same binary the shell would execute)
                $cmd = Get-Command $azCmd -ErrorAction SilentlyContinue
                if ($cmd) { $resolvedExe = $cmd.Source }
            }
            if (-not $resolvedExe) {
                # Can't find the binary → fall through to default
            }
            # Check cached detection for this exe
            elseif ($global:_azCompletionsCache.notAzclips.ContainsKey($resolvedExe)) {
                # Already determined not azclips for this specific exe → fall through
            } else {
                $results = @(_azGetCompletions -exePath $resolvedExe -inputText $line -cursorPos $cursor)
                if ($global:_azCompletionsCache.isAzclips -eq $true -and $results.Count -gt 0) {
                    # Confirmed azclips with results — return them
                    $textUpToCursor = $line.Substring(0, [Math]::Min($cursor, $line.Length))
                    $lastSpace = $textUpToCursor.LastIndexOf(' ')
                    $replIndex = $lastSpace + 1
                    $replLength = $cursor - $replIndex

                    $matches = [System.Collections.ObjectModel.Collection[System.Management.Automation.CompletionResult]]::new()
                    foreach ($r in $results) { $matches.Add($r) }
                    return [System.Management.Automation.CommandCompletion]::new($matches, 0, $replIndex, $replLength)
                }
                # isAzclips is $false after query → not azclips, fall through
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
