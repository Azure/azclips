[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactsRoot,

    [string]$SourcePsRoot,

    [string]$OutputRoot,

    [string]$RepositoryName = 'LocalAzclips',

    [string]$PwshPath = 'pwsh',

    [switch]$SkipInstallValidation
)

$ErrorActionPreference = 'Stop'

. ([System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../build/Get-AzPackageName.ps1')))

if (-not (Get-Command -Name $PwshPath -ErrorAction SilentlyContinue))
{
    throw "PowerShell executable was not found: $PwshPath"
}

function Copy-RequiredFile
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Destination
    )

    if (-not (Test-Path -Path $Path -PathType Leaf))
    {
        throw "Required file was not found: $Path"
    }

    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $Destination) -Force
    Copy-Item -Path $Path -Destination $Destination -Force
}

function Copy-ExtensionFromPackage
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$ArtifactsRoot,

        [Parameter(Mandatory = $true)]
        [string]$PackageId,

        [Parameter(Mandatory = $true)]
        [string]$AssemblyName,

        [Parameter(Mandatory = $true)]
        [string]$DestinationDirectory
    )

    $packagePath = Get-ChildItem -Path $ArtifactsRoot -Filter "$PackageId.*.nupkg" -File -Recurse |
        Sort-Object -Property FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName

    if (-not $packagePath)
    {
        throw "Extension package was not found for '$PackageId' under $ArtifactsRoot."
    }

    $extractRoot = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString())
    try
    {
        Expand-Archive -Path $packagePath -DestinationPath $extractRoot -Force
        $extensionDll = Join-Path $extractRoot "lib/net10.0/$AssemblyName.dll"
        Copy-RequiredFile -Path $extensionDll -Destination (Join-Path $DestinationDirectory "$AssemblyName.dll")
    }
    finally
    {
        Remove-Item -Path $extractRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function New-AzModulePackage
{
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Definition,

        [Parameter(Mandatory = $true)]
        [string]$ArtifactsRoot,

        [Parameter(Mandatory = $true)]
        [string]$SourcePsRoot,

        [Parameter(Mandatory = $true)]
        [string]$ModulesRoot,

        [Parameter(Mandatory = $true)]
        [string[]]$CoreAssemblies
    )

    $moduleRoot = Join-Path $ModulesRoot $Definition.PackageName
    Remove-Item -Path $moduleRoot -Recurse -Force -ErrorAction SilentlyContinue

    if ($Definition.IsRollup)
    {
        $null = New-Item -ItemType Directory -Path $moduleRoot -Force

        $rootModulePath = Join-Path $moduleRoot "$($Definition.PackageName).psm1"
        $rootModuleContent = @(
            '$ErrorActionPreference = ''Stop'''
            ''
        )
        foreach ($dependency in @($Definition.ModuleList))
        {
            $rootModuleContent += "Import-Module $($dependency.ModuleName) -RequiredVersion '$($dependency.ModuleVersion)' -Global -ErrorAction Stop"
        }
        Set-Content -Path $rootModulePath -Value $rootModuleContent -Encoding utf8

        $manifestPath = Join-Path $moduleRoot "$($Definition.PackageName).psd1"
        New-ModuleManifest `
            -Path $manifestPath `
            -RootModule "$($Definition.PackageName).psm1" `
            -ModuleVersion $Definition.Version `
            -Guid $Definition.Guid `
            -Author 'Microsoft Corporation' `
            -CompanyName 'Microsoft Corporation' `
            -Copyright 'Microsoft Corporation. All rights reserved.' `
            -Description 'Azclips PowerShell cmdlets rollup module' `
            -PowerShellVersion '7.6' `
            -CompatiblePSEditions @('Core') `
            -RequiredModules @() `
            -ModuleList @() `
            -FunctionsToExport @() `
            -CmdletsToExport @() `
            -Tags @('Azure', 'ResourceManager', 'ARM', 'Provider', 'Azclips') `
            -ProjectUri 'https://github.com/Azure/azclips' `
            -LicenseUri 'https://aka.ms/azps-license' `
            -ReleaseNotes '* First preview'

        return $moduleRoot
    }

    $binRoot = Join-Path $moduleRoot 'bin'
    $extensionsRoot = Join-Path $moduleRoot 'Extensions'
    $helpRoot = Join-Path $moduleRoot 'en-US'
    $null = New-Item -ItemType Directory -Path $binRoot, $extensionsRoot, $helpRoot -Force

    if ($Definition.SourceModule -eq 'Accounts')
    {
        foreach ($assembly in $CoreAssemblies)
        {
            Copy-RequiredFile -Path (Join-Path $ArtifactsRoot "Core/$assembly") -Destination (Join-Path $moduleRoot "Core/$assembly")
        }
    }

    $requiredExtensionNames = @($Definition.SourceModule)
    foreach ($extensionName in $requiredExtensionNames)
    {
        Copy-ExtensionFromPackage -ArtifactsRoot $ArtifactsRoot `
            -PackageId "azcli.extensions.$($extensionName.ToLowerInvariant())" `
            -AssemblyName "Microsoft.Azure.Azclips.Extensions.$extensionName" `
            -DestinationDirectory $extensionsRoot
    }

    Copy-RequiredFile -Path (Join-Path $ArtifactsRoot "Microsoft.Azure.Azclips.Cmdlets.$($Definition.SourceModule).dll") -Destination (Join-Path $binRoot "Microsoft.Azure.Azclips.Cmdlets.$($Definition.SourceModule).dll")

    $helpFileName = "Microsoft.Azure.Azclips.Cmdlets.$($Definition.SourceModule).dll-Help.xml"
    $sourceHelp = Join-Path $SourcePsRoot "$($Definition.SourceModule)/en-US/$helpFileName"
    Copy-RequiredFile -Path $sourceHelp -Destination (Join-Path $helpRoot $helpFileName)

    $requiredAssemblies = @()
    if ($Definition.SourceModule -eq 'Accounts')
    {
        $requiredAssemblies += @($CoreAssemblies | ForEach-Object { "Core/$_" })
        $requiredAssemblies += "Extensions/Microsoft.Azure.Azclips.Extensions.$($Definition.SourceModule).dll"
    }

    $rootModulePath = Join-Path $moduleRoot "$($Definition.PackageName).psm1"
    $cmdletsToExportLiteral = "@('$(($Definition.CmdletsToExport | ForEach-Object { $_.Replace("'", "''") }) -join "', '")')"
    $rootModuleContent = @(
        '$ErrorActionPreference = ''Stop'''
        ''
    )

    if ($Definition.SourceModule -ne 'Accounts')
    {
        $dependency = @($Definition.ModuleList | Select-Object -First 1)
        if (-not $dependency)
        {
            throw "Module '$($Definition.SourceModule)' does not declare a required source module dependency."
        }

        $rootModuleContent += @(
            "`$dependencyModule = Get-Module -Name '$($dependency.ModuleName)'"
            "if (`$dependencyModule -ne `$null -and `$dependencyModule.Version -ne [System.Version]'$($dependency.ModuleVersion)')",
            '{',
            "    Write-Error 'This module requires $($dependency.ModuleName) version $($dependency.ModuleVersion). A different version of $($dependency.ModuleName) is imported in the current PowerShell session. Please open a new session before importing this module.' -ErrorAction Stop",
            '}',
            'elseif ($dependencyModule -eq $null)',
            '{',
            "    Import-Module $($dependency.ModuleName) -RequiredVersion '$($dependency.ModuleVersion)' -Scope Global -ErrorAction Stop",
            '}',
            ''
        )
    }

    if ($Definition.SourceModule -ne 'Accounts')
    {
        $rootModuleContent += "Add-Type -Path (Join-Path -Path `$PSScriptRoot -ChildPath 'Extensions/Microsoft.Azure.Azclips.Extensions.$($Definition.SourceModule).dll')"
    }

    $rootModuleContent += @(
        "Import-Module (Join-Path -Path `$PSScriptRoot -ChildPath 'bin/Microsoft.Azure.Azclips.Cmdlets.$($Definition.SourceModule).dll')",
        "Export-ModuleMember -Cmdlet $cmdletsToExportLiteral"
    )
    Set-Content -Path $rootModulePath -Value $rootModuleContent -Encoding utf8

    $manifestPath = Join-Path $moduleRoot "$($Definition.PackageName).psd1"
    $manifestParameters = @{
        Path = $manifestPath
        RootModule = "$($Definition.PackageName).psm1"
        ModuleVersion = $Definition.Version
        Guid = $Definition.Guid
        Author = 'Microsoft Corporation'
        CompanyName = 'Microsoft Corporation'
        Copyright = 'Microsoft Corporation. All rights reserved.'
        Description = "Azclips - $($Definition.SourceModule)"
        PowerShellVersion = '7.6'
        CompatiblePSEditions = @('Core')
        RequiredAssemblies = $requiredAssemblies
        NestedModules = @()
        ModuleList = @()
        RequiredModules = @()
        FunctionsToExport = @()
        CmdletsToExport = $Definition.CmdletsToExport
        Tags = @('Azure', 'ResourceManager', 'ARM', 'Provider', $Definition.SourceModule, 'Azclips')
        ProjectUri = 'https://github.com/Azure/azclips'
        LicenseUri = 'https://aka.ms/azps-license'
        ReleaseNotes = '* First preview'
    }
    New-ModuleManifest @manifestParameters

    $manifestContent = Get-Content -Path $manifestPath -Raw
    $manifestContent = $manifestContent -replace '(?m)^# RequiredModules = @\(\)$', 'RequiredModules = @()'
    Set-Content -Path $manifestPath -Value $manifestContent -Encoding utf8

    return $moduleRoot
}

