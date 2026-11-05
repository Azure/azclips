# Syncs AzClips CLI SDP YAML into the destination docs repo
# (MicrosoftDocs/azure-docs-cli). Three-stage escape hatch: dry-run (default; no flag)
# / -Force (write to local target) / -Push (deferred until docs.ms bot-auth is decided).
#
# Usage:
#   pwsh -File build/Sync-AzClipsCliDocs.ps1               # dry-run against a scratch folder
#   pwsh -File build/Sync-AzClipsCliDocs.ps1 -Force        # writes to local target; no push
#   pwsh -File build/Sync-AzClipsCliDocs.ps1 -Push         # WILL FAIL — deferred until auth-pattern decided
#
# CI-oriented parameters (all optional, no effect on interactive use):
#   -NoBuild              skip the leading `dotnet build` (CI usually builds separately)
#   -Configuration <cfg>  Debug|Release for the build + exporter run (default Debug)
#   -FetchIfMissing       auto-fetch CLI2Yaml non-interactively when missing from the NuGet cache
#   -CommitAuthor <name>  inline user.name for the commit (does not persist to target repo config)
#   -CommitEmail <email>  inline user.email — same
#   -OutputManifest <p>   write a machine-readable JSON summary of the run to <p>
#   -SkipServicePages     drop service-page/**/*.yml from CLI2Yaml output before applying to the
#                         target. Defaults to $true because our current service_name.json produces
#                         a single broken page with unresolvable xrefs (tracked as follow-up to
#                         Azure/CLIPS#588). Pass `-SkipServicePages:$false` once upstream is fixed.
#                         When on, TOC.yml is also regenerated as a command-group tree
#                         (Write-CommandGroupToc), since CLI2Yaml's TOC points at the stripped service page.
#
# Preflight: requires CLI2Yaml NuGet package to be resolvable — either already in the local
# NuGet cache under ~/.nuget/packages/cli2yaml/ or fetchable from the internal apidrop feed
# (needs Azure Artifacts Credential Provider on a Microsoft-tenant machine).

[CmdletBinding()]
param(
    [string]$ConfigPath,
    [string]$Moniker,
    [string]$VersionFolder,
    [string]$TargetRepoPath,
    [string]$SourceRepoUrl,
    [string]$SourceBranch,
    [string]$Timestamp,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$NoBuild,
    [switch]$FetchIfMissing,
    [string]$CommitAuthor,
    [string]$CommitEmail,
    [string]$OutputManifest,
    [bool]$SkipServicePages = $true,
    [switch]$Force,
    [switch]$Push
)

$ErrorActionPreference = 'Stop'

$__loadOnly = $env:AZCLIPS_CLI_SYNC_LOAD_ONLY -eq '1'

# ── Load config ──────────────────────────────────────────────────────────────────

if (-not $__loadOnly) {
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    if (-not $ConfigPath) { $ConfigPath = Join-Path $PSScriptRoot 'cli-docs-sync.config.json' }

    if (-not (Test-Path $ConfigPath))
    {
        throw "Config not found at $ConfigPath. Copy build/cli-docs-sync.config.json to that path or pass -ConfigPath."
    }

    $config = Get-Content -Raw -Path $ConfigPath | ConvertFrom-Json

    # CLI param → env var (Moniker/VersionFolder only) → config.
    $effectiveMoniker = $Moniker
    if (-not $effectiveMoniker) { $effectiveMoniker = $env:AZCLIPS_CLI_MONIKER }
    if (-not $effectiveMoniker) { $effectiveMoniker = $config.moniker }
    if ([string]::IsNullOrWhiteSpace($effectiveMoniker))
    {
        throw "Moniker is not set. Pass -Moniker, set `$env:AZCLIPS_CLI_MONIKER, or edit the 'moniker' field in $ConfigPath."
    }

    $effectiveVersionFolder = $VersionFolder
    if (-not $effectiveVersionFolder) { $effectiveVersionFolder = $env:AZCLIPS_CLI_VERSION_FOLDER }
    if (-not $effectiveVersionFolder) { $effectiveVersionFolder = $config.versionFolder }
    if ([string]::IsNullOrWhiteSpace($effectiveVersionFolder))
    {
        throw "versionFolder is not set. Pass -VersionFolder or edit the 'versionFolder' field in $ConfigPath."
    }

    $effectiveTargetRepoPath = $TargetRepoPath
    if (-not $effectiveTargetRepoPath) { $effectiveTargetRepoPath = $config.targetRepoPath }
    $effectiveTargetRepoPath = if ([System.IO.Path]::IsPathRooted($effectiveTargetRepoPath)) {
        $effectiveTargetRepoPath
    } else {
        Join-Path $repoRoot $effectiveTargetRepoPath
    }

    $effectiveSourceRepoUrl = $SourceRepoUrl
    if (-not $effectiveSourceRepoUrl) { $effectiveSourceRepoUrl = $config.sourceRepoUrl }

    $effectiveSourceBranch = $SourceBranch
    if (-not $effectiveSourceBranch) { $effectiveSourceBranch = $config.sourceBranch }
}

# ── CLI2Yaml resolution ─────────────────────────────────────────────────────────

