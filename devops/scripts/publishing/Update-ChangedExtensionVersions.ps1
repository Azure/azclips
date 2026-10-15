param(
    [string] $RepoRoot,
    [string] $BaseRef,
    [string] $PlanPath,
    [switch] $DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
}

$RepoRoot = [System.IO.Path]::GetFullPath($RepoRoot)
$rollupManifestPath = Join-Path $RepoRoot 'src/PS/Az/Az.psd1'

function Get-LatestStableTag {
    param([string] $Root)

    $tagPattern = '^v[0-9]+\.[0-9]+\.[0-9]+\.[0-9]{8}T[0-9]{6}Z$'
    $tagOutput = @(& git --no-pager -C $Root tag --merged HEAD --sort=-version:refname --list 'v*' 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "git tag failed while resolving the latest stable tag. $($tagOutput -join "`n")"
    }

    @($tagOutput | Where-Object { $_ -match $tagPattern }) | Select-Object -First 1
}

function Get-ProjectVersion {
    param([string] $Content)

    $match = [regex]::Match($Content, '<Version>\s*(?<version>[0-9]+\.[0-9]+\.[0-9]+)\s*</Version>')
    if (-not $match.Success) {
        return $null
    }

    $match.Groups['version'].Value
}

function Get-NextPatchVersion {
    param([string] $Version)

    $match = [regex]::Match($Version, '^(?<major>[0-9]+)\.(?<minor>[0-9]+)\.(?<patch>[0-9]+)$')
    if (-not $match.Success) {
        throw "Extension version must be stable SemVer x.y.z. Received: $Version"
    }

    $nextPatch = [int]$match.Groups['patch'].Value + 1
    "$($match.Groups['major'].Value).$($match.Groups['minor'].Value).$nextPatch"
}

function Get-ModuleDependencyPattern {
    param([string] $ModuleName)

    "(?m)^(?<prefix>[^\S\r\n]*@\{[^\S\r\n]*ModuleName[^\S\r\n]*=[^\S\r\n]*'$([regex]::Escape($ModuleName))';[^\S\r\n]*ModuleVersion[^\S\r\n]*=[^\S\r\n]*)'[^']+'(?<suffix>[^\r\n]*)$"
}

function Set-LineVersion {
    param(
        [string] $Path,
        [string] $Pattern,
        [string] $Replacement,
        [string] $Description
    )

    $originalBytes = [System.IO.File]::ReadAllBytes($Path)
    $hasUtf8Bom = $originalBytes.Length -ge 3 -and
        $originalBytes[0] -eq 0xEF -and
        $originalBytes[1] -eq 0xBB -and
        $originalBytes[2] -eq 0xBF
    $content = [System.IO.File]::ReadAllText($Path)
    $linePattern = [regex]::new($Pattern, [System.Text.RegularExpressions.RegexOptions]::Multiline)
    if ($linePattern.IsMatch($content)) {
        $updatedContent = $linePattern.Replace($content, $Replacement, 1)
        if ($updatedContent -ceq $content) {
            return
        }
        if ($DryRun) {
            Write-Host "Dry run: would update $Path"
        }
        else {
            [System.IO.File]::WriteAllText($Path, $updatedContent, [System.Text.UTF8Encoding]::new($hasUtf8Bom))
        }
        return
    }

    throw "$Description was not found in $Path"
}

function Get-FileAtRef {
    param(
        [string] $Root,
        [string] $Ref,
        [string] $RelativePath
    )

    $content = @(& git --no-pager -C $Root show "${Ref}:$RelativePath" 2>&1)
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    $content -join "`n"
}

if (-not (Test-Path -Path $rollupManifestPath -PathType Leaf)) {
    throw "Az rollup manifest was not found: $rollupManifestPath"
}

$resolvedBaseRef = if ([string]::IsNullOrWhiteSpace($BaseRef)) {
    Get-LatestStableTag -Root $RepoRoot
}
else {
    $BaseRef.Trim()
}

$isFirstRelease = [string]::IsNullOrWhiteSpace($resolvedBaseRef)
$changedPaths = if ($isFirstRelease) {
    @(& git --no-pager -C $RepoRoot ls-files -- 'src/Extensions' 'src/PS' 2>&1)
}
else {
    @(& git --no-pager -C $RepoRoot diff --name-only "$resolvedBaseRef..HEAD" -- 'src/Extensions' 'src/PS' 2>&1)
}
if ($LASTEXITCODE -ne 0) {
    throw "git failed while detecting extension files. $($changedPaths -join "`n")"
}

$changedModules = @(
    $changedPaths |
        ForEach-Object {
            if ($_ -match '^src/Extensions/(?<module>[^/]+)/.+\.cs$') {
                $Matches.module
            }
            elseif ($_ -match '^src/PS/(?<module>[^/]+)/' -and $Matches.module -ne 'Az') {
                $Matches.module
            }
        } |
        Sort-Object -Unique
)
    $directChangedModuleSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $changedModules | ForEach-Object { [void]$directChangedModuleSet.Add($_) }

$rollupContent = Get-Content -Path $rollupManifestPath -Raw
$rollupData = Import-PowerShellDataFile -Path $rollupManifestPath
$packageableModules = New-Object System.Collections.Generic.List[string]
$moduleMetadata = @{}
Get-ChildItem -Path (Join-Path $RepoRoot 'src/Extensions') -Directory | ForEach-Object {
    $moduleName = $_.Name
    $projectPath = Join-Path $_.FullName "Extensions.$moduleName.csproj"
    $moduleManifestPath = Join-Path $RepoRoot "src/PS/$moduleName/$moduleName.psd1"
    if (-not (Test-Path -Path $projectPath -PathType Leaf) -or
        -not (Test-Path -Path $moduleManifestPath -PathType Leaf)) {
        return
    }

    $moduleChangeLogPath = Join-Path $RepoRoot "src/PS/$moduleName/ChangeLog.md"
    $rollupModulePath = "../$moduleName/$moduleName.psd1"
    $rollupDependencyPattern = Get-ModuleDependencyPattern -ModuleName $rollupModulePath
    $requiredModules = @(@($rollupData.RequiredModules) | Where-Object {
        $_ -is [hashtable] -and $_.ModuleName -eq $rollupModulePath
    })
    if ($requiredModules.Count -ne 1 -or $rollupContent -notmatch $rollupDependencyPattern) {
        throw "Az rollup dependency was not found for packageable extension $moduleName`: $rollupModulePath"
    }
    if (-not (Test-Path -Path $moduleChangeLogPath -PathType Leaf)) {
        throw "PowerShell changelog was not found for rollup extension $moduleName`: $moduleChangeLogPath"
    }
    $projectVersion = Get-ProjectVersion -Content (Get-Content -Path $projectPath -Raw)
    if ([string]::IsNullOrWhiteSpace($projectVersion)) {
        throw "Version was not found in src/Extensions/$moduleName/Extensions.$moduleName.csproj"
    }
    $packageableModules.Add($moduleName)
    $moduleMetadata[$moduleName] = @{
        ManifestData = Import-PowerShellDataFile -Path $moduleManifestPath
        ProjectVersion = $projectVersion
        RollupDependency = $requiredModules[0]
    }
}

$changedModuleSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$changedModules | ForEach-Object { [void]$changedModuleSet.Add($_) }
do {
    $dependencyAdded = $false
    foreach ($moduleName in $packageableModules) {
        if ($changedModuleSet.Contains($moduleName)) {
            continue
        }
        $dependencyNames = @($moduleMetadata[$moduleName].ManifestData['RequiredModules'] |
            Where-Object { $_ -and $_.ModuleName } |
            ForEach-Object {
            [System.IO.Path]::GetFileNameWithoutExtension([string]$_.ModuleName)
        })
        if (@($dependencyNames | Where-Object { $changedModuleSet.Contains($_) }).Count -gt 0) {
            $dependencyAdded = $changedModuleSet.Add($moduleName) -or $dependencyAdded
        }
    }
} while ($dependencyAdded)
$changedModules = @($changedModuleSet | Sort-Object)

if (-not $isFirstRelease) {
    foreach ($moduleName in $packageableModules) {
        if ($changedModuleSet.Contains($moduleName)) {
            continue
        }
        $metadata = $moduleMetadata[$moduleName]
        if ([string]$metadata.ManifestData.ModuleVersion -ne $metadata.ProjectVersion -or
            [string]$metadata.RollupDependency.ModuleVersion -ne $metadata.ProjectVersion) {
            throw "Version metadata is not aligned for packageable extension $moduleName."
        }
    }
}

