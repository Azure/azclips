#!/usr/bin/env pwsh
# Splits the Agent daemon's self-contained publish output into a layout that
# co-locates the launcher with the CLI (so the runtime pack is shared) while
# keeping every agent-only asset under the Agent extension directory:
#
#   PublishDir/                             (co-located with CLI publish)
#     azclips-agent.exe                     launcher: sibling to az.exe so
#     azclips-agent.dll                       it shares the runtime pack
#     azclips-agent.deps.json                 and hostpolicy.dll during
#     azclips-agent.runtimeconfig.json        Phase 1 native bootstrap
#     Extensions/Microsoft.Azure.Azclips.Extensions.Agent/
#       prompts/                            agent-only assets grouped
#       deps/                               under the Agent extension dir:
#         <agent-only nugets>/<ver>/lib/<tfm>/*.dll   NuGet-style so
#         runtimes/<rid>/native/copilot[.exe]         additionalProbingPaths
#                                                      + SdkNativeDir resolve them
#
# Runtime pack files (System.*.dll, hostpolicy.dll, etc.) come from the CLI's
# own publish and are byte-identical between the two publishes; this script
# deletes them from the agent publish output rather than duplicating.
#
# Top-level dirs other than prompts/ (e.g. runtimes/) are moved verbatim under
# deps/<name>/ — same rule for all of them, so nothing new is silently dropped.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$AgentPublishDir,   # dotnet publish output for Core.Agent
    [Parameter(Mandatory=$true)][string]$CliPublishDir,     # dotnet publish output for Core.Cli
    [Parameter(Mandatory=$true)][string]$AgentExtensionDir  # Extensions/Microsoft.Azure.Azclips.Extensions.Agent
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-Sha256Hex {
    param([string]$Path)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $fs = [System.IO.File]::OpenRead($Path)
        try { return [System.BitConverter]::ToString($sha.ComputeHash($fs)).Replace('-','') }
        finally { $fs.Dispose() }
    }
    finally { $sha.Dispose() }
}

foreach ($p in @($AgentPublishDir, $CliPublishDir, $AgentExtensionDir)) {
    if (-not (Test-Path -Path $p -PathType Container)) {
        throw "Directory not found: $p"
    }
}

$depsDir = Join-Path $AgentExtensionDir 'deps'
$null = New-Item -Path $depsDir -ItemType Directory -Force

Write-Host "Staging agent daemon into hybrid layout"
Write-Host "  agent publish : $AgentPublishDir"
Write-Host "  CLI publish   : $CliPublishDir"
Write-Host "  extension dir : $AgentExtensionDir"

# 1. Launcher files -> CLI publish root (sibling to az.exe)
$launcherFiles = @(
    'azclips-agent.exe', 'azclips-agent',
    'azclips-agent.dll',
    'azclips-agent.deps.json',
    'azclips-agent.runtimeconfig.json'
)
$launcherMoved = 0
foreach ($f in $launcherFiles) {
    $src = Join-Path $AgentPublishDir $f
    if (Test-Path -Path $src -PathType Leaf) {
        Move-Item -Path $src -Destination (Join-Path $CliPublishDir $f) -Force
        $launcherMoved++
    }
}
Write-Host "  [1/6] launcher: moved $launcherMoved files to CLI publish root"

# 2. prompts/ -> extension dir
$promptsSrc = Join-Path $AgentPublishDir 'prompts'
if (Test-Path -Path $promptsSrc -PathType Container) {
    $promptsDst = Join-Path $AgentExtensionDir 'prompts'
    if (Test-Path -Path $promptsDst) { Remove-Item -Path $promptsDst -Recurse -Force }
    Move-Item -Path $promptsSrc -Destination $promptsDst -Force
    Write-Host "  [2/6] prompts/: moved to $promptsDst"
}

