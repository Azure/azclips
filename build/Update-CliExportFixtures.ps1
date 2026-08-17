# Regenerates the checked-in Azclips.Cli.Export fixtures at
# tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/ by running the exporter
# and copying its per-verb output over the current fixtures.
#
# Refresh fixtures after landing a legitimate exporter or authored-markdown change:
#   pwsh -File build/Update-CliExportFixtures.ps1
#
# Prerequisites: repo checkout + `dotnet build azclips.slnx` (fresh extension DLLs).
[CmdletBinding()]
param(
    [string]$ArtifactsRoot,
    [string]$OutputStagingFolder
)

$ErrorActionPreference = 'Stop'

$repoRoot   = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$fixtureDir = [System.IO.Path]::Combine($repoRoot, 'tools', 'Help', 'Azclips.Help.Tool.Tests', 'Fixtures', 'cli-export')

if (-not $ArtifactsRoot)       { $ArtifactsRoot       = [System.IO.Path]::Combine($repoRoot, 'artifacts', 'Debug') }
if (-not $OutputStagingFolder) { $OutputStagingFolder = [System.IO.Path]::Combine($repoRoot, 'artifacts', 'cli-export') }

if (-not (Test-Path $ArtifactsRoot))
{
    throw "Artifacts root not found at $ArtifactsRoot. Run 'dotnet build azclips.slnx' first."
}

Write-Host "Running exporter into $OutputStagingFolder ..."
dotnet run `
    --project ([System.IO.Path]::Combine($repoRoot, 'tools', 'Help', 'Azclips.Cli.Export', 'Azclips.Cli.Export.csproj')) `
    --no-build --configuration Debug `
    -- --artifacts-root $ArtifactsRoot --output-folder $OutputStagingFolder
if ($LASTEXITCODE -ne 0)
{
    throw "Exporter failed with exit code $LASTEXITCODE."
}

$exporterLatest = Join-Path $OutputStagingFolder 'latest'
if (-not (Test-Path $exporterLatest))
{
    throw "Expected exporter output at $exporterLatest but nothing was produced."
}

New-Item -Path $fixtureDir -ItemType Directory -Force | Out-Null

# Enumerate the currently-checked-in fixture set so we only refresh what's tracked;
# adding a new fixture verb is a deliberate, separate edit.
$tracked = Get-ChildItem -Path $fixtureDir -Filter '*.json' -File | ForEach-Object { $_.Name }
if (-not $tracked)
{
    Write-Warning "No existing fixtures under $fixtureDir; nothing to refresh. Add fixtures manually first."
    return
}

$refreshed = @()
$missing   = @()
foreach ($name in $tracked)
{
    $source = Join-Path $exporterLatest $name
    if (Test-Path $source)
    {
        Copy-Item $source -Destination (Join-Path $fixtureDir $name) -Force
        $refreshed += $name
    }
    else
    {
        $missing += $name
    }
}

Write-Host ""
Write-Host "Refreshed $($refreshed.Count) fixture(s):"
$refreshed | ForEach-Object { Write-Host "  $_" }
if ($missing.Count -gt 0)
{
    Write-Warning "Exporter did not produce these tracked fixtures: $($missing -join ', ')"
}