function Add-NupkgDependency
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackagePath,

        [Parameter(Mandatory = $true)]
        [string]$DependencyId,

        [Parameter(Mandatory = $true)]
        [string]$DependencyVersion
    )

    $extractRoot = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString())
    try
    {
        Expand-Archive -Path $PackagePath -DestinationPath $extractRoot -Force
        $nuspecPath = Get-ChildItem -Path $extractRoot -Filter '*.nuspec' -File | Select-Object -First 1 -ExpandProperty FullName
        if (-not $nuspecPath)
        {
            throw "No nuspec was found in package: $PackagePath"
        }

        [xml]$nuspec = Get-Content -Path $nuspecPath -Raw
        $namespaceUri = $nuspec.package.NamespaceURI
        $metadata = $nuspec.package.metadata
        $dependencies = $metadata.dependencies
        if (-not $dependencies)
        {
            $dependencies = $nuspec.CreateElement('dependencies', $namespaceUri)
            $null = $metadata.AppendChild($dependencies)
        }

        $existingDependency = @($dependencies.dependency | Where-Object { $_.id -eq $DependencyId })
        if ($existingDependency.Count -eq 0)
        {
            $dependency = $nuspec.CreateElement('dependency', $namespaceUri)
            $dependency.SetAttribute('id', $DependencyId)
            $dependency.SetAttribute('version', $DependencyVersion)
            $null = $dependencies.AppendChild($dependency)
        }

        $nuspec.Save($nuspecPath)
        Remove-Item -Path $PackagePath -Force
        [System.IO.Compression.ZipFile]::CreateFromDirectory($extractRoot, $PackagePath)
    }
    finally
    {
        Remove-Item -Path $extractRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Update-NupkgManifestModuleList
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackagePath,

        [Parameter(Mandatory = $true)]
        [object[]]$ModuleList
    )

    $extractRoot = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString())
    try
    {
        Expand-Archive -Path $PackagePath -DestinationPath $extractRoot -Force
        $manifestPath = Get-ChildItem -Path $extractRoot -Filter '*.psd1' -File | Select-Object -First 1 -ExpandProperty FullName
        if (-not $manifestPath)
        {
            throw "No module manifest was found in package: $PackagePath"
        }

        $moduleListEntries = @($ModuleList | ForEach-Object { "@{ModuleName = '$($_.ModuleName)'; ModuleVersion = '$($_.ModuleVersion)'; }" }) -join ', '
        $manifestContent = Get-Content -Path $manifestPath -Raw
        $manifestContent = $manifestContent -replace '(?m)^#?\s*ModuleList = @\(\)$', "ModuleList = @($moduleListEntries)"
        Set-Content -Path $manifestPath -Value $manifestContent -Encoding utf8

        Remove-Item -Path $PackagePath -Force
        [System.IO.Compression.ZipFile]::CreateFromDirectory($extractRoot, $PackagePath)
    }
    finally
    {
        Remove-Item -Path $extractRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Resolve-ModuleDependency
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$ModuleName,

        [Parameter(Mandatory = $true)]
        [string]$ManifestDirectory
    )

    # A bare name means a sibling module directory; a path points at the manifest itself.
    if ($ModuleName -notmatch '[\\/]')
    {
        $siblingManifest = Join-Path (Split-Path $ManifestDirectory -Parent) $ModuleName "$ModuleName.psd1"
        if (-not (Test-Path -Path $siblingManifest -PathType Leaf))
        {
            throw "Required module manifest was not found: $siblingManifest"
        }
        return $ModuleName
    }

    $dependencyPath = $ModuleName
    if (-not [System.IO.Path]::IsPathRooted($dependencyPath))
    {
        $dependencyPath = Join-Path $ManifestDirectory $dependencyPath
    }

    $dependencyPath = [System.IO.Path]::GetFullPath($dependencyPath)
    if (-not (Test-Path -Path $dependencyPath -PathType Leaf))
    {
        throw "Required module manifest was not found: $dependencyPath"
    }

    return [System.IO.Path]::GetFileNameWithoutExtension($dependencyPath)
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
        }

        Write-Host "Imported $ModuleName from local repository install root: $($loadedModule.Path)"
        Write-Host "Validated $ModuleName cmdlets: $($expectedCmdlets -join ', ')"
    } -Args $InstallRoot, $Definition.PackageName, (ConvertTo-Json -InputObject @($Definition.CmdletsToExport) -Compress)

    if ($LASTEXITCODE -ne 0)
    {
        throw "$($Definition.PackageName) local repository import validation failed (exit $LASTEXITCODE)."
    }
}

