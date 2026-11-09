# Scaffolds src/PS/<Module>/help/<Cmdlet>.md for every module via PlatyPS.
# Default: idempotent — only writes files that don't exist yet.
# -Refresh: re-syncs existing .md against the live cmdlet, strips PlatyPS's UTF-8 BOM,
#           and re-normalizes {{ Fill in ... }} placeholders. Use to clear PSL14 drift from Test-PSHelpMarkdown.ps1.
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',

    # Modules are discovered from src/PS/<Name>/<Name>.psd1 at runtime (see block below).
    # ArgumentCompleter restores tab-completion in lieu of a static [ValidateSet].
    [ArgumentCompleter({
        param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameters)
        $psRoot = Join-Path $PSScriptRoot '../src/PS'
        if (-not (Test-Path $psRoot)) { return }
        Get-ChildItem -Path $psRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { (Test-Path (Join-Path $_.FullName "$($_.Name).psd1")) -and $_.Name -like "$wordToComplete*" } |
            ForEach-Object { $_.Name }
    })]
    [string[]]$Module,

    [switch]$Refresh
)

$ErrorActionPreference = 'Stop'

Import-Module Microsoft.PowerShell.PlatyPS -RequiredVersion 1.0.1
. (Join-Path $PSScriptRoot 'Convert-PSHelpSyntax.ps1')

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

    # Force Locale to en-US: PlatyPS 1.0.1 seeds Locale from CurrentCulture, so a contributor on
    # en-AU/en-GB would scaffold drift against our hardcoded en-US MAML output dir.
    $Content = $Content -replace '(?m)^Locale:\s*\S+\s*$', 'Locale: en-US'

    $Content = $Content -replace '\{\{\s*Fill in the Synopsis\s*\}\}', $synopsisDefault
    $Content = $Content -replace '\{\{\s*Fill in the Description\s*\}\}', $descriptionDefault
    $Content = $Content -replace '\{\{\s*Add example description here\s*\}\}', "TODO - describe the example."

    # Replace the legacy schema-2.0.0 example code body with a one-liner.
    $exampleArgs = (@($RequiredParameters) | Where-Object { $_ } | ForEach-Object { "-{0} <{1}>" -f $_, $_.ToLower() }) -join ' '
    $exampleLine = if ($exampleArgs) { "$CmdletName $exampleArgs" } else { $CmdletName }
    $Content = $Content -replace 'PS C:\\>\s*\{\{\s*Add example code here\s*\}\}', "PS C:\> $exampleLine"

    # New schema (2024-05-01): PlatyPS emits empty `### <Title>` blocks under `## EXAMPLES`
    # for cmdlets without [Example] metadata. Backfill with a TODO + fenced code stub.
    # (`````` inside a here-string = 3 literal backticks → normal ```powershell fence in markdown.)
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

    return Convert-PSHelpSyntax -Content $merged
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

    if ($Refresh)
    {
        if (-not (Test-Path $helpDir))
        {
            Write-Warning "Skipping refresh for $name : $helpDir does not exist. Drop -Refresh to scaffold from scratch."
            continue
        }

        $mdFiles = @(Get-ChildItem -Path $helpDir -Filter '*.md' -File -ErrorAction SilentlyContinue)
        if ($mdFiles.Count -eq 0)
        {
            Write-Warning "Skipping refresh for $name : no .md files under $helpDir."
            continue
        }

        Write-Host "Refreshing markdown for module '$name' ($($mdFiles.Count) file(s)) -> $helpDir"

        # Hide any locally-built MAML so Update-MarkdownCommandHelp reflects on the DLL alone,
        # matching CI conditions. If MAML is present, PlatyPS pulls Outputs/Inputs/etc. from it
        # and produces an md that stays in drift against fresh-checkout CI. Mirrors the same
        # dance in build/Test-PSHelpMarkdown.ps1.
        $moduleDir  = Split-Path $manifestPath -Parent
        $enUsDir    = Join-Path $moduleDir 'en-US'
        $enUsHidden = $null
        if (Test-Path $enUsDir)
        {
            $enUsHidden = "$enUsDir.hidden-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
            Move-Item -Path $enUsDir -Destination $enUsHidden -Force
        }

        $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("AzclipsHelpRefresh-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

        try
        {
            # Isolated child pwsh: same YamlDotNet isolation as the scaffold path. Runs
            # Update-MarkdownCommandHelp against each existing md, then reflects on the module
            # to capture required parameters as JSON for the placeholder post-processor.
            pwsh -NoProfile -Command {
                param($manifestPath, $mdPaths, $tempDir, $moduleName, $syntaxHelper)
                $ErrorActionPreference = 'Stop'
                . $syntaxHelper
                Import-Module Microsoft.PowerShell.PlatyPS -RequiredVersion 1.0.1
                # Name-form RequiredModules refer to sibling modules; import them first.
                foreach ($required in @((Import-PowerShellDataFile $manifestPath).RequiredModules))
                {
                    $name = if ($required -is [hashtable]) { $required.ModuleName } else { $required }
                    if ($name -and $name -notmatch '[\/]')
                    {
                        $sibling = Join-Path (Split-Path (Split-Path $manifestPath -Parent) -Parent) $name "$name.psd1"
                        if (Test-Path $sibling) { Import-Module $sibling -ErrorAction Stop }
                    }
                }
                $mod = Import-Module $manifestPath -Force -PassThru
                foreach ($p in $mdPaths)
                {
                    $tempMd = Join-Path $tempDir ([IO.Path]::GetFileName($p))
                    $content = Convert-PSHelpSyntax -Content (Get-Content -LiteralPath $p -Raw) -ForPlatyPS
                    Set-Content -LiteralPath $tempMd -Value $content -NoNewline
                    Update-MarkdownCommandHelp -Path $tempMd -NoBackup | Out-Null
                }

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
            } -Args $manifestPath, $mdFiles.FullName, $tempDir, $name, (Join-Path $PSScriptRoot 'Convert-PSHelpSyntax.ps1')

            if ($LASTEXITCODE -ne 0) { throw "PlatyPS refresh failed for module '$name' (exit $LASTEXITCODE)." }

            $cmdletInfoPath = Join-Path $tempDir '_cmdlet-info.json'
            $cmdletInfo = if (Test-Path $cmdletInfoPath) {
                Get-Content $cmdletInfoPath -Raw | ConvertFrom-Json -AsHashtable
            } else { @{} }

            # PlatyPS 1.0.1 writes with a UTF-8 BOM; re-emit each file BOM-free (matches
            # the repo convention used by the scaffold path and build.ps1's Write-Utf8NoBomFile)
            # and run the same placeholder normalizer so new params inherit the TODO defaults.
            $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
            $refreshed = 0
            foreach ($file in $mdFiles)
            {
                $content = [System.IO.File]::ReadAllText((Join-Path $tempDir $file.Name))
                $cmdletName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
                $required = if ($cmdletInfo.ContainsKey($cmdletName)) { $cmdletInfo[$cmdletName] } else { @() }

                $content = Convert-PlatyPSPlaceholders -Content $content -CmdletName $cmdletName -RequiredParameters $required
                [System.IO.File]::WriteAllText($file.FullName, $content, $utf8NoBom)
                $refreshed++
            }

            Write-Host "  $refreshed refreshed"
        }
        finally
        {
            Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
            if ($enUsHidden -and (Test-Path $enUsHidden))
            {
                Move-Item -Path $enUsHidden -Destination $enUsDir -Force
            }
        }

        continue
    }

    if (-not (Test-Path $helpDir))
    {
        New-Item -ItemType Directory -Path $helpDir -Force | Out-Null
    }

    $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("AzclipsHelpScaffold-" + [Guid]::NewGuid().ToString('N'))
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
            # Name-form RequiredModules refer to sibling modules; import them first.
            foreach ($required in @((Import-PowerShellDataFile $manifestPath).RequiredModules))
            {
                $name = if ($required -is [hashtable]) { $required.ModuleName } else { $required }
                if ($name -and $name -notmatch '[\/]')
                {
                    $sibling = Join-Path (Split-Path (Split-Path $manifestPath -Parent) -Parent) $name "$name.psd1"
                    if (Test-Path $sibling) { Import-Module $sibling -ErrorAction Stop }
                }
            }
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
