#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [ValidateSet('win-x64')]
    [string]$Rid = 'win-x64',
    [string]$ProductVersion = '',
    [string]$ProductName = 'Cirrus CLI',
    [string]$Manufacturer = 'Cirrus Development',
    [string]$UpgradeCode = 'D71E4F9B-A5D8-4F16-A0A3-E0D74BB989B8',
    [string]$PathComponentGuid = '{63C2D387-1A8D-4025-B83D-AF7321222436}',
    [string]$RepoRoot = '',
    [string]$OutRoot = '',
    [string]$CommonOutRoot = '',
    [string]$WixBin = '',
    [switch]$RunInstallTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($env:OS -ne 'Windows_NT') {
    throw 'build_win_msi.ps1 must run on Windows because it uses WiX Toolset and Windows Installer.'
}

function Resolve-FullPath([string]$Path) {
    return [System.IO.Path]::GetFullPath($Path)
}

function Get-PackageVersion {
    if (-not [string]::IsNullOrWhiteSpace($ProductVersion)) {
        $version = $ProductVersion.Trim()
    }
    elseif (-not [string]::IsNullOrWhiteSpace($env:BUILD_BUILDID)) {
        $buildNumber = [int]$env:BUILD_BUILDID % 65535
        if ($buildNumber -eq 0) { $buildNumber = 1 }
        $version = "1.0.$buildNumber.0"
    }
    else {
        $version = '1.0.0.0'
    }

    if ($version -notmatch '^\d{1,5}\.\d{1,5}\.\d{1,5}\.\d{1,5}$') {
        throw "MSI package version '$version' must use four numeric components."
    }

    foreach ($component in $version.Split('.')) {
        if ([int]$component -gt 65535) {
            throw "MSI package version component '$component' is greater than 65535."
        }
    }

    return $version
}

function Resolve-WixTools([string]$RequestedWixBin) {
    $candidateBins = @()

    if (-not [string]::IsNullOrWhiteSpace($RequestedWixBin)) {
        $candidateBins += $RequestedWixBin
    }

    if (-not [string]::IsNullOrWhiteSpace($env:WIX)) {
        $candidateBins += (Join-Path $env:WIX 'bin')
        $candidateBins += $env:WIX
    }

    $programFilesX86 = ${env:ProgramFiles(x86)}
    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        $wixDirectories = Get-ChildItem $programFilesX86 -Directory -Filter 'WiX Toolset v3*' -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending

        foreach ($directory in $wixDirectories) {
            $candidateBins += (Join-Path $directory.FullName 'bin')
        }
    }

    foreach ($candidateBin in $candidateBins) {
        if ([string]::IsNullOrWhiteSpace($candidateBin)) { continue }

        $heat = Join-Path $candidateBin 'heat.exe'
        $candle = Join-Path $candidateBin 'candle.exe'
        $light = Join-Path $candidateBin 'light.exe'

        if ((Test-Path $heat) -and (Test-Path $candle) -and (Test-Path $light)) {
            return [pscustomobject]@{ Heat = $heat; Candle = $candle; Light = $light }
        }
    }

    throw 'WiX Toolset v3 tools were not found. Install WiX Toolset v3 or pass -WixBin <path-to-wix-bin>.'
}

function Get-MsiProductCode([string]$MsiPath) {
    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database = $installer.OpenDatabase($MsiPath, 0)
    $view = $database.OpenView("SELECT ``Value`` FROM ``Property`` WHERE ``Property`` = 'ProductCode'")
    $view.Execute()
    $record = $view.Fetch()

    if ($null -eq $record) {
        throw "ProductCode was not found in MSI: $MsiPath"
    }

    return $record.StringData(1)
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Resolve-FullPath (Join-Path $PSScriptRoot '../../../..')
}
else {
    $RepoRoot = Resolve-FullPath $RepoRoot
}

if ([string]::IsNullOrWhiteSpace($OutRoot)) {
    $OutRoot = Join-Path $RepoRoot 'devops/out/windows'
}
else {
    $OutRoot = Resolve-FullPath $OutRoot
}

if ([string]::IsNullOrWhiteSpace($CommonOutRoot)) {
    $CommonOutRoot = Join-Path $RepoRoot 'devops/out/common'
}
else {
    $CommonOutRoot = Resolve-FullPath $CommonOutRoot
}

