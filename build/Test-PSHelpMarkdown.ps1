# Validates every src/PS/<Module>/help/*.md against the built cmdlet binary by
# running Test-/Import-/Update-/Compare-CommandHelp from Microsoft.PowerShell.PlatyPS 1.0.1
# and emitting PSL13/PSL14 records for schema failures and parameter drift.
#
# Prerequisites: run `dotnet build azclips.slnx` first so the cmdlet DLLs exist.
# Usage:
#   pwsh -File build/Test-PSHelpMarkdown.ps1                       # all modules under src/PS/
#   pwsh -File build/Test-PSHelpMarkdown.ps1 -Module Login         # single module
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

# PathRoot anchors the relative paths emitted in PSL records. Prefer the repo root when the caller
# is validating the checked-in src/PS tree; fall back to $ModuleRoot for smoke fixtures pointing at
# an out-of-tree temp dir so records still show a short readable path.
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

    # Run PlatyPS in an isolated child pwsh (mirrors Publish-AzclipsHelpArtifacts in build.ps1) to
    # avoid YamlDotNet assembly conflicts and to guarantee a clean module unload between iterations.
    pwsh -NoProfile -Command {
        param($manifest, $helpDir, $pathRoot)
        $ErrorActionPreference = 'Continue'

        Import-Module Microsoft.PowerShell.PlatyPS -RequiredVersion 1.0.1 -ErrorAction Stop
        Import-Module $manifest -Force -ErrorAction Stop

        # See scratch/CLIPS-652-TEMP-PR-PLAN.md §4.1 for why each exclusion is here. Passing this
        # list REPLACES Compare-CommandHelp's built-in defaults, so Diagnostics, AliasHeaderFound,
        # and ParameterNames are re-listed even though they'd be auto-excluded otherwise.
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
                # Test-MarkdownCommandHelp returns $null / nothing on a clean file. It emits
                # a bool (or MarkdownCommandHelpValidationResult with -DetailView) only when
                # there is something to complain about. We treat: null => pass; false => fail;
                # object => inspect IsValid.
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
                $old   = Import-MarkdownCommandHelp -Path $md
                $new   = Update-CommandHelp         -Path $md
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
                Write-Output "PSL14_ParameterDrift|$relPath|drift check failed: $($_.Exception.Message)"
                $errorCount++
            }
        }

        exit $errorCount
    } -Args $manifest, $helpDir, $pathRoot

    # $LASTEXITCODE from a child pwsh block is the child's exit code = its error count.
    $totalErrors += [int]$LASTEXITCODE
}

if ($totalErrors -gt 0)
{
    Write-Host ""
    Write-Host "Help drift validation failed: $totalErrors record(s) across $($modules.Count) module(s)."
    Write-Host "Fix each drifted file with:"
    Write-Host "    pwsh -c ""Import-Module Microsoft.PowerShell.PlatyPS -RequiredVersion 1.0.1; Update-MarkdownCommandHelp -Path <file>"""
    exit 1
}

Write-Host "Help drift validation: no issues."
exit 0
