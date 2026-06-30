param(
    [string]$OutRoot = (Join-Path $PSScriptRoot '../../../../out/windows'),
    [string]$CommonOutRoot = (Join-Path $PSScriptRoot '../../../../out/common'),
    [string]$Rid = 'win-x64',
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
    $artifactPrefix = 'az'
}
elseif ($packageProfile -eq 'lean') {
    $artifactPrefix = 'az-lean'
}
else {
    throw "Unsupported PACKAGE_PROFILE '$packageProfile'. Expected 'lean' or 'full'."
}

if ([string]::IsNullOrWhiteSpace($SimRoot)) {
    $SimRoot = Join-Path $PSScriptRoot '../../../../sim/win-zip-bundled-plus-ext'
}
$SimRoot = [System.IO.Path]::GetFullPath($SimRoot)

if ([string]::IsNullOrWhiteSpace($CliZipPath)) {
    $artifactVersion = $env:ARTIFACT_VERSION
    if (-not [string]::IsNullOrWhiteSpace($artifactVersion)) {
        $CliZipPath = Join-Path $OutRoot "Release/zip/$artifactPrefix-$Rid-$artifactVersion.zip"
    }
    else {
        $CliZipPath = Join-Path $OutRoot "Release/zip/$artifactPrefix-$Rid.zip"
    }
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

$azExe = Join-Path $SimRoot 'install/az.exe'
if (-not (Test-Path -Path $azExe -PathType Leaf)) {
    throw 'FAIL: az.exe not found in extracted zip.'
}
Write-Host 'PASS: az.exe found'

$builtins = @(
    'Microsoft.Azure.Cirrus.Extensions.Extension',
    'Microsoft.Azure.Azclips.Extensions.Login',
    'Microsoft.Azure.Cirrus.Extensions.Config'
)

foreach ($ext in $builtins) {
    $extPath = Join-Path $SimRoot ("install/Extensions/$ext")
    if (-not (Test-Path -Path $extPath -PathType Container)) {
        throw "FAIL: Extensions/$ext missing in extracted zip."
    }
    Write-Host "PASS: Extensions/$ext present"
}

$resNupkg = Get-ChildItem -Path $ExtPackDir -Filter 'Microsoft.Azure.Azclips.Extensions.Resources*.nupkg' | Select-Object -First 1

if (-not $resNupkg) { throw 'FAIL: Resources nupkg not found.' }

Write-Host "PASS: Resources package found: $($resNupkg.FullName)"

if ($IsWindows -and -not $SkipRuntime) {
    Write-Host ''
    Write-Host '[Runtime] Running az.exe checks on Windows'

    $env:USERPROFILE = Join-Path $SimRoot 'home'

    & $azExe --help | Select-Object -First 20 | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "FAIL: az.exe --help exited with code $LASTEXITCODE."
    }

    Write-Host 'Verifying bundled Config command behavior'
    & $azExe config set --key core.logging.level --value debug --force | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "FAIL: config set exited with code $LASTEXITCODE."
    }
    $cfgGetOut = & $azExe config get --key core.logging.level 2>&1
    $cfgGetExitCode = $LASTEXITCODE
    $cfgGetText = $cfgGetOut | Out-String
    $cfgGetText.Trim() | Out-Host
    if ($cfgGetExitCode -ne 0) {
        throw "FAIL: config get exited with code $cfgGetExitCode."
    }
    if ($cfgGetText -notmatch 'core\.logging\.level\s*=\s*debug') {
        throw 'FAIL: Config command output did not confirm debug value.'
    }

    $addResOut = & $azExe extension add --path $resNupkg.FullName 2>&1
    $addResExitCode = $LASTEXITCODE
    $addResText = $addResOut | Out-String
    $addResText.Trim() | Out-Host
    if ($addResExitCode -ne 0) {
        throw "FAIL: extension add exited with code $addResExitCode."
    }
    if ($addResText -notmatch 'installedExtensionPath') {
        throw 'FAIL: Resources extension installation did not report installedExtensionPath.'
    }

    $groupOut = & $azExe group list 2>&1
    $groupExitCode = $LASTEXITCODE
    $groupText = $groupOut | Out-String
    $groupText.Trim() | Out-Host
    if ($groupText -match 'could not be found') {
        throw 'FAIL: Resources command not resolved after install.'
    } elseif ($groupText -match "Required parameter 'SubscriptionId' is missing|SubscriptionId must be provided") {
        Write-Host 'PASS: Resources command resolved (missing SubscriptionId in CI context)'
    } elseif ($groupText -match 'AcquireTokenSilent|No account or login hint|please run .*login|AADSTS|authentication') {
        Write-Host 'PASS: Resources command resolved (authentication context not available in CI)'
    } else {
        if ($groupExitCode -ne 0) {
            throw "FAIL: group list exited with code $groupExitCode."
        }
        Write-Host 'PASS: Resources command resolved'
    }

    $global:LASTEXITCODE = 0

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

$global:LASTEXITCODE = 0
