# Generates doc_commit_map.json — verb → { file, commit, date } — for CLI2Yaml's
# --commitMap input. Drives Learn pages' "Edit this doc" links and "Last updated"
# metadata.
#
# The resolver walks a small fallback chain per verb:
#   1) <DocRoot>/<verb>/index.md              (group-style, matches azure-cli's per-group semantic)
#   2) <DocRoot>/<verb>.md                    (single-leaf-verb form)
#   3) any .md under <DocRoot>/<verb>/        (last-write-wins fallback)
#
# Prerequisites: repo checkout (git required); no build step needed.
#
# Usage:
#   pwsh -File build/Get-DocCommitMap.ps1
#   pwsh -File build/Get-DocCommitMap.ps1 -OutputPath other/path.json
[CmdletBinding()]
param(
    [string]$ServiceMappingPath,
    [string]$DocRoot,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

if (-not $ServiceMappingPath) { $ServiceMappingPath = Join-Path $repoRoot 'src/CLI/Help/service_name.json' }
if (-not $DocRoot)            { $DocRoot            = Join-Path $repoRoot 'src/CLI/Help/Docs' }
if (-not $OutputPath)         { $OutputPath         = Join-Path $repoRoot 'artifacts/cli-export/doc_commit_map.json' }

if (-not (Test-Path $ServiceMappingPath)) {
    throw "Service mapping not found at $ServiceMappingPath."
}
if (-not (Test-Path $DocRoot)) {
    throw "Doc root not found at $DocRoot."
}

$ServiceMappingPath = (Resolve-Path $ServiceMappingPath).Path
$DocRoot            = (Resolve-Path $DocRoot).Path

$mapping = Get-Content $ServiceMappingPath -Raw | ConvertFrom-Json

function Resolve-DocFile
{
    param([string]$Verb)

    $indexPath = Join-Path $DocRoot "$Verb/index.md"
    if (Test-Path -LiteralPath $indexPath) { return $indexPath }

    $leafPath = Join-Path $DocRoot "$Verb.md"
    if (Test-Path -LiteralPath $leafPath) { return $leafPath }

    $subtree = Join-Path $DocRoot $Verb
    if (Test-Path -LiteralPath $subtree)
    {
        $candidate = Get-ChildItem -Path $subtree -Filter '*.md' -Recurse -File -ErrorAction SilentlyContinue |
            Sort-Object -Property LastWriteTime -Descending |
            Select-Object -First 1
        if ($candidate) { return $candidate.FullName }
    }

    return $null
}

$result  = [ordered]@{}
$missing = @()

Push-Location $repoRoot
try
{
    foreach ($entry in $mapping)
    {
        $verb = $entry.Command
        $file = Resolve-DocFile -Verb $verb
        if (-not $file)
        {
            Write-Warning "No authoring markdown found for verb '$verb' under '$DocRoot'; skipping."
            $missing += $verb
            continue
        }

        $relativePath = ($file.Substring($repoRoot.Length).TrimStart('\', '/')) -replace '\\', '/'

        $commit = (& git rev-list -1 HEAD -- $relativePath 2>$null).Trim()
        $date   = (& git log -1 --format=%cI -- $relativePath 2>$null).Trim()

        if (-not $commit)
        {
            Write-Warning "git rev-list returned no commit for '$relativePath' (file may be untracked); skipping."
            $missing += $verb
            continue
        }

        $result[$verb] = [ordered]@{
            file   = $relativePath
            commit = $commit
            date   = $date
        }
    }
}
finally
{
    Pop-Location
}

$outputDir = Split-Path $OutputPath -Parent
if ($outputDir -and -not (Test-Path $outputDir))
{
    New-Item -Path $outputDir -ItemType Directory -Force | Out-Null
}

$json = $result | ConvertTo-Json -Depth 4
Set-Content -Path $OutputPath -Value ($json + [Environment]::NewLine) -NoNewline

Write-Host "Wrote $($result.Count) verb entries to $OutputPath"
if ($missing.Count -gt 0)
{
    Write-Host "  skipped: $($missing -join ', ')"
}
