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

if (-not (Test-Path -Path $PublishDir -PathType Container)) {
    throw "Publish directory not found at $PublishDir"
}

if (-not (Test-Path -Path $BuiltinRoot -PathType Container)) {
    throw "Built-in publish root not found at $BuiltinRoot"
}

Write-Host "Injecting built-in extensions into $PublishDir"

$builtinDirs = Get-ChildItem -Path $BuiltinRoot -Directory
foreach ($builtinDir in $builtinDirs) {
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

    Write-Host "  BUNDLE $extAssemblyName -> $dest (copied $copied DLLs, skipped $skipped host-provided)"
}
