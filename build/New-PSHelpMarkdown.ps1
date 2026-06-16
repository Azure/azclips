# Scaffolds src/PS/<Module>/help/<Cmdlet>.md for every module via PlatyPS.
# Idempotent — only files that don't already exist are copied; hand-edited markdown is never touched.
# Post-processes PlatyPS's `{{ Fill in ... }}` placeholders into TODO defaults derived from the cmdlet binary.
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',

    # Modules are discovered from src/PS/<Name>/<Name>.psd1 at runtime — see
    # the discovery block below. The ArgumentCompleter restores tab-completion
    # that we lost by dropping the static [ValidateSet]; explicit validation
    # against the discovered set happens after $psSrcRoot is resolved.
    [ArgumentCompleter({
        param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameters)
        $psRoot = Join-Path $PSScriptRoot '../src/PS'
        if (-not (Test-Path $psRoot)) { return }
        Get-ChildItem -Path $psRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { (Test-Path (Join-Path $_.FullName "$($_.Name).psd1")) -and $_.Name -like "$wordToComplete*" } |
            ForEach-Object { $_.Name }
    })]
    [string[]]$Module
)

$ErrorActionPreference = 'Stop'

Import-Module Microsoft.PowerShell.PlatyPS -RequiredVersion 1.0.1

