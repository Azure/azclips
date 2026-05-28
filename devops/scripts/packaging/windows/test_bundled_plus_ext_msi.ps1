param(
    [string]$OutRoot = (Join-Path $PSScriptRoot '../../../out/windows'),
    [string]$CommonOutRoot = (Join-Path $PSScriptRoot '../../../out/common'),
    [string]$SimRoot = (Join-Path $PSScriptRoot '../../../sim/win-msi-bundled-plus-ext'),
    [string]$ProductName = 'Cirrus CLI',
    [string]$ProductVersion = '',
    [switch]$SkipRuntime,
    [switch]$KeepInstalled
)

$ErrorActionPreference = 'Stop'

$OutRoot = [System.IO.Path]::GetFullPath($OutRoot)
$CommonOutRoot = [System.IO.Path]::GetFullPath($CommonOutRoot)
$SimRoot = [System.IO.Path]::GetFullPath($SimRoot)

$MsiPath = Join-Path $OutRoot 'bin/Release/azv2-win-x64.msi'
if (-not (Test-Path -Path $MsiPath -PathType Leaf)) {
    $artifactMsiPath = Join-Path $OutRoot 'azv2-win-x64.msi'
    if (Test-Path -Path $artifactMsiPath -PathType Leaf) {
        $MsiPath = $artifactMsiPath
    }
}
$ExtPackDir = Join-Path $CommonOutRoot 'extensions-pack'
$InstallLog = Join-Path $SimRoot 'msi-install.log'
$UninstallLog = Join-Path $SimRoot 'msi-uninstall.log'
$IsWindowsHost = $env:OS -eq 'Windows_NT'
$BundledExtConfig = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../config/bundled_ext.config'))

if (-not (Test-Path -Path $BundledExtConfig -PathType Leaf)) {
    throw "Bundled extension config not found at $BundledExtConfig"
}

$builtins = foreach ($line in Get-Content $BundledExtConfig) {
    $extensionName = ($line -replace '#.*$', '').Trim()
    if ([string]::IsNullOrWhiteSpace($extensionName)) { continue }
    "Microsoft.Azure.Cirrus.Extensions.$extensionName"
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-MsiProductCode([string]$Path) {
    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database = $installer.OpenDatabase($Path, 0)
    $view = $database.OpenView("SELECT ``Value`` FROM ``Property`` WHERE ``Property`` = 'ProductCode'")
    $view.Execute()
    $record = $view.Fetch()

    if ($null -eq $record) {
        throw "FAIL: ProductCode not found in $Path"
    }

    return $record.StringData(1)
}

function ConvertTo-MsiPackedVersion([string]$Version) {
    $parts = $Version.Split('.')
    if ($parts.Count -lt 3) {
        throw "FAIL: ProductVersion '$Version' must contain at least major.minor.build for MSI log comparison."
    }

    return ([int]$parts[0] * 16777216) + ([int]$parts[1] * 65536) + [int]$parts[2]
}

function Get-MsiLogProductInfo([string]$LogPath) {
    if (-not (Test-Path -Path $LogPath -PathType Leaf)) {
        throw "FAIL: MSI install log not found at $LogPath"
    }

    $productInfoLine = Get-Content $LogPath | Where-Object { $_ -like '*ProductInfo*' } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($productInfoLine)) {
        throw "FAIL: ProductInfo line was not found in MSI install log: $LogPath"
    }

    $properties = @{}
    if ($productInfoLine -match 'ProductInfo\((?<content>.*)\)') {
        foreach ($entry in $Matches.content -split ',') {
            $keyValue = $entry -split '=', 2
            if ($keyValue.Count -eq 2) {
                $properties[$keyValue[0].Trim()] = $keyValue[1].Trim()
            }
        }
    }

    return $properties
}

function Get-InstalledProductCodeByName([string]$Name) {
    $roots = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
    )

    foreach ($root in $roots) {
        if (-not (Test-Path $root)) { continue }

        foreach ($key in Get-ChildItem $root) {
            $item = Get-ItemProperty $key.PSPath -ErrorAction SilentlyContinue
            if ($item.DisplayName -eq $Name -and $key.PSChildName -match '^\{[0-9A-Fa-f-]+\}$') {
                return $key.PSChildName
            }
        }
    }

    return $null
}

