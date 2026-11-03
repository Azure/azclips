#Requires -Version 5.1

<#
.SYNOPSIS
    Bootstraps the Windows validation environment for packaged Az PowerShell modules.

.DESCRIPTION
    Windows agents run natively rather than in a container, so this Windows bootstrap's
    only job is installing the PowerShell 7 dependency.
    It installs the pinned .NET SDK, the .NET runtime the requested PowerShell release
    was built against, and PowerShell itself.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $SourceRoot,

    [Parameter(Mandatory)]
    [string] $PowerShellVersion,

    [Parameter(Mandatory)]
    [string] $DotnetRuntimeVersion,

    [string] $WorkRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory)][string] $FilePath,
        [Parameter(Mandatory)][string[]] $Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "'$FilePath $($Arguments -join ' ')' failed with exit code $LASTEXITCODE."
    }
}

if ([string]::IsNullOrWhiteSpace($WorkRoot)) {
    $WorkRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'azclips-ps-validation'
}

$dotnetRoot = Join-Path $WorkRoot 'dotnet'
$pwshRoot = Join-Path $WorkRoot 'pwsh'
$installScript = Join-Path $WorkRoot 'dotnet-install.ps1'

New-Item -Path $WorkRoot -ItemType Directory -Force | Out-Null

$globalJsonPath = Join-Path $SourceRoot 'global.json'
if (-not (Test-Path -LiteralPath $globalJsonPath)) {
    throw "Could not resolve the .NET SDK version because '$globalJsonPath' was not found."
}

$sdkVersion = (Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json).sdk.version
if ([string]::IsNullOrWhiteSpace($sdkVersion)) {
    throw "Could not resolve the .NET SDK version from '$globalJsonPath'."
}

[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installScript -UseBasicParsing

& $installScript -Version $sdkVersion -InstallDir $dotnetRoot

# The PowerShell .NET tool is framework dependent, so the runtime it was built
# against has to be present alongside the SDK used to build the modules.
& $installScript -Runtime 'dotnet' -Channel $DotnetRuntimeVersion -InstallDir $dotnetRoot

$dotnet = Join-Path $dotnetRoot 'dotnet.exe'
$env:DOTNET_ROOT = $dotnetRoot
$env:PATH = "$dotnetRoot$([System.IO.Path]::PathSeparator)$env:PATH"

Invoke-NativeCommand -FilePath $dotnet -Arguments @('--list-sdks')
Invoke-NativeCommand -FilePath $dotnet -Arguments @('--list-runtimes')

Invoke-NativeCommand -FilePath $dotnet -Arguments @(
    'tool', 'install',
    '--configfile', (Join-Path $SourceRoot 'NuGet.config'),
    '--tool-path', $pwshRoot,
    'PowerShell',
    '--version', "$PowerShellVersion.*"
)

$pwshPath = Join-Path $pwshRoot 'pwsh.exe'
Invoke-NativeCommand -FilePath $pwshPath -Arguments @('-NoLogo', '-NoProfile', '-Command', '$PSVersionTable')

Write-Host "PowerShell installed at: $pwshPath"
