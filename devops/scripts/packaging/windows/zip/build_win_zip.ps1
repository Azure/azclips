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

if ([string]::IsNullOrWhiteSpace($OutRoot)) {
    $OutRoot = Join-Path $repoRoot 'devops/out/windows'
}
if ([string]::IsNullOrWhiteSpace($CommonOutRoot)) {
    $CommonOutRoot = Join-Path $repoRoot 'devops/out/common'
}
if ([string]::IsNullOrWhiteSpace($CliWinZipPath)) {
    $CliWinZipPath = Join-Path $OutRoot "Release/zip/azv2-$Rid.zip"
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
