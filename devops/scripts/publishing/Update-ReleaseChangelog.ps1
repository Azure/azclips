param(
    [string] $RepoRoot,
    [Parameter(Mandatory = $true)]
    [string] $Version,
    [Parameter(Mandatory = $true)]
    [string] $ReleaseTimestamp,
    [string] $GitHubRepository,
    [switch] $AllowEmpty,
    [switch] $DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

. ([System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../build/Get-AzPackageName.ps1')))

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

function Get-LatestStableTag {
    param([string] $Root)

    $tagPattern = '^v[0-9]+\.[0-9]+\.[0-9]+\.[0-9]{8}T[0-9]{6}Z$'
    $tagOutput = @(& git --no-pager -C $Root tag --merged HEAD --sort=-version:refname --list 'v*' 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "git tag failed while resolving the latest stable tag. $($tagOutput -join "`n")"
    }

    $tags = @($tagOutput | Where-Object { $_ -match $tagPattern })

    if ($tags.Count -eq 0) {
        return $null
    }

    return $tags[0]
}

function Test-GitHubRepositoryName {
    param([string] $Name)

    -not [string]::IsNullOrWhiteSpace($Name) -and $Name.Trim() -match '^[^/\s]+/[^/\s]+$'
}

function Get-GitHubRepositoryName {
    param([string] $ConfiguredRepository)

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredRepository)) {
        $configured = $ConfiguredRepository.Trim()
        if (Test-GitHubRepositoryName -Name $configured) {
            return $configured
        }

        throw "GitHub repository must be in owner/repo form. Received: $configured"
    }

    foreach ($candidate in @(
            @{ Name = 'BUILD_REPOSITORY_NAME'; Value = $env:BUILD_REPOSITORY_NAME },
            @{ Name = 'GITHUB_REPOSITORY'; Value = $env:GITHUB_REPOSITORY }
        )) {
        if ([string]::IsNullOrWhiteSpace($candidate.Value)) {
            continue
        }

        $repository = $candidate.Value.Trim()
        if (Test-GitHubRepositoryName -Name $repository) {
            return $repository
        }

        Write-Warning "Ignoring $($candidate.Name) because it is not in owner/repo form: $repository"
    }

    return 'Azure/azclips'
}

function Invoke-GitHubApi {
    param([string] $Url)

    $headers = @{
        Accept = 'application/vnd.github+json'
        'User-Agent' = 'azclips-release-changelog'
        'X-GitHub-Api-Version' = '2022-11-28'
    }

    $token = if (-not [string]::IsNullOrWhiteSpace($env:GH_TOKEN)) { $env:GH_TOKEN } else { $env:GITHUB_TOKEN }
    if (-not [string]::IsNullOrWhiteSpace($token) -and -not $token.StartsWith('$(')) {
        $headers.Authorization = "Bearer $token"
    }

    Invoke-RestMethod -Method Get -Uri $Url -Headers $headers
}

function Get-PullRequestMetadata {
    param(
        [string] $Repository,
        [string] $Number
    )

    $url = "https://api.github.com/repos/$Repository/pulls/$Number"
    try {
        Invoke-GitHubApi -Url $url
    }
    catch {
        Write-Warning "Could not fetch pull request #$Number from $Repository. Falling back to the squash commit title. $($_.Exception.Message)"
        $null
    }
}

function Get-HistoryNoteLines {
    param([string] $Body)

    if ([string]::IsNullOrWhiteSpace($Body)) {
        return @()
    }

    $match = [regex]::Match($Body, '(?s)\*\*History Notes\*\*(?<content>.*?)(?:\r?\n---|\z)')
    if (-not $match.Success) {
        return @()
    }

    @($match.Groups['content'].Value -split "\r?\n" | Where-Object { $_.StartsWith('[') })
}

function ConvertTo-ReleaseNote {
    param(
        [string] $Message,
        [string] $PullRequestNumber
    )

    if ([string]::IsNullOrWhiteSpace($Message)) {
        return $null
    }
    if ($Message -match '^\[Component Name') {
        return $null
    }
    if ($Message -match '^\[[^\]]+\]\(.+\)\s*$') {
        return $null
    }
    if ($Message -notmatch '^\[(?<component>[^\]]+)\]\s+(?<note>\S.*)$') {
        return $null
    }

    $component = $Matches.component.Trim()
    $note = $Matches.note.Trim()
    if ([string]::IsNullOrWhiteSpace($component) -or [string]::IsNullOrWhiteSpace($note)) {
        return $null
    }
    if ($note -match '^\[[^\]]+\]\(.+\)\s*$') {
        return $null
    }

    $note = $note.TrimEnd('.', ',', ' ')

    if ($note -notmatch "\s*\(#$([regex]::Escape($PullRequestNumber))\)$") {
        $note = "$note (#$PullRequestNumber)"
    }

    $note = [regex]::Replace($note, 'BREAKING CHANGE:', '[BREAKING CHANGE]', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($note -match '^(?<command>az [^:]+):(?<rest>.*)$') {
        $command = $Matches.command
        $rest = $Matches.rest
        $note = "``${command}``:$rest"
    }
    if ($note -notmatch ':' -and -not $note.StartsWith('az') -and $note.Length -gt 0) {
        $note = $note.Substring(0, 1).ToUpperInvariant() + $note.Substring(1)
    }

    [pscustomobject]@{
        Component = $component
        Note = $note
    }
}

function Get-PullRequestReleaseNotes {
    param(
        [string] $Title,
        [string] $Body,
        [string] $PullRequestNumber
    )

    $notes = New-Object System.Collections.Generic.List[object]
    foreach ($line in @(Get-HistoryNoteLines -Body $Body)) {
        $note = ConvertTo-ReleaseNote -Message $line -PullRequestNumber $PullRequestNumber
        if ($null -ne $note) {
            $notes.Add($note)
        }
    }

    if ($notes.Count -eq 0) {
        $note = ConvertTo-ReleaseNote -Message $Title -PullRequestNumber $PullRequestNumber
        if ($null -ne $note) {
            $notes.Add($note)
        }
    }

    $notes
}

function Get-MergedPullRequestReleaseNoteLines {
    param(
        [string] $Root,
        [string] $Repository
    )

    $latestStableTag = Get-LatestStableTag -Root $Root
    $range = if ([string]::IsNullOrWhiteSpace($latestStableTag)) { 'HEAD' } else { "$latestStableTag..HEAD" }
    $subjects = @(& git --no-pager -C $Root log --first-parent --pretty=format:%s $range 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "git log failed while collecting merged pull requests for changelog generation. $($subjects -join "`n")"
    }

    $notesByComponent = [ordered]@{}
    foreach ($subject in $subjects) {
        if ($subject -match '\(#(?<number>[0-9]+)\)\s*$') {
            $number = $Matches.number
            $fallbackTitle = ($subject -replace '\s*\(#[0-9]+\)\s*$', '').Trim()
        }
        elseif ($subject -match '^Merge pull request #(?<number>[0-9]+)\s+from\s+.+$') {
            $number = $Matches.number
            $fallbackTitle = ''
        }
        else {
            continue
        }

        $pullRequest = Get-PullRequestMetadata -Repository $Repository -Number $number
        $title = if ($null -ne $pullRequest -and -not [string]::IsNullOrWhiteSpace($pullRequest.title)) { [string]$pullRequest.title } else { $fallbackTitle }
        $body = if ($null -ne $pullRequest) { [string]$pullRequest.body } else { '' }

        foreach ($releaseNote in @(Get-PullRequestReleaseNotes -Title $title -Body $body -PullRequestNumber $number)) {
            if (-not $notesByComponent.Contains($releaseNote.Component)) {
                $notesByComponent[$releaseNote.Component] = New-Object System.Collections.Generic.List[string]
            }

            $notesByComponent[$releaseNote.Component].Add("- $($releaseNote.Note)")
        }
    }

    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($component in @($notesByComponent.Keys | Sort-Object)) {
        $lines.Add("#### $component")
        foreach ($note in $notesByComponent[$component]) {
            $lines.Add($note)
        }
        $lines.Add('')
    }

    $lines
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

$releaseNoteLines = @(Get-MergedPullRequestReleaseNoteLines -Root $RepoRoot -Repository (Get-GitHubRepositoryName -ConfiguredRepository $GitHubRepository))
if ($releaseNoteLines.Count -gt 0) {
    foreach ($line in $releaseNoteLines) {
        $rootSection.Add($line)
    }
}

foreach ($moduleRelease in ($moduleReleases | Sort-Object ModuleName)) {
    $rootSection.Add("#### $(Get-AzPackageName -SourceModule $moduleRelease.ModuleName) $($moduleRelease.ModuleVersion)")
    foreach ($line in $moduleRelease.ReleaseNotes) {
        $rootSection.Add($line)
    }
    $rootSection.Add('')
}

if ($releaseNoteLines.Count -eq 0 -and $moduleReleases.Count -eq 0) {
    $rootSection.Add('- No user-facing CLI or PowerShell module changes were listed for this release.')
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
            $rollupReleaseNotes.Add("$(Get-AzPackageName -SourceModule $moduleRelease.ModuleName) $($moduleRelease.ModuleVersion)")
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