$cliProject = Join-Path $RepoRoot 'src/CLI/Core.Cli.csproj'
$builtinPublishRoot = Join-Path $CommonOutRoot 'builtin-publish'
$publishRoot = Join-Path $OutRoot "publish/cli/$Rid-msi"
$wixRoot = Join-Path $OutRoot "wix/$Rid"
$artifactRoot = Join-Path $OutRoot 'bin/Release'
$msiPath = Join-Path $artifactRoot "azv2-$Rid.msi"
$version = Get-PackageVersion

if (-not (Test-Path $cliProject)) {
    throw "CLI project was not found: $cliProject"
}

if (-not (Test-Path $builtinPublishRoot)) {
    throw "Built-in publish root was not found: $builtinPublishRoot. Run devops/scripts/packaging/common/build_common.sh first."
}

$wixTools = Resolve-WixTools $WixBin

Write-Host "Publishing CLI ($Configuration, $Rid)"
Remove-Item $publishRoot, $wixRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publishRoot, $wixRoot, $artifactRoot -Force | Out-Null

dotnet publish $cliProject `
    --configuration $Configuration `
    --runtime $Rid `
    --self-contained true `
    -p:ContinuousIntegrationBuild=true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -o $publishRoot

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$extensionsRoot = Join-Path $publishRoot 'Extensions'
New-Item -ItemType Directory -Path $extensionsRoot -Force | Out-Null

Write-Host "Injecting built-in extensions into $publishRoot"
foreach ($builtinDirectory in Get-ChildItem $builtinPublishRoot -Directory) {
    $destination = Join-Path $extensionsRoot $builtinDirectory.Name
    New-Item -ItemType Directory -Path $destination -Force | Out-Null

    $copied = 0
    $skipped = 0
    foreach ($dll in Get-ChildItem $builtinDirectory.FullName -Filter *.dll -File) {
        $hostDll = Join-Path $publishRoot $dll.Name
        if (Test-Path $hostDll) {
            $sourceItem = Get-Item $dll.FullName
            $hostItem = Get-Item $hostDll
            if (($sourceItem.Length -eq $hostItem.Length) -and ((Get-FileHash $dll.FullName).Hash -eq (Get-FileHash $hostDll).Hash)) {
                $skipped++
                continue
            }
        }

        Copy-Item $dll.FullName $destination -Force
        $copied++
    }

    Get-ChildItem $builtinDirectory.FullName -Filter *.deps.json -File -ErrorAction SilentlyContinue |
        Copy-Item -Destination $destination -Force
    Write-Host "  BUNDLE $($builtinDirectory.Name) -> $destination (copied $copied DLLs, skipped $skipped host-provided)"
}

$harvestPath = Join-Path $wixRoot 'PublishedFiles.wxs'
$productPath = Join-Path $wixRoot 'Product.wxs'

& $wixTools.Heat dir $publishRoot `
    -cg PublishedFiles `
    -dr INSTALLFOLDER `
    -srd `
    -sfrag `
    -sreg `
    -gg `
    -var var.PublishRoot `
    -out $harvestPath

if ($LASTEXITCODE -ne 0) {
    throw "heat.exe failed with exit code $LASTEXITCODE."
}

$harvestContent = Get-Content $harvestPath -Raw
$harvestContent = $harvestContent -replace '<Component Id="([^"]+)" Guid="([^"]+)">', '<Component Id="$1" Guid="$2" Win64="yes">'
$escapedPublishRoot = [System.Security.SecurityElement]::Escape($publishRoot)
$harvestContent = $harvestContent.Replace('$(var.PublishRoot)', $escapedPublishRoot)
$harvestContent = $harvestContent.Replace('$publishRoot', $escapedPublishRoot)
Set-Content -Path $harvestPath -Value $harvestContent -Encoding utf8

