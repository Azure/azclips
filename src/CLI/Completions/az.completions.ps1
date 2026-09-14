# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# az Tab Completion for PowerShell (Dynamic)
# Usage: . <path-to>/az.completions.ps1
# Or add to your $PROFILE to auto-load on every session.
#
# Completions are resolved dynamically by invoking `az --completions [command-group]` (or no args for full tree)
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

    $subcommands = if ($groupDef.ContainsKey('_subcommands')) { @($groupDef['_subcommands']) } else { @() }

    # The command path within the group is the leading run of non-flag tokens after the
    # verb, excluding the word currently being completed.
    $limit = if ($isCompletingNewToken) { $completedArgs.Count } else { $completedArgs.Count - 1 }
    $subPath = @()
    for ($i = 1; $i -lt $limit; $i++) {
        if ($completedArgs[$i] -like '-*') { break }
        $subPath += $completedArgs[$i].ToLower()
    }

    $wordToComplete = if (-not $isCompletingNewToken) { $completedArgs[-1] } else { '' }
    $results = @()

    # 1) Deeper navigation: the next token of every command whose path extends $subPath.
    $nextTokens = @()
    foreach ($subcommand in $subcommands) {
        $tokens = @($subcommand -split '\s+' | Where-Object { $_ -ne '' })
        if ($tokens.Count -le $subPath.Count) { continue }
        $matchesPrefix = $true
        for ($i = 0; $i -lt $subPath.Count; $i++) {
            if ($tokens[$i] -ne $subPath[$i]) { $matchesPrefix = $false; break }
        }
        if ($matchesPrefix) {
            $next = [string]$tokens[$subPath.Count]
            if ($nextTokens -notcontains $next) { $nextTokens += $next }
        }
    }
    foreach ($token in $nextTokens) {
        if ($token -like "$wordToComplete*") {
            $fullPath = (@($group) + $subPath + $token) -join ' '
            $results += [System.Management.Automation.CompletionResult]::new($token, $token, 'ParameterValue', $fullPath)
        }
    }

    # 2) Parameters: when $subPath names a complete command (or the verb itself is a
    #    command), offer its flags, excluding those already present on the line.
    $joined = ($subPath -join ' ')
    $paramList = @()
    if ($joined -and $groupDef.ContainsKey($joined)) {
        $paramList = @($groupDef[$joined])
    }
    elseif (-not $joined -and $groupDef.ContainsKey('_params')) {
        $paramList = @($groupDef['_params'])
    }

    if ($paramList.Count -gt 0) {
        $typedArgs = if ($isCompletingNewToken) { $completedArgs } else { @($completedArgs[0..($completedArgs.Count - 2)]) }
        $usedParams = @($typedArgs | Where-Object { $_ -like '--*' })
        $results += @($paramList | Where-Object {
            $_ -like "$wordToComplete*" -and $_ -notin $usedParams
        } | ForEach-Object {
            [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterName', $_)
        })
    }

    return $results
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

if (-not $script:_azTabExpansion2Installed) {
    $script:_azPrevTabExpansion2 = if (Get-Command TabExpansion2 -CommandType Function -ErrorAction SilentlyContinue) {
        $Function:global:TabExpansion2
    } else {
        $null
    }
    $script:_azTabExpansion2Installed = $true
}

# Wrap TabExpansion2 for path-qualified az.exe completions, then delegate all fallback
# cases to the user's previous TabExpansion2 implementation when one existed.
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
            $azCmd = $Matches[1]

            $resolvedExe = $null
            if ($azCmd -match '[\\/]') {
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
                $cmd = Get-Command $azCmd -ErrorAction SilentlyContinue
                if ($cmd) { $resolvedExe = $cmd.Source }
            }

            if ($resolvedExe -and -not $global:_azCompletionsCache.notAzclips.ContainsKey($resolvedExe)) {
                $results = @(_azGetCompletions -exePath $resolvedExe -inputText $line -cursorPos $cursor)
                if ($global:_azCompletionsCache.isAzclips -eq $true -and $results.Count -gt 0) {
                    $textUpToCursor = $line.Substring(0, [Math]::Min($cursor, $line.Length))
                    $lastSpace = $textUpToCursor.LastIndexOf(' ')
                    $replIndex = $lastSpace + 1
                    $replLength = $cursor - $replIndex

                    $completionResultMatches = [System.Collections.ObjectModel.Collection[System.Management.Automation.CompletionResult]]::new()
                    foreach ($r in $results) { $completionResultMatches.Add($r) }
                    return [System.Management.Automation.CommandCompletion]::new($completionResultMatches, 0, $replIndex, $replLength)
                }
            }
        }

        if ($script:_azPrevTabExpansion2) {
            return & $script:_azPrevTabExpansion2 @PSBoundParameters
        }

        if ($PSCmdlet.ParameterSetName -eq 'ScriptInputSet') {
            return [System.Management.Automation.CommandCompletion]::CompleteInput($inputScript, $cursorColumn, $options)
        } else {
            return [System.Management.Automation.CommandCompletion]::CompleteInput($ast, $tokens, $positionOfCursor, $options)
        }
    }
}

Write-Host "az argument completion registered (dynamic)." -ForegroundColor Green
