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

Write-Host ""
Write-Host "Done."
