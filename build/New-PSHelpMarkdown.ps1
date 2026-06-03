# Scaffolds src/PS/<Module>/help/<Cmdlet>.md for every module via PlatyPS.
#
# Idempotent: PlatyPS is run against a temp directory and only files that don't
# already exist under src/PS/<Module>/help/ are copied across. Existing
# hand-edited markdown is never touched.
#
# Newly-scaffolded files are post-processed to replace PlatyPS's literal
# `{{ Fill in ... }}` placeholders with sensible defaults derived from the
# cmdlet binary (Verb-Noun in SYNOPSIS, an EXAMPLES block built from required
# parameters), matching the shape of the CLI scaffolder.
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',

    [ValidateSet('Compute', 'Config', 'Login', 'Resources')]
    [string[]]$Module
)

$ErrorActionPreference = 'Stop'

Import-Module platyPS -RequiredVersion 0.14.2

# Replaces PlatyPS's literal `{{ Fill in ... }}` placeholders with TODO-flagged
# defaults derived from the cmdlet name + required parameters. The intent
# matches the CLI scaffolder: the file is still obviously a stub, but it has
# enough shape that `Get-Help` is readable and authors can edit in place.
function Convert-PlatyPSPlaceholders
{
    param(
        [Parameter(Mandatory)][string]$Content,
        [Parameter(Mandatory)][string]$CmdletName,
        [string[]]$RequiredParameters = @()
    )

    $synopsisDefault = "TODO - one-line summary of $CmdletName."
    $descriptionDefault = "TODO - describe what $CmdletName does and when to use it."

    $Content = $Content -replace '\{\{\s*Fill in the Synopsis\s*\}\}', $synopsisDefault
    $Content = $Content -replace '\{\{\s*Fill in the Description\s*\}\}', $descriptionDefault
    $Content = $Content -replace '\{\{\s*Add example description here\s*\}\}', "TODO - describe the example."

    # Replace the example code body with a one-liner that uses the required parameters.
    $exampleArgs = ($RequiredParameters | ForEach-Object { "-{0} <{1}>" -f $_, $_.ToLower() }) -join ' '
    $exampleLine = if ($exampleArgs) { "$CmdletName $exampleArgs" } else { $CmdletName }
    $Content = $Content -replace 'PS C:\\>\s*\{\{\s*Add example code here\s*\}\}', "PS C:\> $exampleLine"

    return $Content
}

$repoRoot = Resolve-Path "$PSScriptRoot/.."
$psSrcRoot = Join-Path $repoRoot "src/PS"

$modules = if ($Module) { $Module } else { 'Compute','Config','Login','Resources' }

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
        # Run PlatyPS in an isolated child session so artifact DLLs are released afterwards.
        # The child also reflects on each cmdlet to discover required parameters, which we
        # serialize as JSON next to the markdown for the post-processor below.
        pwsh -NoProfile -Command {
            param($manifestPath, $tempDir, $moduleName)
            Import-Module platyPS -RequiredVersion 0.14.2
            Import-Module $manifestPath -Force
            New-MarkdownHelp -Module $moduleName -OutputFolder $tempDir -Force -AlphabeticParamsOrder | Out-Null

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

        $copied = 0
        $skipped = 0
        Get-ChildItem -Path $tempDir -Filter '*.md' -File | ForEach-Object {
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
