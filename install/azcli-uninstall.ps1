<#
.SYNOPSIS
  azcli uninstaller (Windows).

.DESCRIPTION
  Removes an azcli install created by azcli-install.ps1 (deletes the install folder).

.EXAMPLE
  ./azcli-uninstall.ps1
  ./azcli-uninstall.ps1 -Force
#>
[CmdletBinding()]
param(
  [switch]$KeepPath,
  [switch]$Force
)

$ErrorActionPreference = 'Stop'

# InstallRoot is the fixed default; the AZCLI_INSTALL_ROOT env override is internal.
$InstallRoot = if ($env:AZCLI_INSTALL_ROOT) { $env:AZCLI_INSTALL_ROOT } else { "$env:LOCALAPPDATA\Programs\azcli" }

Write-Host "Install root: $InstallRoot"

# 1. Remove the install directory.
if (Test-Path $InstallRoot) {
  if ((Test-Path (Join-Path $InstallRoot 'az.exe')) -or $Force) {
    Remove-Item -Recurse -Force $InstallRoot
    Write-Host "Removed $InstallRoot"
  }
  else {
    throw "'$InstallRoot' does not look like an azcli install (no 'az.exe'). Re-run with -Force to remove it anyway."
  }
}
else {
  Write-Host "Nothing to remove at $InstallRoot (already absent)."
}

# 2. Remove the entry from the user PATH.
if (-not $KeepPath) {
  $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
  if ($userPath) {
    $entries = $userPath -split ';'
    $kept = $entries | Where-Object { $_ -and ($_.TrimEnd('\') -ne $InstallRoot.TrimEnd('\')) }
    $newPath = ($kept -join ';')
    if ($newPath -ne $userPath) {
      [Environment]::SetEnvironmentVariable('Path', $newPath, 'User')
      Write-Host "Removed $InstallRoot from your user PATH."
    }
  }
  # Also drop it from the current session PATH.
  $env:Path = (($env:Path -split ';') | Where-Object { $_ -and ($_.TrimEnd('\') -ne $InstallRoot.TrimEnd('\')) }) -join ';'
}

Write-Host ""
Write-Host "Done."