# Non-interactively pulls CLI2Yaml from the configured feed via a throwaway csproj.
# Caller must have credentials pre-arranged (federated identity, env-var PAT, or a warm
# credential-provider session). Interactive AAD auth is intentionally disabled — CI-safe.
function Install-Cli2Yaml
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$PackageName,
        [Parameter(Mandatory)][string]$Feed
    )

    # Unique per-invocation folder so concurrent runs don't clobber each other's Fetch.csproj / obj / assets.
    $fetchDir = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-cli2yaml-fetch-" + [Guid]::NewGuid().ToString('N'))
    New-Item -Path $fetchDir -ItemType Directory -Force | Out-Null

    try {
        $csprojBody = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RestoreSources>$Feed</RestoreSources>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="$PackageName" Version="*" />
  </ItemGroup>
</Project>
"@
        $csprojPath = Join-Path $fetchDir 'Fetch.csproj'
        Set-Content -Path $csprojPath -Value $csprojBody -NoNewline

        Write-Host "Fetching $PackageName from feed (non-interactive)..." -ForegroundColor Cyan
        # `dotnet restore` is non-interactive by default; --interactive is a switch and does not accept a value.
        $output = & dotnet restore $csprojPath 2>&1
        if ($LASTEXITCODE -ne 0)
        {
            throw "Non-interactive fetch of $PackageName failed with exit $LASTEXITCODE. Credentials may not be available; ensure a federated-identity token or PAT is exported via NuGet env vars. Output:`n$($output -join "`n")"
        }
        $output | Select-Object -Last 1 | ForEach-Object { Write-Host "  → $_" }
    }
    finally {
        Remove-Item -Recurse -Force $fetchDir -ErrorAction SilentlyContinue
    }
}

# Locates CLI2Yaml.dll in the NuGet cache. Prefers newest cached version. If none is
# cached and -FetchIfMissing is set, non-interactively pulls the package first (CI use).
# Otherwise throws with a fetch snippet the caller can run manually (dev use).
function Get-Cli2YamlDllPath
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$PackageName,
        [Parameter(Mandatory)][string]$Feed,
        [switch]$FetchIfMissing
    )

    # Respect NUGET_PACKAGES override (common on CI + constrained envs) before falling back to the default global cache.
    if ($env:NUGET_PACKAGES)
    {
        $packagesRoot = $env:NUGET_PACKAGES
    }
    else
    {
        # USERPROFILE on Windows, HOME on Linux/macOS; .NET fallback covers oddballs.
        $userProfile = if ($env:USERPROFILE) { $env:USERPROFILE }
                       elseif ($env:HOME) { $env:HOME }
                       else { [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile) }
        $packagesRoot = Join-Path $userProfile '.nuget/packages'
    }
    $cacheRoot = Join-Path $packagesRoot $PackageName.ToLowerInvariant()
    if (-not (Test-Path $cacheRoot))
    {
        if ($FetchIfMissing)
        {
            Install-Cli2Yaml -PackageName $PackageName -Feed $Feed
        }
        else
        {
            throw @"
$PackageName is not in the local NuGet cache ($cacheRoot).
Fetch it once via a throwaway csproj:

    `$fetch = Join-Path ([System.IO.Path]::GetTempPath()) 'azclips-cli2yaml-fetch'
    New-Item -Path `$fetch -ItemType Directory -Force | Out-Null
    @'
    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <RestoreSources>$Feed</RestoreSources>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="$PackageName" Version="*" />
      </ItemGroup>
    </Project>
    '@ | Set-Content (Join-Path `$fetch 'Fetch.csproj')

    dotnet restore (Join-Path `$fetch 'Fetch.csproj') --interactive

Or re-run this script with -FetchIfMissing to fetch non-interactively (needs pre-arranged credentials).
"@
        }
    }

    # Iterate immediate version dirs and probe the well-known tools/CLI2Yaml.dll path.
    # Avoids -Recurse across the whole package tree and skips folders whose names aren't versions.
    $parsed = $null
    $versionDirs = Get-ChildItem $cacheRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { [Version]::TryParse($_.Name, [ref]$parsed) } |
        Sort-Object { [Version]$_.Name } -Descending

    $dllPath = $null
    foreach ($vd in $versionDirs)
    {
        $candidate = Join-Path $vd.FullName 'tools/CLI2Yaml.dll'
        if (Test-Path $candidate)
        {
            $dllPath = $candidate
            break
        }
    }
    if (-not $dllPath)
    {
        throw "No $PackageName.dll found under $cacheRoot. If `$env:NUGET_PACKAGES is set, the package may live elsewhere — check that path. Otherwise the restore may have failed silently; re-run with -FetchIfMissing or run the fetch snippet manually."
    }
    return $dllPath
}

# ── Exporter ────────────────────────────────────────────────────────────────────

