[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryRoot,

    [string]$SourcePsRoot,

    [string]$InstallRoot,

    [string]$RepositoryName = 'LocalAzclips',

    [string]$PwshPath = 'pwsh'
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command -Name $PwshPath -ErrorAction SilentlyContinue))
{
    throw "PowerShell executable was not found: $PwshPath"
}

function Get-AzModuleDefinitions
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourcePsRoot
    )

    Get-ChildItem -Path $SourcePsRoot -Filter '*.psd1' -Recurse |
        Sort-Object -Property @{ Expression = { if ($_.BaseName -eq 'Accounts') { 0 } elseif ($_.BaseName -eq 'Az') { 2 } else { 1 } } }, FullName |
        ForEach-Object {
            $manifestData = Import-PowerShellDataFile -Path $_.FullName
            $sourceModule = $_.BaseName
            $packageName = if ($sourceModule -eq 'Az') { 'Az' } else { "Az.$sourceModule" }

            [pscustomobject]@{
                SourceModule = $sourceModule
                PackageName = $packageName
                Version = [string]$manifestData.ModuleVersion
                CmdletsToExport = @($manifestData.CmdletsToExport)
            }
        }
}

function Assert-InstalledModuleImport
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$PwshPath,

        [Parameter(Mandatory = $true)]
        [string]$InstallRoot,

        [Parameter(Mandatory = $true)]
        [pscustomobject]$Definition
    )

    & $PwshPath -NoProfile -Command {
        param($InstallRoot, $ModuleName, $ExpectedJson)
        $ErrorActionPreference = 'Stop'

        $separator = [System.IO.Path]::PathSeparator
        $env:PSModulePath = "$InstallRoot$separator$env:PSModulePath"
        $expectedCmdlets = @($ExpectedJson | ConvertFrom-Json)
        $loadedModule = Import-Module -Name $ModuleName -Force -PassThru -Verbose -ErrorAction Stop
        $exportedCmdlets = @($loadedModule.ExportedCommands.Keys)
        $commands = @(Get-Command -Module $ModuleName -CommandType Cmdlet | Select-Object -ExpandProperty Name)

        $unexpectedCmdlets = @($exportedCmdlets | Where-Object { $expectedCmdlets -notcontains $_ })
        if ($unexpectedCmdlets.Count -gt 0)
        {
            throw "$ModuleName exported unexpected cmdlets: $($unexpectedCmdlets -join ', ')"
        }

        foreach ($cmdlet in $expectedCmdlets)
        {
            if ($exportedCmdlets -notcontains $cmdlet)
            {
                throw "$ModuleName did not export expected cmdlet '$cmdlet'. Exported: $($exportedCmdlets -join ', ')"
            }

            if ($commands -notcontains $cmdlet)
            {
                throw "Get-Command -Module $ModuleName did not return expected cmdlet '$cmdlet'. Returned: $($commands -join ', ')"
            }

            $help = Get-Help -Name $cmdlet -ErrorAction Stop
            $examples = @($help.Examples.Example)
            $examplesWithCode = @($examples | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_.code) })
            if ($examplesWithCode.Count -eq 0)
            {
                throw "Get-Help did not return external help examples with code for '$cmdlet' in module '$ModuleName'."
            }
        }

        Write-Host "Imported $ModuleName from local gallery install root: $($loadedModule.Path)"
        Write-Host "Validated $ModuleName cmdlets: $($expectedCmdlets -join ', ')"
    } -Args $InstallRoot, $Definition.PackageName, (ConvertTo-Json -InputObject @($Definition.CmdletsToExport) -Compress)

    if ($LASTEXITCODE -ne 0)
    {
        throw "$($Definition.PackageName) local gallery import validation failed (exit $LASTEXITCODE)."
    }
}

$resolvedRepositoryRoot = (Resolve-Path -Path $RepositoryRoot).Path
if (-not $SourcePsRoot)
{
    $repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
    $SourcePsRoot = Join-Path $repoRoot 'src/PS'
}
$resolvedSourcePsRoot = (Resolve-Path -Path $SourcePsRoot).Path

if (-not $InstallRoot)
{
    $InstallRoot = Join-Path (Split-Path -Parent $resolvedRepositoryRoot) 'test-installed-modules'
}
$resolvedInstallRoot = [System.IO.Path]::GetFullPath($InstallRoot)

Remove-Item -Path $resolvedInstallRoot -Recurse -Force -ErrorAction SilentlyContinue
$null = New-Item -ItemType Directory -Path $resolvedInstallRoot -Force

$definitions = @(Get-AzModuleDefinitions -SourcePsRoot $resolvedSourcePsRoot)

$existingRepository = Get-PSRepository -Name $RepositoryName -ErrorAction SilentlyContinue
if ($existingRepository)
{
    Unregister-PSRepository -Name $RepositoryName
}

try
{
    Register-PSRepository -Name $RepositoryName -SourceLocation $resolvedRepositoryRoot -PublishLocation $resolvedRepositoryRoot -InstallationPolicy Trusted -PackageManagementProvider NuGet

    foreach ($definition in $definitions)
    {
        $packagePath = Join-Path $resolvedRepositoryRoot "$($definition.PackageName).$($definition.Version).nupkg"
        if (-not (Test-Path -Path $packagePath -PathType Leaf))
        {
            throw "Expected package was not found in local gallery: $packagePath"
        }

        Find-Module -Name $definition.PackageName -RequiredVersion $definition.Version -Repository $RepositoryName -ErrorAction Stop | Out-Null
        Save-Module -Name $definition.PackageName -RequiredVersion $definition.Version -Repository $RepositoryName -Path $resolvedInstallRoot -Force
        Assert-InstalledModuleImport -PwshPath $PwshPath -InstallRoot $resolvedInstallRoot -Definition $definition
    }
}
finally
{
    Unregister-PSRepository -Name $RepositoryName -ErrorAction SilentlyContinue
}

Write-Host "Validated Az module packages from local gallery: $resolvedRepositoryRoot"