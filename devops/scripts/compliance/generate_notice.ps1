param(
    [string] $RepoRoot,
    [string] $TemplatePath,
    [string] $OverridesPath,
    [string] $OutputPath,
    [switch] $SkipRestore,
    [switch] $UpdateNotice
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
}
if ([string]::IsNullOrWhiteSpace($TemplatePath)) {
    $TemplatePath = Join-Path $PSScriptRoot 'NOTICE.template'
}
if ([string]::IsNullOrWhiteSpace($OverridesPath)) {
    $OverridesPath = Join-Path $PSScriptRoot 'notice-overrides.json'
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $RepoRoot 'artifacts/compliance/NOTICE.generated'
}

$RepoRoot = [System.IO.Path]::GetFullPath($RepoRoot)
$TemplatePath = [System.IO.Path]::GetFullPath($TemplatePath)
$OverridesPath = [System.IO.Path]::GetFullPath($OverridesPath)
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)

function Get-JsonFile {
    param([string] $Path)

    if (-not (Test-Path -Path $Path -PathType Leaf)) {
        throw "Required file was not found: $Path"
    }

    return Get-Content -Path $Path -Raw | ConvertFrom-Json
}

function Get-NuGetGlobalPackagesRoot {
    $output = & dotnet nuget locals global-packages --list 2>$null
    if ($LASTEXITCODE -eq 0 -and $output -match 'global-packages:\s*(.+)$') {
        return $Matches[1].Trim()
    }

    return Join-Path $HOME '.nuget/packages'
}

function Get-NuGetMetadata {
    param(
        [string] $PackageName,
        [string] $Version,
        [string] $GlobalPackagesRoot
    )

    $packageRoot = Join-Path $GlobalPackagesRoot $PackageName.ToLowerInvariant()
    $versionRoot = Join-Path $packageRoot $Version.ToLowerInvariant()
    $nuspecPath = Join-Path $versionRoot "$($PackageName.ToLowerInvariant()).nuspec"
    $metadata = [ordered]@{
        License = 'Unknown'
        ProjectUrl = $null
        PackageUrl = "https://www.nuget.org/packages/$PackageName/$Version"
        Notes = $null
    }

    if (-not (Test-Path -Path $nuspecPath -PathType Leaf)) {
        return [pscustomobject]$metadata
    }

    [xml] $nuspec = Get-Content -Path $nuspecPath -Raw
    $nuspecMetadata = $nuspec.package.metadata
    if ($nuspecMetadata.PSObject.Properties['license'] -and $nuspecMetadata.license) {
        $metadata.License = [string] $nuspecMetadata.license.InnerText
    }
    elseif ($nuspecMetadata.PSObject.Properties['licenseUrl'] -and $nuspecMetadata.licenseUrl) {
        $metadata.License = [string] $nuspecMetadata.licenseUrl
    }

    if ($nuspecMetadata.PSObject.Properties['projectUrl'] -and $nuspecMetadata.projectUrl) {
        $metadata.ProjectUrl = [string] $nuspecMetadata.projectUrl
    }
    elseif ($nuspecMetadata.PSObject.Properties['repository'] -and $nuspecMetadata.repository -and $nuspecMetadata.repository.url) {
        $metadata.ProjectUrl = [string] $nuspecMetadata.repository.url
    }

    return [pscustomobject]$metadata
}

function Get-OverrideValue {
    param(
        [object] $Overrides,
        [string] $Ecosystem,
        [string] $Name
    )

    if (-not $Overrides -or -not $Overrides.PSObject.Properties['packages']) {
        return $null
    }

    $packages = $Overrides.packages
    foreach ($key in @("${Ecosystem}:$Name", $Name)) {
        $property = $packages.PSObject.Properties[$key]
        if ($property) {
            return $property.Value
        }
    }

    return $null
}

