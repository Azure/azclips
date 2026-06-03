<#
.SYNOPSIS
  Compute SHA-256 hashes for every release asset and emit ReleaseManifest.txt.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ReleaseRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$manifestPath = Join-Path $ReleaseRoot 'ReleaseManifest.txt'
$lines = @("# Cirrus signed release manifest", "# Generated: $(Get-Date -Format o)", "")

Get-ChildItem -Path $ReleaseRoot -File | Where-Object { $_.Name -ne 'ReleaseManifest.txt' } | Sort-Object Name | ForEach-Object {
    $hash = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash
    $lines += ("{0}  {1}" -f $hash, $_.Name)
}

$lines | Out-File -FilePath $manifestPath -Encoding utf8
Write-Host "Wrote $manifestPath"
