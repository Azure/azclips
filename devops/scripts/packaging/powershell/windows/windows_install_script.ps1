#Requires -Version 5.1

<#
.SYNOPSIS
    Bootstraps the Windows validation environment for packaged Az PowerShell modules.

.DESCRIPTION
    Windows agents run natively rather than in a container, so this Windows bootstrap's
    only job is installing the standalone PowerShell 7 ZIP with its bundled runtime.
    Release lines select the latest stable patch; exact versions are also accepted.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9]+\.[0-9]+(\.[0-9]+)?$')]
    [string] $PowerShellVersion,

    [string] $WorkRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Get-PowerShellFile {
    param(
        [Parameter(Mandatory)][string] $Uri,
        [Parameter(Mandatory)][string] $OutFile
    )

    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            Invoke-WebRequest -Uri $Uri -OutFile $OutFile -TimeoutSec 180 -UseBasicParsing -ErrorAction Stop
            return
        }
        catch {
            if (Test-Path -LiteralPath $OutFile) { Remove-Item -LiteralPath $OutFile -Force }
            if ($attempt -eq 5) { throw }
            Write-Warning "Retrying download ($attempt/5): $Uri"
            Start-Sleep -Seconds 2
        }
    }
}

if ([string]::IsNullOrWhiteSpace($WorkRoot)) {
    $WorkRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'azclips-ps-validation'
}

$pwshRoot = Join-Path $WorkRoot 'pwsh'
New-Item -Path $WorkRoot -ItemType Directory -Force | Out-Null
if (Test-Path -LiteralPath $pwshRoot) {
    throw "PowerShell installation directory already exists: $pwshRoot"
}

$architecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant()
if ($architecture -notin 'x64', 'arm64', 'x86') {
    throw "Unsupported PowerShell architecture: $architecture"
}

$resolvedVersion = $PowerShellVersion
if ($PowerShellVersion -match '^[0-9]+\.[0-9]+$') {
    $releasesPath = Join-Path $WorkRoot 'powershell-releases.json'
    Get-PowerShellFile -Uri 'https://api.github.com/repos/PowerShell/PowerShell/releases?per_page=100' -OutFile $releasesPath
    $releases = Get-Content -LiteralPath $releasesPath -Raw | ConvertFrom-Json
    $versions = @($releases | Where-Object {
        -not $_.draft -and -not $_.prerelease -and
        $_.tag_name -match "^v$([regex]::Escape($PowerShellVersion))\.[0-9]+$"
    } | ForEach-Object { [version]$_.tag_name.Substring(1) } | Sort-Object -Descending)
    if ($versions.Count -eq 0) { throw "No stable release for PowerShell $PowerShellVersion." }
    $resolvedVersion = $versions[0].ToString()
}

$archiveName = "PowerShell-$resolvedVersion-win-$architecture.zip"
$releaseUri = "https://github.com/PowerShell/PowerShell/releases/download/v$resolvedVersion"
$archivePath = Join-Path $WorkRoot $archiveName
$hashesPath = Join-Path $WorkRoot 'powershell-hashes.sha256'
Write-Host "Installing standalone PowerShell $resolvedVersion (win-$architecture)"
Get-PowerShellFile -Uri "$releaseUri/$archiveName" -OutFile $archivePath
Get-PowerShellFile -Uri "$releaseUri/hashes.sha256" -OutFile $hashesPath

$hashPattern = "^([0-9a-fA-F]{64})\s+\*?$([regex]::Escape($archiveName))$"
$expectedHashes = @(Get-Content -LiteralPath $hashesPath | ForEach-Object {
    if ($_ -cmatch $hashPattern) { $Matches[1] }
})
if ($expectedHashes.Count -ne 1) { throw "Expected exactly one SHA256 checksum for $archiveName." }
if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $expectedHashes[0]) {
    throw "SHA256 checksum mismatch for $archiveName."
}
Expand-Archive -LiteralPath $archivePath -DestinationPath $pwshRoot

$pwshPath = Join-Path $pwshRoot 'pwsh.exe'
$actualVersion = & $pwshPath -NoLogo -NoProfile -Command '$PSVersionTable.PSVersion.ToString()'
if ($LASTEXITCODE -ne 0 -or "$actualVersion".Trim() -ne $resolvedVersion) {
    throw "Expected PowerShell $resolvedVersion, got '$actualVersion' (exit code $LASTEXITCODE)."
}

Write-Host "PowerShell $resolvedVersion installed at: $pwshPath with its bundled .NET runtime."
