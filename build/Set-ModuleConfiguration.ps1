# Stages PowerShell module manifests into artifacts/<Configuration>/PS/<Module>/.
#
# The committed src/PS/<Module>/<Module>.psd1 files reference DLLs via
# relative paths under '../../../artifacts/Debug/...' so that the local
# `Import-Module ./src/PS/<Module>/<Module>.psd1` dev workflow works against
# a Debug build. For Release builds (notably benchmarks in CI) we need a
# manifest pointing at '../../../artifacts/Release/...' instead.
#
# This script writes a transformed copy to:
#   artifacts/<Configuration>/PS/<Module>/<Module>.psd1
# leaving the tracked source manifests untouched. Benchmark code imports
# from the artifact copy, so the working tree stays clean -- which lets
# benchmark-action/github-action-benchmark switch to gh-pages without
# hitting "local changes would be overwritten".
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$repoRoot     = Resolve-Path "$PSScriptRoot/.."
$sourceRoot   = Join-Path $repoRoot 'src/PS'
$artifactRoot = Join-Path $repoRoot "artifacts/$Configuration/PS"

$psd1Files = Get-ChildItem -Path $sourceRoot -Filter '*.psd1' -Recurse

foreach ($file in $psd1Files) {
    # src/PS/<Module>/<Module>.psd1 -> artifacts/<Config>/PS/<Module>/<Module>.psd1
    $moduleDir  = $file.Directory.Name
    $targetDir  = Join-Path $artifactRoot $moduleDir
    $targetPath = Join-Path $targetDir $file.Name

    $null = New-Item -ItemType Directory -Path $targetDir -Force

    $content = Get-Content -Path $file.FullName -Raw
    # Both Debug and Release source paths get rewritten to the target configuration,
    # so this works regardless of whether someone manually edited the source.
    $updated = $content -replace '/artifacts/(Debug|Release)/', "/artifacts/$Configuration/"

    Set-Content -Path $targetPath -Value $updated -NoNewline
    Write-Host "Staged $moduleDir/$($file.Name) -> $targetPath"
}
