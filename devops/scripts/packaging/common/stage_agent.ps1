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
#       deps/                               under the Agent extension dir,
#         github.copilot.sdk/<ver>/         in NuGet-style layout so
#           lib/net10.0/GitHub.Copilot.SDK.dll        additionalProbingPaths
#           runtimes/<rid>/native/copilot[.exe]       resolves them
#         <other agent-only nugets>/<ver>/lib/<tfm>/*.dll
#
# Runtime pack files (System.*.dll, hostpolicy.dll, etc.) come from the CLI's
# own publish and are byte-identical between the two publishes; this script
# deletes them from the agent publish output rather than duplicating.
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

$sdkRuntime = Join-Path $AgentExtensionDir 'deps'
$null = New-Item -Path $sdkRuntime -ItemType Directory -Force

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
#    and to find runtime pack members + Copilot SDK version.
$depsPath = Join-Path $CliPublishDir 'azclips-agent.deps.json'
if (-not (Test-Path -Path $depsPath -PathType Leaf)) {
    throw "azclips-agent.deps.json not at $depsPath after launcher move"
}
$deps = Get-Content -Raw -Path $depsPath | ConvertFrom-Json

$dllMap = @{}
$runtimePackNames = @{}
$sdkVersion = $null

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

        if ($pkg -eq 'GitHub.Copilot.SDK') { $sdkVersion = $ver }

        if ($lib.PSObject.Properties.Match('runtime').Count -gt 0) {
            foreach ($fileProp in $lib.runtime.PSObject.Properties) {
                $relPath = $fileProp.Name
                $fileName = [System.IO.Path]::GetFileName($relPath)
                $dllMap[$fileName] = @{ pkg = $pkg; ver = $ver; rel = $relPath }
            }
        }
    }
}

if (-not $sdkVersion) {
    throw "Could not find GitHub.Copilot.SDK version in azclips-agent.deps.json"
}

# 4. copilot native binary -> deps/github.copilot.sdk/<ver>/runtimes/<rid>/native/
#    Preserves the exact NuGet package layout so future runtime-pack
#    conventions continue to work.
$nativeSrcRoot = Join-Path $AgentPublishDir 'runtimes'
$copilotMoved = 0
if (Test-Path -Path $nativeSrcRoot -PathType Container) {
    foreach ($ridDir in Get-ChildItem -Path $nativeSrcRoot -Directory) {
        $nativeSrc = Join-Path $ridDir.FullName 'native'
        if (-not (Test-Path -Path $nativeSrc -PathType Container)) { continue }
        foreach ($file in Get-ChildItem -Path $nativeSrc -File) {
            # Only relocate copilot binaries; leave other RID-specific natives alone
            if ($file.Name -ne 'copilot.exe' -and $file.Name -ne 'copilot') { continue }
            $dstDir = Join-Path $sdkRuntime "github.copilot.sdk/$sdkVersion/runtimes/$($ridDir.Name)/native"
            $null = New-Item -Path $dstDir -ItemType Directory -Force
            Move-Item -Path $file.FullName -Destination (Join-Path $dstDir $file.Name) -Force
            $copilotMoved++
        }
    }
}
Write-Host "  [3/6] copilot native: moved $copilotMoved files to deps/github.copilot.sdk/$sdkVersion/runtimes/<rid>/native/"

# 5. Remaining files in AgentPublishDir:
#    - runtime pack member with SHA identical to CLI root: delete
#    - other NuGet dll present in dll_map: move to deps/<pkg-lower>/<ver>/<rel>
#    - .pdb / .env / *.dll.config: delete (never shipped)
#    - unclassified: warn (packaging break)
$runtimePackDeduped = 0
$nugetMoved = 0
$purged = 0
$unclassified = @()

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
        $dstDir = Join-Path $sdkRuntime "$($entry.pkg.ToLowerInvariant())/$($entry.ver)"
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

# Clean up empty subdirs left behind (runtimes/, empty prompts/, etc.)
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
        elseif (Get-ChildItem -Path $sdkRuntime -Filter $name -Recurse -File -ErrorAction SilentlyContinue |
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

Write-Host "OK: stage_agent complete"
