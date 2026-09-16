. (Join-Path $PSScriptRoot 'dependency_runtime.ps1')
. (Join-Path $PSScriptRoot 'dependency_packages.ps1')

function ConvertTo-PSUpgradeVersion
{
    param([Parameter(Mandatory)][string]$Value)

    if ($Value -notmatch '^\d+\.\d+(?:\.\d+){0,2}$' -or -not ($Value -as [version]))
    {
        throw 'PowerShell upgrade versions must be numeric manifest versions (two to four components).'
    }
    $version = [version]$Value
    return [version]::new($version.Major, $version.Minor, [Math]::Max(0, $version.Build), [Math]::Max(0, $version.Revision))
}

function Get-PSUpgradeReleaseCandidate
{
    param([Parameter(Mandatory)]$Release)

    if ($Release.draft -isnot [bool] -or $Release.prerelease -isnot [bool])
    {
        throw 'GitHub release metadata is missing stable-release flags.'
    }
    if ($Release.draft -or $Release.prerelease) { return }
    $assets = @($Release.assets | Where-Object { $_.name -cmatch '^azps-cmdlets-(\d+\.\d+\.\d+)\.tar\.gz$' })
    if ($assets.Count -eq 0) { return }
    if ($assets.Count -ne 1) { throw 'A stable release has ambiguous PowerShell bundle assets.' }
    $asset = $assets[0]
    $null = $asset.name -cmatch '^azps-cmdlets-(\d+\.\d+\.\d+)\.tar\.gz$'
    $version = $Matches[1]
    if ($Release.tag_name -isnot [string] -or $Release.tag_name -notmatch '^[A-Za-z0-9][A-Za-z0-9._/+@-]*$')
    {
        throw 'GitHub release metadata has an unsupported or missing tag name.'
    }
    $published = [datetimeoffset]::MinValue
    if ($Release.published_at -is [datetime]) { $published = [datetimeoffset]::new($Release.published_at) }
    elseif ($Release.published_at -is [datetimeoffset]) { $published = $Release.published_at }
    elseif (-not [datetimeoffset]::TryParse([string]$Release.published_at, [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::AssumeUniversal, [ref]$published))
    {
        throw 'GitHub release metadata has no valid publication timestamp.'
    }
    [pscustomobject]@{
        TagName = $Release.tag_name
        Version = $version
        NumericVersion = ConvertTo-PSUpgradeVersion $version
        PublishedAt = $published.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        AssetName = $asset.name
        Digest = $asset.digest
        AssetSize = $asset.size
    }
}

function Get-PSUpgradeReleaseSelection
{
    param(
        [Parameter(Mandatory)]$LatestRelease,
        [Parameter(Mandatory)][string]$TargetVersion,
        [scriptblock]$GetReleasePage
    )

    $target = ConvertTo-PSUpgradeVersion $TargetVersion
    $latest = Get-PSUpgradeReleaseCandidate $LatestRelease
    if (-not $latest) { throw 'The authoritative latest release has no stable PowerShell bundle.' }
    if ($latest.NumericVersion -gt $target) { throw 'Latest stable Az version exceeds the target; a downgrade is not an upgrade.' }
    $selected = $latest
    $reason = 'latest'
    if ($latest.NumericVersion -eq $target)
    {
        if (-not $GetReleasePage) { throw 'Release history is required to find a previous distinct stable version.' }
        $selected = $null
        $reason = 'previous-version'
        $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $page = 1
        do
        {
            $releases = @(& $GetReleasePage -Page $page -PerPage 100)
            if ($releases.Count -gt 100) { throw 'GitHub release history returned an oversized page.' }
            foreach ($release in $releases)
            {
                if (-not $release.tag_name -or -not $seen.Add([string]$release.tag_name))
                {
                    throw 'GitHub release history is missing tags or repeats releases; retry with a consistent history.'
                }
                $candidate = Get-PSUpgradeReleaseCandidate $release
                if ($candidate -and $candidate.NumericVersion -lt $target -and
                    (-not $selected -or $candidate.NumericVersion -gt $selected.NumericVersion -or
                        ($candidate.NumericVersion -eq $selected.NumericVersion -and $candidate.PublishedAt -gt $selected.PublishedAt)))
                {
                    $selected = $candidate
                }
            }
            $page++
        } while ($releases.Count -eq 100)
        if (-not $selected) { throw 'No previous distinct stable PowerShell version is available below the target.' }
    }
    if ($selected.Digest -isnot [string] -or $selected.Digest -notmatch '^sha256:([0-9a-fA-F]{64})$')
    {
        throw 'The selected GitHub asset has no valid SHA256 digest; refusing an unverified baseline.'
    }
    $sha256 = $Matches[1].ToLowerInvariant()
    $size = 0L
    if (-not [long]::TryParse([string]$selected.AssetSize, [ref]$size) -or $size -le 0 -or $size -gt 1GB)
    {
        throw 'The selected GitHub asset size is missing, invalid, or exceeds the 1 GiB limit.'
    }
    [pscustomobject]@{
        TagName = $selected.TagName
        Version = $selected.Version
        PublishedAt = $selected.PublishedAt
        AssetName = $selected.AssetName
        Sha256 = $sha256
        AssetSize = $size
        SelectionReason = $reason
        LatestVersion = $latest.Version
    }
}

function ConvertFrom-PSUpgradeCheckoutHeaders
{
    param(
        [AllowEmptyString()][string]$Configuration,
        [Parameter(Mandatory)][string]$GitHubRepository
    )

    $tokens = [Collections.Generic.List[object]]::new()
    foreach ($record in $Configuration.Split([char]0, [StringSplitOptions]::RemoveEmptyEntries))
    {
        # git config --null uses a newline between the key and its value.
        $parts = $record.Split("`n", 2)
        if ($parts.Count -ne 2 -or $parts[0] -notmatch '^http\.(https://[^ \r\n]+)\.extraheader$') { continue }
        $uri = $null
        if (-not [uri]::TryCreate($Matches[1], [UriKind]::Absolute, [ref]$uri) -or
            $uri.Host -ne 'github.com' -or $uri.Port -ne 443 -or $uri.UserInfo -or $uri.Query -or $uri.Fragment) { continue }
        $path = $uri.AbsolutePath.TrimEnd('/')
        $repositoryPath = "/$GitHubRepository"
        if ($path -and $path -ine $repositoryPath -and $path -ine "$repositoryPath.git" -and
            -not $repositoryPath.StartsWith("$path/", [StringComparison]::OrdinalIgnoreCase)) { continue }
        $header = $parts[1].Trim()
        if ($header -notmatch '(?i)^authorization\s*:') { continue }
        if ($header -notmatch '(?i)^authorization\s*:\s*(Bearer|Basic)\s+([^\s]+)$')
        {
            throw 'Checkout authorization header has an unsupported format; its value was suppressed.'
        }
        $scheme, $value = $Matches[1], $Matches[2]
        if ($scheme -ieq 'Basic')
        {
            try { $value = [Text.UTF8Encoding]::new($false, $true).GetString([Convert]::FromBase64String($value)) }
            catch { throw 'Checkout Basic authorization is invalid; its value was suppressed.' }
            $separator = $value.IndexOf(':')
            if ($separator -lt 0) { throw 'Checkout Basic authorization has no password; its value was suppressed.' }
            $value = $value.Substring($separator + 1)
        }
        if (-not $value -or $value -match '[\s\x00-\x1f\x7f]')
        {
            throw 'Checkout authorization has an invalid token; its value was suppressed.'
        }
        $tokens.Add([pscustomobject]@{ Specificity = $path.Length; Token = $value })
    }
    if ($tokens.Count -eq 0) { return }
    $specificity = ($tokens | Measure-Object -Property Specificity -Maximum).Maximum
    $values = @($tokens | Where-Object Specificity -eq $specificity | Select-Object -ExpandProperty Token -Unique)
    if ($values.Count -ne 1) { throw 'Conflicting checkout authorization headers were found; their values were suppressed.' }
    return $values[0]
}

function Get-PSUpgradeGitHubEnvironment
{
    param(
        [Parameter(Mandatory)][string]$CheckoutRoot,
        [Parameter(Mandatory)][string]$GitHubRepository,
        [switch]$UseCheckoutCredentials,
        [ValidateRange(1, 3600)][int]$TimeoutSeconds = 180
    )

    $environment = @{ GH_HOST = 'github.com'; GH_PROMPT_DISABLED = '1'; GH_DEBUG = ''; GH_PAGER = ''; GH_FORCE_TTY = '' }
    if ($UseCheckoutCredentials)
    {
        try
        {
            $result = Invoke-PSRuntimeProcess -FilePath 'git' -Arguments @('config', '--local', '--null', '--get-regexp', '^http\..*\.extraheader$') `
                -WorkingDirectory $CheckoutRoot -TimeoutSeconds $TimeoutSeconds `
                -Environment @{ GIT_TRACE = '0'; GIT_TRACE_CURL = '0'; GIT_CURL_VERBOSE = '0' }
        }
        catch { throw 'Could not read local checkout authorization; native diagnostics were suppressed.' }
        if ($result.TimedOut) { throw 'Reading local checkout authorization timed out.' }
        if ($result.Truncated) { throw 'Local checkout authorization exceeded the capture limit.' }
        if ($result.ExitCode -notin 0, 1) { throw 'Reading local checkout authorization failed; native diagnostics were suppressed.' }
        $token = if ($result.ExitCode -eq 0)
        {
            ConvertFrom-PSUpgradeCheckoutHeaders -Configuration $result.StdOut -GitHubRepository $GitHubRepository
        }
        if ($token) { $environment.GH_TOKEN = $token }
        else { Write-Verbose 'No applicable checkout authorization header; using existing gh authentication or GH_TOKEN.' }
    }
    return $environment
}

function Invoke-PSUpgradeGitHub
{
    param(
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [hashtable]$Environment = @{},
        [ValidateRange(1, 3600)][int]$TimeoutSeconds = 180
    )

    try
    {
        $result = Invoke-PSRuntimeProcess -FilePath 'gh' -Arguments $Arguments -WorkingDirectory $WorkingDirectory `
            -Environment $Environment -TimeoutSeconds $TimeoutSeconds
    }
    catch { throw 'GitHub CLI could not be started; verify that gh is installed. Native diagnostics were suppressed.' }
    if ($result.TimedOut) { throw 'GitHub CLI timed out while preparing the release baseline.' }
    if ($result.Truncated) { throw 'GitHub CLI exceeded the capture limit; refusing incomplete release metadata.' }
    if ($result.ExitCode -ne 0)
    {
        throw "GitHub CLI failed (exit code $($result.ExitCode)); check repository read access and gh authentication or GH_TOKEN. Native diagnostics were suppressed."
    }
    return $result.StdOut
}

function Invoke-PSUpgradeGitHubApi
{
    param(
        [Parameter(Mandatory)][string]$Endpoint,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [hashtable]$Environment = @{},
        [switch]$List,
        [ValidateRange(1, 3600)][int]$TimeoutSeconds = 180
    )

    $projection = '{tag_name,draft,prerelease,published_at,assets:[.assets[] | {name,digest,size}]}'
    if ($List) { $projection = "map($projection)" }
    $json = Invoke-PSUpgradeGitHub -Arguments @('api', '--hostname', 'github.com', $Endpoint, '--method', 'GET',
        '--header', 'Accept: application/vnd.github+json', '--jq', $projection) `
        -WorkingDirectory $WorkingDirectory -Environment $Environment -TimeoutSeconds $TimeoutSeconds
    try { $document = ConvertFrom-Json -InputObject $json -NoEnumerate -ErrorAction Stop }
    catch { throw 'GitHub CLI returned invalid release JSON; its content was suppressed.' }
    if (($List -and $document -isnot [array]) -or (-not $List -and $document -isnot [pscustomobject]))
    {
        throw 'GitHub CLI returned an unexpected release JSON shape.'
    }
    return $document
}

function Assert-PSUpgradeArchiveChecksum
{
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Sha256)

    if ($Sha256 -notmatch '^[0-9a-fA-F]{64}$') { throw 'A valid SHA256 digest is required before extraction.' }
    $item = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
    if ($item -isnot [IO.FileInfo] -or $item.Attributes -band [IO.FileAttributes]::ReparsePoint -or $item.Length -gt 1GB)
    {
        throw 'Downloaded release archive is not a regular file within the 1 GiB limit.'
    }
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256 -ErrorAction Stop).Hash -ine $Sha256)
    {
        throw 'GitHub asset SHA256 checksum mismatch; the release archive will not be extracted.'
    }
}

