param(
    [string]$OutRoot = "$PSScriptRoot/out",
    [switch]$SkipRuntime
)

$ErrorActionPreference = 'Stop'

$OutRoot = [System.IO.Path]::GetFullPath($OutRoot)
$SimRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'sim-bundled-plus-win-ps'))

$CliZipPath = Join-Path $OutRoot 'bin/Release/azv2-win-x64.zip'
$ExtPackDir = Join-Path $OutRoot 'extensions-pack'

if (-not (Test-Path -Path $CliZipPath -PathType Leaf)) {
    throw "Windows CLI zip not found at $CliZipPath. Run ./devops/scripts/distribution/build_all.sh first."
}

if (-not (Test-Path -Path $ExtPackDir -PathType Container)) {
    throw "Extension package directory not found at $ExtPackDir. Run ./devops/scripts/distribution/build_all.sh first."
}

Write-Host '============================================'
Write-Host ' Windows: Bundled CLI + Optional Extensions '
Write-Host '        (PowerShell-based validation)       '
Write-Host '============================================'

if (Test-Path $SimRoot) {
    Remove-Item -Path $SimRoot -Recurse -Force
}

$null = New-Item -Path (Join-Path $SimRoot 'install') -ItemType Directory -Force
$null = New-Item -Path (Join-Path $SimRoot 'home') -ItemType Directory -Force

Expand-Archive -Path $CliZipPath -DestinationPath (Join-Path $SimRoot 'install') -Force

$azv2Exe = Join-Path $SimRoot 'install/azv2.exe'
if (-not (Test-Path -Path $azv2Exe -PathType Leaf)) {
    throw 'FAIL: azv2.exe not found in extracted zip.'
}
Write-Host 'PASS: azv2.exe found'

$builtins = @(
    'Microsoft.Azure.Cirrus.Extensions.Extension',
    'Microsoft.Azure.Cirrus.Extensions.Login'
)

foreach ($ext in $builtins) {
    $extPath = Join-Path $SimRoot ("install/Extensions/$ext")
    if (-not (Test-Path -Path $extPath -PathType Container)) {
        throw "FAIL: Extensions/$ext missing in extracted zip."
    }
    Write-Host "PASS: Extensions/$ext present"
}

$cfgNupkg = Get-ChildItem -Path $ExtPackDir -Filter 'Microsoft.Azure.Cirrus.Extensions.Config*.nupkg' | Select-Object -First 1
$resNupkg = Get-ChildItem -Path $ExtPackDir -Filter 'Microsoft.Azure.Cirrus.Extensions.Resources*.nupkg' | Select-Object -First 1

if (-not $cfgNupkg) { throw 'FAIL: Config nupkg not found.' }
if (-not $resNupkg) { throw 'FAIL: Resources nupkg not found.' }

Write-Host "PASS: Config package found: $($cfgNupkg.FullName)"
Write-Host "PASS: Resources package found: $($resNupkg.FullName)"

if ($IsWindows -and -not $SkipRuntime) {
    Write-Host ''
    Write-Host '[Runtime] Running azv2.exe checks on Windows'

    $env:USERPROFILE = Join-Path $SimRoot 'home'

    & $azv2Exe --help | Select-Object -First 20 | Out-Host

    $addCfgOut = & $azv2Exe extension add --path $cfgNupkg.FullName 2>&1
    $addCfgText = $addCfgOut | Out-String
    $addCfgText.Trim() | Out-Host
    if ($addCfgText -notmatch 'installedExtensionPath') {
        throw 'FAIL: Config extension installation did not report installedExtensionPath.'
    }

    & $azv2Exe config set --key core.logging.level --value debug --force | Out-Host
    $cfgGetOut = & $azv2Exe config get --key core.logging.level 2>&1
    $cfgGetText = $cfgGetOut | Out-String
    $cfgGetText.Trim() | Out-Host
    if ($cfgGetText -notmatch 'core\.logging\.level\s*=\s*debug') {
        throw 'FAIL: Config command output did not confirm debug value.'
    }

    $addResOut = & $azv2Exe extension add --path $resNupkg.FullName 2>&1
    $addResText = $addResOut | Out-String
    $addResText.Trim() | Out-Host
    if ($addResText -notmatch 'installedExtensionPath') {
        throw 'FAIL: Resources extension installation did not report installedExtensionPath.'
    }

    $groupOut = & $azv2Exe group list 2>&1
    $groupText = $groupOut | Out-String
    $groupText.Trim() | Out-Host
    if ($groupText -match 'could not be found') {
        throw 'FAIL: Resources command not resolved after install.'
    }

    Write-Host 'PASS: Windows runtime extension-add flow validated'
} else {
    Write-Host ''
    Write-Host 'NOTE: Runtime execution skipped (non-Windows host or -SkipRuntime set).'
    Write-Host '      Package/layout validation completed successfully.'
}

Write-Host ''
Write-Host '============================================'
Write-Host ' Windows Bundled+Optional PS Test Complete '
Write-Host '============================================'
