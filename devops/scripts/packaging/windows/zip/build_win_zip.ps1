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
$noticeFile = if ([string]::IsNullOrWhiteSpace($env:NOTICE_FILE)) { Join-Path $repoRoot 'NOTICE' } else { $env:NOTICE_FILE }
$licenseFile = if ([string]::IsNullOrWhiteSpace($env:LICENSE_FILE)) { Join-Path $repoRoot 'LICENSE' } else { $env:LICENSE_FILE }

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

if ([string]::IsNullOrWhiteSpace($CliWinZipPath)) {
    if (-not [string]::IsNullOrWhiteSpace($artifactVersion)) {
        $artifactVersionSegment = $artifactVersion
        if (-not [string]::IsNullOrWhiteSpace($artifactVersionSuffix) -and -not $artifactVersionSegment.EndsWith("-$artifactVersionSuffix", [System.StringComparison]::OrdinalIgnoreCase)) {
            $artifactVersionSegment = "$artifactVersionSegment-$artifactVersionSuffix"
        }

        $CliWinZipPath = Join-Path $OutRoot "Release/zip/$artifactStem-$artifactVersionSegment-$assetArchitecture.zip"
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
$stageAgentPath = Join-Path $packagingRoot 'common/stage_agent.ps1'

Push-Location $repoRoot
try {
    Write-Host "Publishing CLI (Release, $Rid)"
    dotnet publish $CliProject -c Release -r $Rid --self-contained true -o $cliWinScDir

    # Publish the Agent daemon into a scratch dir; stage_agent.ps1 then
    # splits the output: launcher files move to the CLI publish root (sibling
    # to az.exe so they share the runtime pack), and agent-only NuGet
    # dependencies + prompts + copilot native binary move under
    # Extensions/Microsoft.Azure.Azclips.Extensions.Agent/. Runtime-pack
    # duplicates that are byte-identical to the CLI copy are deleted.
    Write-Host "Publishing Agent exe (Release, $Rid) into scratch"
    $agentScratch = Join-Path $CommonOutRoot "agent-publish/$Rid"
    if (Test-Path $agentScratch) { Remove-Item -Path $agentScratch -Recurse -Force }
    dotnet publish (Join-Path $repoRoot 'Agent/Core.Agent.csproj') -c Release -r $Rid --self-contained true -o $agentScratch -p:IsPackagedBuild=true

    # Publish the Agent.Commands extension into builtin-publish/ so the inject
    # script picks it up in the same pass as Accounts/Config.
    Write-Host "Publishing Agent.Commands extension (Release)"
    $agentExtDir = Join-Path $builtinPublishRoot 'Microsoft.Azure.Azclips.Extensions.Agent'
    dotnet publish (Join-Path $repoRoot 'Agent/Commands/Agent.Commands.csproj') -c Release -o $agentExtDir

    if (-not (Test-Path -Path $injectScriptPath -PathType Leaf)) {
        throw "Injector script not found at $injectScriptPath"
    }

    if (-not (Test-Path -Path $noticeFile -PathType Leaf)) {
        throw "NOTICE file was not found: $noticeFile"
    }

    if (-not (Test-Path -Path $licenseFile -PathType Leaf)) {
        throw "LICENSE file was not found: $licenseFile"
    }

    & $injectScriptPath -PublishDir $cliWinScDir -BuiltinRoot $builtinPublishRoot

    # Repackage agent daemon into the hybrid layout: launcher files land next
    # to az.exe, everything else lands under the freshly-injected agent
    # extension directory (prompts, deps/ NuGet layout, native copilot).
    $agentExtInPublish = Join-Path $cliWinScDir 'Extensions/Microsoft.Azure.Azclips.Extensions.Agent'
    if (-not (Test-Path -Path $agentExtInPublish -PathType Container)) {
        throw "Agent extension dir not present in publish after inject: $agentExtInPublish"
    }
    & $stageAgentPath `
        -AgentPublishDir $agentScratch `
        -CliPublishDir $cliWinScDir `
        -AgentExtensionDir $agentExtInPublish

    Copy-Item -Path $noticeFile -Destination (Join-Path $cliWinScDir 'NOTICE') -Force
    Copy-Item -Path $licenseFile -Destination (Join-Path $cliWinScDir 'LICENSE') -Force

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
