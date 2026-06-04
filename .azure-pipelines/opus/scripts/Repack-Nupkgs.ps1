<#
.SYNOPSIS
  Repack the per-package working directories produced by Expand-Nupkgs.ps1
  back into .nupkg files in-place (overwriting the unsigned originals).

.DESCRIPTION
  For every subdirectory under -ExpandRoot, creates
      <NupkgRoot>/<subdirname>.nupkg
  containing every file in that subdirectory at the same relative path.

  Use after the inner-psd1 ESRP signing step. The resulting .nupkg files are
  then handed to NuGetSign for the package-level signature.

.NOTES
  Uses [System.IO.Compression.ZipFile]::CreateFromDirectory which handles
  files such as `[Content_Types].xml` correctly. Compression level matches
  what NuGet itself produces (Optimal / Deflate).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $NupkgRoot,
    [Parameter(Mandatory)] [string] $ExpandRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

if (-not (Test-Path $ExpandRoot)) {
    Write-Host "Expand root not found ($ExpandRoot) — nothing to repack."
    return
}

$dirs = @(Get-ChildItem -Path $ExpandRoot -Directory)
if (-not $dirs) {
    Write-Host "No expanded packages under $ExpandRoot — nothing to repack."
    return
}

New-Item -ItemType Directory -Path $NupkgRoot -Force | Out-Null

foreach ($dir in $dirs) {
    $target = Join-Path $NupkgRoot ($dir.Name + '.nupkg')
    if (Test-Path $target) { Remove-Item $target -Force }
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $dir.FullName,
        $target,
        [System.IO.Compression.CompressionLevel]::Optimal,
        $false  # do not include the base directory itself
    )
    Write-Host ("Repacked {0} -> {1}  ({2} bytes)" -f $dir.Name, $target, (Get-Item $target).Length)
}
