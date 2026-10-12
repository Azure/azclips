<#
.SYNOPSIS
Offline release preparation fixtures using real local tar/gzip and NuGet archives.
.DESCRIPTION
Mocks the native GitHub/git boundary. Never downloads a release, executes its
installer, authenticates persistently, or installs Az modules.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
. (Join-Path $repoRoot 'devops/scripts/packaging/powershell/shared/upgrade_release.ps1')
$nativeProcess = ${function:Invoke-PSRuntimeProcess}
$scratch = Join-Path $repoRoot ".ps-upgrade-release-fixtures-$([guid]::NewGuid().ToString('N'))"
$null = New-Item -ItemType Directory -Path $scratch
$originalTemp = $env:AGENT_TEMPDIRECTORY
$originalToken = $env:GH_TOKEN
$env:AGENT_TEMPDIRECTORY = $scratch
$count = 0

function Assert-ReleaseFixture
{
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "Release fixture failed: $Message" }
}

function Assert-ReleaseFixtureThrows
{
    param([scriptblock]$Action, [string]$Pattern)
    $caught = $null
    try { & $Action | Out-Null }
    catch { $caught = $_ }
    Assert-ReleaseFixture ($null -ne $caught) "Expected failure matching '$Pattern'."
    Assert-ReleaseFixture ($caught.Exception.Message -match $Pattern) "Failure did not match '$Pattern'."
    if ($script:fixtureSecret)
    {
        Assert-ReleaseFixture ($caught.ToString() -notmatch [regex]::Escape($script:fixtureSecret)) 'Diagnostics must not contain the token.'
        Assert-ReleaseFixture ($caught.ToString() -notmatch [regex]::Escape($script:fixtureBasic)) 'Diagnostics must not contain the Basic authorization value.'
    }
}

function New-ReleaseFixture
{
    param(
        [string]$Version = '0.1.3',
        [string]$Tag,
        [bool]$Draft = $false,
        [bool]$Prerelease = $false,
        [string]$Digest = ('sha256:' + ('a' * 64)),
        [long]$Size = 100,
        [string]$PublishedAt = '2026-09-01T00:00:00Z'
    )
    if (-not $Tag) { $Tag = "fixture-$Version" }
    [pscustomobject]@{
        tag_name = $Tag; draft = $Draft; prerelease = $Prerelease; published_at = $PublishedAt
        assets = @([pscustomobject]@{ name = "azps-cmdlets-$Version.tar.gz"; digest = $Digest; size = $Size })
    }
}

function New-ReleaseFixturePackage
{
    param([string]$Path, [string]$Name, [string]$Version, [string]$Dependencies = '')
    $archive = [IO.Compression.ZipFile]::Open($Path, [IO.Compression.ZipArchiveMode]::Create)
    try
    {
        $writer = [IO.StreamWriter]::new($archive.CreateEntry("$Name.nuspec").Open())
        try
        {
            $writer.Write("<package><metadata><id>$Name</id><version>$Version</version><dependencies>$Dependencies</dependencies></metadata></package>")
        }
        finally { $writer.Dispose() }
        $writer = [IO.StreamWriter]::new($archive.CreateEntry("$Name.psd1").Open())
        try { $writer.Write("@{ ModuleVersion = '$Version' }") }
        finally { $writer.Dispose() }
    }
    finally { $archive.Dispose() }
}

function New-ReleaseFixtureArchive
{
    param([string]$Path, [object[]]$Entries)
    $file = [IO.File]::Open($Path, [IO.FileMode]::CreateNew)
    try
    {
        $gzip = [IO.Compression.GZipStream]::new($file, [IO.Compression.CompressionMode]::Compress, $true)
        try
        {
            $writer = [System.Formats.Tar.TarWriter]::new($gzip, [System.Formats.Tar.TarEntryFormat]::Ustar, $true)
            try
            {
                foreach ($definition in $Entries)
                {
                    $type = if ($definition.Type) { [System.Formats.Tar.TarEntryType]$definition.Type } else { [System.Formats.Tar.TarEntryType]::RegularFile }
                    $entry = [System.Formats.Tar.UstarTarEntry]::new($type, $definition.Name)
                    $data = $null
                    try
                    {
                        if ($type -in [System.Formats.Tar.TarEntryType]::SymbolicLink, [System.Formats.Tar.TarEntryType]::HardLink)
                        {
                            $entry.LinkName = '../outside'
                        }
                        elseif ($type -eq [System.Formats.Tar.TarEntryType]::RegularFile)
                        {
                            $data = if ($definition.Path) { [IO.File]::OpenRead($definition.Path) }
                                else { [IO.MemoryStream]::new([Text.Encoding]::UTF8.GetBytes([string]$definition.Text)) }
                            $entry.DataStream = $data
                        }
                        $writer.WriteEntry($entry)
                    }
                    finally { if ($data) { $data.Dispose() } }
                }
            }
            finally { $writer.Dispose() }
        }
        finally { $gzip.Dispose() }
    }
    finally { $file.Dispose() }
}

