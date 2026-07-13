param(
    [string] $RepoRoot,
    [Parameter(Mandatory = $true)]
    [string] $Version,
    [Parameter(Mandatory = $true)]
    [string] $ReleaseTimestamp,
    [switch] $AllowEmpty,
    [switch] $DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
}

$RepoRoot = [System.IO.Path]::GetFullPath($RepoRoot)
$moduleRoot = Join-Path $RepoRoot 'src/PS'
$rootChangeLogPath = Join-Path $RepoRoot 'CHANGELOG.md'
$rollupManifestPath = Join-Path $moduleRoot 'Az/Az.psd1'

function Write-Utf8NoBomFile {
    param(
        [string] $Path,
        [string[]] $Lines
    )

    $content = ($Lines -join "`n")
    if (-not $content.EndsWith("`n")) {
        $content += "`n"
    }

    if ($DryRun) {
        Write-Host "Dry run: would update $Path"
        return
    }

    [System.IO.File]::WriteAllText($Path, $content, [System.Text.UTF8Encoding]::new($false))
}

function Get-UpcomingReleaseLines {
    param([string[]] $Lines)

    $headerIndex = [Array]::IndexOf($Lines, '## Upcoming Release')
    if ($headerIndex -lt 0) {
        throw 'Missing ## Upcoming Release header.'
    }

    $releaseLines = New-Object System.Collections.Generic.List[string]
    for ($index = $headerIndex + 1; $index -lt $Lines.Count; $index++) {
        if ($Lines[$index] -like '## Version *') {
            break
        }

        $releaseLines.Add($Lines[$index])
    }

    return @($releaseLines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Move-UpcomingReleaseToVersion {
    param(
        [string] $ChangeLogPath,
        [string] $ModuleVersion
    )

    $lines = @(Get-Content -Path $ChangeLogPath)
    $headerIndex = [Array]::IndexOf($lines, '## Upcoming Release')
    if ($headerIndex -lt 0) {
        throw "Missing ## Upcoming Release header in $ChangeLogPath"
    }

    $nextVersionIndex = $lines.Count
    for ($index = $headerIndex + 1; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -like '## Version *') {
            $nextVersionIndex = $index
            break
        }
    }

    $upcomingLines = New-Object System.Collections.Generic.List[string]
    for ($index = $headerIndex + 1; $index -lt $nextVersionIndex; $index++) {
        $upcomingLines.Add($lines[$index])
    }
    while ($upcomingLines.Count -gt 0 -and [string]::IsNullOrWhiteSpace($upcomingLines[0])) {
        $upcomingLines.RemoveAt(0)
    }
    while ($upcomingLines.Count -gt 0 -and [string]::IsNullOrWhiteSpace($upcomingLines[$upcomingLines.Count - 1])) {
        $upcomingLines.RemoveAt($upcomingLines.Count - 1)
    }

    $newLines = New-Object System.Collections.Generic.List[string]
    for ($index = 0; $index -le $headerIndex; $index++) {
        $newLines.Add($lines[$index])
    }
    $newLines.Add('')
    $newLines.Add("## Version $ModuleVersion")
    foreach ($line in $upcomingLines) {
        $newLines.Add($line)
    }
    $newLines.Add('')

    for ($index = $nextVersionIndex; $index -lt $lines.Count; $index++) {
        $newLines.Add($lines[$index])
    }

    Write-Utf8NoBomFile -Path $ChangeLogPath -Lines $newLines
}

function Get-ManifestReleaseNotesBlock {
    param(
        [string] $Indent,
        [string[]] $ReleaseNotes
    )

    $manifestNotes = @($ReleaseNotes | ForEach-Object { $_.Replace('`', '''').Replace('"', '''') })
    return @(
        "${Indent}ReleaseNotes = @'"
        $manifestNotes
        "'@"
    )
}

function Set-ManifestReleaseNotes {
    param(
        [string] $ManifestPath,
        [string[]] $ReleaseNotes
    )

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.AddRange([string[]](Get-Content -Path $ManifestPath))
    $releaseNotesLineIndex = -1
    $releaseNotesIndent = '        '

    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -match '^(?<indent>\s*)#?\s*ReleaseNotes\s*=') {
            $releaseNotesLineIndex = $index
            $releaseNotesIndent = $Matches.indent
            break
        }
    }

    $block = Get-ManifestReleaseNotesBlock -Indent $releaseNotesIndent -ReleaseNotes $ReleaseNotes

    if ($releaseNotesLineIndex -ge 0) {
        $removeCount = 1
        if ($lines[$releaseNotesLineIndex] -match "=\s*@'") {
            for ($index = $releaseNotesLineIndex + 1; $index -lt $lines.Count; $index++) {
                $removeCount++
                if ($lines[$index].Trim() -eq "'@") {
                    break
                }
            }
        }

        $lines.RemoveRange($releaseNotesLineIndex, $removeCount)
        $lines.InsertRange($releaseNotesLineIndex, [string[]]$block)
        Write-Utf8NoBomFile -Path $ManifestPath -Lines $lines
        return
    }

    $insertIndex = $lines.Count - 1
    while ($insertIndex -ge 0 -and [string]::IsNullOrWhiteSpace($lines[$insertIndex])) {
        $insertIndex--
    }
    if ($insertIndex -lt 0 -or $lines[$insertIndex] -ne '}') {
        throw "Could not find closing manifest hashtable in $ManifestPath"
    }

    $releaseNotesBlock = Get-ManifestReleaseNotesBlock -Indent '        ' -ReleaseNotes $ReleaseNotes
    $privateDataBlock = @(
        'PrivateData = @{'
        '    PSData = @{'
    ) + $releaseNotesBlock + @(
        '    }'
        '}'
        ''
    )

    $lines.InsertRange($insertIndex, [string[]]$privateDataBlock)
    Write-Utf8NoBomFile -Path $ManifestPath -Lines $lines
}

function Set-ManifestModuleVersion {
    param(
        [string] $ManifestPath,
        [string] $ModuleVersion
    )

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.AddRange([string[]](Get-Content -Path $ManifestPath))
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -match "^(?<prefix>\s*ModuleVersion\s*=\s*)'[^']+'(?<suffix>.*)$") {
            $lines[$index] = "$($Matches.prefix)'$ModuleVersion'$($Matches.suffix)"
            Write-Utf8NoBomFile -Path $ManifestPath -Lines $lines
            return
        }
    }

    throw "ModuleVersion was not found in $ManifestPath"
}

