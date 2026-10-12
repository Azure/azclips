<#
.SYNOPSIS
Shared, platform-agnostic Az PowerShell module installation validation invoked by the phase dispatcher.

.DESCRIPTION
Registers a local package gallery, installs each packaged Az PowerShell module from the local repository with
Install-Module into the given -Scope, and validates module import, exported cmdlets, and external help examples.
Installed modules are intentionally left in place after successful validation; test_ps_uninstall.ps1 is expected to run
after smoke validation to remove them and verify removal. Upgrade runs and cleans its
own installations before this fresh-install phase.
Use a disposable environment: preexisting exact target versions or a conflicting RepositoryName are rejected before
any mutation. Other installed versions and existing repository registrations are preserved. On failure, only packages
attempted by this run are removed in reverse order; cleanup continues after individual errors without hiding the
original failure. Repository operations isolate both the PowerShell location and the process current directory
in an OS temporary directory to avoid changing the caller's NuGet.config or requiring a writable gallery.
Both original directories are restored, and
only this run's registration and working directory are cleaned up.
An optional ReportRoot records environment and exact-version inventories, and snapshots only attempted package
versions before rollback or a later uninstall. Capture-only failures fail the test and roll back attempted installs;
capture diagnostics never replace a primary lifecycle failure. Omit ReportRoot to retain the original behavior.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RepositoryRoot,
    [string]$SourcePsRoot,
    [string]$RepositoryName = 'LocalAzclips',
    [string]$PwshPath = 'pwsh',
    [ValidateSet('CurrentUser', 'AllUsers')][string]$Scope = 'CurrentUser',
    [string]$ReportRoot
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'shared\module_definitions.ps1')
. (Join-Path $PSScriptRoot 'shared\module_artifacts.ps1')

if (-not (Get-Command -Name $PwshPath -ErrorAction SilentlyContinue))
{
    throw "PowerShell executable was not found: $PwshPath"
}

function Get-TargetInstalledModule
{
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Definition
    )

    try
    {
        Get-InstalledModule -Name $Definition.PackageName -RequiredVersion $Definition.Version -ErrorAction Stop
    }
    catch
    {
        # PowerShellGet reports an absent exact version as PackageManagement's NoMatchFound.
        if (($_.FullyQualifiedErrorId -split ',')[0] -ne 'NoMatchFound')
        {
            throw
        }
    }
}

function Undo-AttemptedModules
{
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [array]$Definitions,
        $ArtifactContext,
        [array]$InventoryDefinitions
    )

    Write-AzModuleInventory -Context $ArtifactContext -Stage install-failure -Definitions $InventoryDefinitions -SnapshotDefinitions $Definitions
    $reverseDefinitions = @($Definitions)
    [array]::Reverse($reverseDefinitions)
    foreach ($definition in $reverseDefinitions)
    {
        try
        {
            if (Get-TargetInstalledModule -Definition $definition)
            {
                Write-Host "Rolling back $($definition.PackageName) $($definition.Version)"
                Uninstall-Module -Name $definition.PackageName -RequiredVersion $definition.Version -Force -ErrorAction Stop
                if (Get-TargetInstalledModule -Definition $definition)
                {
                    throw 'The exact version is still reported by Get-InstalledModule after rollback.'
                }
            }
        }
        catch
        {
            Write-Warning "Failed to clean up $($definition.PackageName) $($definition.Version): $($_.Exception.Message)" -WarningAction Continue
        }
    }
    if ($Definitions.Count -gt 0)
    {
        Write-AzModuleInventory -Context $ArtifactContext -Stage after-rollback -Definitions $InventoryDefinitions
    }
}