function Remove-PSUpgradeOwnedDirectory
{
    param([Parameter(Mandatory)][string]$Path)

    $item = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
    if ($item -isnot [IO.DirectoryInfo] -or $item.Attributes -band [IO.FileAttributes]::ReparsePoint)
    {
        throw 'Owned release workspace is no longer a regular directory; cleanup was refused.'
    }
    [IO.Directory]::Delete($item.FullName, $true)
}

function Assert-PSUpgradeTarStructure
{
    param(
        [Parameter(Mandatory)][IO.Stream]$Stream,
        [Parameter(Mandatory)][long]$MaxEntryBytes,
        [Parameter(Mandatory)][int]$MaxEntries
    )

    # Reject hidden metadata records before TarReader can allocate their contents.
    # The bundle producer emits ordinary GNU/ustar root entries, not PAX or sparse files.
    $header = [byte[]]::new(512)
    $count = 0
    while ($Stream.Position -lt $Stream.Length)
    {
        if ($Stream.Length - $Stream.Position -lt 512) { throw 'Release tar archive contains a truncated header.' }
        $Stream.ReadExactly($header)
        $nonzero = $false
        foreach ($value in $header) { if ($value -ne 0) { $nonzero = $true; break } }
        if (-not $nonzero)
        {
            $buffer = [byte[]]::new(65536)
            while (($length = $Stream.Read($buffer, 0, $buffer.Length)) -gt 0)
            {
                for ($index = 0; $index -lt $length; $index++)
                {
                    if ($buffer[$index] -ne 0) { throw 'Release tar archive contains nonzero trailing data.' }
                }
            }
            $Stream.Position = 0
            return
        }
        if (++$count -gt $MaxEntries) { throw 'Release tar archive exceeds the entry count limit.' }
        if ($header[156] -notin 0, 48, 53) { throw 'Release tar archive contains a link or unsupported entry type.' }
        $text = [Text.Encoding]::ASCII.GetString($header, 124, 12).Trim([char[]]@([char]0, [char]' '))
        if ($text -notmatch '^[0-7]*$') { throw 'Release tar archive contains an unsupported entry size.' }
        $size = if ($text.Length -eq 0) { 0L } else { [Convert]::ToInt64($text, 8) }
        if ($size -gt $MaxEntryBytes) { throw 'Release tar archive exceeds the per-entry size limit.' }
        $next = $Stream.Position + [long]([Math]::Ceiling($size / 512.0) * 512)
        if ($next -gt $Stream.Length) { throw 'Release tar archive contains truncated entry data.' }
        $Stream.Position = $next
    }
    throw 'Release tar archive has no end-of-archive marker.'
}

