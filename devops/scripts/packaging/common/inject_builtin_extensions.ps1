param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir,

    [Parameter(Mandatory = $true)]
    [string]$BuiltinRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$PublishDir = [System.IO.Path]::GetFullPath($PublishDir)
$BuiltinRoot = [System.IO.Path]::GetFullPath($BuiltinRoot)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$packagingRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDir '..'))

$packageProfile = $env:PACKAGE_PROFILE
if ([string]::IsNullOrWhiteSpace($packageProfile)) {
    $packageProfile = 'lean'
}
$packageProfile = $packageProfile.ToLowerInvariant()

if ($packageProfile -eq 'lean') {
    $packageConfig = Join-Path $packagingRoot 'config/lean_package.config'
}
elseif ($packageProfile -eq 'full') {
    $packageConfig = Join-Path $packagingRoot 'config/full_package.config'
}
else {
    throw "Unsupported PACKAGE_PROFILE '$packageProfile'. Expected 'lean' or 'full'."
}

if (-not (Test-Path -Path $PublishDir -PathType Container)) {
    throw "Publish directory not found at $PublishDir"
}

if (-not (Test-Path -Path $BuiltinRoot -PathType Container)) {
    throw "Built-in publish root not found at $BuiltinRoot"
}

if (-not (Test-Path -Path $packageConfig -PathType Leaf)) {
    throw "package config not found at $packageConfig"
}

$configIncludeEntries = @()
Get-Content -Path $packageConfig | ForEach-Object {
    $line = $_
    $hashIndex = $line.IndexOf('#')
    if ($hashIndex -ge 0) {
        $line = $line.Substring(0, $hashIndex)
    }
    $line = $line.Trim()
    if (-not [string]::IsNullOrWhiteSpace($line)) {
        $configIncludeEntries += $line
    }
}

if ($configIncludeEntries.Count -eq 0) {
    throw "package config '$packageConfig' has no include entries"
}

$selectedBuiltinDirs = @()
if ($configIncludeEntries -contains '*') {
    $selectedBuiltinDirs = Get-ChildItem -Path $BuiltinRoot -Directory | Sort-Object Name
}
else {
    foreach ($entry in $configIncludeEntries) {
        $extAssemblyName = "Microsoft.Azure.Azclips.Extensions.$entry"
        $builtinDirPath = Join-Path $BuiltinRoot $extAssemblyName
        if (-not (Test-Path -Path $builtinDirPath -PathType Container)) {
            throw "Built-in publish directory not found for '$entry' at $builtinDirPath"
        }
        $selectedBuiltinDirs += Get-Item -Path $builtinDirPath
    }
}

Write-Host "Injecting built-in extensions into $PublishDir"
Write-Host "Inject profile: $packageProfile"
Write-Host "Inject config : $packageConfig"

foreach ($builtinDir in $selectedBuiltinDirs) {
    $extAssemblyName = $builtinDir.Name
    $dest = Join-Path $PublishDir "Extensions/$extAssemblyName"
    New-Item -Path $dest -ItemType Directory -Force | Out-Null

    $copied = 0
    $skipped = 0

    $dlls = Get-ChildItem -Path $builtinDir.FullName -Filter '*.dll' -File -ErrorAction SilentlyContinue
    foreach ($dll in $dlls) {
        $hostDll = Join-Path $PublishDir $dll.Name
        if (Test-Path -Path $hostDll -PathType Leaf) {
            $srcHash = (Get-FileHash -Algorithm SHA256 -Path $dll.FullName).Hash
            $hostHash = (Get-FileHash -Algorithm SHA256 -Path $hostDll).Hash
            if ($srcHash -eq $hostHash) {
                $skipped++
                continue
            }
        }

        Copy-Item -Path $dll.FullName -Destination $dest -Force
        $copied++
    }

    $depsJsonFiles = Get-ChildItem -Path $builtinDir.FullName -Filter '*.deps.json' -File -ErrorAction SilentlyContinue
    foreach ($deps in $depsJsonFiles) {
        Copy-Item -Path $deps.FullName -Destination $dest -Force
    }

    $sidecarFiles = Get-ChildItem -Path $builtinDir.FullName -Filter '*.commands.json' -File -ErrorAction SilentlyContinue
    foreach ($sidecar in $sidecarFiles) {
        Copy-Item -Path $sidecar.FullName -Destination $dest -Force
    }

    Write-Host "  BUNDLE $extAssemblyName -> $dest (copied $copied DLLs, skipped $skipped host-provided)"
}
