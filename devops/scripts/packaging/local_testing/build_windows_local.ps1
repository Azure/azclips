param(
    [string]$OutRootBase = '',
    [string]$CommonOutRoot = '',
    [string]$WindowsOutRoot = '',
    [string]$WindowsZipPath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDir '../../../..'))
$packagingDir = Join-Path $repoRoot 'devops/scripts/packaging'

if ([string]::IsNullOrWhiteSpace($OutRootBase)) {
    $OutRootBase = Join-Path $repoRoot 'devops/out'
}
if ([string]::IsNullOrWhiteSpace($CommonOutRoot)) {
    $CommonOutRoot = Join-Path $OutRootBase 'common'
}
if ([string]::IsNullOrWhiteSpace($WindowsOutRoot)) {
    $WindowsOutRoot = Join-Path $OutRootBase 'windows'
}
if ([string]::IsNullOrWhiteSpace($WindowsZipPath)) {
    $WindowsZipPath = Join-Path $WindowsOutRoot 'Release/zip/azv2-win-x64.zip'
}

$requiredFiles = @(
    'common/build_common.sh',
    'windows/zip/build_win_zip.ps1',
    'windows/zip/test_windows_zip_bundled_plus_ext.ps1',
    'config/bundled_ext.config'
)

foreach ($required in $requiredFiles) {
    $fullPath = Join-Path $packagingDir $required
    if (-not (Test-Path -Path $fullPath -PathType Leaf)) {
        throw "Missing required file: $fullPath"
    }
}

Push-Location $repoRoot
try {
    Write-Host '============================================'
    Write-Host ' Local Windows Zip Distribution Validation'
    Write-Host '============================================'
    Write-Host "Repo root       : $repoRoot"
    Write-Host "Common out      : $CommonOutRoot"
    Write-Host "Windows out     : $WindowsOutRoot"
    Write-Host "Windows zip path: $WindowsZipPath"

    Write-Host ''
    Write-Host '[1/3] Build common distribution assets'
    $previousOutRoot = $env:OUT_ROOT
    $env:OUT_ROOT = $CommonOutRoot
    try {
        bash (Join-Path $packagingDir 'common/build_common.sh')
    }
    finally {
        if ($null -ne $previousOutRoot) {
            $env:OUT_ROOT = $previousOutRoot
        }
        else {
            Remove-Item Env:OUT_ROOT -ErrorAction SilentlyContinue
        }
    }

    Write-Host ''
    Write-Host '[2/3] Build Windows zip'
    & (Join-Path $packagingDir 'windows/zip/build_win_zip.ps1') -OutRoot $WindowsOutRoot -CommonOutRoot $CommonOutRoot -CliWinZipPath $WindowsZipPath

    Write-Host ''
    Write-Host '[3/3] Validate Windows bundled + optional extensions'
    & (Join-Path $packagingDir 'windows/zip/test_windows_zip_bundled_plus_ext.ps1') -OutRoot $WindowsOutRoot -CommonOutRoot $CommonOutRoot -CliZipPath $WindowsZipPath

    Write-Host ''
    Write-Host 'Windows local distribution checks completed successfully.'
}
finally {
    Pop-Location
}