$resolvedArtifactsRoot = (Resolve-Path -Path $ArtifactsRoot).Path
if (-not $SourcePsRoot)
{
    $repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
    $SourcePsRoot = Join-Path $repoRoot 'src/PS'
}
$resolvedSourcePsRoot = (Resolve-Path -Path $SourcePsRoot).Path

if (-not $OutputRoot)
{
    $OutputRoot = Join-Path $resolvedArtifactsRoot 'PSGallery'
}

$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$modulesRoot = Join-Path $resolvedOutputRoot 'modules'
$repositoryRoot = Join-Path $resolvedOutputRoot 'local-repo'
$installRoot = Join-Path $resolvedOutputRoot 'installed-modules'

Remove-Item -Path $modulesRoot, $repositoryRoot, $installRoot -Recurse -Force -ErrorAction SilentlyContinue
$null = New-Item -ItemType Directory -Path $modulesRoot, $repositoryRoot, $installRoot -Force

$packageDefinitions = Get-ChildItem -Path $resolvedSourcePsRoot -Filter '*.psd1' -Recurse |
    Sort-Object -Property @{ Expression = { if ($_.BaseName -eq 'Accounts') { 0 } elseif ($_.BaseName -eq 'Az') { 2 } else { 1 } } }, FullName |
    ForEach-Object {
        $manifestData = Import-PowerShellDataFile -Path $_.FullName
        $sourceModule = $_.BaseName
        $manifestDirectory = Split-Path -Parent $_.FullName

        $moduleList = @()
        foreach ($requiredModule in @($manifestData.RequiredModules | Where-Object { $_ -and $_.ModuleName }))
        {
            $dependencySourceModule = Resolve-ModuleDependency -ModuleName $requiredModule.ModuleName -ManifestDirectory $manifestDirectory
            $moduleList += @{ ModuleName = (Get-AzPackageName -SourceModule $dependencySourceModule); ModuleVersion = [string]$requiredModule.ModuleVersion }
        }

        [pscustomobject]@{
            SourceModule = $sourceModule
            PackageName = Get-AzPackageName -SourceModule $sourceModule
            Version = [string]$manifestData.ModuleVersion
            CmdletsToExport = @($manifestData.CmdletsToExport)
            Guid = [string]$manifestData.Guid
            ModuleList = $moduleList
            IsRollup = $sourceModule -eq 'Az'
        }
    }