function Assert-InstalledModuleImport
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$PwshPath,

        [Parameter(Mandatory = $true)]
        [pscustomobject]$Definition
    )

    & $PwshPath -NoProfile -Command {
        param($ModuleName, $ModuleVersion, $ExpectedJson)
        $ErrorActionPreference = 'Stop'

        $expectedCmdlets = @($ExpectedJson | ConvertFrom-Json)
        $requiredVersion = [version]$ModuleVersion
        # Agents can ship unrelated Az releases, so pin the version under test.
        $loadedModule = Import-Module -Name $ModuleName -RequiredVersion $requiredVersion -Force -PassThru -Verbose -ErrorAction Stop
        $exportedCmdlets = @($loadedModule.ExportedCommands.Keys)
        $commands = @(Get-Command -Module $ModuleName -CommandType Cmdlet | Select-Object -ExpandProperty Name)

        if ($loadedModule.Version -ne $requiredVersion)
        {
            throw "$ModuleName imported version $($loadedModule.Version) but expected $requiredVersion from $($loadedModule.Path)."
        }

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

        Write-Host "Imported $ModuleName from installed module path: $($loadedModule.Path)"
        Write-Host "Validated $ModuleName cmdlets: $($expectedCmdlets -join ', ')"
    } -Args $Definition.PackageName, $Definition.Version, (ConvertTo-Json -InputObject @($Definition.CmdletsToExport) -Compress)

    if ($LASTEXITCODE -ne 0)
    {
        throw "$($Definition.PackageName) local gallery import validation failed (exit $LASTEXITCODE)."
    }
}

$resolvedRepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot -ErrorAction Stop).ProviderPath
if (-not $SourcePsRoot)
{
    $repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
    $SourcePsRoot = Join-Path $repoRoot 'src\PS'
}
$resolvedSourcePsRoot = (Resolve-Path -LiteralPath $SourcePsRoot -ErrorAction Stop).ProviderPath

$definitions = @(Get-AzModuleDefinitions -SourcePsRoot $resolvedSourcePsRoot)

$attemptedDefinitions = [System.Collections.Generic.List[object]]::new()
$repositoryRegistered = $false
$workingDirectory = $null
$locationPushed = $false
$originalProcessDirectory = [Environment]::CurrentDirectory
$processDirectoryChanged = $false
$failure = $null
$teardownErrors = [System.Collections.Generic.List[string]]::new()
$artifactContext = New-AzModuleArtifactContext -ReportRoot $ReportRoot -ExcludedRoots @($resolvedSourcePsRoot, $resolvedRepositoryRoot)

