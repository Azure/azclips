# Syncs AzClips PowerShell PlatyPS markdown into the destination docs repo
# (MicrosoftDocs/azure-docs-powershell). Three-stage escape hatch: dry-run (default; no flag)
# / -Force (write to local target) / -Push (deferred until docs.ms bot-auth is decided).
#
# Usage:
#   pwsh -File build/Sync-AzclipsPowerShellDocs.ps1               # dry-run against sibling clone (default)
#   pwsh -File build/Sync-AzclipsPowerShellDocs.ps1 -Force         # writes to local target; no push
#   pwsh -File build/Sync-AzclipsPowerShellDocs.ps1 -Push          # WILL FAIL — deferred until auth-pattern decided

[CmdletBinding()]
param(
    [string]$ConfigPath,
    [string]$Moniker,
    [string]$TargetRepoPath,
    [string[]]$Module,
    [string]$Timestamp,
    [switch]$Force,
    [switch]$Push
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'Get-AzPackageName.ps1')

# Load-only mode: tests set this to $env:AZCLIPS_PS_SYNC_LOAD_ONLY=1 to dot-source the
# functions without running config-load or the main body.
$__loadOnly = $env:AZCLIPS_PS_SYNC_LOAD_ONLY -eq '1'

# ── Load config ──────────────────────────────────────────────────────────────────

if (-not $__loadOnly) {
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    if (-not $ConfigPath) { $ConfigPath = Join-Path $PSScriptRoot 'ps-docs-sync.config.json' }

    if (-not (Test-Path $ConfigPath))
    {
        throw "Config not found at $ConfigPath. Copy build/ps-docs-sync.config.json to that path or pass -ConfigPath."
    }

    $config = Get-Content -Raw -Path $ConfigPath | ConvertFrom-Json

    # Env var wins over config for moniker. CLI param wins over both.
    $effectiveMoniker = $Moniker
    if (-not $effectiveMoniker) { $effectiveMoniker = $env:AZCLIPS_PS_MONIKER }
    if (-not $effectiveMoniker) { $effectiveMoniker = $config.moniker }
    if ([string]::IsNullOrWhiteSpace($effectiveMoniker))
    {
        throw "Moniker is not set. Pass -Moniker, set `$env:AZCLIPS_PS_MONIKER, or edit the 'moniker' field in $ConfigPath."
    }
    if ($effectiveMoniker -eq 'azclipsps-preview')
    {
        Write-Host "warning: moniker is '$effectiveMoniker' — the placeholder default. Set AZCLIPS_PS_MONIKER or edit $ConfigPath before a real sync." -ForegroundColor Yellow
    }

    $effectiveTargetRepoPath = $TargetRepoPath
    if (-not $effectiveTargetRepoPath) { $effectiveTargetRepoPath = $config.targetRepoPath }
    $effectiveTargetRepoPath = if ([System.IO.Path]::IsPathRooted($effectiveTargetRepoPath)) {
        $effectiveTargetRepoPath
    } else {
        Join-Path $repoRoot $effectiveTargetRepoPath
    }

    $sourcePsRoot = Join-Path $repoRoot $config.sourcePsRoot
}

# ── Discovery ────────────────────────────────────────────────────────────────────

# Returns [PSCustomObject]s for every module that has BOTH `<Name>.psd1` AND a non-empty
# `help/` folder. Filters like `Common` (shared lib) and `Az` (rollup without help/) out.
function Get-PublishableModule
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$PsRoot
    )

    if (-not (Test-Path $PsRoot))
    {
        throw "PS source root not found at $PsRoot"
    }

    # Plain foreach + explicit output list — the previous ForEach-Object pipeline pattern
    # can be misinterpreted by Pester's internal wrapping on error, producing a
    # "break/continue with mismatched label" secondary failure (pester/Pester#2669).
    $results = @()
    foreach ($dir in Get-ChildItem -Path $PsRoot -Directory)
    {
        $psd1 = Join-Path $dir.FullName "$($dir.Name).psd1"
        $helpDir = Join-Path $dir.FullName 'help'
        $helpFiles = if (Test-Path $helpDir) {
            @(Get-ChildItem -Path $helpDir -Filter '*.md' -File)
        } else {
            @()
        }

        if ((Test-Path $psd1) -and $helpFiles.Count -gt 0)
        {
            $results += [PSCustomObject]@{
                Name      = $dir.Name
                SourceDir = $dir.FullName
                Psd1Path  = $psd1
                HelpDir   = $helpDir
                HelpFiles = $helpFiles
            }
        }
    }
    $results
}

