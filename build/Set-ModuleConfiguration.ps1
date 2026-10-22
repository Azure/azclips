# Stages PowerShell module manifests into artifacts/<Configuration>/PS/<Module>/.
#
# The committed src/PS/<Module>/<Module>.psd1 files reference DLLs via
# relative paths under '../../../artifacts/Debug/...' so that the local
# `Import-Module ./src/PS/<Module>/<Module>.psd1` dev workflow works against
# a Debug build.
#
# This script writes a transformed copy to:
#   artifacts/<Configuration>/PS/<Module>/<Module>.psd1
# leaving the tracked source manifests untouched.
#
# Path rewrite: '../../../artifacts/<Config>/...' relative references are
# rewritten to ABSOLUTE paths. PowerShell's RequiredAssemblies resolver
# silently fails on '..'-relative paths when the manifest lives outside
# the conventional src/ tree (verified: an exact byte copy of the source
# manifest placed under artifacts/.../PS/ fails to load Core.Abstractions
# with a misleading "file not found" error, even though the resolved path
# is correct). Absolute paths sidestep the quirk. Staged manifests are
# regenerated per build, so embedded absolute paths are not a concern.
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$repoRoot     = (Resolve-Path "$PSScriptRoot/..").Path
$sourceRoot   = Join-Path $repoRoot 'src/PS'
$artifactRoot = Join-Path $repoRoot "artifacts/$Configuration/PS"
# Absolute path to artifacts/<Configuration>/ with forward slashes for psd1.
$absArtifacts = ((Join-Path $repoRoot "artifacts/$Configuration") -replace '\\', '/')

$psd1Files = Get-ChildItem -Path $sourceRoot -Filter '*.psd1' -Recurse

foreach ($file in $psd1Files) {
    # src/PS/<Module>/<Module>.psd1 -> artifacts/<Config>/PS/<Module>/<Module>.psd1
    $moduleDir  = $file.Directory.Name
    $targetDir  = Join-Path $artifactRoot $moduleDir
    $targetPath = Join-Path $targetDir $file.Name

    $null = New-Item -ItemType Directory -Path $targetDir -Force

    $content = Get-Content -Path $file.FullName -Raw
    # Rewrite '../../../artifacts/(Debug|Release)/' to an absolute path matching
    # the target configuration. Both Debug and Release source forms are accepted.
    $updated = $content -replace '\.\./\.\./\.\./artifacts/(Debug|Release)/', "$absArtifacts/"

    if ($moduleDir -ne 'Accounts')
    {
        # Path-based RequiredModules entries make Test-ModuleManifest validate the
        # staged Accounts manifest, which has absolute RequiredAssemblies paths.
        # Import validation loads dependency modules explicitly instead.
        $updated = [regex]::Replace(
            $updated,
            '(?ms)^RequiredModules\s*=\s*@\(\s*@\{.*?\}\s*\)',
            'RequiredModules = @()')
    }

    Set-Content -Path $targetPath -Value $updated -NoNewline
    $manifest = Import-PowerShellDataFile -Path $file.FullName
    foreach ($formatFile in $manifest.FormatsToProcess)
    {
        $targetFormat = Join-Path $targetDir $formatFile
        $null = New-Item -ItemType Directory -Path (Split-Path -Parent $targetFormat) -Force
        Copy-Item -LiteralPath (Join-Path $file.Directory.FullName $formatFile) -Destination $targetFormat -Force
    }
    Write-Host "Staged $moduleDir/$($file.Name) -> $targetPath"
}
