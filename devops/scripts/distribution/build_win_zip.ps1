param(
    [string]$OutRoot = "$PSScriptRoot/out",
    [string]$Rid = "win-x64",
    [string]$CliProject = "src/CLI/Core.Cli.csproj"
)

$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../../.."))
Set-Location $repoRoot

$OutRoot = [System.IO.Path]::GetFullPath($OutRoot)
$cliWinScDir = Join-Path $OutRoot "publish/cli/$Rid"
$cliBinDir = Join-Path $OutRoot "bin/Release"
$cliZipPath = Join-Path $cliBinDir "azv2-$Rid.zip"
$builtinPublishRoot = Join-Path $OutRoot "builtin-publish"

if (-not (Test-Path -Path $builtinPublishRoot -PathType Container)) {
    throw "Built-in publish root not found at $builtinPublishRoot. Run ./devops/scripts/distribution/build_common.sh first."
}

Write-Host "Publishing CLI (Release, $Rid)"
& dotnet publish $CliProject -c Release -r $Rid --self-contained true -o $cliWinScDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for $CliProject"
}

Write-Host "Injecting built-in extensions into Windows publish output"
$builtinDirs = Get-ChildItem -Path $builtinPublishRoot -Directory
foreach ($builtinDir in $builtinDirs) {
    $extAssemblyName = $builtinDir.Name
    $dest = Join-Path $cliWinScDir "Extensions/$extAssemblyName"
    New-Item -ItemType Directory -Path $dest -Force | Out-Null

    $copied = 0
    $skipped = 0

    $dlls = Get-ChildItem -Path $builtinDir.FullName -Filter *.dll -File -ErrorAction SilentlyContinue
    foreach ($dll in $dlls) {
        $hostDll = Join-Path $cliWinScDir $dll.Name
        if ((Test-Path -Path $hostDll -PathType Leaf) -and ((Get-FileHash $dll.FullName).Hash -eq (Get-FileHash $hostDll).Hash)) {
            $skipped++
        } else {
            Copy-Item -Path $dll.FullName -Destination $dest -Force
            $copied++
        }
    }

    $deps = Get-ChildItem -Path $builtinDir.FullName -Filter *.deps.json -File -ErrorAction SilentlyContinue
    foreach ($dep in $deps) {
        Copy-Item -Path $dep.FullName -Destination $dest -Force
    }

    Write-Host "  BUNDLE $extAssemblyName -> $dest (copied $copied DLLs, skipped $skipped host-provided)"
}

New-Item -ItemType Directory -Path $cliBinDir -Force | Out-Null
if (Test-Path -Path $cliZipPath -PathType Leaf) {
    Remove-Item -Path $cliZipPath -Force
}
Compress-Archive -Path (Join-Path $cliWinScDir '*') -DestinationPath $cliZipPath
Write-Host "Created $cliZipPath"