function New-ReleaseFixtureResult
{
    param([string]$StdOut = '', [int]$ExitCode = 0, [bool]$TimedOut = $false, [bool]$Truncated = $false)
    [pscustomobject]@{
        StdOut = $StdOut; StdErr = $script:fixtureSecret; Output = "$StdOut`n$script:fixtureSecret"
        ExitCode = $ExitCode; TimedOut = $TimedOut; Truncated = $Truncated
    }
}

try
{
    $latest = New-ReleaseFixture -Tag 'unrelated-tag-20260901T010203Z'
    $selection = Get-PSUpgradeReleaseSelection -LatestRelease $latest -TargetVersion '0.1.4' -GetReleasePage { throw 'History must not be queried.' }
    Assert-ReleaseFixture ($selection.Version -eq '0.1.3' -and $selection.SelectionReason -eq 'latest') 'Latest comes from the PS asset filename, not the timestamped tag.'
    Assert-ReleaseFixture ($selection.TagName -eq $latest.tag_name -and $selection.LatestVersion -eq '0.1.3') 'The latest endpoint remains authoritative.'
    Assert-ReleaseFixtureThrows { Get-PSUpgradeReleaseSelection -LatestRelease $latest -TargetVersion '0.1.2' } 'downgrade'
    Assert-ReleaseFixtureThrows { Get-PSUpgradeReleaseSelection -LatestRelease $latest -TargetVersion '0.1.4-preview' } 'numeric'
    Assert-ReleaseFixtureThrows { Get-PSUpgradeReleaseSelection -LatestRelease (New-ReleaseFixture -Prerelease $true) -TargetVersion '0.1.4' } 'no stable'
    $missingAsset = New-ReleaseFixture
    $missingAsset.assets = @()
    Assert-ReleaseFixtureThrows { Get-PSUpgradeReleaseSelection -LatestRelease $missingAsset -TargetVersion '0.1.4' } 'no stable'
    foreach ($digest in '', 'sha512:abcd', ('sha256:' + ('a' * 63)))
    {
        Assert-ReleaseFixtureThrows { Get-PSUpgradeReleaseSelection -LatestRelease (New-ReleaseFixture -Digest $digest) -TargetVersion '0.1.4' } 'SHA256 digest'
    }
    Write-Host 'PASS: authoritative latest, asset-derived numeric version, invalid digest and downgrade rejection'
    $count++

    $script:fixturePages = @{
        1 = @(
            New-ReleaseFixture -Version '0.2.0'
            New-ReleaseFixture -Version '0.1.9'
            New-ReleaseFixture -Version '0.1.19' -Draft $true
            New-ReleaseFixture -Version '0.1.18' -Prerelease $true
            for ($index = 0; $index -lt 96; $index++) { New-ReleaseFixture -Version '0.1.1' -Tag "history-$index" }
        )
        2 = @(
            New-ReleaseFixture -Version '0.1.10'
            New-ReleaseFixture -Version '0.1.10' -Tag 'newer-same-version' -PublishedAt '2026-09-02T00:00:00Z'
        )
    }
    $script:pagesRead = [Collections.Generic.List[int]]::new()
    $getPage = {
        param($Page, $PerPage)
        Assert-ReleaseFixture ($PerPage -eq 100) 'Release pagination requests the full page size.'
        $script:pagesRead.Add($Page)
        $script:fixturePages[$Page]
    }
    $selection = Get-PSUpgradeReleaseSelection -LatestRelease (New-ReleaseFixture -Version '0.2.0') -TargetVersion '0.2.0' -GetReleasePage $getPage
    Assert-ReleaseFixture ($selection.Version -eq '0.1.10' -and $selection.TagName -eq 'newer-same-version') 'Highest numeric previous version wins, using publication date for version ties.'
    Assert-ReleaseFixture ($selection.SelectionReason -eq 'previous-version' -and $selection.LatestVersion -eq '0.2.0') 'Equal versions never become same-version reinstalls.'
    Assert-ReleaseFixture (($script:pagesRead -join ',') -eq '1,2') 'Older candidates beyond the first page are considered.'
    $selection = Get-PSUpgradeReleaseSelection -LatestRelease $latest -TargetVersion '0.1.3.0' -GetReleasePage {
        param($Page, $PerPage)
        New-ReleaseFixture -Version '0.1.2'
    }
    Assert-ReleaseFixture ($selection.Version -eq '0.1.2') 'Equivalent three/four-component versions require an older baseline.'
    Assert-ReleaseFixtureThrows {
        Get-PSUpgradeReleaseSelection -LatestRelease $latest -TargetVersion '0.1.3' -GetReleasePage {
            param($Page, $PerPage)
            New-ReleaseFixture
            New-ReleaseFixture -Version '0.1.2' -Draft $true
            New-ReleaseFixture -Version '0.1.1' -Prerelease $true
        }
    } 'No previous distinct'
    Assert-ReleaseFixtureThrows {
        Get-PSUpgradeReleaseSelection -LatestRelease $latest -TargetVersion '0.1.3' -GetReleasePage { param($Page, $PerPage) }
    } 'No previous distinct'
    Assert-ReleaseFixtureThrows {
        Get-PSUpgradeReleaseSelection -LatestRelease $latest -TargetVersion '0.1.3' -GetReleasePage {
            param($Page, $PerPage)
            New-ReleaseFixture -Version '0.1.2' -Digest ''
            New-ReleaseFixture -Version '0.1.1'
        }
    } 'SHA256 digest'
    $script:fixturePages[2] = $script:fixturePages[1]
    Assert-ReleaseFixtureThrows {
        Get-PSUpgradeReleaseSelection -LatestRelease (New-ReleaseFixture -Version '0.2.0') -TargetVersion '0.2.0' -GetReleasePage $getPage
    } 'repeats releases'
    Write-Host 'PASS: full pagination, distinct stable fallback, numeric order, drafts/prereleases, no older release and inconsistent history'
    $count++

    $rollup = Join-Path $scratch 'Az.0.1.2.nupkg'
    $dependency = Join-Path $scratch 'Az.Fixture.0.1.2.nupkg'
    New-ReleaseFixturePackage -Path $rollup -Name Az -Version '0.1.2' -Dependencies '<dependency id="Az.Fixture" version="0.1.2" />'
    New-ReleaseFixturePackage -Path $dependency -Name Az.Fixture -Version '0.1.2'
    $entries = @(
        @{ Name = './'; Type = 'Directory' }
        @{ Name = 'Az.0.1.2.nupkg'; Path = $rollup }
        @{ Name = './Az.Fixture.0.1.2.nupkg'; Path = $dependency }
        @{ Name = 'InstallModule.ps1'; Text = "throw 'INSTALLER_MUST_NEVER_EXECUTE'" }
        @{ Name = 'LICENSE'; Text = 'fixture license' }
        @{ Name = 'NOTICE'; Text = 'fixture notice' }
    )
    $script:fixtureArchive = Join-Path $scratch 'baseline.tar.gz'
    New-ReleaseFixtureArchive -Path $script:fixtureArchive -Entries $entries
    $script:fixtureHash = (Get-FileHash -LiteralPath $script:fixtureArchive -Algorithm SHA256).Hash.ToLowerInvariant()
    Assert-PSUpgradeArchiveChecksum -Path $script:fixtureArchive -Sha256 $script:fixtureHash
    Assert-ReleaseFixtureThrows { Assert-PSUpgradeArchiveChecksum -Path $script:fixtureArchive -Sha256 ('b' * 64) } 'checksum mismatch'
    Assert-ReleaseFixtureThrows { Assert-PSUpgradeArchiveChecksum -Path $script:fixtureArchive -Sha256 'absent' } 'valid SHA256'
    $gallery = Join-Path $scratch 'valid-gallery'
    Expand-PSUpgradeArchive -ArchivePath $script:fixtureArchive -DestinationPath $gallery
    Assert-PSUpgradeGallery -GalleryPath $gallery -Version '0.1.2'
    Assert-ReleaseFixture (@(Get-ChildItem -LiteralPath $gallery -File -Force).Count -eq 5) 'Gallery is flat, complete and contains no hidden intermediate tar.'
    Assert-ReleaseFixture ((Get-FileHash -LiteralPath (Join-Path $gallery 'Az.0.1.2.nupkg')).Hash -eq (Get-FileHash $rollup).Hash) 'Extracted package bytes are unchanged.'
    Assert-ReleaseFixtureThrows { Assert-PSUpgradeGallery -GalleryPath $gallery -Version '0.1.3' } 'selected Az version'
    $originalGallery = Get-ChildItem -LiteralPath $gallery -File | Get-FileHash | Select-Object Path, Hash | ConvertTo-Json -Compress
    Assert-ReleaseFixtureThrows { Expand-PSUpgradeArchive -ArchivePath $script:fixtureArchive -DestinationPath $gallery } 'already exists'
    Assert-ReleaseFixture ($originalGallery -ceq (Get-ChildItem -LiteralPath $gallery -File | Get-FileHash | Select-Object Path, Hash | ConvertTo-Json -Compress)) 'Existing extraction destinations are untouched.'
    Write-Host 'PASS: real tar/gzip and NuGet packages, SHA256 checks, flat gallery and selected rollup identity'
    $count++

    $hiddenGallery = Join-Path $scratch 'hidden-intermediate-gallery'
    $script:hiddenCleanupCalls = 0
    function Remove-Item
    {
        [CmdletBinding()]
        param([Parameter(Mandatory)][string]$LiteralPath, [switch]$Force)

        Assert-ReleaseFixture ([IO.Path]::GetFileName($LiteralPath) -eq '.verified-bundle.tar') 'Only the owned intermediate archive is intercepted.'
        if ($IsWindows)
        {
            # Unix marks dotfiles hidden automatically; reproduce that attribute on Windows.
            [IO.File]::SetAttributes($LiteralPath, ([IO.File]::GetAttributes($LiteralPath) -bor [IO.FileAttributes]::Hidden))
        }
        Assert-ReleaseFixture (([IO.File]::GetAttributes($LiteralPath) -band [IO.FileAttributes]::Hidden) -ne 0) 'The actual cleanup command receives a hidden file.'
        $script:hiddenCleanupCalls++
        Microsoft.PowerShell.Management\Remove-Item @PSBoundParameters
    }
    try
    {
        Expand-PSUpgradeArchive -ArchivePath $script:fixtureArchive -DestinationPath $hiddenGallery
    }
    finally
    {
        Microsoft.PowerShell.Management\Remove-Item -LiteralPath Function:\Remove-Item
    }
    Assert-ReleaseFixture ($script:hiddenCleanupCalls -eq 1) 'Extraction cleans its owned intermediate archive exactly once.'
    Assert-ReleaseFixture (-not (Test-Path -LiteralPath (Join-Path $hiddenGallery '.verified-bundle.tar'))) 'Hidden intermediate archive is removed after extraction.'
    Assert-PSUpgradeGallery -GalleryPath $hiddenGallery -Version '0.1.2'
    Assert-ReleaseFixture ((Get-FileHash -LiteralPath (Join-Path $hiddenGallery 'Az.0.1.2.nupkg')).Hash -eq (Get-FileHash -LiteralPath $rollup).Hash) 'Hidden-file cleanup preserves extracted package bytes.'
    Write-Host 'PASS: real hidden-file cleanup without elevation on Windows and Unix'
    $count++

    $badEntries = @(
        @{ Name = '../escaped.nupkg'; Text = 'traversal' }
        @{ Name = '/absolute.nupkg'; Text = 'absolute' }
        @{ Name = 'C:\absolute.nupkg'; Text = 'drive' }
        @{ Name = 'Az.0.1.2.nupkg:stream'; Text = 'alternate data stream' }
        @{ Name = '.\Az.0.1.2.nupkg'; Text = 'backslash' }
        @{ Name = 'nested/Az.0.1.2.nupkg'; Text = 'nested' }
        @{ Name = 'unexpected.ps1'; Text = 'unexpected script' }
        @{ Name = 'InstallModule.ps1'; Type = 'SymbolicLink' }
        @{ Name = 'Az.0.1.2.nupkg'; Type = 'HardLink' }
        @{ Name = 'nested/'; Type = 'Directory' }
    )
    $index = 0
    foreach ($entry in $badEntries)
    {
        $archive = Join-Path $scratch "unsafe-$index.tar.gz"
        $destination = Join-Path $scratch "unsafe-$index"
        New-ReleaseFixtureArchive -Path $archive -Entries @($entry)
        Assert-ReleaseFixtureThrows { Expand-PSUpgradeArchive -ArchivePath $archive -DestinationPath $destination } 'unsafe|unsupported|non-root'
        Assert-ReleaseFixture (-not (Test-Path -LiteralPath $destination)) 'Failed extraction removes only its new destination.'
        $index++
    }
    foreach ($pair in @(
        @(@{ Name = 'LICENSE'; Text = 'one' }, @{ Name = './LICENSE'; Text = 'two' }),
        @(@{ Name = 'Az.Fixture.0.1.2.nupkg'; Text = 'one' }, @{ Name = 'Az.fixture.0.1.2.nupkg'; Text = 'two' }),
        @(@{ Name = '.'; Type = 'Directory' }, @{ Name = './'; Type = 'Directory' })
    ))
    {
        $archive = Join-Path $scratch "duplicate-$index.tar.gz"
        $destination = Join-Path $scratch "duplicate-$index"
        New-ReleaseFixtureArchive -Path $archive -Entries $pair
        Assert-ReleaseFixtureThrows { Expand-PSUpgradeArchive -ArchivePath $archive -DestinationPath $destination } 'duplicate'
        Assert-ReleaseFixture (-not (Test-Path -LiteralPath $destination)) 'Duplicate entry rejection cleans the owned destination.'
        $index++
    }
    Assert-ReleaseFixture (-not (Test-Path -LiteralPath (Join-Path $scratch 'escaped.nupkg'))) 'Traversal creates no escaped file.'
    foreach ($limits in @(@{ MaxExpandedBytes = 512 }, @{ MaxEntryBytes = 1 }, @{ MaxEntries = 1 }))
    {
        $destination = Join-Path $scratch "bounded-$index"
        Assert-ReleaseFixtureThrows { Expand-PSUpgradeArchive -ArchivePath $script:fixtureArchive -DestinationPath $destination @limits } 'limit'
        Assert-ReleaseFixture (-not (Test-Path -LiteralPath $destination)) 'Archive size/count rejection cleans the owned destination.'
        $index++
    }
    Write-Host 'PASS: traversal, absolute paths, streams, nested names, links, duplicates, case collisions and size/count bounds'
    $count++

    $brokenGallery = Join-Path $scratch 'broken-gallery'
    Expand-PSUpgradeArchive -ArchivePath $script:fixtureArchive -DestinationPath $brokenGallery
    Remove-Item -LiteralPath (Join-Path $brokenGallery 'Az.0.1.2.nupkg')
    New-ReleaseFixturePackage -Path (Join-Path $brokenGallery 'Az.0.1.2.nupkg') -Name Az -Version '0.1.1'
    Assert-ReleaseFixtureThrows { Assert-PSUpgradeGallery -GalleryPath $brokenGallery -Version '0.1.2' } 'identity'
    Remove-Item -LiteralPath (Join-Path $brokenGallery 'Az.0.1.2.nupkg')
    Copy-Item -LiteralPath $rollup -Destination $brokenGallery
    Remove-Item -LiteralPath (Join-Path $brokenGallery 'Az.Fixture.0.1.2.nupkg')
    Assert-ReleaseFixtureThrows { Assert-PSUpgradeGallery -GalleryPath $brokenGallery -Version '0.1.2' } 'dependency'
    Remove-Item -LiteralPath (Join-Path $brokenGallery 'LICENSE')
    Assert-ReleaseFixtureThrows { Assert-PSUpgradeGallery -GalleryPath $brokenGallery -Version '0.1.2' } 'legal files'
    Write-Host 'PASS: package metadata mismatch, missing dependency and incomplete installer/legal bundle rejection'
    $count++

    $script:fixtureSecret = 'offline-fixture-token-not-a-real-credential'
    $script:fixtureBasic = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("x-access-token:$script:fixtureSecret"))
    $script:fixtureConfig = "http.https://github.com/.extraheader`nAUTHORIZATION: Basic $script:fixtureBasic$([char]0)"
    $token = ConvertFrom-PSUpgradeCheckoutHeaders -Configuration $script:fixtureConfig -GitHubRepository Azure/azclips
    Assert-ReleaseFixture ($token -ceq $script:fixtureSecret) 'Basic password becomes the in-memory token.'
    $bearer = "http.https://github.com/Azure/azclips.extraheader`nauthorization: Bearer $script:fixtureSecret$([char]0)"
    Assert-ReleaseFixture ((ConvertFrom-PSUpgradeCheckoutHeaders -Configuration $bearer -GitHubRepository Azure/azclips) -ceq $script:fixtureSecret) 'Bearer checkout headers work.'
    $other = "http.https://github.com/Other/Repository.extraheader`nAUTHORIZATION: Bearer unrelated$([char]0)"
    $wrongHost = "http.https://github.com.evil.example/.extraheader`nAUTHORIZATION: Bearer unrelated$([char]0)"
    Assert-ReleaseFixture (-not (ConvertFrom-PSUpgradeCheckoutHeaders -Configuration ($other + $wrongHost) -GitHubRepository Azure/azclips)) 'Unrelated hosts/repositories cannot supply credentials.'
    $general = "http.https://github.com/.extraheader`nAUTHORIZATION: Bearer lower-specificity$([char]0)"
    Assert-ReleaseFixture ((ConvertFrom-PSUpgradeCheckoutHeaders -Configuration ($general + $bearer) -GitHubRepository Azure/azclips) -ceq $script:fixtureSecret) 'Repository-specific headers take precedence.'
    Assert-ReleaseFixtureThrows {
        ConvertFrom-PSUpgradeCheckoutHeaders -Configuration ($general + $script:fixtureConfig) -GitHubRepository Azure/azclips
    } 'Conflicting'
    foreach ($invalid in 'Basic !!!', 'Basic bm9jb2xvbg==', 'Basic dXNlcjo=', "Bearer $script:fixtureSecret`nInjected: header", 'Unknown secret')
    {
        Assert-ReleaseFixtureThrows {
            ConvertFrom-PSUpgradeCheckoutHeaders -Configuration "http.https://github.com/.extraheader`nAUTHORIZATION: $invalid$([char]0)" -GitHubRepository Azure/azclips
        } 'authorization'
    }
    Write-Host 'PASS: scoped Bearer/Basic checkout credential parsing with redacted failure diagnostics'
    $count++

    $script:fixtureCalls = [Collections.Generic.List[object]]::new()
    $script:fixtureMode = 'Success'
    $script:fixtureGitExitCode = 0
    $script:fixtureLatest = New-ReleaseFixture -Version '0.1.3'
    $script:fixturePages = @{
        1 = @(New-ReleaseFixture -Version '0.1.2' -Digest "sha256:$script:fixtureHash" -Size (Get-Item $script:fixtureArchive).Length)
    }
    function Invoke-PSRuntimeProcess
    {
        param($FilePath, $Arguments, $WorkingDirectory, $Environment, $TimeoutSeconds)
        $script:fixtureCalls.Add(@{ FilePath = $FilePath; Arguments = $Arguments; Environment = $Environment.Clone(); Timeout = $TimeoutSeconds })
        if ($FilePath -eq 'git')
        {
            Assert-ReleaseFixture (($Arguments -join ' ') -eq 'config --local --null --get-regexp ^http\..*\.extraheader$') 'Checkout config is read locally and never changed.'
            return New-ReleaseFixtureResult -StdOut $script:fixtureConfig -ExitCode $script:fixtureGitExitCode
        }
        Assert-ReleaseFixture ($FilePath -eq 'gh') 'Only gh and optional read-only git are invoked; never an installer.'
        Assert-ReleaseFixture ($Environment.GH_DEBUG -eq '' -and $Environment.GH_PROMPT_DISABLED -eq '1') 'GitHub subprocess diagnostics and prompting are disabled.'
        if ($script:fixtureMode -eq 'NativeThrow') { throw $script:fixtureSecret }
        if ($script:fixtureMode -eq 'Failure') { return New-ReleaseFixtureResult -ExitCode 7 -StdOut $script:fixtureSecret }
        if ($script:fixtureMode -eq 'Timeout') { return New-ReleaseFixtureResult -ExitCode -1 -TimedOut $true }
        if ($script:fixtureMode -eq 'Truncated') { return New-ReleaseFixtureResult -Truncated $true }
        if ($Arguments[0] -eq 'api')
        {
            if ($script:fixtureMode -eq 'BadJson') { return New-ReleaseFixtureResult -StdOut $script:fixtureSecret }
            $endpoint = @($Arguments | Where-Object { $_ -like 'repos/*' })[0]
            if ($endpoint -eq 'repos/Azure/azclips/releases/latest')
            {
                return New-ReleaseFixtureResult -StdOut (ConvertTo-Json -InputObject $script:fixtureLatest -Depth 10 -Compress)
            }
            Assert-ReleaseFixture ($endpoint -match '^repos/Azure/azclips/releases\?per_page=100&page=(\d+)$') 'History uses explicit complete pagination.'
            return New-ReleaseFixtureResult -StdOut (ConvertTo-Json -InputObject $script:fixturePages[[int]$Matches[1]] -Depth 10 -Compress)
        }
        Assert-ReleaseFixture ($Arguments[0] -eq 'release' -and $Arguments[1] -eq 'download') 'Recommended gh release download is used.'
        $pattern = $Arguments[[array]::IndexOf($Arguments, '--pattern') + 1]
        Assert-ReleaseFixture ($pattern -eq 'azps-cmdlets-0.1.2.tar.gz' -and $Arguments[-1] -eq 'fixture-0.1.2' -and $Arguments[-2] -eq '--') 'Download is pinned to exactly the selected tag and asset, with no glob.'
        Assert-ReleaseFixture ($Arguments -notcontains '--clobber') 'Download cannot overwrite preexisting files.'
        if ($script:fixtureMode -eq 'DownloadFailure') { return New-ReleaseFixtureResult -ExitCode 8 -StdOut $script:fixtureSecret }
        $directory = $Arguments[[array]::IndexOf($Arguments, '--dir') + 1]
        Copy-Item -LiteralPath $script:fixtureArchive -Destination (Join-Path $directory $pattern)
        if ($script:fixtureMode -eq 'ChecksumFailure') { Add-Content -LiteralPath (Join-Path $directory $pattern) -Value 'changed' }
        return New-ReleaseFixtureResult
    }

    $env:GH_TOKEN = 'offline-existing-token'
    $destination = Join-Path $scratch 'prepared'
    $metadata = New-PSUpgradeBaseline -TargetVersion '0.1.3' -DestinationRoot $destination -UseCheckoutCredentials -TimeoutSeconds 23
    $saved = Get-Content -LiteralPath (Join-Path $destination 'release.json') -Raw | ConvertFrom-Json
    $expectedFields = 'schemaVersion,repository,tagName,version,targetVersion,publishedAt,assetName,sha256,selectionReason,latestVersion'
    Assert-ReleaseFixture (($saved.PSObject.Properties.Name -join ',') -eq $expectedFields) 'release.json has the exact agreed schema.'
    Assert-ReleaseFixture ($saved.schemaVersion -eq 1 -and $saved.repository -eq 'Azure/azclips' -and $saved.version -eq '0.1.2' -and
        $saved.targetVersion -eq '0.1.3' -and $saved.latestVersion -eq '0.1.3' -and $saved.selectionReason -eq 'previous-version') 'Persisted baseline selection records a real upgrade.'
    Assert-ReleaseFixture ($saved.sha256 -ceq $script:fixtureHash -and $saved.assetName -eq 'azps-cmdlets-0.1.2.tar.gz' -and
        ([datetimeoffset]$saved.publishedAt).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ') -eq '2026-09-01T00:00:00Z' -and
        $saved.tagName -eq 'fixture-0.1.2') 'Metadata preserves verified digest, selected tag/asset and publication time.'
    Assert-ReleaseFixture ($metadata.version -eq $saved.version) 'Returned metadata agrees with persistent metadata.'
    Assert-PSUpgradeGallery -GalleryPath (Join-Path $destination 'gallery') -Version '0.1.2'
    Assert-ReleaseFixture ($env:GH_TOKEN -ceq 'offline-existing-token') 'Checkout credentials do not mutate the parent environment.'
    foreach ($call in $script:fixtureCalls)
    {
        Assert-ReleaseFixture ($call.Timeout -eq 23) 'Every native process uses the caller deadline.'
        if ($call.FilePath -eq 'gh')
        {
            Assert-ReleaseFixture ($call.Environment.GH_TOKEN -ceq $script:fixtureSecret) 'Checkout token is passed only in the gh child environment.'
            Assert-ReleaseFixture (($call.Arguments -join ' ') -notmatch [regex]::Escape($script:fixtureSecret)) 'Token is never a process argument.'
        }
    }
    Assert-ReleaseFixture ((Get-Content -LiteralPath (Join-Path $destination 'release.json') -Raw) -notmatch 'token|AUTHORIZATION|Basic') 'No credential data enters release.json.'
    Assert-ReleaseFixture (@(Get-ChildItem -LiteralPath $scratch -Directory -Filter '.ps-upgrade-*').Count -eq 0) 'All owned download/publish workspaces are cleaned.'
    Write-Host 'PASS: complete mocked preparation, exact release.json contract, tag/asset pinning, bounded subprocesses and child-only credentials'
    $count++

    $existingFiles = Get-ChildItem -LiteralPath $destination -File -Recurse | Get-FileHash | Select-Object Path, Hash | ConvertTo-Json -Compress
    $beforeCalls = $script:fixtureCalls.Count
    Assert-ReleaseFixtureThrows { New-PSUpgradeBaseline -TargetVersion '0.1.3' -DestinationRoot $destination } 'already exists'
    Assert-ReleaseFixture ($script:fixtureCalls.Count -eq $beforeCalls) 'Existing output is rejected before invoking native tools.'
    Assert-ReleaseFixture ($existingFiles -ceq (Get-ChildItem -LiteralPath $destination -File -Recurse | Get-FileHash | Select-Object Path, Hash | ConvertTo-Json -Compress)) 'Existing baseline files are unchanged.'
    foreach ($mode in 'Failure', 'Timeout', 'Truncated', 'BadJson', 'DownloadFailure', 'ChecksumFailure', 'NativeThrow')
    {
        $script:fixtureMode = $mode
        $destination = Join-Path $scratch "failure-$mode"
        Assert-ReleaseFixtureThrows {
            New-PSUpgradeBaseline -TargetVersion '0.1.3' -DestinationRoot $destination -UseCheckoutCredentials
        } 'GitHub CLI|checksum mismatch'
        Assert-ReleaseFixture (-not (Test-Path -LiteralPath $destination)) 'A failed preparation never publishes partial baseline output.'
        Assert-ReleaseFixture (@(Get-ChildItem -LiteralPath $scratch -Directory -Filter '.ps-upgrade-*').Count -eq 0) 'Failure removes owned workspaces.'
    }
    Assert-ReleaseFixture ((Get-FileHash -LiteralPath $script:fixtureArchive).Hash -ieq $script:fixtureHash) 'All input archives remain untouched after failures.'
    $script:fixtureMode = 'Success'
    $script:fixtureGitExitCode = 1
    $environment = Get-PSUpgradeGitHubEnvironment -CheckoutRoot $repoRoot -GitHubRepository Azure/azclips -UseCheckoutCredentials
    Assert-ReleaseFixture (-not $environment.ContainsKey('GH_TOKEN')) 'Missing checkout credentials allow normal existing gh authentication.'
    $environment = Get-PSUpgradeGitHubEnvironment -CheckoutRoot $repoRoot -GitHubRepository Azure/azclips
    Assert-ReleaseFixture (-not $environment.ContainsKey('GH_TOKEN')) 'Default authentication is inherited rather than persisted or rewritten.'
    $script:fixtureGitExitCode = 128
    Assert-ReleaseFixtureThrows {
        Get-PSUpgradeGitHubEnvironment -CheckoutRoot $repoRoot -GitHubRepository Azure/azclips -UseCheckoutCredentials
    } 'authorization failed'
    Write-Host 'PASS: failure propagation, redacted native/JSON errors, checksum-before-extraction, unchanged inputs and explicit auth fallback'
    $count++

    $script:fixtureGitExitCode = 1
    $script:fixtureLatest = $script:fixturePages[1][0]
    $script:fixtureCalls.Clear()
    $latestDestination = (Join-Path $scratch 'prepared-latest') + [IO.Path]::DirectorySeparatorChar
    New-PSUpgradeBaseline -TargetVersion '0.1.3' -DestinationRoot $latestDestination | Out-Null
    $saved = Get-Content -LiteralPath (Join-Path $latestDestination 'release.json') -Raw | ConvertFrom-Json
    Assert-ReleaseFixture ($saved.selectionReason -eq 'latest' -and $saved.latestVersion -eq '0.1.2') 'Strictly older authoritative latest is persisted without history.'
    Assert-ReleaseFixture ($script:fixtureCalls.Count -eq 2 -and @($script:fixtureCalls | Where-Object FilePath -eq git).Count -eq 0) 'Default authentication needs only latest metadata and the pinned download.'
    Write-Host 'PASS: normal authenticated latest preparation without checkout credentials or unnecessary history calls'
    $count++

    $entryPath = Join-Path $repoRoot 'devops/scripts/packaging/powershell/prepare_ps_upgrade_baseline.ps1'
    $script:entryFinallyObserved = $false
    Assert-ReleaseFixtureThrows {
        try { & $entryPath -TargetVersion '0.1.3' -DestinationRoot $latestDestination -UseCheckoutCredentials }
        finally { $script:entryFinallyObserved = $true }
    } 'already exists'
    Assert-ReleaseFixture $script:entryFinallyObserved 'Entry failure preserves caller finally for checkout credential cleanup.'
    foreach ($path in $entryPath, (Join-Path $repoRoot 'devops/scripts/packaging/powershell/shared/upgrade_release.ps1'))
    {
        $tokens = $null
        $parseErrors = $null
        $ast = [Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$parseErrors)
        Assert-ReleaseFixture ($parseErrors.Count -eq 0) 'Preparation scripts parse on the host PowerShell version.'
        $exitStatement = $ast.Find({ param($node) $node -is [Management.Automation.Language.ExitStatementAst] }, $true)
        Assert-ReleaseFixture ($null -eq $exitStatement) 'Preparation propagates failure with throw, never exit.'
    }
    Write-Host 'PASS: real entry failure preserves caller finally and preparation contains no exit statements'
    $count++

    Write-Host "PowerShell upgrade release fixtures passed: $count groups."
}
finally
{
    Set-Item -Path Function:\Invoke-PSRuntimeProcess -Value $nativeProcess
    $env:AGENT_TEMPDIRECTORY = $originalTemp
    $env:GH_TOKEN = $originalToken
    Remove-PSUpgradeOwnedDirectory $scratch
}