# Runs Azclips.Cli.Export → snake_case JSON at <RepoRoot>/artifacts/cli-export/latest/.
# Returns the path of the produced folder. Assumes solution is already built.
function Invoke-CliExporter
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RepoRoot,
        [Parameter(Mandatory)][string]$ExportProject,
        [string]$Configuration = 'Debug'
    )

    $projectPath = Join-Path $RepoRoot $ExportProject
    if (-not (Test-Path $projectPath))
    {
        throw "Exporter project not found at $projectPath. Check the 'exportProject' field in the config."
    }

    Write-Host "Running exporter ($Configuration): $projectPath" -ForegroundColor Cyan
    Push-Location $RepoRoot
    try {
        $output = & dotnet run --project $projectPath --no-build --configuration $Configuration 2>&1
        if ($LASTEXITCODE -ne 0)
        {
            throw "Exporter failed with exit $LASTEXITCODE. Output:`n$($output -join "`n")"
        }
        $output | Select-Object -Last 1 | ForEach-Object { Write-Host "  → $_" }
    }
    finally {
        Pop-Location
    }

    $outputFolder = Join-Path $RepoRoot 'artifacts/cli-export/latest'
    if (-not (Test-Path $outputFolder))
    {
        throw "Exporter completed but expected output folder is missing: $outputFolder"
    }
    return $outputFolder
}

# ── CLI2Yaml orchestration ──────────────────────────────────────────────────────

# Assembles the smoke workspace that CLI2Yaml expects (input/latest/, input-ext/, commit map)
# and returns the path to it. Doesn't run CLI2Yaml itself — separate function.
function New-Cli2YamlWorkspace
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RepoRoot,
        [Parameter(Mandatory)][string]$ExporterOutput
    )

    $workspace = Join-Path $RepoRoot 'artifacts/cli-yaml'
    Remove-Item -Recurse -Force $workspace -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path (Join-Path $workspace 'input/latest'), (Join-Path $workspace 'input-ext') | Out-Null

    Copy-Item (Join-Path $ExporterOutput '*.json') -Destination (Join-Path $workspace 'input/latest') -Force

    $commitMapPath = Join-Path $workspace 'input/doc_commit_map.json'
    $commitMapScript = Join-Path $RepoRoot 'build/Get-DocCommitMap.ps1'
    if (-not (Test-Path $commitMapScript))
    {
        throw "Get-DocCommitMap.ps1 not found at $commitMapScript"
    }

    Push-Location $RepoRoot
    try {
        $output = & pwsh -File $commitMapScript -OutputPath $commitMapPath 2>&1
        if ($LASTEXITCODE -ne 0)
        {
            throw "Get-DocCommitMap.ps1 failed with exit $LASTEXITCODE. Output:`n$($output -join "`n")"
        }
        $output | Select-Object -Last 1 | ForEach-Object { Write-Host "  → $_" }
    }
    finally {
        Pop-Location
    }

    return $workspace
}

# Runs CLI2Yaml with our AzClips overrides. OutputVersionFolder is the parent of the
# CLI2Yaml-internal `latest` subdirectory (e.g. .../docs-ref-autogen/preview-version).
function Invoke-Cli2Yaml
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Cli2YamlDll,
        [Parameter(Mandatory)][string]$Workspace,
        [Parameter(Mandatory)][string]$OutputVersionFolder,
        [Parameter(Mandatory)][string]$DocsReferenceConfig,
        [Parameter(Mandatory)][string]$ExtensionIndex,
        [Parameter(Mandatory)][string]$ServiceMapping,
        [Parameter(Mandatory)][string]$SourceRepoUrl,
        [Parameter(Mandatory)][string]$SourceBranch
    )

    $inputPath   = Join-Path $Workspace 'input'
    $extInputPath = Join-Path $Workspace 'input-ext'
    $commitMap   = Join-Path $inputPath 'doc_commit_map.json'

    New-Item -ItemType Directory -Path $OutputVersionFolder -Force | Out-Null

    Write-Host "Running CLI2Yaml → $OutputVersionFolder" -ForegroundColor Cyan
    $output = & dotnet $Cli2YamlDll `
        -c $inputPath `
        -e $extInputPath `
        -o $OutputVersionFolder `
        --config $DocsReferenceConfig `
        --commitMap $commitMap `
        --extensionIndex $ExtensionIndex `
        --serviceMapping $ServiceMapping `
        --sourceRepo $SourceRepoUrl `
        --editBranch $SourceBranch 2>&1

    if ($LASTEXITCODE -ne 0)
    {
        throw "CLI2Yaml failed with exit $LASTEXITCODE. Output:`n$($output -join "`n")"
    }

    $output | Select-Object -Last 1 | ForEach-Object { Write-Host "  → $_" }
}

# Strips the CLI2Yaml-emitted service-page/ subdirectory from OutputVersionFolder/latest/.
# See Azure/CLIPS#588 follow-up for the upstream fix that makes this obsolete.
function Remove-ServicePageOutput
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$OutputVersionFolder
    )

    $servicePageDir = Join-Path (Join-Path $OutputVersionFolder 'latest') 'service-page'
    if (Test-Path $servicePageDir)
    {
        $count = @(Get-ChildItem $servicePageDir -Recurse -File).Count
        Remove-Item -Recurse -Force $servicePageDir
        Write-Host "  → Removed service-page/ ($count file(s)) — SkipServicePages is on" -ForegroundColor DarkGray
    }
}