# Replaces PlatyPS `{{ Fill in ... }}` placeholders with TODO-flagged defaults
# derived from the cmdlet name + required parameters.
function Convert-PlatyPSPlaceholders
{
    param(
        [Parameter(Mandatory)][string]$Content,
        [Parameter(Mandatory)][string]$CmdletName,
        [string[]]$RequiredParameters = @()
    )

    $synopsisDefault = "TODO - one-line summary of $CmdletName."
    $descriptionDefault = "TODO - describe what $CmdletName does and when to use it."

    # Force Locale to en-US regardless of the host's CurrentCulture. PlatyPS 1.0.1 seeds
    # Locale from [CultureInfo]::CurrentCulture, so a contributor on en-AU/en-GB/etc.
    # would scaffold drift that mismatches our hardcoded en-US MAML output dir
    # (build.ps1 emits artifacts/.../en-US/<dll>-Help.xml regardless). The frontmatter
    # is purely informational, but inconsistent values produce review-noise and
    # metadata drift across the corpus.
    $Content = $Content -replace '(?m)^Locale:\s*\S+\s*$', 'Locale: en-US'

    $Content = $Content -replace '\{\{\s*Fill in the Synopsis\s*\}\}', $synopsisDefault
    $Content = $Content -replace '\{\{\s*Fill in the Description\s*\}\}', $descriptionDefault
    $Content = $Content -replace '\{\{\s*Add example description here\s*\}\}', "TODO - describe the example."

    # Replace the legacy schema-2.0.0 example code body with a one-liner.
    $exampleArgs = (@($RequiredParameters) | Where-Object { $_ } | ForEach-Object { "-{0} <{1}>" -f $_, $_.ToLower() }) -join ' '
    $exampleLine = if ($exampleArgs) { "$CmdletName $exampleArgs" } else { $CmdletName }
    $Content = $Content -replace 'PS C:\\>\s*\{\{\s*Add example code here\s*\}\}', "PS C:\> $exampleLine"

    # New schema (2024-05-01): PlatyPS emits empty `### <Title>` blocks under `## EXAMPLES`
    # when the cmdlet has no [Example] metadata. Backfill with a TODO + fenced code stub.
    #
    # Note: `````` inside a here-string is 3 literal backticks (each backtick is the PS escape char),
    # so the emitted markdown is a normal ```powershell fence (PR #226 review false-positive).
    $exampleStub = @"
TODO - describe the example.

``````powershell
$exampleLine
``````
"@

    $lines = $Content -split "`r?`n"
    $output = New-Object System.Collections.Generic.List[string]
    $inExamples = $false
    $examplesHeaderIndex = -1
    $exampleHeadingsSeen = 0
    $i = 0
    while ($i -lt $lines.Count)
    {
        $line = $lines[$i]
        $output.Add($line)

        if ($line -match '^##\s+EXAMPLES\s*$')
        {
            $inExamples = $true
            $examplesHeaderIndex = $output.Count - 1
            $exampleHeadingsSeen = 0
            $i++
            continue
        }
        elseif ($inExamples -and $line -match '^##\s+(?!#)')
        {
            # Reached the next H2 with no `###` examples seen — inject a default stub so PSL8 passes.
            if ($exampleHeadingsSeen -eq 0 -and $examplesHeaderIndex -ge 0)
            {
                $insertAt = $output.Count - 1
                $stubBlock = @('', "### Example 1: $CmdletName", '', 'TODO - describe the example.', '', '```powershell', $exampleLine, '```', '')
                for ($k = 0; $k -lt $stubBlock.Count; $k++)
                {
                    $output.Insert($insertAt + $k, $stubBlock[$k])
                }
            }
            $inExamples = $false
        }

        if ($inExamples -and $line -match '^###\s+\S')
        {
            $exampleHeadingsSeen++
            # Look ahead to the next heading. Replace the body with our stub when it's blank
            # OR has no ``` fence — PlatyPS 1.0.1 + schema 2024-05-01 pre-renders unfenced
            # invocation lines that would otherwise trip PSL8 (NoExamples).
            $j = $i + 1
            $bodyBlank = $true
            $bodyHasFence = $false
            while ($j -lt $lines.Count -and $lines[$j] -notmatch '^#{2,3}\s')
            {
                if ($lines[$j].Trim().Length -gt 0) { $bodyBlank = $false }
                if ($lines[$j] -match '^\s*```') { $bodyHasFence = $true; break }
                $j++
            }
            if ($bodyBlank -or -not $bodyHasFence)
            {
                # Swallow the existing fenceless body up to the next heading, then inject our stub.
                $skipTo = $i + 1
                while ($skipTo -lt $lines.Count -and $lines[$skipTo] -notmatch '^#{2,3}\s')
                {
                    $skipTo++
                }
                $output.Add('')
                foreach ($stubLine in ($exampleStub -split "`r?`n")) { $output.Add($stubLine) }
                $output.Add('')
                $i = $skipTo
                continue
            }
        }

        $i++
    }

    # Collapse duplicate `[Online Version]()` entries under `## RELATED LINKS`. PlatyPS 1.0.1's
    # New-MarkdownCommandHelp -ModuleInfo seeds Links with one entry per cmdlet (all empty when
    $merged = $output -join "`n"
    $merged = [regex]::Replace(
        $merged,
        '(?ms)(^## RELATED LINKS\s*\r?\n)(?:\s*\r?\n)*((?:- \[[^\]]+\]\([^)]*\)\s*\r?\n?)+)',
        {
            param($m)
            $header = $m.Groups[1].Value
            $links = $m.Groups[2].Value -split "`r?`n" |
                Where-Object { $_ -match '^- \[' } |
                Select-Object -Unique
            $body = if ($links) { ($links -join "`n") + "`n" } else { '' }
            return $header + "`n" + $body
        })

    return $merged
}

$repoRoot = Resolve-Path "$PSScriptRoot/.."
$psSrcRoot = Join-Path $repoRoot "src/PS"

# Discover modules from src/PS/<Name>/<Name>.psd1 — single source of truth.
# Avoids drift as new modules (Storage, Network, ...) land without touching this script.
$discovered = @(
    Get-ChildItem -Path $psSrcRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { Test-Path (Join-Path $_.FullName "$($_.Name).psd1") } |
        Select-Object -ExpandProperty Name |
        Sort-Object
)

if (-not $discovered)
{
    throw "No PowerShell modules found under '$psSrcRoot'. Expected layout: src/PS/<Name>/<Name>.psd1."
}