function Merge-Override {
    param(
        [pscustomobject] $Dependency,
        [object] $Override
    )

    if (-not $Override) {
        return $Dependency
    }

    foreach ($propertyName in @('license', 'projectUrl', 'packageUrl', 'notes')) {
        $property = $Override.PSObject.Properties[$propertyName]
        if ($property -and -not [string]::IsNullOrWhiteSpace([string] $property.Value)) {
            $targetName = $propertyName.Substring(0, 1).ToUpperInvariant() + $propertyName.Substring(1)
            $Dependency.$targetName = [string] $property.Value
        }
    }

    return $Dependency
}

function Add-Dependency {
    param(
        [hashtable] $Dependencies,
        [string] $Ecosystem,
        [string] $Name,
        [string] $Version,
        [string] $License,
        [string] $ProjectUrl,
        [string] $PackageUrl,
        [string] $Notes,
        [object] $Overrides
    )

    if ([string]::IsNullOrWhiteSpace($Name) -or [string]::IsNullOrWhiteSpace($Version)) {
        return
    }

    $dependency = [pscustomobject]@{
        Ecosystem = $Ecosystem
        Name = $Name
        Version = $Version
        License = if ([string]::IsNullOrWhiteSpace($License)) { 'Unknown' } else { $License }
        ProjectUrl = $ProjectUrl
        PackageUrl = $PackageUrl
        Notes = $Notes
    }
    $dependency = Merge-Override -Dependency $dependency -Override (Get-OverrideValue -Overrides $Overrides -Ecosystem $Ecosystem -Name $Name)

    $key = "$($dependency.Ecosystem.ToLowerInvariant())|$($dependency.Name.ToLowerInvariant())|$($dependency.Version.ToLowerInvariant())"
    $Dependencies[$key] = $dependency
}

function Test-DistributableProjectPath {
    param(
        [string] $Root,
        [string] $ProjectPath
    )

    $relativePath = [System.IO.Path]::GetRelativePath($Root, $ProjectPath).Replace('\\', '/')
    if ($relativePath -match '(^|/)(tests?|Benchmark|Fixtures)(/|$)') {
        return $false
    }

    return ($relativePath.StartsWith('src/') -or
        $relativePath -eq 'Agent/Core.Agent.csproj' -or
        $relativePath -eq 'TUI/Core.Tui.csproj')
}

function Get-DistributableProjectFiles {
    param(
        [string] $Root
    )

    return @(Get-ChildItem -Path $Root -Filter '*.csproj' -Recurse -Force |
        Where-Object {
            $_.FullName -notmatch '[\/](artifacts|bin|obj)[\/]' -and
            (Test-DistributableProjectPath -Root $Root -ProjectPath $_.FullName)
        })
}

function Get-PrivateAssetPackageNames {
    param([System.IO.FileInfo] $ProjectFile)

    $privatePackages = @{}
    [xml] $projectXml = Get-Content -Path $ProjectFile.FullName -Raw
    $packageReferences = $projectXml.SelectNodes('//*[local-name()="PackageReference"]')
    foreach ($packageReference in $packageReferences) {
        $name = [string] $packageReference.GetAttribute('Include')
        if ([string]::IsNullOrWhiteSpace($name)) {
            continue
        }

        $privateAssets = [string] $packageReference.GetAttribute('PrivateAssets')
        if ([string]::IsNullOrWhiteSpace($privateAssets)) {
            $privateAssetsNode = $packageReference.SelectSingleNode('*[local-name()="PrivateAssets"]')
            if ($privateAssetsNode) {
                $privateAssets = [string] $privateAssetsNode.InnerText
            }
        }

        $privateAssetValues = @($privateAssets.Split(';', [System.StringSplitOptions]::RemoveEmptyEntries) |
            ForEach-Object { $_.Trim().ToLowerInvariant() })
        if ($privateAssetValues -contains 'all') {
            $privatePackages[$name.ToLowerInvariant()] = $true
        }
    }

    return $privatePackages
}

function Get-PackageIdentityFromAssetsKey {
    param([string] $LibraryKey)

    $separatorIndex = $LibraryKey.LastIndexOf('/')
    if ($separatorIndex -lt 1 -or $separatorIndex -eq ($LibraryKey.Length - 1)) {
        return $null
    }

    return [pscustomobject]@{
        Name = $LibraryKey.Substring(0, $separatorIndex)
        Version = $LibraryKey.Substring($separatorIndex + 1)
    }
}

