<#
.SYNOPSIS
  azcli installer (Windows).

.DESCRIPTION
  Downloads the latest signed release of the az CLI from a PRIVATE GitHub repo
  using your authenticated GitHub CLI (gh) session.

  The CLI is installed in isolation and PATH is NOT modified, so it coexists
  with the classic azure-cli.

  Private-preview users only need read access to the repo and a logged-in `gh`.

.EXAMPLE
  ./azcli-install.ps1
  ./azcli-install.ps1 -Package lean
#>
[CmdletBinding()]
param(
  [ValidateSet('lean', 'full')]
  [string]$Package = $(if ($env:AZCLI_PACKAGE) { $env:AZCLI_PACKAGE } else { 'full' }),

  # Internal only: installs an exact local artifact for CI and release-candidate
  # validation before GitHub publication.
  [string]$ArchivePath
)

$ErrorActionPreference = 'Stop'

function Remove-DirectoryWithRetry {
  param(
    [Parameter(Mandatory = $true)]
    [string]$Path,

    [int]$MaxAttempts = 10,

    [int]$DelayMilliseconds = 500
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

if ($PSBoundParameters.ContainsKey('ArchivePath') -and [string]::IsNullOrWhiteSpace($ArchivePath)) {
  throw 'ArchivePath requires a path.'
}

# Repo and InstallRoot are fixed. Their AZCLI_REPO / AZCLI_INSTALL_ROOT env
# overrides exist for internal release testing only and are undocumented.
$Repo = if ($env:AZCLI_REPO) { $env:AZCLI_REPO } else { 'Azure/azclips' }
$InstallRoot = if ($env:AZCLI_INSTALL_ROOT) { $env:AZCLI_INSTALL_ROOT } else { "$env:LOCALAPPDATA\Programs\azcli" }

# Refuse obviously unsafe install roots (guards against a mis-set AZCLI_INSTALL_ROOT).
$__root = $InstallRoot.TrimEnd('\', '/')
if ([string]::IsNullOrWhiteSpace($InstallRoot) -or $__root -match '^[A-Za-z]:$' -or $__root -eq $env:USERPROFILE.TrimEnd('\', '/')) {
  throw "Refusing to use unsafe install root: '$InstallRoot'."
}

# 1. Detect architecture.
$osArch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
switch ($osArch) {
  'X64'   { $assetLabel = 'win-x64' }
  'Arm64' { $assetLabel = 'win-arm64' }
  default { throw "Unsupported CPU architecture: $osArch" }
}
$ext = 'zip'
$suffix = "-$assetLabel.$ext"
$fullStem = 'azcli'
$leanStem = 'azcli-lean'
$stem = if ($Package -eq 'full') { $fullStem } else { $leanStem }

Write-Host "Repository : $Repo"
Write-Host "Package    : $Package ($stem)"
Write-Host "Platform   : $assetLabel"

$tmp = $null
$stageRoot = $null
try {
  if ($PSBoundParameters.ContainsKey('ArchivePath')) {
    if (-not (Test-Path -LiteralPath $ArchivePath -PathType Leaf)) {
      throw "Archive was not found: $ArchivePath"
    }
    $archive = (Resolve-Path -LiteralPath $ArchivePath).Path
    $archiveName = Split-Path -Leaf $archive
    if (-not $archiveName.EndsWith($suffix, [System.StringComparison]::OrdinalIgnoreCase)) {
      throw "Archive '$archiveName' does not match platform $assetLabel."
    }
    if ($Package -eq 'lean' -and $archiveName -notlike "$leanStem-*") {
      throw "Archive '$archiveName' is not a lean CLI package."
    }
    if ($Package -eq 'full' -and ($archiveName -notlike "$fullStem-*" -or $archiveName -like "$leanStem-*")) {
      throw "Archive '$archiveName' is not a full CLI package."
    }
    Write-Host "Archive    : $archive"
  }
  else {
    # 2. Require an authenticated GitHub CLI.
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
      throw "GitHub CLI 'gh' is required. Install it from https://cli.github.com/ and re-run."
    }
    gh auth status *> $null
    if ($LASTEXITCODE -ne 0) {
      Write-Host "You are not signed in to GitHub. Launching 'gh auth login'..."
      gh auth login
      if ($LASTEXITCODE -ne 0) { throw "gh auth login did not complete." }
    }

    # 3. Resolve the latest release tag.
    $tag = (gh release view --repo $Repo --json tagName --jq '.tagName')
    if ($LASTEXITCODE -ne 0 -or -not $tag) {
      throw "Could not resolve a release for $Repo. Confirm you have read access and that a release exists."
    }
    Write-Host "Release    : $tag"

    # 4. Choose the matching asset for this platform + package.
    $assetNames = gh release view $tag --repo $Repo --json assets --jq '.assets[].name'
    if ($LASTEXITCODE -ne 0 -or -not $assetNames) {
      throw "Release $tag has no downloadable assets."
    }

    $chosen = $assetNames | Where-Object {
      $_.EndsWith($suffix) -and (
        ($Package -eq 'lean' -and $_ -like "$leanStem-*") -or
        ($Package -eq 'full' -and $_ -like "$fullStem-*" -and $_ -notlike "$leanStem-*")
      )
    } | Select-Object -First 1

    if (-not $chosen) {
      Write-Host "Available assets in ${tag}:"
      $assetNames | ForEach-Object { Write-Host "  $_" }
      throw "No $Package CLI asset found for $assetLabel in release $tag."
    }
    Write-Host "Asset      : $chosen"

    # 5. Download via the authenticated gh session (handles private-repo access).
    $tmp = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString())
    New-Item -ItemType Directory -Path $tmp -Force | Out-Null
    gh release download $tag --repo $Repo --pattern $chosen --dir $tmp --clobber
    if ($LASTEXITCODE -ne 0) { throw "gh release download failed for $chosen." }
    $archive = Join-Path $tmp $chosen
    if (-not (Test-Path $archive)) { throw "Download did not produce the expected file: $archive" }
  }

  # 6. Install (clean replace of the install root).
  if ((Test-Path $InstallRoot) -and -not (Test-Path (Join-Path $InstallRoot 'az.exe')) -and (Get-ChildItem $InstallRoot -Force | Select-Object -First 1)) {
    throw "Install root '$InstallRoot' is not empty and does not look like an azcli install. Refusing to overwrite. Remove it and re-run."
  }

  $installParent = Split-Path -Parent ([System.IO.Path]::GetFullPath($InstallRoot))
  New-Item -ItemType Directory -Path $installParent -Force | Out-Null
  $stageRoot = Join-Path $installParent ".azcli-stage-$([System.Guid]::NewGuid())"
  New-Item -ItemType Directory -Path $stageRoot | Out-Null
  Expand-Archive -Path $archive -DestinationPath $stageRoot -Force
  $stagedAzExe = Join-Path $stageRoot 'az.exe'
  if (-not (Test-Path $stagedAzExe -PathType Leaf)) {
    throw "Extraction did not yield 'az.exe'."
  }
  & $stagedAzExe --help *> $null
  if ($LASTEXITCODE -ne 0) {
    throw "Extracted 'az.exe --help' did not exit cleanly."
  }

  if (Test-Path $InstallRoot) { Remove-DirectoryWithRetry -Path $InstallRoot }
  Move-Item -Path $stageRoot -Destination $InstallRoot
  $stageRoot = $null
}
finally {
  if ($stageRoot) {
    Remove-Item -Recurse -Force $stageRoot -ErrorAction SilentlyContinue
  }
  if ($tmp) {
    Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
  }
}

# 7. Verify.
Write-Host ""
Write-Host "Installed to: $InstallRoot"
$azExe = Join-Path $InstallRoot 'az.exe'
Write-Host "Verified: '$azExe --help' ran successfully."
Write-Host ""
Write-Host "Done. Installed in isolation - PATH was not modified (coexists with classic 'az')."
Write-Host ""
Write-Host "  Run it directly:               $azExe --help"
Write-Host "  Or add to PATH (this session): `$env:Path = `"$InstallRoot;`$env:Path`""
