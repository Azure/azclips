# Validates every src/PS/<Module>/help/*.md against the built cmdlet binary by
# running Test-/Import-/Update-/Compare-CommandHelp from Microsoft.PowerShell.PlatyPS 1.0.1
# and emitting PSL13/PSL14 records for schema failures and parameter drift.
#
# Prerequisites: run `dotnet build azclips.slnx` first so the cmdlet DLLs exist.
# Usage:
#   pwsh -File build/Test-PSHelpMarkdown.ps1                       # all modules under src/PS/
#   pwsh -File build/Test-PSHelpMarkdown.ps1 -Module Accounts         # single module
#   pwsh -File build/Test-PSHelpMarkdown.ps1 -ModuleRoot <tempDir> # arbitrary root (smoke fixtures)
[CmdletBinding()]
param(
    [string]$ModuleRoot,

    # ArgumentCompleter mirrors the sibling scaffolder (New-PSHelpMarkdown.ps1).
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

if (-not $ModuleRoot)
{
    $repoRoot   = Resolve-Path (Join-Path $PSScriptRoot '..')
    $ModuleRoot = Join-Path $repoRoot 'src/PS'
}
$ModuleRoot = (Resolve-Path $ModuleRoot).Path

# Anchor for PSL record paths. Prefer repo root for the checked-in tree; fall back to the given
# ModuleRoot for out-of-tree smoke fixtures.
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$pathRoot = if ($ModuleRoot.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) { $repoRoot } else { $ModuleRoot }

$discovered = @(
    Get-ChildItem -Path $ModuleRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { Test-Path (Join-Path $_.FullName "$($_.Name).psd1") } |
        Select-Object -ExpandProperty Name |
        Sort-Object
)

if (-not $discovered)
{
    Write-Host "No PowerShell modules found under '$ModuleRoot'."
    exit 0
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

$totalErrors = 0

foreach ($moduleName in $modules)
{
    $moduleDir = Join-Path $ModuleRoot $moduleName
    $manifest  = Join-Path $moduleDir "$moduleName.psd1"
    $helpDir   = Join-Path $moduleDir 'help'

    if (-not (Test-Path $helpDir))
    {
        # PSL10 in the C# tool already flags a missing help/ folder; don't duplicate.
        continue
    }

    Write-Host "Validating help drift for module '$moduleName' ..."

    # Hide locally-built MAML (gitignored, absent in CI) so Update-CommandHelp reflects on the DLL
    # alone — avoids false-negative "no drift" locally while CI stays red. Restored in finally.
    $enUsDir    = Join-Path $moduleDir 'en-US'
    $enUsHidden = $null
    if (Test-Path $enUsDir)
    {
        $enUsHidden = "$enUsDir.hidden-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
        Move-Item -Path $enUsDir -Destination $enUsHidden -Force
    }

    try
    {
        # Run PlatyPS in an isolated child pwsh (mirrors Publish-AzclipsHelpArtifacts in build.ps1) to
        # avoid YamlDotNet assembly conflicts and to guarantee a clean module unload between iterations.
        pwsh -NoProfile -Command {
        param($manifest, $helpDir, $pathRoot, $syntaxHelper)
        $ErrorActionPreference = 'Continue'

        Import-Module Microsoft.PowerShell.PlatyPS -RequiredVersion 1.0.1 -ErrorAction Stop
        . $syntaxHelper
        # Name-form RequiredModules refer to sibling modules; import them first.
        foreach ($required in @((Import-PowerShellDataFile $manifest).RequiredModules))
        {
            $name = if ($required -is [hashtable]) { $required.ModuleName } else { $required }
            if ($name -and $name -notmatch '[\/]')
            {
                $sibling = Join-Path (Split-Path (Split-Path $manifest -Parent) -Parent) $name "$name.psd1"
                if (Test-Path $sibling) { Import-Module $sibling -ErrorAction Stop }
            }
        }
        Import-Module $manifest -Force -ErrorAction Stop

        # See scratch/CLIPS-652-TEMP-PR-PLAN.md §4.1. Passing this list REPLACES Compare-CommandHelp's
        # built-in defaults, hence Diagnostics/AliasHeaderFound/ParameterNames re-listed.
        $excludes = @(
            'Description', 'Synopsis', 'Examples', 'Notes', 'RelatedLinks',
            'Metadata', 'Diagnostics', 'AliasHeaderFound', 'ParameterNames'
        )

        $errorCount = 0
        $mdFiles = Get-ChildItem -Path $helpDir -Filter '*.md' -File
        foreach ($file in $mdFiles)
        {
            $md      = $file.FullName
            $relPath = [System.IO.Path]::GetRelativePath($pathRoot, $md).Replace('\', '/')

            try
            {
                # Test-MarkdownCommandHelp emits nothing on a clean file; null => pass.
                $schema = Test-MarkdownCommandHelp -LiteralPath $md -DetailView
                $isValid = $true
                if ($null -ne $schema)
                {
                    if ($schema -is [bool])
                    {
                        $isValid = $schema
                    }
                    elseif ($null -ne $schema.IsValid)
                    {
                        $isValid = [bool]$schema.IsValid
                    }
                }

                if (-not $isValid)
                {
                    $reason =
                        if ($schema -is [bool])   { 'Test-MarkdownCommandHelp returned $false' }
                        elseif ($schema.Errors)   { ($schema.Errors | ForEach-Object { $_.ToString() }) -join '; ' }
                        else                      { 'schema invalid (no detail returned)' }
                    Write-Output "PSL13_SchemaFailure|$relPath|$reason"
                    $errorCount++
                    continue
                }
            }
            catch
            {
                Write-Output "PSL13_SchemaFailure|$relPath|Test-MarkdownCommandHelp threw: $($_.Exception.Message)"
                $errorCount++
                continue
            }

            try
            {
                # Update-CommandHelp returns syntax parameters in reflection/declaration order,
                # while Markdown uses canonical PowerShell syntax order (mandatory before optional).
                # Round-trip a temporary copy through the same writer used by -Refresh so comparison
                # detects semantic drift rather than parameter ordering differences.
                $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("AzclipsHelpCompare-" + [Guid]::NewGuid().ToString('N'))
                $tempMd = Join-Path $tempDir $file.Name
                New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
                try
                {
                    $content = Convert-PSHelpSyntax -Content (Get-Content -LiteralPath $md -Raw) -ForPlatyPS
                    Set-Content -LiteralPath $tempMd -Value $content -NoNewline
                    $old = Import-MarkdownCommandHelp -Path $tempMd -ErrorAction Stop 2>$null
                    Update-MarkdownCommandHelp -Path $tempMd -NoBackup -ErrorAction Stop | Out-Null
                    $new = Import-MarkdownCommandHelp -Path $tempMd -ErrorAction Stop 2>$null
                }
                finally
                {
                    Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
                }

                # *>&1 folds Compare-CommandHelp's Information/Verbose trace into $trace as strings.
                $trace = Compare-CommandHelp `
                    -ReferenceCommandHelp $old `
                    -DifferenceCommandHelp $new `
                    -PropertyNamesToExclude $excludes *>&1 |
                    ForEach-Object { $_.ToString() }

                if ($trace -match "'NOT OK'")
                {
                    $driftLines = @($trace | Where-Object { $_ -match '^D ' })
                    if ($driftLines.Count -eq 0)
                    {
                        # Guard: 'NOT OK' without any D lines means our regex missed a diff shape.
                        Write-Output "PSL14_ParameterDrift|$relPath|Compare-CommandHelp reported 'NOT OK' with no parseable D lines. Full trace:`n$($trace -join "`n")"
                        $errorCount++
                    }
                    else
                    {
                        foreach ($line in $driftLines)
                        {
                            Write-Output "PSL14_ParameterDrift|$relPath|$($line.Trim())"
                            $errorCount++
                        }
                    }
                }
            }
            catch
            {
                # Schema-shaped errors -> PSL13 for triage accuracy; everything else -> PSL14.
                $errMsg = $_.Exception.Message
                $psl = if ($errMsg -match 'metadata|schema|frontmatter|YAML|parse') { 'PSL13_SchemaFailure' } else { 'PSL14_ParameterDrift' }
                Write-Output "$psl|$relPath|$errMsg"
                $errorCount++
            }
        }

        exit $errorCount
    } -Args $manifest, $helpDir, $pathRoot, (Join-Path $PSScriptRoot 'Convert-PSHelpSyntax.ps1')

        # $LASTEXITCODE from a child pwsh block is the child's exit code = its error count.
        $totalErrors += [int]$LASTEXITCODE
    }
    finally
    {
        if ($enUsHidden -and (Test-Path $enUsHidden))
        {
            Move-Item -Path $enUsHidden -Destination $enUsDir -Force
        }
    }
}

if ($totalErrors -gt 0)
{
    Write-Host ""
    Write-Host "Help drift validation failed: $totalErrors record(s) across $($modules.Count) module(s)."
    Write-Host "Fix a whole module in one shot:"
    Write-Host "    pwsh -File build/New-PSHelpMarkdown.ps1 -Module <name> -Refresh"
    Write-Host "Or fix a single file:"
    Write-Host "    pwsh -c ""Import-Module Microsoft.PowerShell.PlatyPS -RequiredVersion 1.0.1; Update-MarkdownCommandHelp -Path <file>"""
    exit 1
}

Write-Host "Help drift validation: no issues."
exit 0
