# Scaffolds src/PS/<Module>/help/<Cmdlet>.md for every module via PlatyPS.
# Idempotent: New-MarkdownHelp -Force overwrites; preserve hand-edited content
# by using -RefreshModulePage / -UpdateInputOutput flow in a follow-up if needed.
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',

    [ValidateSet('Compute', 'Config', 'Login', 'Resources')]
    [string[]]$Module
)

$ErrorActionPreference = 'Stop'

Import-Module platyPS -RequiredVersion 0.14.2

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

    Write-Host "Scaffolding markdown for module '$name' -> $helpDir"

    # Import in an isolated child session so artifact DLLs are released afterwards.
    pwsh -NoProfile -Command {
        param($manifestPath, $helpDir, $moduleName)
        Import-Module platyPS -RequiredVersion 0.14.2
        Import-Module $manifestPath -Force
        if (-not (Test-Path $helpDir)) { New-Item -ItemType Directory -Path $helpDir -Force | Out-Null }
        New-MarkdownHelp -Module $moduleName -OutputFolder $helpDir -Force -AlphabeticParamsOrder | Out-Null
    } -Args $manifestPath, $helpDir, $name
}

Write-Host "Done."
