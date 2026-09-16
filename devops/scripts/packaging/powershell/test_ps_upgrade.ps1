<#
.SYNOPSIS
Validates a released PowerShell bundle upgrading to pipeline packages in the same local gallery.
.DESCRIPTION
Uses the released InstallModule.ps1 with a unique repository, verifies package payloads
and fresh-process imports, then removes this transaction's installations and gallery.
Run only in a disposable environment without the exact baseline or target versions.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BaselineRoot,
    [Parameter(Mandatory)][string]$RepositoryRoot,
    [string]$SourcePsRoot = (Join-Path $PSScriptRoot '../../../../src/PS'),
    [string]$PwshPath = 'pwsh',
    [string]$ReportRoot,
    [ValidateRange(1, 1800)][int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'shared/upgrade_modules.ps1')
Invoke-PSUpgradeValidation -BaselineRoot $BaselineRoot -RepositoryRoot $RepositoryRoot `
    -SourcePsRoot $SourcePsRoot -PwshPath $PwshPath -ReportRoot $ReportRoot `
    -TimeoutSeconds $TimeoutSeconds | Out-Null
