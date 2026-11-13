<#
.SYNOPSIS
Finalizes this matrix leg's diagnostic report, including phases that never ran.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReportRoot,
    [string]$ValidationKey,
    [string]$JobStatus = $env:AGENT_JOBSTATUS
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'shared/phase_artifacts.ps1')
Complete-PSValidationReport -ReportRoot $ReportRoot -ValidationKey $ValidationKey -JobStatus $JobStatus
