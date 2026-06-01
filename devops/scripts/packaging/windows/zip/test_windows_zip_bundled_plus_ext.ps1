param(
    [string]$OutRoot = (Join-Path $PSScriptRoot '../../../../out/windows'),
    [string]$CommonOutRoot = (Join-Path $PSScriptRoot '../../../../out/common'),
    [string]$CliZipPath = '',
    [string]$SimRoot = '',
    [switch]$SkipRuntime
)

$ErrorActionPreference = 'Stop'

$OutRoot = [System.IO.Path]::GetFullPath($OutRoot)
$CommonOutRoot = [System.IO.Path]::GetFullPath($CommonOutRoot)

$packageProfile = $env:PACKAGE_PROFILE
if ([string]::IsNullOrWhiteSpace($packageProfile)) {
    $packageProfile = 'lean'
}
$packageProfile = $packageProfile.ToLowerInvariant()

if ($packageProfile -eq 'full') {
    $artifactPrefix = 'azv2'
}
elseif ($packageProfile -eq 'lean') {
    $artifactPrefix = 'azv2-lean'
}
else {
    throw "Unsupported PACKAGE_PROFILE '$packageProfile'. Expected 'lean' or 'full'."
}

if ([string]::IsNullOrWhiteSpace($SimRoot)) {
    $SimRoot = Join-Path $PSScriptRoot '../../../../sim/win-zip-bundled-plus-ext'
}
$SimRoot = [System.IO.Path]::GetFullPath($SimRoot)

if ([string]::IsNullOrWhiteSpace($CliZipPath)) {
    $CliZipPath = Join-Path $OutRoot "Release/zip/$artifactPrefix-win-x64.zip"
}
else {
    $CliZipPath = [System.IO.Path]::GetFullPath($CliZipPath)
}
$ExtPackDir = Join-Path $CommonOutRoot 'extensions-pack'

if (-not (Test-Path -Path $CliZipPath -PathType Leaf)) {
    throw "Windows CLI zip not found at $CliZipPath. Run ./devops/scripts/packaging/common/build_common.sh, then ./devops/scripts/packaging/windows/zip/build_win_zip.sh."
}

if (-not (Test-Path -Path $ExtPackDir -PathType Container)) {
    throw "Extension package directory not found at $ExtPackDir. Run ./devops/scripts/packaging/common/build_common.sh first."
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
    'Microsoft.Azure.Cirrus.Extensions.Login',
    'Microsoft.Azure.Cirrus.Extensions.Config'
)

foreach ($ext in $builtins) {
    $extPath = Join-Path $SimRoot ("install/Extensions/$ext")
    if (-not (Test-Path -Path $extPath -PathType Container)) {
        throw "FAIL: Extensions/$ext missing in extracted zip."
    }
    Write-Host "PASS: Extensions/$ext present"
}

$resNupkg = Get-ChildItem -Path $ExtPackDir -Filter 'Microsoft.Azure.Cirrus.Extensions.Resources*.nupkg' | Select-Object -First 1

if (-not $resNupkg) { throw 'FAIL: Resources nupkg not found.' }

Write-Host "PASS: Resources package found: $($resNupkg.FullName)"

if ($IsWindows -and -not $SkipRuntime) {
    Write-Host ''
    Write-Host '[Runtime] Running azv2.exe checks on Windows'

    $env:USERPROFILE = Join-Path $SimRoot 'home'

    & $azv2Exe --help | Select-Object -First 20 | Out-Host

    Write-Host 'Verifying bundled Config command behavior'
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
    } elseif ($groupText -match "Required parameter 'SubscriptionId' is missing") {
        Write-Host 'PASS: Resources command resolved (missing SubscriptionId in CI context)'
    } elseif ($groupText -match 'AcquireTokenSilent|No account or login hint|please run .*login|AADSTS|authentication') {
        Write-Host 'PASS: Resources command resolved (authentication context not available in CI)'
    } else {
        Write-Host 'PASS: Resources command resolved'
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
