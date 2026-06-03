<#
.SYNOPSIS
  Stage unsigned artifacts produced by the cirrus-build pipeline for signing.

.DESCRIPTION
  Walks the downloaded `cirrusBuild` artifact tree, identifies the asset types,
  and produces three working folders:

      <WorkingRoot>/windows-extracted/<zipname>/   # extracted Windows zips
      <WorkingRoot>/nupkg/                          # flat copy of every .nupkg
      <WorkingRoot>/msi/                            # flat copy of every .msi

  A copy of every release asset is placed into <ReleaseRoot> so that we can
  later replace each one with its signed counterpart.

  Sets pipeline variables:
      HasWindowsPayload = true|false
      HasNupkg          = true|false
      HasMsi            = true|false

.NOTES
  This is the only entry point for staging.  The signing template depends on
  the directory layout it produces.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $UnsignedRoot,
    [Parameter(Mandatory)] [string] $WorkingRoot,
    [Parameter(Mandatory)] [string] $ReleaseRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not (Test-Path $UnsignedRoot)) {
    throw "Unsigned artifact root was not found: $UnsignedRoot"
}

Remove-Item $WorkingRoot, $ReleaseRoot -Recurse -Force -ErrorAction SilentlyContinue
$windowsExtractRoot = Join-Path $WorkingRoot 'windows-extracted'
$nupkgRoot          = Join-Path $WorkingRoot 'nupkg'
$msiRoot            = Join-Path $WorkingRoot 'msi'
New-Item -ItemType Directory -Force -Path $WorkingRoot, $ReleaseRoot, $windowsExtractRoot, $nupkgRoot, $msiRoot | Out-Null

$assets = Get-ChildItem -Path $UnsignedRoot -Recurse -File | Where-Object {
    $_.Extension -in '.zip', '.tar.gz', '.nupkg', '.msi' -or $_.Name -like '*.tar.gz'
}

if (-not $assets) {
    throw "No release assets (.zip, .tar.gz, .nupkg, .msi) found under $UnsignedRoot"
}

# Mirror every asset into the release root (signed copies will overwrite).
foreach ($asset in $assets) {
    Copy-Item $asset.FullName -Destination (Join-Path $ReleaseRoot $asset.Name) -Force
}

# Flat-copy nupkg and msi so ESRP signs them in place.
$nupkgs = $assets | Where-Object { $_.Name -like '*.nupkg' -and $_.Name -notlike '*.snupkg' }
foreach ($p in $nupkgs) { Copy-Item $p.FullName -Destination (Join-Path $nupkgRoot $p.Name) -Force }

$msis = $assets | Where-Object { $_.Extension -eq '.msi' }
foreach ($m in $msis) { Copy-Item $m.FullName -Destination (Join-Path $msiRoot $m.Name) -Force }

# Extract Windows zips (anything matching *win*.zip) so binaries can be signed.
$windowsZips = $assets | Where-Object { $_.Name -like '*win-*' -and $_.Extension -eq '.zip' }
$zipMap = @()
foreach ($zip in $windowsZips) {
    $folderName  = [System.IO.Path]::GetFileNameWithoutExtension($zip.Name)
    $extractDir  = Join-Path $windowsExtractRoot $folderName
    New-Item -ItemType Directory -Path $extractDir -Force | Out-Null
    Expand-Archive -Path $zip.FullName -DestinationPath $extractDir -Force
    $zipMap += [PSCustomObject]@{
        ZipName      = $zip.Name
        ExtractedDir = $extractDir
    }
}

$mapPath = Join-Path $WorkingRoot 'windows-zip-map.json'
$zipMap | ConvertTo-Json -Depth 5 | Out-File -FilePath $mapPath -Encoding utf8

$hasWindows = if ($windowsZips.Count -gt 0) { 'true' } else { 'false' }
$hasNupkg   = if ($nupkgs.Count       -gt 0) { 'true' } else { 'false' }
$hasMsi     = if ($msis.Count         -gt 0) { 'true' } else { 'false' }

Write-Host "Staged $($assets.Count) assets (windows=$($windowsZips.Count), nupkg=$($nupkgs.Count), msi=$($msis.Count))"
Write-Host "##vso[task.setvariable variable=HasWindowsPayload]$hasWindows"
Write-Host "##vso[task.setvariable variable=HasNupkg]$hasNupkg"
Write-Host "##vso[task.setvariable variable=HasMsi]$hasMsi"
