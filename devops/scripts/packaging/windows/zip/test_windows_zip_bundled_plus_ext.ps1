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

$packagingRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))

function Get-ArtifactConfigValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$DefaultValue
    )

    $configPath = $env:ARTIFACT_NAMING_CONFIG
    if ([string]::IsNullOrWhiteSpace($configPath)) {
        $configPath = Join-Path $packagingRoot 'config/artifact_naming.config'
    }

    if (Test-Path -Path $configPath -PathType Leaf) {
        foreach ($line in Get-Content -Path $configPath) {
            $trimmed = $line.Trim()
            if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('#')) {
                continue
            }

            $parts = $trimmed.Split('=', 2)
            if ($parts.Count -eq 2 -and $parts[0].Trim() -eq $Name) {
                return $parts[1].Trim()
            }
        }
    }

    return $DefaultValue
}

$packageProfile = $env:PACKAGE_PROFILE
if ([string]::IsNullOrWhiteSpace($packageProfile)) {
    $packageProfile = 'lean'
}
$packageProfile = $packageProfile.ToLowerInvariant()

if ($packageProfile -eq 'full') {
    $artifactStem = $env:CLI_ARCHIVE_STEM
    if ([string]::IsNullOrWhiteSpace($artifactStem)) {
        $artifactStem = Get-ArtifactConfigValue -Name 'cli_archive_stem' -DefaultValue 'azcli'
    }
}
elseif ($packageProfile -eq 'lean') {
    $artifactStem = $env:CLI_LEAN_ARCHIVE_STEM
    if ([string]::IsNullOrWhiteSpace($artifactStem)) {
        $artifactStem = Get-ArtifactConfigValue -Name 'cli_lean_archive_stem' -DefaultValue 'azcli-lean'
    }
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
    $artifactVersionSuffix = [System.Environment]::GetEnvironmentVariable('ARTIFACT_VERSION_SUFFIX')
    if ($null -eq $artifactVersionSuffix) {
        $artifactVersionSuffix = Get-ArtifactConfigValue -Name 'artifact_version_suffix' -DefaultValue 'preview'
    }
    $assetArchitecture = $Rid
    if ($Rid -eq 'win-x64') {
        $assetArchitecture = Get-ArtifactConfigValue -Name 'windows_x64_label' -DefaultValue 'win-x64'
    }
    elseif ($Rid -eq 'win-arm64') {
        $assetArchitecture = Get-ArtifactConfigValue -Name 'windows_arm64_label' -DefaultValue 'win-arm64'
    }

    if (-not [string]::IsNullOrWhiteSpace($artifactVersion)) {
        $artifactVersionSegment = $artifactVersion
        if (-not [string]::IsNullOrWhiteSpace($artifactVersionSuffix) -and -not $artifactVersionSegment.EndsWith("-$artifactVersionSuffix", [System.StringComparison]::OrdinalIgnoreCase)) {
            $artifactVersionSegment = "$artifactVersionSegment-$artifactVersionSuffix"
        }

        $CliZipPath = Join-Path $OutRoot "Release/zip/$artifactStem-$artifactVersionSegment-$assetArchitecture.zip"
    }
    else {
        $CliZipPath = Join-Path $OutRoot "Release/zip/$artifactStem-$assetArchitecture.zip"
    }
}
else {
    $CliZipPath = [System.IO.Path]::GetFullPath($CliZipPath)
}
$ExtPackDir = Join-Path $CommonOutRoot 'extensions-pack'