function Get-PackageDependencyMapFromAssets {
    param([object] $Assets)

    $dependencyMap = @{}
    if (-not $Assets.PSObject.Properties['targets']) {
        return $dependencyMap
    }

    foreach ($targetProperty in $Assets.targets.PSObject.Properties) {
        foreach ($packageProperty in $targetProperty.Value.PSObject.Properties) {
            $identity = Get-PackageIdentityFromAssetsKey -LibraryKey $packageProperty.Name
            if (-not $identity) {
                continue
            }

            $package = $packageProperty.Value
            if (-not $package.PSObject.Properties['type'] -or [string] $package.type -ne 'package') {
                continue
            }

            $packageName = $identity.Name.ToLowerInvariant()
            if (-not $dependencyMap.ContainsKey($packageName)) {
                $dependencyMap[$packageName] = @{}
            }

            if (-not $package.PSObject.Properties['dependencies']) {
                continue
            }

            foreach ($dependencyProperty in $package.dependencies.PSObject.Properties) {
                $dependencyMap[$packageName][$dependencyProperty.Name.ToLowerInvariant()] = $true
            }
        }
    }

    return $dependencyMap
}

function Get-PackageDependencyClosure {
    param(
        [hashtable] $DependencyMap,
        [hashtable] $RootPackageNames
    )

    $closure = @{}
    $queue = New-Object System.Collections.Generic.Queue[string]
    foreach ($packageName in $RootPackageNames.Keys) {
        $queue.Enqueue($packageName)
    }

    while ($queue.Count -gt 0) {
        $packageName = $queue.Dequeue()
        if ($closure.ContainsKey($packageName)) {
            continue
        }

        $closure[$packageName] = $true
        if (-not $DependencyMap.ContainsKey($packageName)) {
            continue
        }

        foreach ($dependencyName in $DependencyMap[$packageName].Keys) {
            if (-not $closure.ContainsKey($dependencyName)) {
                $queue.Enqueue($dependencyName)
            }
        }
    }

    return $closure
}

function Get-NuGetDependenciesFromRestoreAssets {
    param(
        [string] $Root,
        [string] $GlobalPackagesRoot,
        [object] $Overrides
    )

    $dependencies = @{}
    $projectFiles = Get-DistributableProjectFiles -Root $Root

    foreach ($projectFile in $projectFiles) {
        $assetsPath = Join-Path $projectFile.DirectoryName 'obj/project.assets.json'
        if (-not (Test-Path -Path $assetsPath -PathType Leaf)) {
            throw "Restore assets were not found for distributable project $($projectFile.FullName). Run dotnet restore or omit -SkipRestore."
        }

        $privateAssetPackageNames = Get-PrivateAssetPackageNames -ProjectFile $projectFile
        $assets = Get-JsonFile -Path $assetsPath
        $privateAssetPackageClosure = Get-PackageDependencyClosure -DependencyMap (Get-PackageDependencyMapFromAssets -Assets $assets) -RootPackageNames $privateAssetPackageNames
        if (-not $assets.PSObject.Properties['libraries']) {
            continue
        }

        foreach ($libraryProperty in $assets.libraries.PSObject.Properties) {
            $library = $libraryProperty.Value
            if (-not $library.PSObject.Properties['type'] -or [string] $library.type -ne 'package') {
                continue
            }

            $identity = Get-PackageIdentityFromAssetsKey -LibraryKey $libraryProperty.Name
            if (-not $identity) {
                continue
            }

            if ($privateAssetPackageClosure.ContainsKey($identity.Name.ToLowerInvariant())) {
                continue
            }

            $metadata = Get-NuGetMetadata -PackageName $identity.Name -Version $identity.Version -GlobalPackagesRoot $GlobalPackagesRoot
            Add-Dependency -Dependencies $dependencies -Ecosystem 'NuGet' -Name $identity.Name -Version $identity.Version -License $metadata.License -ProjectUrl $metadata.ProjectUrl -PackageUrl $metadata.PackageUrl -Notes $metadata.Notes -Overrides $Overrides
        }
    }

    return $dependencies.Values
}