function Expand-PSUpgradeArchive
{
    param(
        [Parameter(Mandatory)][string]$ArchivePath,
        [Parameter(Mandatory)][string]$DestinationPath,
        [ValidateRange(1, 2147483648)][long]$MaxExpandedBytes = 2GB,
        [ValidateRange(1, 536870912)][long]$MaxEntryBytes = 512MB,
        [ValidateRange(1, 4096)][int]$MaxEntries = 4096
    )

    if (Get-Item -LiteralPath $DestinationPath -Force -ErrorAction SilentlyContinue) { throw 'Release extraction destination already exists.' }
    $null = New-Item -ItemType Directory -Path $DestinationPath -ErrorAction Stop
    $tarPath = Join-Path $DestinationPath '.verified-bundle.tar'
    try
    {
        $inputStream = [IO.File]::OpenRead($ArchivePath)
        try
        {
            $gzip = [IO.Compression.GZipStream]::new($inputStream, [IO.Compression.CompressionMode]::Decompress, $true)
            try
            {
                $expanded = [IO.File]::Open($tarPath, [IO.FileMode]::CreateNew)
                try
                {
                    $buffer = [byte[]]::new(131072)
                    $total = 0L
                    while (($length = $gzip.Read($buffer, 0, $buffer.Length)) -gt 0)
                    {
                        $total += $length
                        if ($total -gt $MaxExpandedBytes) { throw 'Release archive exceeds the expanded size limit.' }
                        $expanded.Write($buffer, 0, $length)
                    }
                }
                finally { $expanded.Dispose() }
            }
            finally { $gzip.Dispose() }
        }
        finally { $inputStream.Dispose() }

        $tarStream = [IO.File]::OpenRead($tarPath)
        try
        {
            Assert-PSUpgradeTarStructure -Stream $tarStream -MaxEntryBytes $MaxEntryBytes -MaxEntries $MaxEntries
            $reader = [System.Formats.Tar.TarReader]::new($tarStream, $true)
            try
            {
                $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
                while ($null -ne ($entry = $reader.GetNextEntry($false)))
                {
                    $name = $entry.Name
                    if ($entry.EntryType -eq [System.Formats.Tar.TarEntryType]::Directory)
                    {
                        if ($name -notin '.', './' -or $entry.Length -ne 0) { throw 'Release archive contains a non-root directory.' }
                        if (-not $names.Add('.')) { throw 'Release archive contains duplicate root directory entries.' }
                        continue
                    }
                    if ($entry.EntryType -notin [System.Formats.Tar.TarEntryType]::RegularFile, [System.Formats.Tar.TarEntryType]::V7RegularFile)
                    {
                        throw 'Release archive contains a link or unsupported entry type.'
                    }
                    if ($name.StartsWith('./', [StringComparison]::Ordinal)) { $name = $name.Substring(2) }
                    if ($name -cnotmatch '^(?:Az(?:\.[A-Za-z][A-Za-z0-9_-]*)*\.\d+\.\d+\.\d+(?:\.\d+)?\.nupkg|InstallModule\.ps1|LICENSE|NOTICE)$')
                    {
                        throw 'Release archive contains an unexpected or unsafe root filename.'
                    }
                    if (-not $names.Add($name)) { throw 'Release archive contains duplicate or case-colliding filenames.' }
                    $output = [IO.File]::Open((Join-Path $DestinationPath $name), [IO.FileMode]::CreateNew)
                    try { if ($entry.DataStream) { $entry.DataStream.CopyTo($output) } }
                    finally { $output.Dispose() }
                }
            }
            finally { $reader.Dispose() }
        }
        finally { $tarStream.Dispose() }
        Remove-Item -LiteralPath $tarPath -ErrorAction Stop
    }
    catch
    {
        $failure = $_
        try { Remove-PSUpgradeOwnedDirectory $DestinationPath }
        catch { Write-Warning 'Failed to remove the owned release extraction directory.' }
        throw $failure
    }
}

