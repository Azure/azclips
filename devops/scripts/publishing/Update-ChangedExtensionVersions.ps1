param(
    [string] $RepoRoot,
    [string] $BaseRef,
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
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.AddRange([string[]](Get-Content -Path $Path))
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -match $Pattern) {
            $lines[$index] = [regex]::Replace($lines[$index], $Pattern, $Replacement)
            if ($DryRun) {
                Write-Host "Dry run: would update $Path"
            }
            else {
                $content = ($lines -join "`n") + "`n"
                [System.IO.File]::WriteAllText($Path, $content, [System.Text.UTF8Encoding]::new($hasUtf8Bom))
            }
            return
        }
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
    @(& git --no-pager -C $RepoRoot ls-files -- 'src/Extensions' 2>&1)
}
else {
    @(& git --no-pager -C $RepoRoot diff --name-only "$resolvedBaseRef..HEAD" -- 'src/Extensions' 2>&1)
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
        } |
        Sort-Object -Unique
)

$rollupContent = Get-Content -Path $rollupManifestPath -Raw
$updatedModules = New-Object System.Collections.Generic.List[string]
foreach ($moduleName in $changedModules) {
    $projectRelativePath = "src/Extensions/$moduleName/Extensions.$moduleName.csproj"
    $projectPath = Join-Path $RepoRoot $projectRelativePath
    $moduleManifestPath = Join-Path $RepoRoot "src/PS/$moduleName/$moduleName.psd1"
    $moduleChangeLogPath = Join-Path $RepoRoot "src/PS/$moduleName/ChangeLog.md"
    $rollupModulePath = "../$moduleName/$moduleName.psd1"

    if (-not (Test-Path -Path $projectPath -PathType Leaf) -or
        -not (Test-Path -Path $moduleManifestPath -PathType Leaf)) {
        continue
    }
    if ($rollupContent -notmatch [regex]::Escape("ModuleName = '$rollupModulePath'")) {
        throw "Az rollup dependency was not found for packageable extension $moduleName`: $rollupModulePath"
    }
    if (-not (Test-Path -Path $moduleChangeLogPath -PathType Leaf)) {
        throw "PowerShell changelog was not found for rollup extension $moduleName`: $moduleChangeLogPath"
    }
    if ($isFirstRelease) {
        continue
    }

    $baseProjectContent = Get-FileAtRef -Root $RepoRoot -Ref $resolvedBaseRef -RelativePath $projectRelativePath
    $isNewExtension = $null -eq $baseProjectContent
    if ($null -eq $baseProjectContent) {
        $baseProjectContent = Get-Content -Path $projectPath -Raw
    }

    $baseVersion = Get-ProjectVersion -Content $baseProjectContent
    if ([string]::IsNullOrWhiteSpace($baseVersion)) {
        throw "Version was not found in $projectRelativePath"
    }
    $targetVersion = if ($isNewExtension) {
        $baseVersion
    }
    else {
        Get-NextPatchVersion -Version $baseVersion
    }

    Set-LineVersion -Path $projectPath `
        -Pattern '(<Version>)\s*[0-9]+\.[0-9]+\.[0-9]+\s*(</Version>)' `
        -Replacement "`${1}$targetVersion`${2}" `
        -Description 'Version'
    Set-LineVersion -Path $moduleManifestPath `
        -Pattern "(?<prefix>^\s*ModuleVersion\s*=\s*)'[^']+'(?<suffix>.*)$" `
        -Replacement "`${prefix}'$targetVersion'`${suffix}" `
        -Description 'ModuleVersion'
    Set-LineVersion -Path $rollupManifestPath `
        -Pattern "(?<prefix>ModuleName\s*=\s*'$([regex]::Escape($rollupModulePath))';\s*ModuleVersion\s*=\s*)'[^']+'(?<suffix>.*)" `
        -Replacement "`${prefix}'$targetVersion'`${suffix}" `
        -Description "Az.$moduleName rollup dependency"

    $versionSummary = if ($isNewExtension) { "$targetVersion (new)" } else { "$baseVersion -> $targetVersion" }
    $updatedModules.Add("${moduleName}: $versionSummary")
}

if ($isFirstRelease) {
    Write-Host 'Validated extension packaging metadata without changing versions for the first stable release.'
}
elseif ($updatedModules.Count -eq 0) {
    Write-Host "No existing rollup extension code changed since $resolvedBaseRef."
}
else {
    Write-Host "Updated extension package and PowerShell module versions since ${resolvedBaseRef}:"
    $updatedModules | ForEach-Object { Write-Host "- $_" }
}