try
{
    Write-AzModuleInventory -Context $artifactContext -Stage before-install -Definitions $definitions
    $existingRepository = @(Get-PSRepository -ErrorAction Stop | Where-Object { $_.Name -eq $RepositoryName })
    if ($existingRepository)
    {
        throw "Repository '$RepositoryName' is already registered. Use a disposable environment or an unused RepositoryName; existing registrations will not be changed."
    }

    foreach ($definition in $definitions)
    {
        $installedModule = Get-TargetInstalledModule -Definition $definition
        $availableModules = @(Get-Module -ListAvailable -Name $definition.PackageName -ErrorAction Stop |
            Where-Object { $_.Version -eq [version]$definition.Version })
        if ($installedModule -or $availableModules.Count -gt 0)
        {
            throw "Target module $($definition.PackageName) $($definition.Version) already exists. Use a disposable environment without the exact target versions; preexisting modules will not be removed."
        }
    }
    if ($artifactContext -and $artifactContext.Errors.Count -gt 0)
    {
        throw 'Module artifact capture failed before installation. See capture-errors.json or warnings.'
    }

    $workingDirectory = New-Item -ItemType Directory -Path (Join-Path ([System.IO.Path]::GetTempPath()) "azclips-install-$([guid]::NewGuid().ToString('N'))") -ErrorAction Stop
    Push-Location -LiteralPath $workingDirectory.FullName -ErrorAction Stop
    $locationPushed = $true
    # Package providers can use the process directory rather than PowerShell's $PWD.
    [Environment]::CurrentDirectory = $workingDirectory.FullName
    $processDirectoryChanged = $true
    Register-PSRepository -Name $RepositoryName -SourceLocation $resolvedRepositoryRoot -PublishLocation $resolvedRepositoryRoot -InstallationPolicy Trusted -PackageManagementProvider NuGet -ErrorAction Stop
    $repositoryRegistered = $true

    foreach ($definition in $definitions)
    {
        $packagePath = Join-Path $resolvedRepositoryRoot "$($definition.PackageName).$($definition.Version).nupkg"
        if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf))
        {
            throw "Expected package was not found in local gallery: $packagePath"
        }

        Find-Module -Name $definition.PackageName -RequiredVersion $definition.Version -Repository $RepositoryName -ErrorAction Stop | Out-Null
        $attemptedDefinitions.Add($definition)
        Install-Module -Name $definition.PackageName -RequiredVersion $definition.Version -Repository $RepositoryName -Scope $Scope -Force -AllowClobber -SkipPublisherCheck -ErrorAction Stop
        try
        {
            $installedModule = Get-InstalledModule -Name $definition.PackageName -RequiredVersion $definition.Version -ErrorAction Stop
        }
        catch
        {
            throw "Installed module was not visible to PowerShellGet: $($definition.PackageName) $($definition.Version). $($_.Exception.Message)"
        }

        Write-Host "Installed $($definition.PackageName) $($definition.Version) to: $($installedModule.InstalledLocation)"
        Assert-InstalledModuleImport -PwshPath $PwshPath -Definition $definition
    }
}
catch
{
    $failure = $_
}
finally
{
    if ($repositoryRegistered)
    {
        try
        {
            Unregister-PSRepository -Name $RepositoryName -ErrorAction Stop
        }
        catch
        {
            $teardownErrors.Add("Failed to unregister this run's repository '$RepositoryName': $($_.Exception.Message)")
            if (-not $failure) { $failure = $_ }
        }
    }

    if (-not $failure)
    {
        Write-AzModuleInventory -Context $artifactContext -Stage after-install -Definitions $definitions -SnapshotDefinitions $attemptedDefinitions.ToArray()
        if ($artifactContext -and $artifactContext.Errors.Count -gt 0)
        {
            $failure = [InvalidOperationException]::new('Module artifact capture failed. See capture-errors.json or warnings.')
        }
    }
    if ($failure)
    {
        Undo-AttemptedModules -Definitions $attemptedDefinitions.ToArray() -ArtifactContext $artifactContext -InventoryDefinitions $definitions
    }

    if ($locationPushed)
    {
        try { Pop-Location -ErrorAction Stop }
        catch
        {
            $teardownErrors.Add("Failed to restore the working directory: $($_.Exception.Message)")
            if (-not $failure)
            {
                $failure = $_
                Undo-AttemptedModules -Definitions $attemptedDefinitions.ToArray() -ArtifactContext $artifactContext -InventoryDefinitions $definitions
            }
        }
    }
    if ($processDirectoryChanged)
    {
        try { [Environment]::CurrentDirectory = $originalProcessDirectory }
        catch
        {
            $teardownErrors.Add("Failed to restore the process current directory: $($_.Exception.Message)")
            if (-not $failure)
            {
                $failure = $_
                Undo-AttemptedModules -Definitions $attemptedDefinitions.ToArray() -ArtifactContext $artifactContext -InventoryDefinitions $definitions
            }
        }
    }
    if ($workingDirectory)
    {
        try { Remove-Item -LiteralPath $workingDirectory.FullName -Recurse -Force -ErrorAction Stop }
        catch
        {
            $teardownErrors.Add("Failed to remove this run's working directory: $($_.Exception.Message)")
            if (-not $failure)
            {
                $failure = $_
                Undo-AttemptedModules -Definitions $attemptedDefinitions.ToArray() -ArtifactContext $artifactContext -InventoryDefinitions $definitions
            }
        }
    }
    foreach ($message in $teardownErrors)
    {
        Write-Warning $message -WarningAction Continue
    }
}

if ($failure)
{
    throw $failure
}

Write-Host "Validated Az module package installation from local gallery: $resolvedRepositoryRoot"