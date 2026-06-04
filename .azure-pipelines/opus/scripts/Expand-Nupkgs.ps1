<#
.SYNOPSIS
  Expand each .nupkg into a per-package working directory so the inner files
  (notably *.psd1) can be signed by ESRP, then later repacked.

.DESCRIPTION
  For every .nupkg under -NupkgRoot, creates
      <ExpandRoot>/<package-basename>/
  containing the package's full file tree (matches the nuspec layout).

  Sets pipeline variable `HasInnerPsd1` to 'true' if any expanded package
  contains at least one .psd1 file (so the psd1 signing ESRP step can be
  conditioned on it).

.NOTES
  A .nupkg is a ZIP archive. We use [System.IO.Compression.ZipFile] (not
  Expand-Archive) to avoid glob/character pitfalls with files like
  `[Content_Types].xml`.
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

if (-not (Test-Path $NupkgRoot)) {
    throw "NuGet root not found: $NupkgRoot"
}

Remove-Item $ExpandRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $ExpandRoot -Force | Out-Null

$nupkgs = @(Get-ChildItem -Path $NupkgRoot -File -Filter '*.nupkg' |
    Where-Object { $_.Name -notlike '*.snupkg' })

if (-not $nupkgs) {
    Write-Host "No .nupkg files found under $NupkgRoot — skipping expansion."
    Write-Host "##vso[task.setvariable variable=HasInnerPsd1]false"
    return
}

$psd1Total = 0
foreach ($pkg in $nupkgs) {
    $dest = Join-Path $ExpandRoot $pkg.BaseName
    if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    [System.IO.Compression.ZipFile]::ExtractToDirectory($pkg.FullName, $dest)
    $psd1s = @(Get-ChildItem -Path $dest -Recurse -File -Filter '*.psd1')
    $psd1Total += $psd1s.Count
    Write-Host ("Expanded {0} -> {1}  (psd1 inside: {2})" -f $pkg.Name, $dest, $psd1s.Count)
}

$hasInner = if ($psd1Total -gt 0) { 'true' } else { 'false' }
Write-Host "Total .psd1 files across expanded packages: $psd1Total"
Write-Host "##vso[task.setvariable variable=HasInnerPsd1]$hasInner"