if ($Module)
{
    $unknown = @($Module | Where-Object { $_ -notin $discovered })
    if ($unknown)
    {
        throw "Unknown module(s): $($unknown -join ', '). Available: $($discovered -join ', ')."
    }
    $modules = $Module
}
else
{
    $modules = $discovered
}

foreach ($name in $modules)
{
    $manifestPath = Join-Path $psSrcRoot "$name/$name.psd1"
    $helpDir = Join-Path $psSrcRoot "$name/help"

    if (-not (Test-Path $manifestPath))
    {
        Write-Warning "Skipping $name : manifest not found at $manifestPath"
        continue
    }

    if (-not (Test-Path $helpDir))
    {
        New-Item -ItemType Directory -Path $helpDir -Force | Out-Null
    }

    $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("CirrusHelpScaffold-" + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

    Write-Host "Scaffolding markdown for module '$name' -> $helpDir"

    try
    {
        # Run PlatyPS in an isolated child session (artifact DLLs released afterward) and
        # also reflect on each cmdlet to capture required parameters as JSON for the post-processor.
        # New-MarkdownCommandHelp emits files under $tempDir/<ModuleName>/.
        pwsh -NoProfile -Command {
            param($manifestPath, $tempDir, $moduleName)
            Import-Module Microsoft.PowerShell.PlatyPS -RequiredVersion 1.0.1
            $mod = Import-Module $manifestPath -Force -PassThru
            New-MarkdownCommandHelp -ModuleInfo $mod -OutputFolder $tempDir -Force | Out-Null

            $cmdletInfo = @{}
            foreach ($cmd in (Get-Command -Module $moduleName -CommandType Cmdlet,Function))
            {
                $required = @()
                foreach ($paramSet in $cmd.ParameterSets)
                {
                    foreach ($p in $paramSet.Parameters)
                    {
                        if ($p.IsMandatory -and $required -notcontains $p.Name -and
                            $p.Name -notin @('Verbose','Debug','ErrorAction','ErrorVariable','WarningAction','WarningVariable','OutBuffer','OutVariable','PipelineVariable','InformationAction','InformationVariable','WhatIf','Confirm','ProgressAction'))
                        {
                            $required += $p.Name
                        }
                    }
                }
                $cmdletInfo[$cmd.Name] = $required
            }

            $cmdletInfo | ConvertTo-Json -Depth 3 | Set-Content -Path (Join-Path $tempDir '_cmdlet-info.json') -Encoding utf8
        } -Args $manifestPath, $tempDir, $name

        $cmdletInfoPath = Join-Path $tempDir '_cmdlet-info.json'
        $cmdletInfo = if (Test-Path $cmdletInfoPath) {
            Get-Content $cmdletInfoPath -Raw | ConvertFrom-Json -AsHashtable
        } else { @{} }

        # New-MarkdownCommandHelp emits files under $tempDir/<ModuleName>/.
        $scaffoldRoot = Join-Path $tempDir $name
        if (-not (Test-Path $scaffoldRoot))
        {
            # Fallback for legacy layout (in case a future tool change flattens output).
            $scaffoldRoot = $tempDir
        }

        $copied = 0
        $skipped = 0
        Get-ChildItem -Path $scaffoldRoot -Filter '*.md' -File | ForEach-Object {
            $target = Join-Path $helpDir $_.Name
            if (Test-Path $target)
            {
                $skipped++
                return
            }

            $content = Get-Content $_.FullName -Raw
            $cmdletName = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
            $required = if ($cmdletInfo.ContainsKey($cmdletName)) { $cmdletInfo[$cmdletName] } else { @() }

            $content = Convert-PlatyPSPlaceholders -Content $content -CmdletName $cmdletName -RequiredParameters $required
            Set-Content -Path $target -Value $content -Encoding utf8 -NoNewline
            $copied++
        }

        Write-Host "  $copied created, $skipped skipped (already existed)"
    }
    finally
    {
        Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Done."