function Assert-PSUpgradeGallery
{
    param([Parameter(Mandatory)][string]$GalleryPath, [Parameter(Mandatory)][string]$Version)

    foreach ($name in 'InstallModule.ps1', 'LICENSE', 'NOTICE')
    {
        if (-not (Test-Path -LiteralPath (Join-Path $GalleryPath $name) -PathType Leaf))
        {
            throw 'The release bundle is missing its installer or legal files.'
        }
    }
    $packages = @(Get-ChildItem -LiteralPath $GalleryPath -File -Filter '*.nupkg')
    $rollups = @($packages | Where-Object Name -Match '^Az\.\d')
    if ($rollups.Count -ne 1 -or $rollups[0].Name -cne "Az.$Version.nupkg")
    {
        throw 'The release bundle must contain exactly the selected Az version rollup package.'
    }
    $byName = @{}
    foreach ($package in $packages)
    {
        $archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
        try
        {
            $specs = @($archive.Entries | Where-Object FullName -Match '^[^/\\]+\.nuspec$')
            if ($archive.Entries.Count -gt 100000 -or $specs.Count -ne 1 -or $specs[0].Length -gt 1MB)
            {
                throw 'Release package metadata is missing, ambiguous, or exceeds the size limit.'
            }
        }
        finally { $archive.Dispose() }
        try { $metadata = Read-PSDependencyPackage -Path $package.FullName }
        catch { throw 'Release package metadata could not be read; its content was suppressed.' }
        if ($package.Name -cne "$($metadata.Name).$($metadata.Version).nupkg" -or $byName.ContainsKey($metadata.Name))
        {
            throw 'Release package identity does not match its filename or duplicates another module.'
        }
        $byName[$metadata.Name] = $metadata
    }
    if ($byName['Az'].Name -cne 'Az' -or $byName['Az'].Version -cne $Version)
    {
        throw 'Az package metadata does not match the selected release version.'
    }
    foreach ($dependency in $byName['Az'].Dependencies)
    {
        if (-not $byName.ContainsKey($dependency.Name) -or
            $dependency.Version -notin $byName[$dependency.Name].Version, "[$($byName[$dependency.Name].Version)]")
        {
            throw 'The release gallery is missing a matching Az rollup dependency.'
        }
    }
}