# 3. Parse the (now-moved) deps.json to build the dll_name -> (pkg, ver, rel_path) map
#    and to find runtime pack members.
$depsPath = Join-Path $CliPublishDir 'azclips-agent.deps.json'
if (-not (Test-Path -Path $depsPath -PathType Leaf)) {
    throw "azclips-agent.deps.json not at $depsPath after launcher move"
}
$deps = Get-Content -Raw -Path $depsPath | ConvertFrom-Json

$dllMap = @{}
$runtimePackNames = @{}

foreach ($tfmProp in $deps.targets.PSObject.Properties) {
    foreach ($libProp in $tfmProp.Value.PSObject.Properties) {
        $libId = $libProp.Name
        $lib = $libProp.Value

        if ($libId -like 'runtimepack.*') {
            foreach ($section in @('runtime', 'native')) {
                if ($lib.PSObject.Properties.Match($section).Count -eq 0) { continue }
                foreach ($fileProp in $lib.$section.PSObject.Properties) {
                    $runtimePackNames[[System.IO.Path]::GetFileName($fileProp.Name)] = $true
                }
            }
            continue
        }

        if ($libId -notmatch '/') { continue }
        $pkg, $ver = $libId -split '/', 2
        # Skip .Reference libraries (compile-time refs only). The real dll
        # comes from the same-name non-.Reference entry, e.g.
        # Azclips.Agent.Tools/1.0.0 vs Azclips.Agent.Tools.Reference/1.0.0.0.
        if ($pkg.EndsWith('.Reference')) { continue }

        if ($lib.PSObject.Properties.Match('runtime').Count -gt 0) {
            foreach ($fileProp in $lib.runtime.PSObject.Properties) {
                $relPath = $fileProp.Name
                $fileName = [System.IO.Path]::GetFileName($relPath)
                # hostfxr resolves the agent subprocess's deps two ways:
                #   package (NuGet): probed under deps/ using the SDK-lowercased
                #     libraries[id].path (e.g. "azure.core/1.54.0").
                #   project ref (libraries[id].path == null): resolves from the APP
                #     BASE (CLI publish root, next to azclips-agent). Every project
                #     ref except agent-only Azclips.Agent.Tools already has a copy
                #     in the CLI root — which is why only that one goes missing.
                # So: still lay project refs under deps/ (correct-cased, harmless),
                # AND copy any the CLI root lacks into the CLI root (done below).
                $libEntry = $deps.libraries.PSObject.Properties.Match($libId)
                $libPath = $null
                $isProject = $false
                if ($libEntry.Count -gt 0) {
                    $libVal = $libEntry[0].Value
                    # 'path'/'type' exist per lib; accessing a missing property
                    # throws in strict PowerShell, so probe before reading.
                    if ($libVal.PSObject.Properties.Match('path').Count -gt 0) { $libPath = $libVal.path }
                    if ($libVal.PSObject.Properties.Match('type').Count -gt 0) { $isProject = ($libVal.type -eq 'project') }
                }
                $subdir = if ($libPath) { $libPath.Replace('\', '/') } else { "$pkg/$ver" }
                $dllMap[$fileName] = @{ subdir = $subdir; rel = $relPath; isProject = $isProject }
            }
        }
    }
}

# 5. Remaining files in AgentPublishDir:
#    - runtime pack member with SHA identical to CLI root: delete
#    - other NuGet dll present in dll_map: move to deps/<probe-subdir>/<rel>
#    - .pdb / .env / *.dll.config: delete (never shipped)
#    - unclassified: warn (packaging break)
$runtimePackDeduped = 0
$nugetMoved = 0
$purged = 0
$unclassified = @()
$flatKeptAtExtTop = @{}   # agent-only project dlls we copy flat to ext top

foreach ($file in Get-ChildItem -Path $AgentPublishDir -File) {
    $name = $file.Name

    if ($runtimePackNames.ContainsKey($name)) {
        $rootTwin = Join-Path $CliPublishDir $name
        if (Test-Path -Path $rootTwin -PathType Leaf) {
            $shaAgent = Get-Sha256Hex -Path $file.FullName
            $shaRoot = Get-Sha256Hex -Path $rootTwin
            if ($shaAgent -eq $shaRoot) {
                Remove-Item -Path $file.FullName -Force
                $runtimePackDeduped++
                continue
            }
        }
        # Runtime pack member missing at CLI root or hash differs. This is a
        # build-time inconsistency that would break the agent at runtime; keep
        # it loud so packaging fails audibly instead of shipping it.
        $unclassified += "$name (runtime pack, differs from CLI root - publish version drift?)"
        continue
    }

    if ($dllMap.ContainsKey($name)) {
        $entry = $dllMap[$name]
        # An agent-only project ref (only Azclips.Agent.Tools today) must satisfy
        # BOTH loaders, which look in different flat dirs:
        #   - agent subprocess: app base = CLI root (flat)
        #   - `az agent tool ...` CLI extension ALC: extension top level (flat)
        # Copy to whichever lacks it (from the publish src), then move the src
        # into deps/. Refs the CLI root already carries skip the copy.
        if ($entry.isProject) {
            foreach ($flatDir in @($CliPublishDir, $AgentExtensionDir)) {
                $twin = Join-Path $flatDir $name
                if (-not (Test-Path -Path $twin -PathType Leaf)) {
                    Copy-Item -Path $file.FullName -Destination $twin -Force
                }
            }
            # Remember ones placed flat at ext top so step-6 cleanup keeps them.
            $flatKeptAtExtTop[$name] = $true
        }
        # subdir already carries the correctly-cased <pkg>/<ver> hostfxr probes.
        $dstDir = Join-Path $depsDir $entry.subdir
        $relDir = [System.IO.Path]::GetDirectoryName($entry.rel)
        if ($relDir) { $dstDir = Join-Path $dstDir $relDir }
        $null = New-Item -Path $dstDir -ItemType Directory -Force
        Move-Item -Path $file.FullName -Destination (Join-Path $dstDir $name) -Force
        $nugetMoved++
        continue
    }

    if ($name -like '*.pdb' -or $name -eq '.env' -or $name -like '*.dll.config') {
        Remove-Item -Path $file.FullName -Force
        $purged++
        continue
    }

    $unclassified += $name
}

Write-Host "  [4/6] runtime pack: deduped $runtimePackDeduped files (byte-identical to CLI root)"
Write-Host "  [5/6] agent NuGet: moved $nugetMoved dlls to deps/<pkg>/<ver>/<lib-path>/"
if ($purged -gt 0) {
    Write-Host "        (purged $purged non-shipping files: pdb / .env / dll.config)"
}

# Any directory still at the agent-publish top level is one not handled above
# (prompts/ was moved earlier; runtimes/ and anything new land here). Move each
# verbatim under deps/<name>/ so it ships instead of being silently dropped —
# the directory-side counterpart of the unclassified-file check below.
$dirsMoved = 0
foreach ($dir in Get-ChildItem -Path $AgentPublishDir -Directory) {
    $dst = Join-Path $depsDir $dir.Name
    if (Test-Path -Path $dst) { Remove-Item -Path $dst -Recurse -Force }
    Move-Item -Path $dir.FullName -Destination $dst -Force
    $dirsMoved++
}
if ($dirsMoved -gt 0) {
    Write-Host "        (moved $dirsMoved top-level dir(s) to deps/ verbatim)"
}

# Clean up empty subdirs left behind (empty prompts/, etc.)
Get-ChildItem -Path $AgentPublishDir -Directory -Recurse |
    Sort-Object FullName -Descending |
    Where-Object { -not (Get-ChildItem -Path $_.FullName -Force -ErrorAction SilentlyContinue) } |
    ForEach-Object { Remove-Item -Path $_.FullName -Force }

# 6. Clean up the Agent extension top level: drop anything that's already
#    covered by deps (dep dlls), the CLI root (Core.* / ExtensionRuntime),
#    or is a non-shipping artifact (.pdb / .dll.config / stray runtimeconfig).
#    A normal extension directory ships just the extension's own dll + deps.json
#    + docs (Help/, prompts/); the packaging side-effects that pull in ~40 extra
#    files come from Agent.Commands referencing Core.Cli for the help pipeline.
$extKeep = @{}
# Always keep: extension code dll + deps.json + docs directories
$extKeep['Microsoft.Azure.Azclips.Extensions.Agent.dll'] = $true
$extKeep['Microsoft.Azure.Azclips.Extensions.Agent.deps.json'] = $true

$extCleaned = 0
foreach ($file in Get-ChildItem -Path $AgentExtensionDir -File) {
    $name = $file.Name
    if ($extKeep.ContainsKey($name)) { continue }
    # Agent-only project dlls we deliberately placed flat here (for the CLI
    # extension loader) must NOT be cleaned up, even though deps/ has a twin.
    if ($flatKeptAtExtTop.ContainsKey($name)) { continue }
    # Any dll that's byte-identical to a copy at the CLI root or under
    # deps is redundant; the extension loader falls back to the host
    # anyway. .pdb / .dll.config / extension .runtimeconfig.json aren't
    # shipping payload either.
    $isDeletable = $false
    if ($name -like '*.pdb' -or $name -like '*.dll.config' -or $name -eq 'Microsoft.Azure.Azclips.Extensions.Agent.runtimeconfig.json') {
        $isDeletable = $true
    }
    elseif ($name -like '*.dll') {
        $rootTwin = Join-Path $CliPublishDir $name
        if ((Test-Path -Path $rootTwin -PathType Leaf) -and
            ((Get-Sha256Hex -Path $file.FullName) -eq (Get-Sha256Hex -Path $rootTwin))) {
            $isDeletable = $true
        }
        elseif (Get-ChildItem -Path $depsDir -Filter $name -Recurse -File -ErrorAction SilentlyContinue |
                Where-Object { (Get-Sha256Hex -Path $_.FullName) -eq (Get-Sha256Hex -Path $file.FullName) } |
                Select-Object -First 1) {
            $isDeletable = $true
        }
    }
    if ($isDeletable) {
        Remove-Item -Path $file.FullName -Force
        $extCleaned++
    }
}

# Drop the netstandard fallback runtimes/ subdir wholesale: every native it
# carries is already staged under deps/<pkg>/.../runtimes/ (the NuGet-style
# path additionalProbingPaths resolves), so the extension-top copy is dead
# weight. Count the files removed so the [6/6] tally is accurate.
$extRuntimes = Join-Path $AgentExtensionDir 'runtimes'
if (Test-Path -Path $extRuntimes -PathType Container) {
    $extCleaned += @(Get-ChildItem -Path $extRuntimes -Recurse -File).Count
    Remove-Item -Path $extRuntimes -Recurse -Force
}

Write-Host "  [6/6] extension top-level: removed $extCleaned redundant files"

if ($unclassified.Count -gt 0) {
    Write-Host ""
    Write-Host "ERROR: unclassified agent publish outputs. Packaging cannot silently drop or ship unknown files:" -ForegroundColor Red
    foreach ($u in $unclassified) { Write-Host "  - $u" -ForegroundColor Red }
    throw "stage_agent: $($unclassified.Count) unclassified file(s); update stage_agent classification rules before shipping."
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolProject = [System.IO.Path]::GetFullPath((Join-Path $scriptDir '../../../../tools/Packaging/Azclips.Packaging.Tool.csproj'))
dotnet run --project $toolProject --configuration Release -- $CliPublishDir $AgentExtensionDir
if ($LASTEXITCODE -ne 0) {
    throw "Host-shared dependency validation failed with exit code $LASTEXITCODE."
}

Write-Host "OK: stage_agent complete"