$productXml = @"
<Wix xmlns="http://schemas.microsoft.com/wix/2006/wi">
  <Product Id="*"
           Name="$ProductName"
           Language="1033"
           Version="$version"
           Manufacturer="$Manufacturer"
           UpgradeCode="$UpgradeCode">
    <Package InstallerVersion="500" Compressed="yes" InstallScope="perMachine" Platform="x64" />
    <MajorUpgrade AllowSameVersionUpgrades="yes"
                  DowngradeErrorMessage="A later version of [ProductName] is already installed." />
    <MediaTemplate EmbedCab="yes" />
    <Property Id="MSIFASTINSTALL" Value="7" />
    <Property Id="ARPNOMODIFY" Value="1" />
    <SetProperty Id="REINSTALLMODE" Value="amus" Before="CostFinalize">WIX_UPGRADE_DETECTED</SetProperty>

    <Directory Id="TARGETDIR" Name="SourceDir">
      <Directory Id="ProgramFiles64Folder">
        <Directory Id="INSTALLFOLDER" Name="Cirrus CLI" />
      </Directory>
    </Directory>

    <DirectoryRef Id="INSTALLFOLDER">
      <Component Id="PathEnvironment" Guid="$PathComponentGuid" Win64="yes">
        <RegistryValue Root="HKLM" Key="Software\Cirrus CLI" Name="InstallDir" Type="string" Value="[INSTALLFOLDER]" KeyPath="yes" />
                <RegistryValue Root="HKLM" Key="Software\Cirrus CLI" Name="Version" Type="string" Value="$version" />
        <Environment Id="AddInstallFolderToPath" Name="PATH" Value="[INSTALLFOLDER]" Permanent="no" Part="last" Action="set" System="yes" />
      </Component>
    </DirectoryRef>

    <Feature Id="MainFeature" Title="$ProductName" Level="1">
      <ComponentGroupRef Id="PublishedFiles" />
      <ComponentRef Id="PathEnvironment" />
    </Feature>
        <CustomActionRef Id="WixBroadcastEnvironmentChange" />
  </Product>
</Wix>
"@

Set-Content -Path $productPath -Value $productXml -Encoding utf8

& $wixTools.Candle `
    -arch x64 `
    -ext WixUtilExtension `
    -out "$wixRoot\" `
    $productPath `
    $harvestPath

if ($LASTEXITCODE -ne 0) {
    throw "candle.exe failed with exit code $LASTEXITCODE."
}

$wixObjects = Get-ChildItem $wixRoot -Filter *.wixobj | ForEach-Object { $_.FullName }
& $wixTools.Light -ext WixUtilExtension -out $msiPath $wixObjects

if ($LASTEXITCODE -ne 0) {
    throw "light.exe failed with exit code $LASTEXITCODE."
}

$productCode = Get-MsiProductCode $msiPath
Write-Host "Created MSI: $msiPath"
Write-Host "ProductCode: $productCode"

if ($RunInstallTest) {
    $installLog = Join-Path $artifactRoot 'msi-install.log'
    $uninstallLog = Join-Path $artifactRoot 'msi-uninstall.log'
    $installPath = Join-Path ${env:ProgramFiles} 'Cirrus CLI'
    $installedExe = Join-Path $installPath 'azv2.exe'
    $installedExtensions = Join-Path $installPath 'Extensions'

    $install = Start-Process msiexec.exe -Wait -PassThru -ArgumentList @(
        '/i', "`"$msiPath`"",
        '/quiet',
        '/norestart',
        '/L*V', "`"$installLog`""
    )

    if ($install.ExitCode -ne 0) {
        throw "MSI install test failed with exit code $($install.ExitCode). See $installLog"
    }

    if (-not (Test-Path $installedExe)) {
        throw "Installed executable was not found: $installedExe"
    }

    if (-not (Test-Path $installedExtensions)) {
        throw "Installed built-in extensions directory was not found: $installedExtensions"
    }

    foreach ($builtinDirectory in Get-ChildItem $builtinPublishRoot -Directory) {
        $installedAssembly = Join-Path $installedExtensions "$($builtinDirectory.Name)\$($builtinDirectory.Name).dll"
        if (-not (Test-Path $installedAssembly)) {
            throw "Installed built-in extension assembly was not found: $installedAssembly"
        }
    }

    & $installedExe --help

    $uninstall = Start-Process msiexec.exe -Wait -PassThru -ArgumentList @(
        '/x', $productCode,
        '/quiet',
        '/norestart',
        '/L*V', "`"$uninstallLog`""
    )

    if ($uninstall.ExitCode -ne 0) {
        throw "MSI uninstall test failed with exit code $($uninstall.ExitCode). See $uninstallLog"
    }

    if (Test-Path $installedExe) {
        throw "Installed executable still exists after uninstall: $installedExe"
    }
}
