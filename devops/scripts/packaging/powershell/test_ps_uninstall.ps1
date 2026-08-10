<#
.SYNOPSIS
Validates Az PowerShell modules uninstall cleanly through the phase dispatcher.

.DESCRIPTION
This shared, platform-agnostic uninstall validation is called by the phase
dispatcher after installation and the upgrade/validate placeholder phases.
It verifies Uninstall-Module cleanly
removes every Az module installed by the installation validation on the same
agent, and confirms those exact package versions are no longer visible or
importable. Missing required versions fail this strict, normal uninstall phase;
installation failures instead perform their own best-effort rollback.
#>
[CmdletBinding()]
param(
    [string]$SourcePsRoot,
    [string]$PwshPath = 'pwsh'
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'shared\module_definitions.ps1')

if (-not (Get-Command -Name $PwshPath -ErrorAction SilentlyContinue))
{
    throw "PowerShell executable was not found: $PwshPath"
}

function Assert-ModuleInstalled
{
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Definition
    )

    try
    {
        $installedModule = Get-InstalledModule -Name $Definition.PackageName -RequiredVersion $Definition.Version -ErrorAction Stop
        Write-Host "Found installed module $($Definition.PackageName) $($Definition.Version): $($installedModule.InstalledLocation)"
    }
    catch
    {
        throw "Expected installed module was not found: $($Definition.PackageName) $($Definition.Version). The installation test must run successfully on the same agent before uninstall validation. $($_.Exception.Message)"
    }
}

function Uninstall-AzModule
{
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Definition
    )

    try
    {
        Write-Host "Uninstalling $($Definition.PackageName) $($Definition.Version)"
        Uninstall-Module -Name $Definition.PackageName -RequiredVersion $Definition.Version -Force -ErrorAction Stop
    }
    catch
    {
        $message = "Failed to uninstall $($Definition.PackageName) $($Definition.Version): $($_.Exception.Message)"
        throw [System.InvalidOperationException]::new($message, $_.Exception)
    }
}

function Assert-ModuleRemoved
{
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Definition
    )

    $installedModule = Get-InstalledModule -Name $Definition.PackageName -RequiredVersion $Definition.Version -ErrorAction SilentlyContinue
    if ($installedModule)
    {
        throw "$($Definition.PackageName) is still reported by Get-InstalledModule after uninstall: $($installedModule.InstalledLocation -join ', ')"
    }

    $availableModules = @(Get-Module -ListAvailable -Name $Definition.PackageName | Where-Object { $_.Version -eq [version]$Definition.Version })
    if ($availableModules.Count -gt 0)
    {
        throw "$($Definition.PackageName) is still visible after uninstall: $($availableModules.Path -join ', ')"
    }

    Write-Host "Removed $($Definition.PackageName)"
}

function Assert-ModulesNotImportable
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$PwshPath,

        [Parameter(Mandatory = $true)]
        [array]$Definitions
    )

    & $PwshPath -NoProfile -Command {
        param($DefinitionsJson)
        $ErrorActionPreference = 'Stop'

        $definitions = @($DefinitionsJson | ConvertFrom-Json)
        $failures = @()

        foreach ($definition in $definitions)
        {
            $moduleName = [string]$definition.PackageName
            $moduleVersion = [version][string]$definition.Version
            $availableModules = @(Get-Module -ListAvailable -Name $moduleName | Where-Object { $_.Version -eq $moduleVersion })
            if ($availableModules.Count -gt 0)
            {
                $failures += "$moduleName $moduleVersion is still listable: $($availableModules.Path -join ', ')"
                continue
            }

            $importedModule = Import-Module -Name $moduleName -RequiredVersion $moduleVersion -ErrorAction SilentlyContinue -PassThru
            if ($importedModule)
            {
                $failures += "$moduleName $moduleVersion is still importable: $($importedModule.Path -join ', ')"
                Remove-Module -Name $moduleName -Force -ErrorAction SilentlyContinue
            }
        }

        if ($failures.Count -gt 0)
        {
            throw "Modules are still available after uninstall: $($failures -join '; ')"
        }

        Write-Host "Confirmed uninstalled modules are not importable."
    } -Args (ConvertTo-Json -InputObject @($Definitions) -Compress)

    if ($LASTEXITCODE -ne 0)
    {
        throw "Post-uninstall import validation failed (exit $LASTEXITCODE)."
    }
}

if (-not $SourcePsRoot)
{
    $repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
    $SourcePsRoot = Join-Path $repoRoot 'src\PS'
}

$resolvedSourcePsRoot = (Resolve-Path -LiteralPath $SourcePsRoot -ErrorAction Stop).ProviderPath
$definitions = @(Get-AzModuleDefinitions -SourcePsRoot $resolvedSourcePsRoot)
[array]::Reverse($definitions)
$uninstalledPackages = @()

foreach ($definition in $definitions)
{
    Assert-ModuleInstalled -Definition $definition
    Uninstall-AzModule -Definition $definition
    Assert-ModuleRemoved -Definition $definition
    $uninstalledPackages += $definition.PackageName
}

Assert-ModulesNotImportable -PwshPath $PwshPath -Definitions $definitions

Write-Host "Uninstalled Az module packages: $($uninstalledPackages -join ', ')"