function Repair-CommandGroupUidCollisions
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$OutputVersionFolder
    )

    $latest = Join-Path $OutputVersionFolder 'latest'
    if (-not (Test-Path $latest)) { throw "CLI2Yaml output folder '$latest' does not exist." }

    $pages = @{}
    $groups = @{}
    $commandUids = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($file in Get-ChildItem $latest -Recurse -Filter '*.yml' -File)
    {
        $text = [IO.File]::ReadAllText($file.FullName)
        $pages.Add($file.FullName, $text)
        $group = [regex]::Match($text, '(?m)^uid:[ \t]*(\S+)[ \t]*\r?$')
        if ($group.Success) { $groups.Add($group.Groups[1].Value, $file.FullName) }
        foreach ($command in [regex]::Matches($text, '(?m)^- uid:[ \t]*(\S+)[ \t]*\r?$'))
        {
            if (-not $commandUids.Add($command.Groups[1].Value))
            {
                throw "Duplicate generated command UID '$($command.Groups[1].Value)'."
            }
        }
    }

    foreach ($uid in @($groups.Keys | Where-Object { $commandUids.Contains($_) }))
    {
        $groupUid = "${uid}_group"
        if ($groups.ContainsKey($groupUid) -or $commandUids.Contains($groupUid))
        {
            throw "Cannot disambiguate '$uid': generated UID '$groupUid' already exists."
        }

        $groupPath = $groups[$uid]
        $pages[$groupPath] = [regex]::Replace($pages[$groupPath], '(?m)^uid:[^\r\n]+', "uid: $groupUid")
        $escapedUid = [regex]::Escape($uid)
        foreach ($path in @($pages.Keys))
        {
            # Ancestor command lists contain both identities in UID-sorted order.
            # Keep the first as the runnable command and the second as the group.
            $pages[$path] = [regex]::Replace($pages[$path], '(?ms)^commands:\r?\n(?:- [^\r\n]+\r?\n?)*', {
                param($block)
                $references = [regex]::Matches($block.Value, "(?m)^- $escapedUid[ \t]*(?=`r?$)")
                if ($references.Count -gt 2) { throw "Unexpected repeated UID '$uid' in '$path'." }
                if ($references.Count -eq 0) { return $block.Value }
                $reference = $references[$references.Count - 1]
                return $block.Value.Remove($reference.Index, $reference.Length).Insert($reference.Index, "- $groupUid")
            })
            # CLI2Yaml's TOC refers to groups, not runnable commands.
            $pages[$path] = [regex]::Replace($pages[$path], "(?m)^([ \t]+uid: )$escapedUid[ \t]*(?=`r?$)", "`${1}$groupUid")
        }
        Write-Host "  Renamed group UID '$uid' to '$groupUid'; command UID retained."
    }

    foreach ($path in $pages.Keys)
    {
        if ($pages[$path] -cne [IO.File]::ReadAllText($path))
        {
            [IO.File]::WriteAllText($path, $pages[$path], [Text.UTF8Encoding]::new($false))
        }
    }
}

# Rewrites the generated TOC as a literal command-group tree, including root commands.
# CLI2Yaml's service-page TOC does not support the AzClips command-only layout.
function Write-CommandGroupToc
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$OutputVersionFolder
    )

    $latest = Join-Path $OutputVersionFolder 'latest'
    if (-not (Test-Path $latest)) { throw "CLI2Yaml output folder '$latest' does not exist." }

    $tocPath = Join-Path $latest 'TOC.yml'
    $newline = "`n"
    if ((Test-Path $tocPath) -and ([IO.File]::ReadAllText($tocPath).Contains("`r`n"))) { $newline = "`r`n" }

    function Read-GroupHeader([string]$Path)
    {
        $text = [IO.File]::ReadAllText($Path)
        $uid  = [regex]::Match($text, '(?m)^uid:[ \t]*(\S+)[ \t]*\r?$')
        if (-not $uid.Success) { throw "Generated reference file '$Path' has no top-level uid." }
        $name = [regex]::Match($text, '(?m)^name:[ \t]*(.+?)[ \t]*\r?$')
        [pscustomobject]@{
            Uid  = $uid.Groups[1].Value
            Name = if ($name.Success) { $name.Groups[1].Value } else { $uid.Groups[1].Value }
        }
    }

    $lines = [System.Collections.Generic.List[string]]::new()
    $rootCommands = @{}
    $index = Join-Path $latest 'reference-index.yml'
    if (Test-Path $index)
    {
        # CLI2Yaml places root commands inside the index, not in separate group files.
        $text = [IO.File]::ReadAllText($index)
        $block = [regex]::Match($text, '(?ms)^directCommands:\r?\n(.*?)(?=^[A-Za-z]|\z)')
        foreach ($command in [regex]::Matches($block.Groups[1].Value, '(?ms)^- uid:[ \t]*(\S+)[ \t]*\r?\n(.*?)(?=^- uid:|\z)'))
        {
            $name = [regex]::Match($command.Groups[2].Value, '(?m)^  name:[ \t]*az ([^ \t\r\n]+)[ \t]*\r?$')
            if (-not $name.Success) { throw "Root command '$($command.Groups[1].Value)' has no valid name in '$index'." }
            $rootCommands.Add($name.Groups[1].Value, [pscustomobject]@{
                Uid = $command.Groups[1].Value
                Name = "az $($name.Groups[1].Value)"
            })
        }
    }

    function Add-Groups([string]$Folder, [string]$Indent)
    {
        $files = @(Get-ChildItem -Path $Folder -Filter '*.yml' -File |
            Where-Object { $_.Name -notin @('TOC.yml', 'reference-index.yml') })
        $dirs  = @(Get-ChildItem -Path $Folder -Directory)
        $segments = @($files | ForEach-Object { $_.BaseName }) + @($dirs | ForEach-Object { $_.Name })
        if ($Folder -eq $latest) { $segments += @($rootCommands.Keys) }
        $segments = $segments |
            Sort-Object -Unique -Culture ([cultureinfo]::InvariantCulture)

        foreach ($segment in $segments)
        {
            $file  = Join-Path $Folder "$segment.yml"
            $child = Join-Path $Folder $segment
            $header = if (Test-Path $file -PathType Leaf) { Read-GroupHeader $file } else { $null }
            $hasChildren = (Test-Path $child -PathType Container) -and
                @(Get-ChildItem -Path $child -Recurse -Filter '*.yml' -File).Count -gt 0

            if ($Folder -eq $latest -and $rootCommands.ContainsKey($segment))
            {
                if ($header -or $hasChildren) { throw "Root command '$segment' conflicts with a generated group." }
                $header = $rootCommands[$segment]
            }
            $lines.Add("$Indent- name: $segment")
            if ($hasChildren)
            {
                $lines.Add("$Indent  items:")
                if ($header)
                {
                    $lines.Add("$Indent  - name: Commands")
                    $lines.Add("$Indent    uid: $($header.Uid)")
                    $lines.Add("$Indent    displayName: $($header.Name)")
                }
                Add-Groups -Folder $child -Indent "$Indent  "
            }
            elseif ($header)
            {
                $lines.Add("$Indent  uid: $($header.Uid)")
                $lines.Add("$Indent  displayName: $($header.Name)")
            }
            $lines.Add("$Indent  no-loc:")
            $lines.Add("$Indent  - $segment")
        }
    }

    $lines.Add('- name: CLI reference')
    $lines.Add('  displayName: CLI reference')
    $lines.Add('  items:')
    if (Test-Path $index)
    {
        $lines.Add('  - name: List A - Z')
        $lines.Add("    uid: $((Read-GroupHeader $index).Uid)")
        $lines.Add('    displayName: List A - Z')
    }
    Add-Groups -Folder $latest -Indent '  '

    [IO.File]::WriteAllText($tocPath, ($lines -join $newline) + $newline, [Text.UTF8Encoding]::new($false))
    Write-Host "  → Wrote command-group TOC.yml ($($lines.Count) lines)" -ForegroundColor DarkGray
}