$baseVersions = @{}
$targetVersions = @{}
$newExtensions = @{}
foreach ($moduleName in $changedModules) {
    if ($moduleName -notin $packageableModules) {
        continue
    }
    $projectRelativePath = "src/Extensions/$moduleName/Extensions.$moduleName.csproj"
    $projectPath = Join-Path $RepoRoot $projectRelativePath
    $baseProjectContent = if ($isFirstRelease) {
        $null
    }
    else {
        Get-FileAtRef -Root $RepoRoot -Ref $resolvedBaseRef -RelativePath $projectRelativePath
    }
    $isNewExtension = $isFirstRelease -or $null -eq $baseProjectContent
    if ($isNewExtension) {
        $baseProjectContent = Get-Content -Path $projectPath -Raw
    }
    $baseVersion = Get-ProjectVersion -Content $baseProjectContent
    if ([string]::IsNullOrWhiteSpace($baseVersion)) {
        throw "Version was not found in $projectRelativePath"
    }
    $baseVersions[$moduleName] = $baseVersion
    $targetVersions[$moduleName] = if ($isNewExtension) { $baseVersion } else { Get-NextPatchVersion $baseVersion }
    $newExtensions[$moduleName] = $isNewExtension
}

$updatedModules = New-Object System.Collections.Generic.List[string]
foreach ($moduleName in $changedModules) {
    $projectRelativePath = "src/Extensions/$moduleName/Extensions.$moduleName.csproj"
    $projectPath = Join-Path $RepoRoot $projectRelativePath
    $moduleManifestPath = Join-Path $RepoRoot "src/PS/$moduleName/$moduleName.psd1"
    $moduleChangeLogPath = Join-Path $RepoRoot "src/PS/$moduleName/ChangeLog.md"
    $rollupModulePath = "../$moduleName/$moduleName.psd1"
    $rollupDependencyPattern = Get-ModuleDependencyPattern -ModuleName $rollupModulePath

    if ($moduleName -notin $packageableModules) {
        continue
    }

    $baseVersion = $baseVersions[$moduleName]
    $targetVersion = $targetVersions[$moduleName]

    Set-LineVersion -Path $projectPath `
        -Pattern '(<Version>)[^\S\r\n]*[0-9]+\.[0-9]+\.[0-9]+[^\S\r\n]*(</Version>)' `
        -Replacement "`${1}$targetVersion`${2}" `
        -Description 'Version'
    Set-LineVersion -Path $moduleManifestPath `
        -Pattern "(?<prefix>^[^\S\r\n]*ModuleVersion[^\S\r\n]*=[^\S\r\n]*)'[^']+'(?<suffix>[^\r\n]*)$" `
        -Replacement "`${prefix}'$targetVersion'`${suffix}" `
        -Description 'ModuleVersion'
    Set-LineVersion -Path $rollupManifestPath `
        -Pattern $rollupDependencyPattern `
        -Replacement "`${prefix}'$targetVersion'`${suffix}" `
        -Description "Az.$moduleName rollup dependency"

    $versionSummary = if ($newExtensions[$moduleName]) { "$targetVersion (new)" } else { "$baseVersion -> $targetVersion" }
    $updatedModules.Add("${moduleName}: $versionSummary")
}

foreach ($moduleName in $changedModules) {
    if ($moduleName -notin $packageableModules) {
        continue
    }
    $moduleManifestPath = Join-Path $RepoRoot "src/PS/$moduleName/$moduleName.psd1"
    $dependencies = @($moduleMetadata[$moduleName].ManifestData['RequiredModules'] |
        Where-Object { $_ -and $_.ModuleName })
    foreach ($dependency in $dependencies) {
        $dependencyName = [System.IO.Path]::GetFileNameWithoutExtension([string]$dependency.ModuleName)
        if (-not $targetVersions.ContainsKey($dependencyName)) {
            continue
        }
        Set-LineVersion -Path $moduleManifestPath `
            -Pattern (Get-ModuleDependencyPattern -ModuleName ([string]$dependency.ModuleName)) `
            -Replacement "`${prefix}'$($targetVersions[$dependencyName])'`${suffix}" `
            -Description "$moduleName dependency on $dependencyName"
    }
}