# ── Module index generation ─────────────────────────────────────────────────────

# Reads a cmdlet md file and returns the first non-empty line under `## SYNOPSIS`.
# Returns empty string when SYNOPSIS is missing (index still emits a link, just no summary).
function Get-CmdletSynopsis
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path
    )

    # Avoids break/continue inside the foreach — some Pester versions warn on those under
    # PS 7 and mis-report the whole Describe as failed. See pester/Pester#2669.
    $inSynopsis = $false
    foreach ($line in Get-Content -Path $Path)
    {
        if ($inSynopsis -and $line -match '^## ') { return '' }
        if ($line -match '^## SYNOPSIS\s*$')
        {
            $inSynopsis = $true
        }
        elseif ($inSynopsis)
        {
            $trimmed = $line.Trim()
            if ($trimmed) { return $trimmed }
        }
    }
    return ''
}

# Reads a .psd1 via Import-PowerShellDataFile (no code execution) and returns a hashtable
# of the fields the module-index generator needs.
function Read-ModuleManifest
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path
    )

    $data = Import-PowerShellDataFile -Path $Path
    [PSCustomObject]@{
        ModuleVersion = if ($data.ModuleVersion) { $data.ModuleVersion } else { '0.0.0.0' }
        Guid          = if ($data.GUID) { $data.GUID } else { '00000000-0000-0000-0000-000000000000' }
        Description   = if ($data.Description) { $data.Description } else { '' }
    }
}

# Emits the target-repo module index (e.g. `Az.Compute/Az.Compute.md`). Format matches
# what docs.ms's docfx expects — see azure-docs-powershell/azps-16.1.0/Az.Accounts/Az.Accounts.md.
function New-ModuleIndexContent
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][PSCustomObject]$Module,
        [Parameter(Mandatory)][string]$TargetModuleName,
        [Parameter(Mandatory)][PSCustomObject]$Manifest,
        [Parameter(Mandatory)][PSCustomObject]$Config,
        [Parameter(Mandatory)][string]$RepoRootRelativeHelpDir
    )

    $downloadHelpLink = "$($Config.downloadHelpLinkBase.TrimEnd('/'))/$($TargetModuleName.ToLowerInvariant())"
    # Module-index is generated at sync time (no `<TargetModuleName>.md` exists in-tree);
    # point at the help/ directory so docfx's "Edit this page" lands on the real source folder.
    $sourceUrl = "$($Config.sourceRepoUrl)/tree/$($Config.sourceBranch)/$RepoRootRelativeHelpDir"

    # Explicit LF via .Append+"`n" avoids StringBuilder.AppendLine()'s Environment.NewLine
    # emitting CRLF on Windows (mixing with the trailing LF and PlatyPS-source conventions).
    $lines = @(
        '---'
        "Module Name: $TargetModuleName"
        "Module Guid: $($Manifest.Guid)"
        "Download Help Link: $downloadHelpLink"
        "Help Version: $($Manifest.ModuleVersion)"
        'Locale: en-US'
        "content_git_url: $sourceUrl"
        "original_content_git_url: $sourceUrl"
        '---'
        ''
        "# $TargetModuleName Module"
        '## Description'
        $(if ($Manifest.Description) { $Manifest.Description } else { "TODO: fill in $TargetModuleName module description." })
        ''
        "## $TargetModuleName Cmdlets"
    )

    # Pre-sort to avoid the pipeline inside foreach — some Pester wrappers turn that into a
    # break/continue with mismatched label during BeforeAll cleanup on failure.
    $sortedHelpFiles = @($Module.HelpFiles | Sort-Object Name)
    foreach ($helpFile in $sortedHelpFiles)
    {
        $cmdletName = [System.IO.Path]::GetFileNameWithoutExtension($helpFile.Name)
        $summary = Get-CmdletSynopsis -Path $helpFile.FullName
        $lines += "### [$cmdletName]($cmdletName.md)"
        if ($summary) {
            $lines += $summary
        } else {
            $lines += '(No SYNOPSIS.)'
        }
        $lines += ''
    }

    return ($lines -join "`n").TrimEnd("`n") + "`n"
}