function Set-RollupRequiredModuleVersions {
    param(
        [string] $ManifestPath,
        [hashtable] $ModuleVersions
    )

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.AddRange([string[]](Get-Content -Path $ManifestPath))
    for ($index = 0; $index -lt $lines.Count; $index++) {
        foreach ($moduleName in $ModuleVersions.Keys) {
            $escapedModuleName = [regex]::Escape("../$moduleName/$moduleName.psd1")
            if ($lines[$index] -match "(?<prefix>ModuleName\s*=\s*'$escapedModuleName';\s*ModuleVersion\s*=\s*)'[^']+'(?<suffix>.*)") {
                $lines[$index] = $lines[$index] -replace "ModuleVersion\s*=\s*'[^']+'", "ModuleVersion = '$($ModuleVersions[$moduleName])'"
                break
            }
        }
    }

    Write-Utf8NoBomFile -Path $ManifestPath -Lines $lines
}

if (-not (Test-Path -Path $moduleRoot -PathType Container)) {
    throw "PowerShell module root was not found: $moduleRoot"
}
if (-not (Test-Path -Path $rootChangeLogPath -PathType Leaf)) {
    throw "Root changelog was not found: $rootChangeLogPath"
}

$moduleReleases = New-Object System.Collections.Generic.List[object]
$moduleChangeLogs = @(Get-ChildItem -Path $moduleRoot -Filter 'ChangeLog.md' -Recurse -File | Sort-Object FullName)
foreach ($changeLog in $moduleChangeLogs) {
    $moduleDirectory = $changeLog.Directory
    $moduleName = $moduleDirectory.Name
    $manifestPath = Join-Path $moduleDirectory.FullName "$moduleName.psd1"
    if (-not (Test-Path -Path $manifestPath -PathType Leaf)) {
        throw "Module manifest was not found for $($changeLog.FullName): $manifestPath"
    }

    $changeLogLines = @(Get-Content -Path $changeLog.FullName)
    $upcomingReleaseLines = @(Get-UpcomingReleaseLines -Lines $changeLogLines)
    if ($upcomingReleaseLines.Count -eq 0) {
        continue
    }

    $manifest = Import-PowerShellDataFile -Path $manifestPath
    $currentModuleVersion = [string]$manifest.ModuleVersion
    if ([string]::IsNullOrWhiteSpace($currentModuleVersion)) {
        throw "ModuleVersion was not found in $manifestPath"
    }

    $moduleReleases.Add([pscustomobject]@{
            ModuleName = $moduleName
            ModuleVersion = $Version
            ChangeLogPath = $changeLog.FullName
            ManifestPath = $manifestPath
            ReleaseNotes = $upcomingReleaseLines
        })
}

