<#
.SYNOPSIS
  azcli uninstaller (Windows).

.DESCRIPTION
  Removes an azcli install created by azcli-install.ps1 (deletes the install folder).

.EXAMPLE
  ./azcli-uninstall.ps1

  Removes a recognized azcli installation.

.EXAMPLE
  ./azcli-uninstall.ps1 -Force

  Also permits removing an unrecognized install folder. Use -Force only after
  verifying that the install folder contains nothing you need to keep.
#>
& {
[CmdletBinding()]
param(
  [switch]$Force
)

$ErrorActionPreference = 'Stop'

function Remove-DirectoryWithRetry {
  param(
    [Parameter(Mandatory = $true)]
    [string]$Path,

    [int]$MaxAttempts = 30,

    [int]$DelayMilliseconds = 1000
  )

  for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
    try {
      Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
      return
    }
    catch {
      if ($attempt -eq $MaxAttempts) {
        throw
      }
      Write-Verbose "Remove attempt $attempt failed for '$Path': $($_.Exception.Message)"
      Start-Sleep -Milliseconds $DelayMilliseconds
    }
  }
}

# InstallRoot is the fixed default; the AZCLI_INSTALL_ROOT env override is internal.
$InstallRoot = if ($env:AZCLI_INSTALL_ROOT) { $env:AZCLI_INSTALL_ROOT } else { "$env:LOCALAPPDATA\Programs\azcli" }

# Refuse obviously unsafe install roots (guards against a mis-set AZCLI_INSTALL_ROOT).
$__root = $InstallRoot.TrimEnd('\', '/')
if ([string]::IsNullOrWhiteSpace($InstallRoot) -or $__root -match '^[A-Za-z]:$' -or $__root -eq $env:USERPROFILE.TrimEnd('\', '/')) {
  throw "Refusing to operate on unsafe install root: '$InstallRoot'."
}

Write-Host "Install root: $InstallRoot"

# 1. Remove the install directory.
if (Test-Path $InstallRoot) {
  if ((Test-Path (Join-Path $InstallRoot 'az.exe')) -or $Force) {
    Remove-DirectoryWithRetry -Path $InstallRoot
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
} @args