# ── Plan ────────────────────────────────────────────────────────────────────────

# Copies a cmdlet PlatyPS .md file, rewriting the frontmatter `Module Name:` from the
# source module name (bare, e.g. 'Config') to the packaged name ('Az.Config'). Docs.ms's
# PSMD2Yaml prebuild requires the module name in the frontmatter to match the folder;
# leaving it bare fails with PSMD2Yaml_FileProcessFailed.
function Copy-CmdletMarkdownWithRewrite
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$SourcePath,
        [Parameter(Mandatory)][string]$TargetPath,
        [Parameter(Mandatory)][string]$TargetModuleName
    )

    $content = Get-Content -LiteralPath $SourcePath -Raw
    $frontmatter = [regex]::Match($content, '(?ms)\A---\r?\n.*?^---(?:\r?\n|\z)')
    $moduleNamePattern = [regex]'(?m)^Module Name:[^\r\n]*'
    if (-not $frontmatter.Success -or $moduleNamePattern.Matches($frontmatter.Value).Count -ne 1)
    {
        throw "Expected YAML frontmatter with exactly one 'Module Name:' field in '$SourcePath'."
    }

    $rewritten = $moduleNamePattern.Replace($frontmatter.Value, { "Module Name: $TargetModuleName" }, 1) +
        $content.Substring($frontmatter.Length)
    Set-Content -LiteralPath $TargetPath -Value $rewritten -NoNewline
}

