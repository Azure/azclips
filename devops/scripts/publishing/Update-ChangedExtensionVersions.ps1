param(
    [string] $RepoRoot,
    [Parameter(Mandatory = $true)]
    [string] $Version,
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

function Set-LineVersion {
    param(
        [string] $Path,
        [string] $Pattern,
        [string] $Replacement,
        [string] $Description
    )

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
                [System.IO.File]::WriteAllText($Path, $content, [System.Text.UTF8Encoding]::new($false))
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
if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
    throw "Target extension version must be stable SemVer x.y.z. Received: $Version"
}

$resolvedBaseRef = if ([string]::IsNullOrWhiteSpace($BaseRef)) {
    Get-LatestStableTag -Root $RepoRoot
}
else {
    $BaseRef.Trim()
}

if ([string]::IsNullOrWhiteSpace($resolvedBaseRef)) {
    Write-Host 'No prior stable tag was found. Skipping extension version updates for the first stable release.'
    return
}

$changedPaths = @(& git --no-pager -C $RepoRoot diff --name-only "$resolvedBaseRef..HEAD" -- 'src/Extensions' 2>&1)
if ($LASTEXITCODE -ne 0) {
    throw "git diff failed while detecting extension changes since $resolvedBaseRef. $($changedPaths -join "`n")"
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
    $rollupModulePath = "../$moduleName/$moduleName.psd1"

    if (-not (Test-Path -Path $projectPath -PathType Leaf) -or
        -not (Test-Path -Path $moduleManifestPath -PathType Leaf) -or
        $rollupContent -notmatch [regex]::Escape("ModuleName = '$rollupModulePath'")) {
        continue
    }

    $baseProjectContent = Get-FileAtRef -Root $RepoRoot -Ref $resolvedBaseRef -RelativePath $projectRelativePath
    if ($null -eq $baseProjectContent) {
        $baseProjectContent = Get-Content -Path $projectPath -Raw
    }

    $baseVersion = Get-ProjectVersion -Content $baseProjectContent
    if ([string]::IsNullOrWhiteSpace($baseVersion)) {
        throw "Version was not found in $projectRelativePath"
    }

    Set-LineVersion -Path $projectPath `
        -Pattern '(<Version>)\s*[0-9]+\.[0-9]+\.[0-9]+\s*(</Version>)' `
        -Replacement "`${1}$Version`${2}" `
        -Description 'Version'
    Set-LineVersion -Path $moduleManifestPath `
        -Pattern "(?<prefix>^\s*ModuleVersion\s*=\s*)'[^']+'(?<suffix>.*)$" `
        -Replacement "`${prefix}'$Version'`${suffix}" `
        -Description 'ModuleVersion'
    Set-LineVersion -Path $rollupManifestPath `
        -Pattern "(?<prefix>ModuleName\s*=\s*'$([regex]::Escape($rollupModulePath))';\s*ModuleVersion\s*=\s*)'[^']+'(?<suffix>.*)" `
        -Replacement "`${prefix}'$Version'`${suffix}" `
        -Description "Az.$moduleName rollup dependency"

    $updatedModules.Add("${moduleName}: $baseVersion -> $Version")
}

if ($updatedModules.Count -eq 0) {
    Write-Host "No existing rollup extension code changed since $resolvedBaseRef."
}
else {
    Write-Host "Updated extension package and PowerShell module versions since ${resolvedBaseRef}:"
    $updatedModules | ForEach-Object { Write-Host "- $_" }
}