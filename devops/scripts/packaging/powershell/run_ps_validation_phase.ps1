<#
.SYNOPSIS
Runs one shared Azure PowerShell module validation phase.

.DESCRIPTION
Run Install, Upgrade, Validate, and Uninstall in that order using the same installed
PowerShell executable and user account. Install requires an absolute local gallery
path in RepositoryRoot; the other phases do not. Upgrade and Validate are explicit
placeholders. Install already checks imports, exported cmdlets, and external help.

The distribution bootstrap must run separately first. For a PowerShell global-tool
installation, put its dotnet directory on PATH and set DOTNET_ROOT in each process.
Successful Install leaves modules for Uninstall; failed Install performs its own
partial-install cleanup. See README.md in this directory for local and CI usage.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Install', 'Upgrade', 'Validate', 'Uninstall')]
    [string] $Phase,

    [string] $RepositoryRoot,

    [Parameter(Mandatory)]
    [string] $SourceRoot,

    [Parameter(Mandatory)]
    [string] $PwshPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host "=== Azure PS validation phase: $Phase ==="

$sourcePsRoot = Join-Path $SourceRoot 'src/PS'

function Invoke-ValidationScript {
    param(
        [Parameter(Mandatory)]
        [string] $ScriptPath,

        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    & $PwshPath -NoProfile -File $ScriptPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
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

    'Upgrade' {
        # Intentional placeholder reserved for future upgrade coverage.
        Write-Host "PLACEHOLDER: the 'Upgrade' phase is not implemented yet; skipping."
    }

    'Validate' {
        # Intentional placeholder reserved for future post-install validation coverage.
        Write-Host "PLACEHOLDER: the 'Validate' phase is not implemented yet; skipping."
    }
}