$coreAssemblies = Get-ChildItem -Path (Join-Path $resolvedArtifactsRoot 'Core') -Filter '*.dll' -File |
    Sort-Object -Property Name |
    Select-Object -ExpandProperty Name

foreach ($definition in $packageDefinitions)
{
    $moduleRoot = New-AzModulePackage -Definition $definition -ArtifactsRoot $resolvedArtifactsRoot -SourcePsRoot $resolvedSourcePsRoot -ModulesRoot $modulesRoot -CoreAssemblies $coreAssemblies
    Write-Host "Created $($definition.PackageName) module folder: $moduleRoot"
}

$existingRepository = Get-PSRepository -Name $RepositoryName -ErrorAction SilentlyContinue
if ($existingRepository)
{
    Unregister-PSRepository -Name $RepositoryName
}

try
{
    Register-PSRepository -Name $RepositoryName -SourceLocation $repositoryRoot -PublishLocation $repositoryRoot -InstallationPolicy Trusted -PackageManagementProvider NuGet

    foreach ($definition in $packageDefinitions)
    {
        Publish-Module -Path (Join-Path $modulesRoot $definition.PackageName) -Repository $RepositoryName -Force
    }

    $publishedPackages = Get-ChildItem -Path $repositoryRoot -Filter 'Az.*.nupkg' -File | Sort-Object -Property Name
    foreach ($definition in $packageDefinitions)
    {
        $packageFile = $publishedPackages | Where-Object { $_.Name -eq "$($definition.PackageName).$($definition.Version).nupkg" } | Select-Object -First 1
        if (-not $packageFile)
        {
            throw "$($definition.PackageName) package was not published to local repository: $repositoryRoot"
        }

        if ($definition.SourceModule -ne 'Accounts')
        {
            Update-NupkgManifestModuleList -PackagePath $packageFile.FullName -ModuleList $definition.ModuleList
            foreach ($dependency in @($definition.ModuleList))
            {
                Add-NupkgDependency -PackagePath $packageFile.FullName -DependencyId $dependency.ModuleName -DependencyVersion $dependency.ModuleVersion
            }
        }

        Write-Host "Published $($definition.PackageName) package: $($packageFile.FullName)"
    }

    if (-not $SkipInstallValidation)
    {
        foreach ($definition in $packageDefinitions)
        {
            Save-Module -Name $definition.PackageName -Repository $RepositoryName -Path $installRoot -Force
            Assert-InstalledModuleImport -PwshPath $PwshPath -InstallRoot $installRoot -Definition $definition
        }
    }
}
finally
{
    Unregister-PSRepository -Name $RepositoryName -ErrorAction SilentlyContinue
}

Write-Host "Published Az module packages to local repository: $repositoryRoot"