# ── Target-repo helpers ─────────────────────────────────────────────────────────

# Returns @() when target is ready; otherwise an array of human-readable problems.
function Test-TargetReady
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$TargetRepoPath,
        [Parameter(Mandatory)][string]$VersionFolder
    )

    if (-not (Test-Path $TargetRepoPath)) {
        "Target repo path '$TargetRepoPath' does not exist."
        return
    }

    $gitDir = git -C $TargetRepoPath rev-parse --git-dir 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $gitDir) {
        "Target '$TargetRepoPath' is not a git repository."
        return
    }

    $dirty = git -C $TargetRepoPath status --porcelain 2>$null
    if ($dirty) {
        "Target working tree is dirty. Commit, stash, or discard changes before syncing:`n$($dirty -join "`n")"
    }

    $versionPath = Join-Path (Join-Path $TargetRepoPath 'docs-ref-autogen') $VersionFolder
    if (-not (Test-Path $versionPath)) {
        "Target version folder '$VersionFolder' does not exist under '$TargetRepoPath/docs-ref-autogen/'. Config PR to register the moniker may not be merged yet."
    }
}

# Composes the full CLI2Yaml output path inside the target repo:
# <TargetRepoPath>/docs-ref-autogen/<VersionFolder>. CLI2Yaml writes a `latest/` subdir underneath.
function Get-TargetVersionFolder
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$TargetRepoPath,
        [Parameter(Mandatory)][string]$VersionFolder
    )
    return Join-Path (Join-Path $TargetRepoPath 'docs-ref-autogen') $VersionFolder
}

# Clears the moniker's `latest/` subfolder before writing new content. Matches the
# offshore pipeline's `Get-ChildItem ... | Remove-Item -Recurse` step.
function Clear-TargetLatest
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$TargetVersionFolder
    )

    $latestPath = Join-Path $TargetVersionFolder 'latest'
    if (Test-Path $latestPath)
    {
        Get-ChildItem $latestPath -Recurse -File | Remove-Item -Force
        Get-ChildItem $latestPath -Recurse -Directory | Sort-Object FullName -Descending | Remove-Item -Force
    }
}

# Creates a sync branch, commits everything under docs-ref-autogen/<VersionFolder>/, returns
# a summary object. Assumes Test-TargetReady already passed.
function Invoke-SyncApply
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$TargetRepoPath,
        [Parameter(Mandatory)][string]$VersionFolder,
        [Parameter(Mandatory)][string]$Moniker,
        [Parameter(Mandatory)][string]$Timestamp,
        [string]$CommitAuthor,
        [string]$CommitEmail
    )

    $branchName = "azclips-cli-sync-$Timestamp"
    $commitMessage = "AzClips-CLI docs sync -- $Moniker -- $Timestamp"

    $branchCheck = git -C $TargetRepoPath rev-parse --verify --quiet "refs/heads/$branchName" 2>$null
    if ($LASTEXITCODE -eq 0 -and $branchCheck)
    {
        throw "Branch '$branchName' already exists in target. Delete it (git -C '$TargetRepoPath' branch -D $branchName) or pass a different -Timestamp."
    }

    # Capture baseline for deterministic rollback (Test-TargetReady guarantees a clean tree here).
    $originalBranch = (git -C $TargetRepoPath rev-parse --abbrev-ref HEAD 2>$null).Trim()
    $originalHead   = (git -C $TargetRepoPath rev-parse HEAD 2>$null).Trim()

    git -C $TargetRepoPath checkout -b $branchName 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "git checkout -b '$branchName' failed." }

    try {
        $versionPath = Join-Path 'docs-ref-autogen' $VersionFolder
        git -C $TargetRepoPath add -A -- $versionPath 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "git add -A -- '$versionPath' failed." }

        $staged = git -C $TargetRepoPath diff --cached --name-only -- $versionPath 2>$null
        if (-not $staged)
        {
            git -C $TargetRepoPath checkout - 2>&1 | Out-Null
            git -C $TargetRepoPath branch -D $branchName 2>&1 | Out-Null
            return [PSCustomObject]@{
                Branch        = $null
                CommitSha     = $null
                CommitMessage = $null
                FileCount     = 0
                NoChanges     = $true
            }
        }

        # Compose commit args, injecting inline user.name / user.email if provided.
        # Inline `-c` is preferred over persistent config so we don't mutate the target repo.
        $commitArgs = @('-C', $TargetRepoPath)
        if ($CommitAuthor) { $commitArgs += @('-c', "user.name=$CommitAuthor") }
        if ($CommitEmail)  { $commitArgs += @('-c', "user.email=$CommitEmail") }
        $commitArgs += @('commit', '-m', $commitMessage)
        git @commitArgs 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "git commit failed." }

        $commitSha = git -C $TargetRepoPath rev-parse HEAD 2>$null
        $fileCount = ($staged | Measure-Object).Count

        [PSCustomObject]@{
            Branch        = $branchName
            CommitSha     = $commitSha.Trim()
            CommitMessage = $commitMessage
            FileCount     = $fileCount
            NoChanges     = $false
        }
    }
    catch {
        # Deterministic rollback: reset any staged/working-tree changes to the pre-run baseline,
        # then switch back to the caller's branch and drop the sync branch.
        git -C $TargetRepoPath reset --hard $originalHead 2>&1 | Out-Null
        if ($originalBranch -and $originalBranch -ne 'HEAD')
        {
            git -C $TargetRepoPath checkout $originalBranch 2>&1 | Out-Null
        }
        else
        {
            git -C $TargetRepoPath checkout - 2>&1 | Out-Null
        }
        git -C $TargetRepoPath branch -D $branchName 2>&1 | Out-Null
        throw
    }
}

# ── Reporting ───────────────────────────────────────────────────────────────────

function Write-DryRunReport
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ScratchOutput,
        [Parameter(Mandatory)][string]$Moniker,
        [Parameter(Mandatory)][string]$VersionFolder,
        [Parameter(Mandatory)][string]$SourceRepoUrl,
        [Parameter(Mandatory)][string]$SourceBranch
    )

    $files = @(Get-ChildItem $ScratchOutput -Recurse -File)
    $ymlFiles = @($files | Where-Object Extension -eq '.yml')

    Write-Host ''
    Write-Host '=== AzClips CLI docs sync — dry-run report ===' -ForegroundColor Cyan
    Write-Host ''
    Write-Host "Moniker              : $Moniker"
    Write-Host "Version folder       : $VersionFolder"
    Write-Host "editLink source repo : $SourceRepoUrl"
    Write-Host "editLink branch      : $SourceBranch"
    Write-Host "Files produced       : $($files.Count) ($($ymlFiles.Count) yml)"
    Write-Host "Output location      : $ScratchOutput"
    Write-Host ''

    Write-Host 'Top-level files:' -ForegroundColor Cyan
    $files | Where-Object { $_.DirectoryName -eq (Join-Path $ScratchOutput 'latest') } | Sort-Object Name | ForEach-Object {
        Write-Host ("  {0,-40}  {1,10:N0} bytes" -f $_.Name, $_.Length)
    }

    # editLink sanity: sample the first 3 to confirm the override applied.
    $editLinks = @(Get-ChildItem (Join-Path $ScratchOutput 'latest') -Filter '*.yml' -File |
        Select-String -Pattern '^\s*editLink:' |
        Select-Object -First 3 |
        ForEach-Object { $_.Line.Trim() })

    if ($editLinks.Count -gt 0)
    {
        Write-Host ''
        Write-Host 'editLink sanity (first 3):' -ForegroundColor Cyan
        $editLinks | ForEach-Object { Write-Host "  $_" }

        $expectedPrefix = "editLink: $SourceRepoUrl/blob/$SourceBranch/"
        $wrongLinks = @($editLinks | Where-Object { -not $_.StartsWith($expectedPrefix) })
        if ($wrongLinks.Count -gt 0)
        {
            Write-Host ''
            Write-Host "warning: some editLinks do NOT start with '$expectedPrefix'. Override may not have applied." -ForegroundColor Yellow
        }
    }

    Write-Host ''
    Write-Host '(No files written to a target repo. Pass -Force to write to the local target. Pass -Push to also open a PR — currently DEFERRED, will fail.)' -ForegroundColor DarkGray
}

# ── Machine-readable manifest ───────────────────────────────────────────────────

# Builds a hashtable describing the run outcome. Callers write it to disk as JSON when
# -OutputManifest is set — feeds CI-side PR-body generation, artifact upload, downstream
# steps. Shape is deliberately flat; add keys additively (don't rename existing ones).
function New-SyncManifest
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('dry-run', 'force')][string]$Mode,
        [Parameter(Mandatory)][string]$Moniker,
        [Parameter(Mandatory)][string]$VersionFolder,
        [Parameter(Mandatory)][string]$SourceRepoUrl,
        [Parameter(Mandatory)][string]$SourceBranch,
        [Parameter(Mandatory)][string]$OutputLocation,
        [Parameter(Mandatory)][string]$Cli2YamlDll,
        [PSCustomObject]$ApplyResult
    )

    $files = @(Get-ChildItem $OutputLocation -Recurse -File -ErrorAction SilentlyContinue)
    $ymlFiles = @($files | Where-Object Extension -eq '.yml')
    $topLevel = @(
        $files
        | Where-Object { $_.DirectoryName -eq (Join-Path $OutputLocation 'latest') }
        | Sort-Object Name
        | ForEach-Object { $_.Name }
    )

    $editLinkPrefix = "$SourceRepoUrl/blob/$SourceBranch/"
    $allEditLinks = @(Get-ChildItem (Join-Path $OutputLocation 'latest') -Filter '*.yml' -File -ErrorAction SilentlyContinue |
        Select-String -Pattern '^\s*editLink:' |
        ForEach-Object { $_.Line.Trim() })
    $editLinkAllMatch = $allEditLinks.Count -eq 0 -or -not @($allEditLinks | Where-Object { -not $_.StartsWith("editLink: $editLinkPrefix") })

    $manifest = [ordered]@{
        mode              = $Mode
        moniker           = $Moniker
        versionFolder     = $VersionFolder
        sourceRepoUrl     = $SourceRepoUrl
        sourceBranch      = $SourceBranch
        cli2yamlDll       = $Cli2YamlDll
        cli2yamlVersion   = (Split-Path (Split-Path $Cli2YamlDll -Parent) -Parent | Split-Path -Leaf)
        outputLocation    = $OutputLocation
        filesProduced     = $files.Count
        ymlProduced       = $ymlFiles.Count
        topLevelFiles     = $topLevel
        editLinkPrefix    = $editLinkPrefix
        editLinkAllMatch  = [bool]$editLinkAllMatch
        editLinkSampled   = $allEditLinks.Count
        generatedAt       = ([DateTime]::UtcNow).ToString('o')
    }

    if ($Mode -eq 'force' -and $ApplyResult)
    {
        $manifest.branch         = $ApplyResult.Branch
        $manifest.commitSha      = $ApplyResult.CommitSha
        $manifest.commitMessage  = $ApplyResult.CommitMessage
        $manifest.commitFileCount = $ApplyResult.FileCount
        $manifest.noChanges      = [bool]$ApplyResult.NoChanges
    }

    return [PSCustomObject]$manifest
}

function Write-SyncManifest
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][PSCustomObject]$Manifest
    )

    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path $dir))
    {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    $Manifest | ConvertTo-Json -Depth 6 | Set-Content -Path $Path -Encoding utf8 -NoNewline
    Write-Host "Wrote manifest → $Path" -ForegroundColor DarkGray
}

# ── Main ────────────────────────────────────────────────────────────────────────

if ($__loadOnly) { return }

if ($Push)
{
    throw "-Push is not yet implemented. docs.ms bot-auth pattern is still pending decision. Use -Force to write to the local target repo only, then hand off to your normal PR flow."
}

# Resolve CLI2Yaml first — cheap check that fails fast on unresolved package.
$cli2yamlDll = Get-Cli2YamlDllPath -PackageName $config.cli2yamlPackage -Feed $config.cli2yamlFeed -FetchIfMissing:$FetchIfMissing
Write-Host "CLI2Yaml : $cli2yamlDll" -ForegroundColor DarkGray

# Build the solution so exporter reflects over up-to-date DLLs. CI typically has already
# built in a preceding step and passes -NoBuild to skip this.
if ($NoBuild)
{
    Write-Host "Skipping build ($Configuration) — assuming caller has already built." -ForegroundColor DarkGray
}
else
{
    Write-Host "Building azclips.slnx ($Configuration) ..." -ForegroundColor Cyan
    Push-Location $repoRoot
    try {
        $buildOutput = & dotnet build azclips.slnx --configuration $Configuration --nologo -clp:ErrorsOnly 2>&1
        if ($LASTEXITCODE -ne 0)
        {
            throw "dotnet build failed with exit $LASTEXITCODE. Output:`n$($buildOutput -join "`n")"
        }
        $buildOutput | Select-Object -Last 3 | ForEach-Object { Write-Host "  → $_" }
    }
    finally {
        Pop-Location
    }
}