function Invoke-MsiExec([string[]]$Arguments, [string]$FailureMessage) {
    $process = Start-Process msiexec.exe -Wait -PassThru -ArgumentList $Arguments
    if ($process.ExitCode -ne 0) {
        throw "$FailureMessage ExitCode=$($process.ExitCode)"
    }
}

if (-not (Test-Path -Path $MsiPath -PathType Leaf)) {
    throw "Windows CLI MSI not found at $MsiPath. Run ./devops/scripts/packaging/common/build_common.sh, then .\devops\scripts\packaging\windows\build_win_msi.ps1."
}

if (-not (Test-Path -Path $ExtPackDir -PathType Container)) {
    throw "Extension package directory not found at $ExtPackDir. Run ./devops/scripts/packaging/common/build_common.sh first."
}

Write-Host '============================================'
Write-Host ' Windows: MSI Bundled CLI + Optional Ext    '
Write-Host '        (PowerShell-based validation)       '
Write-Host '============================================'

if (Test-Path $SimRoot) {
    Remove-Item -Path $SimRoot -Recurse -Force
}

$null = New-Item -Path $SimRoot -ItemType Directory -Force
$null = New-Item -Path (Join-Path $SimRoot 'home') -ItemType Directory -Force

Write-Host "PASS: MSI package found: $MsiPath"

$resNupkg = Get-ChildItem -Path $ExtPackDir -Filter 'Microsoft.Azure.Cirrus.Extensions.Resources*.nupkg' | Select-Object -First 1
if (-not $resNupkg) { throw 'FAIL: Resources nupkg not found.' }
Write-Host "PASS: Resources package found: $($resNupkg.FullName)"

if (-not $IsWindowsHost -or $SkipRuntime) {
    Write-Host ''
    Write-Host 'NOTE: Runtime execution skipped (non-Windows host or -SkipRuntime set).'
    Write-Host '      MSI/package availability validation completed successfully.'
    return
}

if (-not (Test-IsAdministrator)) {
    throw 'FAIL: MSI runtime validation requires elevated Windows PowerShell because the MSI installs per-machine.'
}

Write-Host ''
Write-Host '[Runtime] Installing MSI and running azv2.exe checks on Windows'

$InstallPath = Join-Path ${env:ProgramFiles} 'Cirrus CLI'
$Azv2Exe = Join-Path $InstallPath 'azv2.exe'
$InstalledExtensions = Join-Path $InstallPath 'Extensions'

$existingProductCode = Get-InstalledProductCodeByName -Name $ProductName
if ($existingProductCode) {
    Write-Host "Removing existing $ProductName install: $existingProductCode"
    Invoke-MsiExec -Arguments @('/x', $existingProductCode, '/quiet', '/norestart') -FailureMessage 'FAIL: Pre-test uninstall failed.'
}

$productCode = Get-MsiProductCode -Path $MsiPath
$installCompleted = $false
$originalUserProfile = $env:USERPROFILE

