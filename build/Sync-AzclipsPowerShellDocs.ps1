# Syncs AzClips PowerShell PlatyPS markdown into the destination docs repo
# (MicrosoftDocs/azure-docs-powershell). Three-stage escape hatch (see -DryRun / -Force /
# -Push). Push stage is intentionally stubbed until docs.ms bot-auth is decided.
#
# Usage:
#   pwsh -File build/Sync-AzclipsPowerShellDocs.ps1               # dry-run against sibling clone
#   pwsh -File build/Sync-AzclipsPowerShellDocs.ps1 -Force         # writes to local target; no push
#   pwsh -File build/Sync-AzclipsPowerShellDocs.ps1 -Push          # WILL FAIL — deferred until auth-pattern decided
#
# See scratch/CLIPS-588-ps-sync-shim-PR-PLAN.md for design.

[CmdletBinding()]
param(
    [string]$ConfigPath,
    [string]$Moniker,
    [string]$TargetRepoPath,
    [string[]]$Module,
    [switch]$Force,
    [switch]$Push
)

$ErrorActionPreference = 'Stop'

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
    if ([string]::IsNullOrWhiteSpace($effectiveMoniker) -or $effectiveMoniker -eq 'azclipsps-preview')
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

    $targetNamePattern = $config.targetModuleNamePattern
    if ([string]::IsNullOrWhiteSpace($targetNamePattern)) { $targetNamePattern = 'Az.{ModuleName}' }

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

    Get-ChildItem -Path $PsRoot -Directory | ForEach-Object {
        $psd1 = Join-Path $_.FullName "$($_.Name).psd1"
        $helpDir = Join-Path $_.FullName 'help'
        $helpFiles = if (Test-Path $helpDir) {
            Get-ChildItem -Path $helpDir -Filter '*.md' -File
        } else {
            @()
        }

        if ((Test-Path $psd1) -and $helpFiles.Count -gt 0)
        {
            [PSCustomObject]@{
                Name       = $_.Name
                SourceDir  = $_.FullName
                Psd1Path   = $psd1
                HelpDir    = $helpDir
                HelpFiles  = $helpFiles
            }
        }
    }
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

    $inSynopsis = $false
    foreach ($line in Get-Content -Path $Path)
    {
        if ($line -match '^## SYNOPSIS\s*$')
        {
            $inSynopsis = $true
            continue
        }
        if ($inSynopsis)
        {
            if ($line -match '^## ') { break }
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

    $downloadHelpLink = "https://learn.microsoft.com/powershell/module/$($TargetModuleName.ToLowerInvariant())"
    $sourceUrl = "$($Config.sourceRepoUrl)/blob/$($Config.sourceBranch)/$RepoRootRelativeHelpDir/$TargetModuleName.md"

    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine('---')
    [void]$sb.AppendLine("Module Name: $TargetModuleName")
    [void]$sb.AppendLine("Module Guid: $($Manifest.Guid)")
    [void]$sb.AppendLine("Download Help Link: $downloadHelpLink")
    [void]$sb.AppendLine("Help Version: $($Manifest.ModuleVersion)")
    [void]$sb.AppendLine('Locale: en-US')
    [void]$sb.AppendLine("content_git_url: $sourceUrl")
    [void]$sb.AppendLine("original_content_git_url: $sourceUrl")
    [void]$sb.AppendLine('---')
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("# $TargetModuleName Module")
    [void]$sb.AppendLine('## Description')
    if ($Manifest.Description) {
        [void]$sb.AppendLine($Manifest.Description)
    } else {
        [void]$sb.AppendLine("TODO: fill in $TargetModuleName module description.")
    }
    [void]$sb.AppendLine()
    [void]$sb.AppendLine("## $TargetModuleName Cmdlets")

    foreach ($helpFile in $Module.HelpFiles | Sort-Object Name)
    {
        $cmdletName = [System.IO.Path]::GetFileNameWithoutExtension($helpFile.Name)
        $summary = Get-CmdletSynopsis -Path $helpFile.FullName
        [void]$sb.AppendLine("### [$cmdletName]($cmdletName.md)")
        if ($summary) { [void]$sb.AppendLine($summary) } else { [void]$sb.AppendLine("(No SYNOPSIS.)") }
        [void]$sb.AppendLine()
    }

    return $sb.ToString().TrimEnd() + "`n"
}

# ── Target path resolution ──────────────────────────────────────────────────────

function Resolve-TargetModuleName
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$SourceName,
        [Parameter(Mandatory)][string]$Pattern
    )
    return $Pattern.Replace('{ModuleName}', $SourceName)
}

# ── Plan ────────────────────────────────────────────────────────────────────────

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
        [Parameter(Mandatory)][string]$TargetModuleNamePattern,
        [Parameter(Mandatory)][PSCustomObject]$Config,
        [Parameter(Mandatory)][string]$RepoRoot
    )

    foreach ($module in $Modules)
    {
        $targetName = Resolve-TargetModuleName -SourceName $module.Name -Pattern $TargetModuleNamePattern
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
    -TargetModuleNamePattern $targetNamePattern -Config $config -RepoRoot $repoRoot)

if ($Push)
{
    throw "-Push is not yet implemented. docs.ms bot-auth pattern is still pending decision. Use -Force to write to the local target repo only, then hand off to your normal PR flow."
}

if ($Force)
{
    throw "-Force (write to local target) will land in a follow-up phase. This phase (P1) only supports dry-run."
}

Write-DryRunReport -Modules $modules -Plan $plan -Moniker $effectiveMoniker -TargetRepoPath $effectiveTargetRepoPath