if ($moduleReleases.Count -eq 0 -and -not $AllowEmpty) {
    throw 'No module changelog entries were found under ## Upcoming Release. Add release notes or pass -AllowEmpty.'
}

foreach ($moduleRelease in $moduleReleases) {
    Move-UpcomingReleaseToVersion -ChangeLogPath $moduleRelease.ChangeLogPath -ModuleVersion $moduleRelease.ModuleVersion
    Set-ManifestModuleVersion -ManifestPath $moduleRelease.ManifestPath -ModuleVersion $moduleRelease.ModuleVersion
    Set-ManifestReleaseNotes -ManifestPath $moduleRelease.ManifestPath -ReleaseNotes $moduleRelease.ReleaseNotes
}

$rootSection = New-Object System.Collections.Generic.List[string]
$rootSection.Add("## v$Version - $ReleaseTimestamp")
$rootSection.Add('')

foreach ($moduleRelease in ($moduleReleases | Sort-Object ModuleName)) {
    $rootSection.Add("#### Az.$($moduleRelease.ModuleName) $($moduleRelease.ModuleVersion)")
    foreach ($line in $moduleRelease.ReleaseNotes) {
        $rootSection.Add($line)
    }
    $rootSection.Add('')
}

if ($moduleReleases.Count -eq 0) {
    $rootSection.Add('- No user-facing PowerShell module changes were listed for this release.')
    $rootSection.Add('')
}

$existingRootChangeLog = @(Get-Content -Path $rootChangeLogPath)
$upcomingRootHeaderIndex = [Array]::IndexOf($existingRootChangeLog, '## Upcoming Release')
if ($upcomingRootHeaderIndex -ge 0) {
    $insertIndex = $upcomingRootHeaderIndex + 1
    while ($insertIndex -lt $existingRootChangeLog.Count -and [string]::IsNullOrWhiteSpace($existingRootChangeLog[$insertIndex])) {
        $insertIndex++
    }

    $updatedRootChangeLog = New-Object System.Collections.Generic.List[string]
    $updatedRootChangeLog.AddRange([string[]]$existingRootChangeLog[0..$upcomingRootHeaderIndex])
    $updatedRootChangeLog.Add('')
    $updatedRootChangeLog.AddRange([string[]]$rootSection)
    if ($insertIndex -lt $existingRootChangeLog.Count) {
        $updatedRootChangeLog.AddRange([string[]]$existingRootChangeLog[$insertIndex..($existingRootChangeLog.Count - 1)])
    }
    Write-Utf8NoBomFile -Path $rootChangeLogPath -Lines ([string[]]$updatedRootChangeLog)
}
else {
    Write-Utf8NoBomFile -Path $rootChangeLogPath -Lines ([string[]]($rootSection + $existingRootChangeLog))
}

if (Test-Path -Path $rollupManifestPath -PathType Leaf) {
    Set-ManifestModuleVersion -ManifestPath $rollupManifestPath -ModuleVersion $Version

    if ($moduleReleases.Count -gt 0) {
        $rollupReleaseNotes = New-Object System.Collections.Generic.List[string]
        $rollupReleaseNotes.Add("$Version - $ReleaseTimestamp")
        foreach ($moduleRelease in ($moduleReleases | Sort-Object ModuleName)) {
            $rollupReleaseNotes.Add("Az.$($moduleRelease.ModuleName) $($moduleRelease.ModuleVersion)")
            foreach ($line in $moduleRelease.ReleaseNotes) {
                $rollupReleaseNotes.Add($line)
            }
            $rollupReleaseNotes.Add('')
        }

        $requiredModuleVersions = @{}
        foreach ($moduleRelease in $moduleReleases) {
            $requiredModuleVersions[$moduleRelease.ModuleName] = $moduleRelease.ModuleVersion
        }

        Set-RollupRequiredModuleVersions -ManifestPath $rollupManifestPath -ModuleVersions $requiredModuleVersions
        Set-ManifestReleaseNotes -ManifestPath $rollupManifestPath -ReleaseNotes $rollupReleaseNotes
    }
}

Write-Host "Aggregated release notes for $($moduleReleases.Count) module(s)."
