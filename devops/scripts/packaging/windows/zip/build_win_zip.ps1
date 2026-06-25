param(
    [string]$OutRoot = '',
    [string]$CommonOutRoot = '',
    [string]$CliProject = 'src/CLI/Core.Cli.csproj',
    [string]$Rid = 'win-x64',
    [string]$CliWinZipPath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$packagingRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDir '../..'))
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $packagingRoot '../../..'))

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
                $value = $parts[1].Trim()
                if (-not [string]::IsNullOrWhiteSpace($value)) {
                    return $value
                }
            }
        }
    }

    return $DefaultValue
}

if ([string]::IsNullOrWhiteSpace($OutRoot)) {
    $OutRoot = Join-Path $repoRoot 'devops/out/windows'
}
if ([string]::IsNullOrWhiteSpace($CommonOutRoot)) {
    $CommonOutRoot = Join-Path $repoRoot 'devops/out/common'
}

$packageProfile = $env:PACKAGE_PROFILE
if ([string]::IsNullOrWhiteSpace($packageProfile)) {
    $packageProfile = 'lean'
}
$packageProfile = $packageProfile.ToLowerInvariant()

if ($packageProfile -eq 'full') {
    $artifactStem = $env:CLI_ARCHIVE_STEM
    if ([string]::IsNullOrWhiteSpace($artifactStem)) {
        $artifactStem = Get-ArtifactConfigValue -Name 'cli_archive_stem' -DefaultValue 'azcli-preview'
    }
}
elseif ($packageProfile -eq 'lean') {
    $artifactStem = $env:CLI_LEAN_ARCHIVE_STEM
    if ([string]::IsNullOrWhiteSpace($artifactStem)) {
        $artifactStem = Get-ArtifactConfigValue -Name 'cli_lean_archive_stem' -DefaultValue 'azcli-lean-preview'
    }
}
else {
    throw "Unsupported PACKAGE_PROFILE '$packageProfile'. Expected 'lean' or 'full'."
}

$artifactVersion = $env:ARTIFACT_VERSION
$assetArchitecture = $Rid
if ($Rid -eq 'win-x64') {
    $assetArchitecture = Get-ArtifactConfigValue -Name 'windows_x64_label' -DefaultValue 'win-x64'
}
elseif ($Rid -eq 'win-arm64') {
    $assetArchitecture = Get-ArtifactConfigValue -Name 'windows_arm64_label' -DefaultValue 'win-arm64'
}

if ([string]::IsNullOrWhiteSpace($CliWinZipPath)) {
    if (-not [string]::IsNullOrWhiteSpace($artifactVersion)) {
        $CliWinZipPath = Join-Path $OutRoot "Release/zip/$artifactStem-$artifactVersion-$assetArchitecture.zip"
    }
    else {
        $CliWinZipPath = Join-Path $OutRoot "Release/zip/$artifactStem-$assetArchitecture.zip"
    }
}

$OutRoot = [System.IO.Path]::GetFullPath($OutRoot)
$CommonOutRoot = [System.IO.Path]::GetFullPath($CommonOutRoot)
$CliWinZipPath = [System.IO.Path]::GetFullPath($CliWinZipPath)

$cliWinScDir = Join-Path $OutRoot "publish/cli/$Rid"
$builtinPublishRoot = Join-Path $CommonOutRoot 'builtin-publish'
$injectScriptPath = Join-Path $packagingRoot 'common/inject_builtin_extensions.ps1'

Push-Location $repoRoot
try {
    Write-Host "Publishing CLI (Release, $Rid)"
    dotnet publish $CliProject -c Release -r $Rid --self-contained true -o $cliWinScDir

    if (-not (Test-Path -Path $injectScriptPath -PathType Leaf)) {
        throw "Injector script not found at $injectScriptPath"
    }

    & $injectScriptPath -PublishDir $cliWinScDir -BuiltinRoot $builtinPublishRoot

    $zipDir = Split-Path -Parent $CliWinZipPath
    New-Item -Path $zipDir -ItemType Directory -Force | Out-Null
    if (Test-Path -Path $CliWinZipPath -PathType Leaf) {
        Remove-Item -Path $CliWinZipPath -Force
    }

    Compress-Archive -Path (Join-Path $cliWinScDir '*') -DestinationPath $CliWinZipPath -Force
    Write-Host "Created $CliWinZipPath"
}
finally {
    Pop-Location
}
