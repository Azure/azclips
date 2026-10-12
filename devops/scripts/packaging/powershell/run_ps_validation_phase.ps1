<#
.SYNOPSIS
Runs one shared Azure PowerShell module validation phase.

.DESCRIPTION
Run Dependencies, Upgrade, Install, Validate, and Uninstall in that order using the same
PowerShell executable. Dependencies and Install require a local gallery path in
RepositoryRoot. Dependencies uses private module directories and the bootstrapped
DOTNET_ROOT; it does not install user modules. Upgrade uses a prepared GitHub baseline,
verifies replacement by pipeline packages, and cleans its own installed modules.
Both dependency suites run before the phase returns the first failing exit code.
Validate runs the configured module smoke tests after Install checks imports,
exported cmdlets, and external help. Smoke coverage is checked once before the
distribution matrix, not as a module phase.

The distribution bootstrap must run separately first. For a PowerShell global-tool
installation, put its dotnet directory on PATH and set DOTNET_ROOT in each process.
Successful Install leaves modules for Uninstall; failed Install performs its own
partial-install cleanup. See README.md in this directory for local and CI usage.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Dependencies', 'Upgrade', 'Install', 'Validate', 'Uninstall')]
    [string] $Phase,

    [string] $RepositoryRoot,

    [string] $BaselineRoot,

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

$sourcePsRoot = Join-Path $SourceRoot 'src/PS'

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
    'Dependencies' {
        if ([string]::IsNullOrWhiteSpace($RepositoryRoot) -or
            -not (Test-Path -LiteralPath $RepositoryRoot -PathType Container)) {
            throw "The 'Dependencies' phase requires an existing -RepositoryRoot."
        }
        if ([string]::IsNullOrWhiteSpace($env:DOTNET_ROOT)) {
            throw "The 'Dependencies' phase requires the bootstrapped DOTNET_ROOT."
        }
        Invoke-ValidationScript `
            -ScriptPath (Join-Path $PSScriptRoot 'test_ps_dependencies.ps1') `
            -Arguments @(
                '-RepositoryRoot', $RepositoryRoot,
                '-SourcePsRoot', $sourcePsRoot,
                '-PwshPath', $PwshPath
            )
        Invoke-ValidationScript `
            -ScriptPath (Join-Path $PSScriptRoot 'test_ps_dependency_runtime.ps1') `
            -Arguments @(
                '-RepositoryRoot', $RepositoryRoot,
                '-SourcePsRoot', $sourcePsRoot,
                '-PwshPath', $PwshPath,
                '-DotnetRootPath', $env:DOTNET_ROOT
            )
    }

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

    'Upgrade' {
        if ([string]::IsNullOrWhiteSpace($RepositoryRoot) -or
            [string]::IsNullOrWhiteSpace($BaselineRoot)) {
            throw "The 'Upgrade' phase requires -RepositoryRoot and -BaselineRoot."
        }
        Invoke-ValidationScript `
            -ScriptPath (Join-Path $PSScriptRoot 'test_ps_upgrade.ps1') `
            -Arguments @(
                '-BaselineRoot', $BaselineRoot,
                '-RepositoryRoot', $RepositoryRoot,
                '-SourcePsRoot', $sourcePsRoot,
                '-PwshPath', $PwshPath
            )
    }

    'Validate' {
        Invoke-ValidationScript `
            -ScriptPath (Join-Path $PSScriptRoot 'test_ps_smoke.ps1') `
            -Arguments @(
                '-SourcePsRoot', $sourcePsRoot,
                '-PwshPath', $PwshPath,
                '-SkipCoverageWarnings'
            )
    }
}

exit $script:validationExitCode