function Get-LicenseSectionTitle {
    param([string] $License)

    switch -Regex ($License) {
        '^MIT$' { return 'MIT License:' }
        '^(Apache-2\.0|Apache License 2\.0)$' { return 'Apache License 2.0:' }
        '^Pending verification$' { return 'License pending verification (confirm via Component Governance before release):' }
        '^Unknown$' { return 'License pending verification (confirm via Component Governance before release):' }
        default { return "$License License:" }
    }
}

function Format-LicenseNoticeSections {
    param([object[]] $Dependencies)

    $lines = New-Object System.Collections.Generic.List[string]
    $sectionOrder = @(
        'MIT',
        'Apache-2.0',
        'Apache License 2.0',
        'Pending verification',
        'Unknown'
    )
    $licenses = @($Dependencies | ForEach-Object { $_.License } | Sort-Object -Unique)
    $orderedLicenses = @($sectionOrder | Where-Object { $licenses -contains $_ }) + @($licenses | Where-Object { $sectionOrder -notcontains $_ })
    $emittedTitles = @{}

    foreach ($license in $orderedLicenses) {
        $title = Get-LicenseSectionTitle -License $license
        if ($emittedTitles.ContainsKey($title)) {
            continue
        }

        $sectionDependencies = @($Dependencies | Where-Object { (Get-LicenseSectionTitle -License $_.License) -eq $title } | Sort-Object Name, Version)
        if ($sectionDependencies.Count -eq 0) {
            continue
        }

        $lines.Add($title)
        $lines.Add('')
        foreach ($dependency in ($sectionDependencies | Sort-Object Name, Version)) {
            $lines.Add("- $($dependency.Name) ($($dependency.Version))")
        }
        $lines.Add('')
        $emittedTitles[$title] = $true
    }

    return $lines
}

function Add-ContentLines {
    param(
        [System.Collections.Generic.List[string]] $Target,
        [object[]] $Lines
    )

    foreach ($line in $Lines) {
        $Target.Add([string] $line)
    }
}

if (-not (Test-Path -Path $TemplatePath -PathType Leaf)) {
    throw "NOTICE template was not found: $TemplatePath"
}

$overrides = Get-JsonFile -Path $OverridesPath

if (-not $SkipRestore) {
    $solutionPath = Get-ChildItem -Path $RepoRoot -Filter '*.slnx' -File | Sort-Object Name | Select-Object -First 1 -ExpandProperty FullName
    if ($solutionPath -and (Test-Path -Path $solutionPath -PathType Leaf)) {
        & dotnet restore $solutionPath
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet restore failed with exit code $LASTEXITCODE"
        }
    }
}

$nugetGlobalPackagesRoot = Get-NuGetGlobalPackagesRoot
$nugetDependencies = @(Get-NuGetDependenciesFromRestoreAssets -Root $RepoRoot -GlobalPackagesRoot $nugetGlobalPackagesRoot -Overrides $overrides)

$contentLines = New-Object System.Collections.Generic.List[string]
Add-ContentLines -Target $contentLines -Lines (Format-LicenseNoticeSections -Dependencies $nugetDependencies)

$template = Get-Content -Path $TemplatePath -Raw
if (-not $template.Contains('{{GENERATED_NOTICE_CONTENT}}')) {
    throw "NOTICE template must contain {{GENERATED_NOTICE_CONTENT}} placeholder."
}
$notice = $template.Replace('{{GENERATED_NOTICE_CONTENT}}', ($contentLines -join "`n")) -replace "`r`n", "`n"
if (-not $notice.EndsWith("`n")) {
    $notice += "`n"
}

New-Item -Path (Split-Path -Parent $OutputPath) -ItemType Directory -Force | Out-Null
[System.IO.File]::WriteAllText($OutputPath, $notice, [System.Text.UTF8Encoding]::new($false))
Write-Host "Generated $OutputPath"

if ($UpdateNotice) {
    $noticePath = Join-Path $RepoRoot 'NOTICE'
    [System.IO.File]::WriteAllText($noticePath, $notice, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Updated $noticePath"
}