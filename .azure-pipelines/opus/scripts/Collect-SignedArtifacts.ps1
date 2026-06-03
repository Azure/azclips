<#
.SYNOPSIS
  Copy signed nupkg and msi files from the working folders into the release drop.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $WorkingRoot,
    [Parameter(Mandatory)] [string] $ReleaseRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

foreach ($sub in 'nupkg', 'msi') {
    $src = Join-Path $WorkingRoot $sub
    if (-not (Test-Path $src)) { continue }
    Get-ChildItem -Path $src -File | ForEach-Object {
        Copy-Item $_.FullName -Destination (Join-Path $ReleaseRoot $_.Name) -Force
        Write-Host "Collected signed: $($_.Name)"
    }
}