function New-PSUpgradeBaseline
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$TargetVersion,
        [Parameter(Mandatory)][string]$DestinationRoot,
        [string]$GitHubRepository = 'Azure/azclips',
        [switch]$UseCheckoutCredentials,
        [ValidateRange(1, 3600)][int]$TimeoutSeconds = 180
    )

    $null = ConvertTo-PSUpgradeVersion $TargetVersion
    if ($GitHubRepository -notmatch '^[A-Za-z0-9][A-Za-z0-9-]*/[A-Za-z0-9][A-Za-z0-9._-]*$')
    {
        throw 'GitHubRepository must be a github.com owner/repository name.'
    }
    $destination = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($DestinationRoot, (Get-Location).ProviderPath))
    if (Get-Item -LiteralPath $destination -Force -ErrorAction SilentlyContinue) { throw 'Baseline destination already exists; it will not be modified.' }
    $parent = Split-Path $destination -Parent
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) { throw 'The baseline destination parent directory must already exist.' }
    $checkout = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../..'))
    $environment = Get-PSUpgradeGitHubEnvironment -CheckoutRoot $checkout -GitHubRepository $GitHubRepository `
        -UseCheckoutCredentials:$UseCheckoutCredentials -TimeoutSeconds $TimeoutSeconds
    $scratch = $null
    $publish = $null
    try
    {
        $scratchPath = Join-Path (Get-PSRuntimeScratchRoot) ".ps-upgrade-release-$([guid]::NewGuid().ToString('N'))"
        $null = New-Item -ItemType Directory -Path $scratchPath -ErrorAction Stop
        $scratch = $scratchPath
        $latest = Invoke-PSUpgradeGitHubApi -Endpoint "repos/$GitHubRepository/releases/latest" `
            -WorkingDirectory $scratch -Environment $environment -TimeoutSeconds $TimeoutSeconds
        $getPage = {
            param($Page, $PerPage)
            Invoke-PSUpgradeGitHubApi -Endpoint "repos/$GitHubRepository/releases?per_page=$PerPage&page=$Page" -List `
                -WorkingDirectory $scratch -Environment $environment -TimeoutSeconds $TimeoutSeconds
        }
        $selection = Get-PSUpgradeReleaseSelection -LatestRelease $latest -TargetVersion $TargetVersion -GetReleasePage $getPage
        $null = Invoke-PSUpgradeGitHub -Arguments @('release', 'download', '--repo', $GitHubRepository,
            '--pattern', $selection.AssetName, '--dir', $scratch, '--', $selection.TagName) `
            -WorkingDirectory $scratch -Environment $environment -TimeoutSeconds $TimeoutSeconds
        $archivePath = Join-Path $scratch $selection.AssetName
        Assert-PSUpgradeArchiveChecksum -Path $archivePath -Sha256 $selection.Sha256
        if ((Get-Item -LiteralPath $archivePath).Length -ne $selection.AssetSize) { throw 'Downloaded release archive size differs from GitHub asset metadata.' }

        # Publish from a sibling so Directory.Move remains atomic across temp/output volumes.
        $publishPath = Join-Path $parent ".ps-upgrade-publish-$([guid]::NewGuid().ToString('N'))"
        $null = New-Item -ItemType Directory -Path $publishPath -ErrorAction Stop
        $publish = $publishPath
        $gallery = Join-Path $publish 'gallery'
        Expand-PSUpgradeArchive -ArchivePath $archivePath -DestinationPath $gallery
        Assert-PSUpgradeGallery -GalleryPath $gallery -Version $selection.Version
        $metadata = [pscustomobject][ordered]@{
            schemaVersion = 1
            repository = $GitHubRepository
            tagName = $selection.TagName
            version = $selection.Version
            targetVersion = $TargetVersion
            publishedAt = $selection.PublishedAt
            assetName = $selection.AssetName
            sha256 = $selection.Sha256
            selectionReason = $selection.SelectionReason
            latestVersion = $selection.LatestVersion
        }
        [IO.File]::WriteAllText((Join-Path $publish 'release.json'), ($metadata | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
        Remove-PSUpgradeOwnedDirectory $scratch
        $scratch = $null
        [IO.Directory]::Move($publish, $destination)
        $publish = $null
        return $metadata
    }
    finally
    {
        foreach ($owned in @($publish, $scratch))
        {
            if ($owned)
            {
                try { Remove-PSUpgradeOwnedDirectory $owned }
                catch { Write-Warning 'Failed to remove an owned release preparation workspace.' }
            }
        }
        $environment.Clear()
    }
}
