[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Rid,

    [Parameter(Mandatory = $true)]
    [string]$OutputDir,

    [string]$ConfigPath = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$packagingRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDir '..'))
if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = if ([string]::IsNullOrWhiteSpace($env:JQ_BINARIES_CONFIG)) {
        Join-Path $packagingRoot 'config/jq_binaries.config'
    }
    else {
        $env:JQ_BINARIES_CONFIG
    }
}

if (-not (Test-Path -Path $ConfigPath -PathType Leaf)) {
    throw "jq config not found: $ConfigPath"
}

function Read-JqConfigValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    foreach ($line in Get-Content -Path $ConfigPath) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('#')) {
            continue
        }

        $parts = $trimmed.Split('=', 2)
        if ($parts.Count -eq 2 -and $parts[0].Trim() -eq $Name) {
            return $parts[1].Trim()
        }
    }

    throw "jq config value '$Name' was not found in $ConfigPath"
}

$version = Read-JqConfigValue -Name 'version'
$asset = Read-JqConfigValue -Name "$Rid.asset"
$expectedSha = (Read-JqConfigValue -Name "$Rid.sha256").ToLowerInvariant()
$url = "https://github.com/jqlang/jq/releases/download/jq-$version/$asset"
$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString())

try {
    New-Item -Path $tmp -ItemType Directory -Force | Out-Null
    $downloadPath = Join-Path $tmp $asset

    Write-Host "Acquiring jq $version for $Rid"
    Invoke-WebRequest -Uri $url -OutFile $downloadPath

    $actualSha = (Get-FileHash -Path $downloadPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualSha -ne $expectedSha) {
        throw "jq checksum mismatch for $asset. Expected $expectedSha, actual $actualSha."
    }

    New-Item -Path $OutputDir -ItemType Directory -Force | Out-Null
    Copy-Item -Path $downloadPath -Destination (Join-Path $OutputDir 'jq.exe') -Force

    @(
        "version=$version"
        "rid=$Rid"
        "asset=$asset"
        "url=$url"
        "sha256=$expectedSha"
    ) | Set-Content -Path (Join-Path $OutputDir 'version.txt') -Encoding utf8

    Write-Host "Bundled jq at $(Join-Path $OutputDir 'jq.exe')"
}
finally {
    Remove-Item -Path $tmp -Recurse -Force -ErrorAction SilentlyContinue
}