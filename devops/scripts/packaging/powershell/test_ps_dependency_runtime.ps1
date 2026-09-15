<#
.SYNOPSIS
Validates packaged PowerShell runtime requirements without installing Az modules.
.DESCRIPTION
Discovers the actual target runtime, probes a private frameworkless dotnet host,
then provisions PowerShell 7.5 and .NET 9 privately to verify production guards.
Reports and separate child stdout/stderr logs are optional; assertions are not.
Every child command has a deadline. Only the generated scratch directory is removed.
#>
[CmdletBinding()]
param(
    [string]$SourcePsRoot = (Join-Path $PSScriptRoot '../../../../src/PS'),
    [Parameter(Mandatory)][string]$RepositoryRoot,
    [Parameter(Mandatory)][string]$PwshPath,
    [Parameter(Mandatory)][string]$DotnetRootPath,
    [string]$ReportRoot,
    [ValidateRange(1, 3600)][int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'shared/dependency_runtime.ps1')
$report = Invoke-PSRuntimeValidation -SourcePsRoot $SourcePsRoot -RepositoryRoot $RepositoryRoot `
    -PwshPath $PwshPath -DotnetRootPath $DotnetRootPath -ReportRoot $ReportRoot -TimeoutSeconds $TimeoutSeconds `
    -InstallScriptPath (Join-Path $PSScriptRoot 'InstallModule.ps1')
Write-Host "PASS: $($report.cases.Count) PowerShell runtime dependency cases."