# Builds the copy-plan — one entry per file that would land in the target. Pure function,
# no filesystem writes. Consumed by both dry-run reporting and the Force/Push stages.
# Emits each entry to the pipeline (caller wraps with @() to collect a flat array).
function New-SyncPlan
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][PSCustomObject[]]$Modules,
        [Parameter(Mandatory)][string]$Moniker,
        [Parameter(Mandatory)][string]$TargetRepoPath,
        [Parameter(Mandatory)][PSCustomObject]$Config,
        [Parameter(Mandatory)][string]$RepoRoot
    )

    foreach ($module in $Modules)
    {
        $targetName = Get-AzPackageName -SourceModule $module.Name
        $targetModuleDir = Join-Path (Join-Path $TargetRepoPath $Moniker) $targetName

        foreach ($helpFile in $module.HelpFiles)
        {
            [PSCustomObject]@{
                Kind         = 'CmdletMarkdown'
                SourcePath   = $helpFile.FullName
                TargetPath   = Join-Path $targetModuleDir $helpFile.Name
                ModuleName   = $module.Name
                TargetModule = $targetName
            }
        }

        $manifest = Read-ModuleManifest -Path $module.Psd1Path
        $relativeHelpDir = ([System.IO.Path]::GetRelativePath($RepoRoot, $module.HelpDir)) -replace '\\', '/'
        $indexContent = New-ModuleIndexContent -Module $module -TargetModuleName $targetName `
            -Manifest $manifest -Config $Config -RepoRootRelativeHelpDir $relativeHelpDir

        [PSCustomObject]@{
            Kind          = 'ModuleIndex'
            SourcePath    = '(generated at sync time)'
            TargetPath    = Join-Path $targetModuleDir "$targetName.md"
            ModuleName    = $module.Name
            TargetModule  = $targetName
            GeneratedBody = $indexContent
        }
    }
}

# ── Force-mode helpers ──────────────────────────────────────────────────────────

# Returns @() when target is ready; otherwise an array of human-readable problems. Callers
# either display + throw (real run) or assert against (tests). Emits via pipeline.
function Test-TargetReady
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$TargetRepoPath,
        [Parameter(Mandatory)][string]$Moniker
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

    $monikerPath = Join-Path $TargetRepoPath $Moniker
    if (-not (Test-Path $monikerPath)) {
        "Target moniker folder '$Moniker' does not exist under '$TargetRepoPath'. docs.ms desk-ticket onboarding is likely not yet completed."
    }
}

# Executes the plan against the target repo. Assumes Test-TargetReady already passed.
# Emits an [PSCustomObject] with the branch name + commit SHA for reporting/asserts.
function Invoke-SyncApply
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][PSCustomObject[]]$Plan,
        [Parameter(Mandatory)][string]$TargetRepoPath,
        [Parameter(Mandatory)][string]$Moniker,
        [Parameter(Mandatory)][string]$Timestamp
    )

    $branchName = "azclips-ps-sync-$Timestamp"
    $commitMessage = "AzClips-PS docs sync -- $Moniker -- $Timestamp"

    $branchCheck = git -C $TargetRepoPath rev-parse --verify --quiet "refs/heads/$branchName" 2>$null
    if ($LASTEXITCODE -eq 0 -and $branchCheck) {
        throw "Branch '$branchName' already exists in target. Delete it first (git -C '$TargetRepoPath' branch -D $branchName) or pass a different -Timestamp."
    }

    git -C $TargetRepoPath checkout -b $branchName 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "git checkout -b '$branchName' failed." }

    try {
        foreach ($entry in $Plan) {
            $targetDir = Split-Path -Parent $entry.TargetPath
            if (-not (Test-Path $targetDir)) {
                New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
            }

            switch ($entry.Kind) {
                'CmdletMarkdown' {
                    Copy-CmdletMarkdownWithRewrite -SourcePath $entry.SourcePath -TargetPath $entry.TargetPath -TargetModuleName $entry.TargetModule
                }
                'ModuleIndex' {
                    Set-Content -LiteralPath $entry.TargetPath -Value $entry.GeneratedBody -NoNewline
                }
                default {
                    throw "Unknown plan-entry kind: $($entry.Kind)"
                }
            }
        }

        git -C $TargetRepoPath add -A 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "git add -A failed." }

        git -C $TargetRepoPath commit -m $commitMessage 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "git commit failed." }

        $commitSha = git -C $TargetRepoPath rev-parse HEAD 2>$null

        [PSCustomObject]@{
            Branch        = $branchName
            CommitSha     = $commitSha.Trim()
            CommitMessage = $commitMessage
            FileCount     = $Plan.Count
        }
    }
    catch {
        # Best-effort rollback: switch back off the sync branch so re-runs aren't blocked by
        # a half-applied working tree. Don't delete the branch — leaves state visible for
        # post-mortem debugging.
        git -C $TargetRepoPath checkout - 2>&1 | Out-Null
        throw
    }
}

# ── Reporting ───────────────────────────────────────────────────────────────────

function Write-DryRunReport
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][PSCustomObject[]]$Modules,
        [Parameter(Mandatory)][PSCustomObject[]]$Plan,
        [Parameter(Mandatory)][string]$Moniker,
        [Parameter(Mandatory)][string]$TargetRepoPath
    )

    Write-Host ''
    Write-Host '=== AzClips PS docs sync — dry-run report ===' -ForegroundColor Cyan
    Write-Host ''
    Write-Host "Moniker         : $Moniker"
    Write-Host "Target repo     : $TargetRepoPath"
    Write-Host "Publishable     : $($Modules.Count) module(s) — $(($Modules | ForEach-Object Name) -join ', ')"
    Write-Host "Total files     : $($Plan.Count) ($((@($Plan | Where-Object Kind -eq 'CmdletMarkdown')).Count) cmdlet md + $((@($Plan | Where-Object Kind -eq 'ModuleIndex')).Count) module-index)"
    Write-Host ''
    Write-Host 'Copy plan:' -ForegroundColor Cyan

    foreach ($group in $Plan | Group-Object TargetModule | Sort-Object Name)
    {
        Write-Host ''
        Write-Host "  $($group.Name)/" -ForegroundColor Yellow
        foreach ($entry in $group.Group | Sort-Object { $_.Kind + $_.TargetPath })
        {
            $target = [System.IO.Path]::GetFileName($entry.TargetPath)
            $source = if ($entry.Kind -eq 'ModuleIndex') { '(generated)' } else { [System.IO.Path]::GetFileName($entry.SourcePath) }
            Write-Host ("    {0,-45}  <-  {1}" -f $target, $source)
        }
    }

    if (-not (Test-Path $TargetRepoPath))
    {
        Write-Host ''
        Write-Host "warning: target repo path '$TargetRepoPath' does not exist. Point -TargetRepoPath at a sibling clone or update the config." -ForegroundColor Yellow
    }
    elseif (-not (Test-Path (Join-Path $TargetRepoPath $Moniker)))
    {
        Write-Host ''
        Write-Host "warning: target moniker folder '$Moniker' does not exist under $TargetRepoPath — docs.ms desk-ticket onboarding likely not yet completed." -ForegroundColor Yellow
    }

    Write-Host ''
    Write-Host '(No files written. Pass -Force to write to the local target repo. Pass -Push to also open a PR — currently DEFERRED, will fail.)' -ForegroundColor DarkGray
}

# ── Main ────────────────────────────────────────────────────────────────────────

if ($__loadOnly) { return }

$modules = @(Get-PublishableModule -PsRoot $sourcePsRoot)

if ($Module)
{
    $modules = @($modules | Where-Object { $_.Name -in $Module })
    if ($modules.Count -eq 0)
    {
        throw "No publishable module matched -Module $($Module -join ', '). Discovered: $((Get-PublishableModule -PsRoot $sourcePsRoot).Name -join ', ')"
    }
}

$plan = @(New-SyncPlan -Modules $modules -Moniker $effectiveMoniker -TargetRepoPath $effectiveTargetRepoPath `
    -Config $config -RepoRoot $repoRoot)

