param(
    [string]$OutRoot = (Join-Path $PSScriptRoot '../../../../out/windows'),
    [string]$CommonOutRoot = (Join-Path $PSScriptRoot '../../../../out/common'),
    [string]$Rid = 'win-x64',
    [string]$CliZipPath = '',
    [string]$SimRoot = ''
)

$ErrorActionPreference = 'Stop'

if (-not $IsWindows) {
    throw "Windows runtime validation requires a Windows host. Detected: $([System.Runtime.InteropServices.RuntimeInformation]::OSDescription)."
}

$hostArchitecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
$expectedHostArchitecture = switch ($Rid) {
    'win-x64' { 'x64' }
    'win-arm64' { 'arm64' }
    default { throw "Unsupported RID '$Rid'. Expected 'win-x64' or 'win-arm64'." }
}
if ($hostArchitecture -ne $expectedHostArchitecture) {
    throw "Runtime validation for $Rid requires a matching host architecture. Detected: $hostArchitecture."
}

$OutRoot = [System.IO.Path]::GetFullPath($OutRoot)
$CommonOutRoot = [System.IO.Path]::GetFullPath($CommonOutRoot)

$packagingRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $packagingRoot '../../..'))
$installerPath = Join-Path $repoRoot 'install/azcli-install.ps1'
$uninstallerPath = Join-Path $repoRoot 'install/azcli-uninstall.ps1'

function Assert-InstallFailure {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Description,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedPattern,

        [Parameter(Mandatory = $true)]
        [hashtable]$Arguments
    )

    try {
        & $installerPath @Arguments *> $null
        throw "FAIL: $Description unexpectedly succeeded."
    }
    catch {
        if ($_.Exception.Message -like 'FAIL:*unexpectedly succeeded.*') {
            throw
        }
        $exceptionMessages = @()
        $currentException = $_.Exception
        while ($null -ne $currentException) {
            $exceptionMessages += $currentException.Message
            $currentException = $currentException.InnerException
        }
        $failureText = (@($_ | Out-String) + $exceptionMessages) -join [Environment]::NewLine
        if ($failureText -notmatch $ExpectedPattern) {
            throw "FAIL: $Description did not report the expected error. Actual error: $failureText"
        }
        Write-Host "  PASS: $Description was rejected"
    }
}

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

$installRoot = Join-Path $SimRoot 'install'
$homeRoot = Join-Path $SimRoot 'home'
$invalidArchivesRoot = Join-Path $SimRoot 'invalid-archives'
$classicBinRoot = Join-Path $SimRoot 'classic-bin'
$null = New-Item -Path $installRoot, $homeRoot, $invalidArchivesRoot, $classicBinRoot -ItemType Directory -Force

$env:USERPROFILE = $homeRoot
$env:AZCLIPS_HOME = Join-Path $homeRoot '.azclips'
$env:AZCLI_INSTALL_ROOT = $installRoot