try {
    Invoke-MsiExec -Arguments @('/i', "`"$MsiPath`"", '/quiet', '/norestart', '/L*V', "`"$InstallLog`"") -FailureMessage "FAIL: MSI install failed. See $InstallLog."
    $installCompleted = $true

    if (-not (Test-Path -Path $Azv2Exe -PathType Leaf)) {
        throw "FAIL: azv2.exe not found after MSI install: $Azv2Exe"
    }
    Write-Host 'PASS: azv2.exe found after MSI install'

    $productInfo = Get-MsiLogProductInfo -LogPath $InstallLog
    if ($productInfo.ProductName -ne $ProductName) {
        throw "FAIL: Install log ProductName '$($productInfo.ProductName)' did not match '$ProductName'."
    }
    Write-Host "PASS: Install log ProductName is $ProductName"

    $registryItem = Get-ItemProperty 'HKLM:\Software\Cirrus CLI' -ErrorAction Stop
    if ([string]::IsNullOrWhiteSpace($registryItem.InstallDir)) {
        throw 'FAIL: HKLM:\Software\Cirrus CLI InstallDir registry value was empty.'
    }

    if (-not [string]::IsNullOrWhiteSpace($ProductVersion)) {
        $expectedPackedVersion = ConvertTo-MsiPackedVersion -Version $ProductVersion
        if ([int]$productInfo.Version -ne $expectedPackedVersion) {
            throw "FAIL: Install log Version '$($productInfo.Version)' did not match packed ProductVersion '$expectedPackedVersion' for '$ProductVersion'."
        }

        if ($registryItem.Version -ne $ProductVersion) {
            throw "FAIL: HKLM:\Software\Cirrus CLI Version '$($registryItem.Version)' did not match '$ProductVersion'."
        }

        Write-Host "PASS: Install log and registry version match $ProductVersion"
    }

    foreach ($ext in $builtins) {
        $extPath = Join-Path $InstalledExtensions $ext
        $extDll = Join-Path $extPath "$ext.dll"

        if (-not (Test-Path -Path $extPath -PathType Container)) {
            throw "FAIL: Extensions/$ext missing after MSI install."
        }

        if (-not (Test-Path -Path $extDll -PathType Leaf)) {
            throw "FAIL: Extensions/$ext/$ext.dll missing after MSI install."
        }

        Write-Host "PASS: Extensions/$ext present after MSI install"
    }

    $env:USERPROFILE = Join-Path $SimRoot 'home'

    & $Azv2Exe --help | Select-Object -First 20 | Out-Host

    Write-Host 'Verifying bundled Config command behavior'
    & $Azv2Exe config set --key core.logging.level --value debug --force | Out-Host
    $cfgGetOut = & $Azv2Exe config get --key core.logging.level 2>&1
    $cfgGetText = $cfgGetOut | Out-String
    $cfgGetText.Trim() | Out-Host
    if ($cfgGetText -notmatch 'core\.logging\.level\s*=\s*debug') {
        throw 'FAIL: Config command output did not confirm debug value.'
    }

    $addResOut = & $Azv2Exe extension add --path $resNupkg.FullName 2>&1
    $addResText = $addResOut | Out-String
    $addResText.Trim() | Out-Host
    if ($addResText -notmatch 'installedExtensionPath') {
        throw 'FAIL: Resources extension installation did not report installedExtensionPath.'
    }

    $groupOut = & $Azv2Exe group list 2>&1
    $groupText = $groupOut | Out-String
    $groupText.Trim() | Out-Host
    if ($groupText -match 'could not be found') {
        throw 'FAIL: Resources command not resolved after install.'
    } elseif ($groupText -match "Required parameter 'SubscriptionId' is missing") {
        Write-Host 'PASS: Resources command resolved (missing SubscriptionId in CI context)'
    } elseif ($groupText -match 'AcquireTokenSilent|No account or login hint|please run .*login|AADSTS\d+') {
        Write-Host 'PASS: Resources command resolved (authentication context not available in CI)'
    } else {
        Write-Host 'PASS: Resources command resolved'
    }

    Write-Host 'PASS: Windows MSI runtime extension-add flow validated'
}
finally {
    $env:USERPROFILE = $originalUserProfile

    if ($installCompleted -and -not $KeepInstalled) {
        Write-Host 'Uninstalling test MSI'
        Invoke-MsiExec -Arguments @('/x', $productCode, '/quiet', '/norestart', '/L*V', "`"$UninstallLog`"") -FailureMessage "FAIL: MSI uninstall failed. See $UninstallLog."

        if (Test-Path -Path $Azv2Exe -PathType Leaf) {
            throw "FAIL: azv2.exe still exists after uninstall: $Azv2Exe"
        }

        Write-Host 'PASS: MSI uninstalled cleanly'
    }
}

Write-Host ''
Write-Host '============================================'
Write-Host ' Windows MSI Bundled+Optional Test Complete '
Write-Host '============================================'
