<#
.SYNOPSIS
Runs one shared Azure PowerShell module validation phase.

.DESCRIPTION
Run Install and Uninstall in that order using the same PowerShell executable.
Install requires a local gallery path in RepositoryRoot and checks that every
module imports under its exact packaged version, exports its manifest cmdlets,
and has external help examples.

The distribution bootstrap must run separately first. For a PowerShell global-tool
installation, put its dotnet directory on PATH and set DOTNET_ROOT in each process.
Successful Install leaves modules for Uninstall; failed Install performs its own
partial-install cleanup. See README.md in this directory for local and CI usage.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Install', 'Uninstall')]
    [string] $Phase,

    [string] $RepositoryRoot,

    [Parameter(Mandatory)]
    [string] $SourceRoot,

    [Parameter(Mandatory)]
    [string] $PwshPath,

    [string] $ReportRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$script:validationExitCode = 0

Write-Host "=== Azure PS validation phase: $Phase ==="

$sourcePsRoot = Join-Path $SourceRoot 'src' 'PS'

function Invoke-ValidationScript {
    param(
        [Parameter(Mandatory)]
        [string] $ScriptPath,

        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    if ($ReportRoot) { $Arguments += @('-ReportRoot', $ReportRoot) }
    & $PwshPath -NoProfile -File $ScriptPath @Arguments
    if ($LASTEXITCODE -ne 0 -and $script:validationExitCode -eq 0) {
        $script:validationExitCode = $LASTEXITCODE
    }
}

switch ($Phase) {
    'Install' {
        if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
            throw "The 'Install' phase requires -RepositoryRoot."
        }

        if (-not (Test-Path -LiteralPath $RepositoryRoot -PathType Container)) {
            throw "PowerShell module package repository not found: $RepositoryRoot"
        }

        Invoke-ValidationScript `
            -ScriptPath (Join-Path $PSScriptRoot 'test_ps_installation.ps1') `
            -Arguments @(
                '-RepositoryRoot', $RepositoryRoot,
                '-SourcePsRoot', $sourcePsRoot,
                '-PwshPath', $PwshPath
            )
    }

    'Uninstall' {
        Invoke-ValidationScript `
            -ScriptPath (Join-Path $PSScriptRoot 'test_ps_uninstall.ps1') `
            -Arguments @(
                '-SourcePsRoot', $sourcePsRoot,
                '-PwshPath', $PwshPath
            )
    }
}

exit $script:validationExitCode