$archiveName = Split-Path -Leaf $CliZipPath
$archiveVersionAndRid = $archiveName.Substring($artifactStem.Length + 1)
$fullStem = Get-ArtifactConfigValue -Name 'cli_archive_stem' -DefaultValue 'azcli'
$leanStem = Get-ArtifactConfigValue -Name 'cli_lean_archive_stem' -DefaultValue 'azcli-lean'
$currentAssetRid = if ($Rid -eq 'win-x64') {
    Get-ArtifactConfigValue -Name 'windows_x64_label' -DefaultValue 'win-x64'
}
else {
    Get-ArtifactConfigValue -Name 'windows_arm64_label' -DefaultValue 'win-arm64'
}
$wrongProfileName = if ($packageProfile -eq 'lean') {
    "$fullStem-$archiveVersionAndRid"
}
else {
    "$leanStem-$archiveVersionAndRid"
}
$oppositeAssetRid = if ($Rid -eq 'win-x64') {
    Get-ArtifactConfigValue -Name 'windows_arm64_label' -DefaultValue 'win-arm64'
}
else {
    Get-ArtifactConfigValue -Name 'windows_x64_label' -DefaultValue 'win-x64'
}
$wrongRidName = $archiveName -replace "-$([regex]::Escape($currentAssetRid))\.zip$", "-$oppositeAssetRid.zip"
    $wrongProfilePath = Join-Path $invalidArchivesRoot $wrongProfileName
    $wrongRidPath = Join-Path $invalidArchivesRoot $wrongRidName
    $corruptArchivePath = Join-Path $invalidArchivesRoot $archiveName
    Copy-Item -Path $CliZipPath -Destination $wrongProfilePath
    Copy-Item -Path $CliZipPath -Destination $wrongRidPath
    Set-Content -Path $corruptArchivePath -Value 'not a zip archive'

    Write-Host '[1/15] Reject invalid local archives'
    Assert-InstallFailure -Description 'empty archive path' -ExpectedPattern 'ArchivePath requires a path' -Arguments @{
        Package = $packageProfile
        ArchivePath = ''
    }
    Assert-InstallFailure -Description 'missing archive' -ExpectedPattern 'Archive was not found' -Arguments @{
        Package = $packageProfile
        ArchivePath = (Join-Path $invalidArchivesRoot "missing-$archiveName")
    }
    Assert-InstallFailure -Description 'wrong package profile' -ExpectedPattern "is not a $packageProfile CLI package" -Arguments @{
        Package = $packageProfile
        ArchivePath = $wrongProfilePath
    }
    Assert-InstallFailure -Description 'wrong runtime identifier' -ExpectedPattern 'does not match platform' -Arguments @{
        Package = $packageProfile
        ArchivePath = $wrongRidPath
    }
    $installSentinel = Join-Path $installRoot 'az.exe'
    Set-Content -Path $installSentinel -Value 'existing install sentinel'
    Assert-InstallFailure -Description 'corrupt archive' -ExpectedPattern 'archive|zip|central directory' -Arguments @{
        Package = $packageProfile
        ArchivePath = $corruptArchivePath
    }
    if ((Get-Content -Path $installSentinel -Raw).Trim() -ne 'existing install sentinel') {
        throw 'FAIL: corrupt archive modified the existing installation.'
    }
    Write-Host '  PASS: corrupt archive preserved the existing installation'
    Remove-Item -Path $invalidArchivesRoot -Recurse -Force

    $classicAz = Join-Path $classicBinRoot 'az.cmd'
    Set-Content -Path $classicAz -Value '@echo classic az sentinel'

    & $installerPath -Package $packageProfile -ArchivePath $CliZipPath
    if ($LASTEXITCODE -ne 0) {
        throw "FAIL: installer exited with code $LASTEXITCODE."
    }

$azExe = Join-Path $installRoot 'az.exe'
if (-not (Test-Path -Path $azExe -PathType Leaf)) {
    throw 'FAIL: az.exe not found in installed payload.'
}
Write-Host 'PASS: az.exe found'

$copilotExe = Join-Path $installRoot "Extensions/Microsoft.Azure.Azclips.Extensions.Agent/deps/runtimes/$Rid/native/copilot.exe"
if (-not (Test-Path -Path $copilotExe -PathType Leaf)) {
    throw "FAIL: Bundled Copilot CLI not found at the agent resolver path: $copilotExe"
}
Write-Host 'PASS: Bundled Copilot CLI found at the agent resolver path'

foreach ($legalFile in @('NOTICE', 'LICENSE')) {
    $legalPath = Join-Path $installRoot $legalFile
    if (-not (Test-Path -Path $legalPath -PathType Leaf)) {
        throw "FAIL: $legalFile file missing from extracted zip."
    }
    Write-Host "PASS: $legalFile file present"
}

foreach ($completionFile in @('az.completions.ps1', 'az.completions.bash', 'az.completions.zsh')) {
    $completionPath = Join-Path (Join-Path $installRoot 'completions') $completionFile
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
    $extPath = Join-Path $installRoot "Extensions/$ext"
    if (-not (Test-Path -Path $extPath -PathType Container)) {
        throw "FAIL: Extensions/$ext missing in extracted zip."
    }
    Write-Host "PASS: Extensions/$ext present"
}

$resNupkg = Get-ChildItem -Path $ExtPackDir -Filter 'azcli.extensions.resources*.nupkg' | Select-Object -First 1

if (-not $resNupkg) { throw 'FAIL: Resources nupkg not found.' }

