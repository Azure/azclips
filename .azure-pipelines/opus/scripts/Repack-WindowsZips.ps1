<#
.SYNOPSIS
  Re-pack the extracted Windows payloads (now signed) back into their .zip files.

.DESCRIPTION
  Reads the windows-zip-map.json produced by Stage-SigningInputs.ps1 and, for
  each entry, re-creates the zip from the (now-signed) extracted directory and
  drops it into <ReleaseRoot>, overwriting the unsigned copy that was placed
  there during staging.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $WorkingRoot,
    [Parameter(Mandatory)] [string] $ReleaseRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$mapPath = Join-Path $WorkingRoot 'windows-zip-map.json'
if (-not (Test-Path $mapPath)) {
    Write-Host "No windows-zip-map.json found at $mapPath; skipping repack."
    return
}

$zipMap = Get-Content $mapPath -Raw | ConvertFrom-Json
foreach ($entry in $zipMap) {
    $target = Join-Path $ReleaseRoot $entry.ZipName
    if (Test-Path $target) { Remove-Item $target -Force }
    Compress-Archive -Path (Join-Path $entry.ExtractedDir '*') -DestinationPath $target -Force
    Write-Host "Re-packed signed zip: $target"
}