foreach ($moduleName in $packageableModules) {
    $projectVersion = Get-ProjectVersion -Content (Get-Content -Path (Join-Path $RepoRoot "src/Extensions/$moduleName/Extensions.$moduleName.csproj") -Raw)
    $manifestVersion = [string](Import-PowerShellDataFile -Path (Join-Path $RepoRoot "src/PS/$moduleName/$moduleName.psd1")).ModuleVersion
    $updatedRollupData = Import-PowerShellDataFile -Path $rollupManifestPath
    $rollupVersion = [string](@($updatedRollupData.RequiredModules) |
        Where-Object { $_.ModuleName -eq "../$moduleName/$moduleName.psd1" } |
        Select-Object -ExpandProperty ModuleVersion -First 1)
    if ($projectVersion -ne $manifestVersion -or $projectVersion -ne $rollupVersion) {
        throw "Version metadata is not aligned for packageable extension $moduleName."
    }
}

if (-not [string]::IsNullOrWhiteSpace($PlanPath)) {
    $releaseModules = @(
        foreach ($moduleName in $changedModules) {
            if ($moduleName -notin $packageableModules) {
                continue
            }

            $dependencyUpdates = @(
                foreach ($dependency in @($moduleMetadata[$moduleName].ManifestData['RequiredModules'] |
                        Where-Object { $_ -and $_.ModuleName })) {
                    $dependencyName = [System.IO.Path]::GetFileNameWithoutExtension([string]$dependency.ModuleName)
                    if (-not $targetVersions.ContainsKey($dependencyName)) {
                        continue
                    }

                    $baseDependencyVersion = [string]$dependency.ModuleVersion
                    $targetDependencyVersion = [string]$targetVersions[$dependencyName]
                    if ($baseDependencyVersion -eq $targetDependencyVersion) {
                        continue
                    }

                    [pscustomobject]@{
                        ModuleName = $dependencyName
                        BaseVersion = $baseDependencyVersion
                        TargetVersion = $targetDependencyVersion
                    }
                }
            )

            $reason = if ($newExtensions[$moduleName]) {
                'New'
            }
            elseif ($directChangedModuleSet.Contains($moduleName)) {
                'Direct'
            }
            else {
                'Dependency'
            }

            [pscustomobject]@{
                ModuleName = $moduleName
                BaseVersion = [string]$baseVersions[$moduleName]
                TargetVersion = [string]$targetVersions[$moduleName]
                Reason = $reason
                DependencyUpdates = $dependencyUpdates
            }
        }
    )
    $releasePlan = [pscustomobject]@{
        SchemaVersion = 1
        BaseRef = [string]$resolvedBaseRef
        Modules = $releaseModules
    }
    $resolvedPlanPath = [System.IO.Path]::GetFullPath($PlanPath)
    $planDirectory = Split-Path -Path $resolvedPlanPath -Parent
    if (-not (Test-Path -Path $planDirectory -PathType Container)) {
        New-Item -Path $planDirectory -ItemType Directory -Force | Out-Null
    }
    [System.IO.File]::WriteAllText(
        $resolvedPlanPath,
        ($releasePlan | ConvertTo-Json -Depth 6) + "`n",
        [System.Text.UTF8Encoding]::new($false)
    )
    Write-Host "Wrote release plan to $resolvedPlanPath"
}

if ($isFirstRelease) {
    Write-Host 'Aligned extension packaging metadata without incrementing versions for the first stable release.'
}
elseif ($updatedModules.Count -eq 0) {
    Write-Host "No existing rollup extension code changed since $resolvedBaseRef."
}
else {
    Write-Host "Updated extension package and PowerShell module versions since ${resolvedBaseRef}:"
    $updatedModules | ForEach-Object { Write-Host "- $_" }
}