Write-Host "PASS: Resources package found: $($resNupkg.FullName)"

    Write-Host ''
    Write-Host '[Runtime] Running az.exe checks on Windows'

    Write-Host ''
    Write-Host '[2/15] CLI version (bundled install)'
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
    Write-Host '[3/15] CLI help (bundled install)'
    $helpOutput = & $azExe --help 2>&1
    $helpExitCode = $LASTEXITCODE
    $helpOutput | Select-Object -First 20 | Out-Host
    if ($helpExitCode -ne 0) {
        throw "FAIL: az.exe --help exited with code $helpExitCode."
    }

    Write-Host ''
    Write-Host '[4/15] Verify bundled Copilot CLI daemon starts'
    $agentTempRoot = if ([string]::IsNullOrWhiteSpace($env:AGENT_TEMPDIRECTORY)) {
        [System.IO.Path]::GetTempPath()
    }
    else {
        $env:AGENT_TEMPDIRECTORY
    }
    $copilotLogRoot = Join-Path $agentTempRoot "azclips-copilot-$([guid]::NewGuid())"
    $null = New-Item -Path $copilotLogRoot -ItemType Directory -Force
    $copilotInput = Join-Path $copilotLogRoot 'copilot.stdin'
    $copilotOutput = Join-Path $copilotLogRoot 'copilot.stdout'
    $copilotError = Join-Path $copilotLogRoot 'copilot.stderr'
    Set-Content -Path $copilotInput -Value '' -NoNewline
    $copilotProcess = $null
    $copilotExitCode = $null
    $copilotTimedOut = $false
    try {
        $copilotProcess = Start-Process -FilePath $copilotExe `
            -ArgumentList '--server', '--stdio' `
            -NoNewWindow `
            -PassThru `
            -RedirectStandardInput $copilotInput `
            -RedirectStandardOutput $copilotOutput `
            -RedirectStandardError $copilotError
        if (-not $copilotProcess.WaitForExit(15000)) {
            $copilotProcess.Kill($true)
            $copilotTimedOut = $true
        }
        # The parameterless wait flushes asynchronous redirected streams after the timed wait.
        $copilotProcess.WaitForExit()
        $copilotExitCode = $copilotProcess.ExitCode
    }
    finally {
        if ($null -ne $copilotProcess) {
            if (-not $copilotProcess.HasExited) {
                $copilotProcess.Kill($true)
                $copilotProcess.WaitForExit()
            }
            $copilotProcess.Dispose()
        }
        try {
            Copy-Item -Path $copilotInput, $copilotOutput, $copilotError -Destination $SimRoot
        }
        finally {
            Remove-Item -Path $copilotLogRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    if ($copilotTimedOut) {
        throw 'FAIL: Bundled Copilot CLI daemon did not stop within 15 seconds.'
    }
    $publishedCopilotOutput = Join-Path $SimRoot 'copilot.stdout'
    $publishedCopilotError = Join-Path $SimRoot 'copilot.stderr'
    if ($copilotExitCode -ne 0) {
        Get-Content -Path $publishedCopilotOutput, $publishedCopilotError | Select-Object -First 20 | Out-Host
        throw "FAIL: Bundled Copilot CLI daemon exited with code $copilotExitCode."
    }
    Write-Host 'PASS: Bundled Copilot CLI daemon started and stopped cleanly'

    Write-Host ''
    Write-Host '[5/15] Verify built-in extension manager is available'
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
    Write-Host '[6/15] Verify bundled Config commands work'
    & $azExe config set --key core.logging.level --value debug --force | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "FAIL: config set exited with code $LASTEXITCODE."
    }
    $cfgGetOut = & $azExe config get --key core.logging.level --query 'value'
    $cfgGetExitCode = $LASTEXITCODE
    $cfgGetText = $cfgGetOut | Out-String
    $cfgGetText.Trim() | Out-Host
    if ($cfgGetExitCode -ne 0) {
        throw "FAIL: config get exited with code $cfgGetExitCode."
    }
    if ($cfgGetText.Trim() -ne 'debug') {
        throw 'FAIL: Config command output did not confirm debug value.'
    }

    Write-Host ''
    Write-Host '[7/15] Verify reinstall cleanly replaces the install root'
    $reinstallSentinel = Join-Path $installRoot 'reinstall-sentinel'
    Set-Content -Path $reinstallSentinel -Value 'stale file'
    & $installerPath -Package $packageProfile -ArchivePath $CliZipPath
    if ($LASTEXITCODE -ne 0) {
        throw "FAIL: reinstall exited with code $LASTEXITCODE."
    }
    if (Test-Path $reinstallSentinel) {
        throw 'FAIL: Reinstall did not cleanly replace the install root.'
    }
    Write-Host 'PASS: Reinstall removed stale files and restored the CLI'

    Write-Host ''
    Write-Host "[8/15] Install Resources extension via 'extension add'"
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
    Write-Host '[9/15] Verify Resources appears in extension list JSON'
    $listOutput = & $azExe extension list --output json 2>$null
    $listExitCode = $LASTEXITCODE
    if ($listExitCode -ne 0) {
        throw "FAIL: extension list exited with code $listExitCode."
    }
    $listJson = $listOutput -join [Environment]::NewLine
    $listedResources = @($listJson | ConvertFrom-Json) | Where-Object packageId -eq 'Microsoft.Azure.Azclips.Extensions.Resources'
    if ($listedResources.Count -ne 1 -or [string]::IsNullOrWhiteSpace($listedResources[0].selectedVersion)) {
        throw 'FAIL: extension list JSON did not contain one Resources package with a selected version.'
    }
    Write-Host 'PASS: extension list reports Resources and its selected version'

    Write-Host ''
    Write-Host '[10/15] Verify Resources appears in extension show JSON'
    $showOutput = & $azExe extension show --name Resources --output json 2>$null
    $showExitCode = $LASTEXITCODE
    if ($showExitCode -ne 0) {
        throw "FAIL: extension show exited with code $showExitCode."
    }
    $showJson = $showOutput -join [Environment]::NewLine
    $shownResources = $showJson | ConvertFrom-Json
    if ($shownResources.packageId -ne 'Microsoft.Azure.Azclips.Extensions.Resources' -or [string]::IsNullOrWhiteSpace($shownResources.location)) {
        throw 'FAIL: extension show JSON did not contain the Resources package and location.'
    }
    Write-Host 'PASS: extension show reports Resources and its location'

    Write-Host ''
    Write-Host '[11/15] Verify Resources command resolves'
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

    Write-Host ''
    Write-Host '[12/15] Remove Resources extension'
    $removeOutput = & $azExe extension remove --name Resources 2>&1
    $removeExitCode = $LASTEXITCODE
    $removeOutput | Out-Host
    if ($removeExitCode -ne 0) {
        throw "FAIL: extension remove exited with code $removeExitCode."
    }
    Write-Host 'PASS: Resources extension remove completed'

    Write-Host ''
    Write-Host '[13/15] Verify Resources files and registry entry are gone'
    $extensionsRoot = Join-Path $env:AZCLIPS_HOME 'extensions'
    $remainingResources = Get-ChildItem -Path $extensionsRoot -Recurse -Force -ErrorAction SilentlyContinue |
        Where-Object Name -Match 'resources' |
        Select-Object -First 1
    if ($remainingResources) {
        throw "FAIL: Resources files remain at $($remainingResources.FullName)."
    }
    $registryPath = Join-Path $extensionsRoot 'registry.json'
    if ((Test-Path $registryPath) -and (Get-Content -Path $registryPath -Raw) -match 'Microsoft\.Azure\.Azclips\.Extensions\.Resources') {
        throw 'FAIL: Resources remains in the extension registry.'
    }
    Write-Host 'PASS: Resources files and registry entry are gone'

    Write-Host ''
    Write-Host '[14/15] Verify Resources command no longer resolves'
    $removedCommandOutput = & $azExe group list 2>&1
    $removedCommandText = $removedCommandOutput | Out-String
    $removedCommandText.Trim() | Out-Host
    if ($packageProfile -eq 'lean' -and $removedCommandText -match '\[ExtensionNotFound\]') {
        Write-Host 'PASS: Removed Resources command reports the extension installation path'
    }
    elseif ($packageProfile -eq 'full' -and
        $removedCommandText -match "\[BundledExtensionLoader\] Loaded 'Resources'" -and
        $removedCommandText -notmatch '\[ExtensionNotFound\]') {
        Write-Host 'PASS: Resources command fell back to the bundled full-package extension'
    }
    else {
        throw "FAIL: az group list did not match the expected $packageProfile profile behavior after removal."
    }

    Write-Host ''
    Write-Host '[15/15] Uninstall isolated azcli and preserve classic az'
    & $uninstallerPath
    if (Test-Path $installRoot) {
        throw 'FAIL: azcli install root remains after uninstall.'
    }
    $classicOutput = & $classicAz
    if ($LASTEXITCODE -ne 0 -or ($classicOutput | Out-String).Trim() -ne 'classic az sentinel') {
        throw 'FAIL: Uninstall modified the classic az sentinel.'
    }
    Write-Host 'PASS: Isolated azcli was removed and classic az was preserved'

    $global:LASTEXITCODE = 0

    Write-Host 'PASS: Windows installer, runtime, reinstall, extension lifecycle, failure paths, and uninstall validated'

Write-Host ''
Write-Host '============================================'
Write-Host ' Windows Bundled+Optional PS Test Complete '
Write-Host '============================================'

$global:LASTEXITCODE = 0
