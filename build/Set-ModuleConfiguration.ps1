# Stages configuration-neutral PowerShell module manifests beside their
# extension artifacts. Cmdlet projects also perform this copy after Build;
# this script stages the rollup manifest and supports explicit restaging.
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$repoRoot     = (Resolve-Path "$PSScriptRoot/..").Path
$sourceRoot   = Join-Path $repoRoot 'src/PS'
$artifactRoot = Join-Path $repoRoot "artifacts/$Configuration"

$psd1Files = Get-ChildItem -Path $sourceRoot -Filter '*.psd1' -Recurse

foreach ($file in $psd1Files) {
    $moduleDir  = $file.Directory.Name
    $artifactName = if ($moduleDir -eq 'Az') { 'Az' } else { "Microsoft.Azure.Azclips.Extensions.$moduleDir" }
    $targetDir  = Join-Path $artifactRoot $artifactName
    $targetPath = Join-Path $targetDir $file.Name

    $null = New-Item -ItemType Directory -Path $targetDir -Force
    if ($moduleDir -eq 'Az')
    {
        $content = Get-Content -Path $file.FullName -Raw
        $content = [regex]::Replace(
            $content,
            '(?ms)^RequiredModules\s*=\s*@\(.*?^\)',
            'RequiredModules = @()')
        Set-Content -Path $targetPath -Value $content -NoNewline
        Copy-Item -Path (Join-Path $file.Directory.FullName 'Az.psm1') -Destination $targetDir -Force
    }
    else
    {
        Copy-Item -Path $file.FullName -Destination $targetPath -Force
    }
    Write-Host "Staged $moduleDir/$($file.Name) -> $targetPath"
}
