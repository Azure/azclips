<#
.SYNOPSIS
Freezes a verified stable GitHub PowerShell bundle for subsequent upgrade validation.
.DESCRIPTION
Downloads with the authenticated GitHub CLI. This script never runs the bundled
installer, registers a gallery, or installs modules. DestinationRoot must be new,
and its parent directory must already exist.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$TargetVersion,
    [Parameter(Mandatory)][string]$DestinationRoot,
    [string]$GitHubRepository = 'Azure/azclips',
    [switch]$UseCheckoutCredentials,
    [ValidateRange(1, 3600)][int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'shared/upgrade_release.ps1')
New-PSUpgradeBaseline @PSBoundParameters | Out-Null
Write-Host 'Prepared checksum-verified PowerShell upgrade baseline; no modules were installed.'