if (-not (Test-Path -Path $CliZipPath -PathType Leaf)) {
    throw "Windows CLI zip not found at $CliZipPath. Run ./devops/scripts/packaging/common/build_common.sh, then ./devops/scripts/packaging/windows/zip/build_win_zip.ps1."
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

$copilotExe = Join-Path $SimRoot "install/Extensions/Microsoft.Azure.Azclips.Extensions.Agent/deps/runtimes/$Rid/native/copilot.exe"
if (-not (Test-Path -Path $copilotExe -PathType Leaf)) {
    throw "FAIL: Bundled Copilot CLI not found at the agent resolver path: $copilotExe"
}
Write-Host 'PASS: Bundled Copilot CLI found at the agent resolver path'

foreach ($legalFile in @('NOTICE', 'LICENSE')) {
    $legalPath = Join-Path $SimRoot "install/$legalFile"
    if (-not (Test-Path -Path $legalPath -PathType Leaf)) {
        throw "FAIL: $legalFile file missing from extracted zip."
    }
    Write-Host "PASS: $legalFile file present"
}

foreach ($completionFile in @('az.completions.ps1', 'az.completions.bash', 'az.completions.zsh')) {
    $completionPath = Join-Path (Join-Path (Join-Path $SimRoot 'install') 'completions') $completionFile
    if (-not (Test-Path -Path $completionPath -PathType Leaf)) {
        throw "FAIL: completions/$completionFile missing from extracted zip."
    }
    Write-Host "PASS: completions/$completionFile present"
}

$builtins = @(
    'Microsoft.Azure.Azclips.Extensions.Extension',
    'Microsoft.Azure.Azclips.Extensions.Accounts',
    'Microsoft.Azure.Azclips.Extensions.Config'
)

foreach ($ext in $builtins) {
    $extPath = Join-Path $SimRoot ("install/Extensions/$ext")
    if (-not (Test-Path -Path $extPath -PathType Container)) {
        throw "FAIL: Extensions/$ext missing in extracted zip."
    }
    Write-Host "PASS: Extensions/$ext present"
}

$resNupkg = Get-ChildItem -Path $ExtPackDir -Filter 'azcli.extensions.resources*.nupkg' | Select-Object -First 1

if (-not $resNupkg) { throw 'FAIL: Resources nupkg not found.' }

Write-Host "PASS: Resources package found: $($resNupkg.FullName)"

if ($IsWindows -and -not $SkipRuntime) {
    Write-Host ''
    Write-Host '[Runtime] Running az.exe checks on Windows'

    $env:USERPROFILE = Join-Path $SimRoot 'home'

    Write-Host ''
    Write-Host '[1/7] CLI version (bundled install)'
    $versionOutput = & $azExe --version 2>&1
    $versionExitCode = $LASTEXITCODE
    $versionText = $versionOutput | Out-String
    $versionText.Trim() | Out-Host
    if ($versionExitCode -ne 0) {
        throw "FAIL: az.exe --version exited with code $versionExitCode."
    }
    if ([string]::IsNullOrWhiteSpace($versionText)) {
        throw 'FAIL: az.exe --version returned no output.'
    }

    Write-Host ''
    Write-Host '[2/7] CLI help (bundled install)'
    & $azExe --help | Select-Object -First 20 | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "FAIL: az.exe --help exited with code $LASTEXITCODE."
    }

    Write-Host ''
    Write-Host '[3/7] Verify bundled Copilot CLI daemon starts'
    $copilotInput = Join-Path $SimRoot 'copilot.stdin'
    $copilotOutput = Join-Path $SimRoot 'copilot.stdout'
    $copilotError = Join-Path $SimRoot 'copilot.stderr'
    Set-Content -Path $copilotInput -Value '' -NoNewline
    $copilotProcess = Start-Process -FilePath $copilotExe `
        -ArgumentList '--server', '--stdio' `
        -NoNewWindow `
        -PassThru `
        -RedirectStandardInput $copilotInput `
        -RedirectStandardOutput $copilotOutput `
        -RedirectStandardError $copilotError
    if (-not $copilotProcess.WaitForExit(15000)) {
        $copilotProcess.Kill($true)
        $copilotProcess.WaitForExit()
        throw 'FAIL: Bundled Copilot CLI daemon did not stop within 15 seconds.'
    }
    if ($copilotProcess.ExitCode -ne 0) {
        Get-Content -Path $copilotOutput, $copilotError | Select-Object -First 20 | Out-Host
        throw "FAIL: Bundled Copilot CLI daemon exited with code $($copilotProcess.ExitCode)."
    }
    Write-Host 'PASS: Bundled Copilot CLI daemon started and stopped cleanly'

    Write-Host ''
    Write-Host '[4/7] Verify built-in extension manager is available'
    $extensionOutput = & $azExe extension add 2>&1
    $extensionText = $extensionOutput | Out-String
    $extensionText.Trim() | Out-Host
    if ($extensionText -match 'Extension.*could not be found') {
        throw 'FAIL: Extension manager not loaded.'
    }
    if ($extensionText -notmatch 'Either Name or Path must be provided|Required option.*(name|path)') {
        throw 'FAIL: Extension command response was unexpected.'
    }
    Write-Host 'PASS: Extension manager loaded'
    $global:LASTEXITCODE = 0

    Write-Host ''
    Write-Host '[5/7] Verify bundled Config commands work'
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

    Write-Host ''
    Write-Host "[6/7] Install Resources extension via 'extension add'"
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
    Write-Host 'PASS: Resources extension installed'

    Write-Host ''
    Write-Host '[7/7] Verify Resources command resolves'
    $groupOut = & $azExe group list 2>&1
    $groupExitCode = $LASTEXITCODE
    $groupText = $groupOut | Out-String
    $groupText.Trim() | Out-Host
    if ($groupText -match 'could not be found') {
        throw 'FAIL: Resources command not resolved after install.'
    } elseif ($groupText -match "Required parameter 'SubscriptionId' is missing|SubscriptionId must be provided|SubscriptionId cannot be resolved") {
        Write-Host 'PASS: Resources command resolved (missing SubscriptionId in CI context)'
    } elseif ($groupText -match "Missing required argument 'SubscriptionId' and no value found in profile.") {
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