if ($Push)
{
    throw "-Push is not yet implemented. docs.ms bot-auth pattern is still pending decision. Use -Force to write to the local target repo only, then hand off to your normal PR flow."
}

if ($Force)
{
    $problems = @(Test-TargetReady -TargetRepoPath $effectiveTargetRepoPath -Moniker $effectiveMoniker)
    if ($problems.Count -gt 0) {
        foreach ($p in $problems) { Write-Host "  ✗ $p" -ForegroundColor Red }
        throw "Target repo is not ready for sync. See errors above."
    }

    $effectiveTimestamp = if ($Timestamp) { $Timestamp } else { [DateTime]::UtcNow.ToString('yyyyMMddHHmm') }
    Write-Host ""
    Write-Host "Applying $($plan.Count) file(s) to $effectiveTargetRepoPath ..." -ForegroundColor Cyan
    $result = Invoke-SyncApply -Plan $plan -TargetRepoPath $effectiveTargetRepoPath -Moniker $effectiveMoniker -Timestamp $effectiveTimestamp

    Write-Host ""
    Write-Host "=== Sync applied ===" -ForegroundColor Cyan
    Write-Host "Branch      : $($result.Branch)"
    Write-Host "Commit      : $($result.CommitSha)"
    Write-Host "Message     : $($result.CommitMessage)"
    Write-Host "Files       : $($result.FileCount)"
    Write-Host ""
    Write-Host "(Nothing pushed. Inspect with: git -C '$effectiveTargetRepoPath' log $($result.Branch) --stat)" -ForegroundColor DarkGray
    return
}

Write-DryRunReport -Modules $modules -Plan $plan -Moniker $effectiveMoniker -TargetRepoPath $effectiveTargetRepoPath