# Stage 1: exporter → JSON
$exporterOutput = Invoke-CliExporter -RepoRoot $repoRoot -ExportProject $config.exportProject -Configuration $Configuration

# Stage 2: assemble CLI2Yaml workspace + commit map
$workspace = New-Cli2YamlWorkspace -RepoRoot $repoRoot -ExporterOutput $exporterOutput

# Resolve absolute paths for side-cars — CLI2Yaml is picky about relative paths.
$docsReferenceConfig = (Resolve-Path (Join-Path $repoRoot $config.docsReferenceConfig)).Path
$extensionIndex      = (Resolve-Path (Join-Path $repoRoot $config.extensionIndex)).Path
$serviceMapping      = (Resolve-Path (Join-Path $repoRoot $config.serviceMapping)).Path

if ($Force)
{
    $problems = @(Test-TargetReady -TargetRepoPath $effectiveTargetRepoPath -VersionFolder $effectiveVersionFolder)
    if ($problems.Count -gt 0)
    {
        foreach ($p in $problems) { Write-Host "  ✗ $p" -ForegroundColor Red }
        throw "Target repo is not ready for sync. See errors above."
    }

    $targetVersionFolder = Get-TargetVersionFolder -TargetRepoPath $effectiveTargetRepoPath -VersionFolder $effectiveVersionFolder

    # Capture pre-mutation baseline so any failure between here and Invoke-SyncApply
    # (Clear-TargetLatest, CLI2Yaml, Remove-ServicePageOutput) can hard-reset the target.
    $targetOriginalBranch = (git -C $effectiveTargetRepoPath rev-parse --abbrev-ref HEAD 2>$null).Trim()
    $targetOriginalHead   = (git -C $effectiveTargetRepoPath rev-parse HEAD 2>$null).Trim()

    try {
        Clear-TargetLatest -TargetVersionFolder $targetVersionFolder

        Invoke-Cli2Yaml `
            -Cli2YamlDll $cli2yamlDll `
            -Workspace $workspace `
            -OutputVersionFolder $targetVersionFolder `
            -DocsReferenceConfig $docsReferenceConfig `
            -ExtensionIndex $extensionIndex `
            -ServiceMapping $serviceMapping `
            -SourceRepoUrl $effectiveSourceRepoUrl `
            -SourceBranch $effectiveSourceBranch

        Repair-CommandGroupUidCollisions -OutputVersionFolder $targetVersionFolder
        if ($SkipServicePages)
        {
            Remove-ServicePageOutput -OutputVersionFolder $targetVersionFolder
            Write-CommandGroupToc -OutputVersionFolder $targetVersionFolder
        }

        $effectiveTimestamp = if ($Timestamp) { $Timestamp } else { [DateTime]::UtcNow.ToString('yyyyMMddHHmm') }
        Write-Host ''
        Write-Host "Applying CLI docs to $effectiveTargetRepoPath ..." -ForegroundColor Cyan
        $applyResult = Invoke-SyncApply `
            -TargetRepoPath $effectiveTargetRepoPath `
            -VersionFolder $effectiveVersionFolder `
            -Moniker $effectiveMoniker `
            -Timestamp $effectiveTimestamp `
            -CommitAuthor $CommitAuthor `
            -CommitEmail $CommitEmail
    }
    catch {
        Write-Host "Sync failed; rolling target back to $targetOriginalHead ..." -ForegroundColor Yellow
        git -C $effectiveTargetRepoPath reset --hard $targetOriginalHead 2>&1 | Out-Null
        # -fd removes untracked files+dirs (e.g. YAML that CLI2Yaml wrote before failing).
        # Test-TargetReady required a clean tree at entry, so nothing developer-authored is at risk.
        git -C $effectiveTargetRepoPath clean -fd 2>&1 | Out-Null
        if ($targetOriginalBranch -and $targetOriginalBranch -ne 'HEAD')
        {
            git -C $effectiveTargetRepoPath checkout $targetOriginalBranch 2>&1 | Out-Null
        }
        throw
    }

    Write-Host ''
    if ($applyResult.NoChanges)
    {
        Write-Host '=== No changes to sync ===' -ForegroundColor Yellow
        Write-Host "Target already matches AzClips CLI output (moniker: $effectiveMoniker). Nothing committed." -ForegroundColor DarkGray
    }
    else
    {
        Write-Host '=== Sync applied ===' -ForegroundColor Cyan
        Write-Host "Branch      : $($applyResult.Branch)"
        Write-Host "Commit      : $($applyResult.CommitSha)"
        Write-Host "Message     : $($applyResult.CommitMessage)"
        Write-Host "Files       : $($applyResult.FileCount)"
        Write-Host ''
        Write-Host "(Nothing pushed. Inspect with: git -C '$effectiveTargetRepoPath' log $($applyResult.Branch) --stat)" -ForegroundColor DarkGray
    }

    if ($OutputManifest)
    {
        $manifest = New-SyncManifest -Mode 'force' -Moniker $effectiveMoniker -VersionFolder $effectiveVersionFolder `
            -SourceRepoUrl $effectiveSourceRepoUrl -SourceBranch $effectiveSourceBranch `
            -OutputLocation $targetVersionFolder -Cli2YamlDll $cli2yamlDll `
            -ApplyResult $applyResult
        Write-SyncManifest -Path $OutputManifest -Manifest $manifest
    }
    return
}

# Dry-run: write to a scratch folder inside artifacts/ and report on the shape.
$scratchOutput = Join-Path $repoRoot 'artifacts/cli-yaml/dry-run'
Remove-Item -Recurse -Force $scratchOutput -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $scratchOutput -Force | Out-Null

Invoke-Cli2Yaml `
    -Cli2YamlDll $cli2yamlDll `
    -Workspace $workspace `
    -OutputVersionFolder $scratchOutput `
    -DocsReferenceConfig $docsReferenceConfig `
    -ExtensionIndex $extensionIndex `
    -ServiceMapping $serviceMapping `
    -SourceRepoUrl $effectiveSourceRepoUrl `
    -SourceBranch $effectiveSourceBranch

Repair-CommandGroupUidCollisions -OutputVersionFolder $scratchOutput
if ($SkipServicePages)
{
    Remove-ServicePageOutput -OutputVersionFolder $scratchOutput
    Write-CommandGroupToc -OutputVersionFolder $scratchOutput
}

Write-DryRunReport -ScratchOutput $scratchOutput -Moniker $effectiveMoniker -VersionFolder $effectiveVersionFolder -SourceRepoUrl $effectiveSourceRepoUrl -SourceBranch $effectiveSourceBranch

if ($OutputManifest)
{
    $manifest = New-SyncManifest -Mode 'dry-run' -Moniker $effectiveMoniker -VersionFolder $effectiveVersionFolder `
        -SourceRepoUrl $effectiveSourceRepoUrl -SourceBranch $effectiveSourceBranch `
        -OutputLocation $scratchOutput -Cli2YamlDll $cli2yamlDll
    Write-SyncManifest -Path $OutputManifest -Manifest $manifest
}
