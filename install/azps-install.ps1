<#
.SYNOPSIS
  Installs the Azure PowerShell preview modules on Windows, Linux, and macOS.
.DESCRIPTION
  Downloads the latest public release bundle without GitHub authentication and
  delegates installation to its InstallModule.ps1 helper. Requires PowerShell 7.6+.
.EXAMPLE
  ./azps-install.ps1
  ./azps-install.ps1 -ModuleName Az.Compute -Scope AllUsers
#>
& {
[CmdletBinding()]
param(
  [string]$ModuleName = 'Az',
  [string]$RequiredVersion,
  [ValidateSet('CurrentUser', 'AllUsers')]
  [string]$Scope = 'CurrentUser',
  [string]$RepositoryName = "AzclipsInstall-$([guid]::NewGuid().ToString('N'))"
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion -lt [version]'7.6') {
  throw 'Azure PowerShell preview modules require PowerShell 7.6 or later. See https://aka.ms/powershell.'
}
if (Get-PSRepository -Name $RepositoryName -ErrorAction SilentlyContinue) {
  throw "Repository '$RepositoryName' already exists. Choose a different -RepositoryName."
}

$stage = Join-Path ([IO.Path]::GetTempPath()) "azps-install-$([guid]::NewGuid().ToString('N'))"
try {
  $release = Invoke-RestMethod 'https://api.github.com/repos/Azure/azclips/releases/latest' -ErrorAction Stop
  if ([string]::IsNullOrWhiteSpace($release.tag_name)) {
    throw 'Release metadata has no valid tag_name.'
  }
  $assets = @($release.assets | Where-Object { $_.name -like 'azps-cmdlets-*.tar.gz' })
  if ($assets.Count -ne 1) {
    throw "Expected exactly one PowerShell bundle in release $($release.tag_name); found $($assets.Count)."
  }
  $asset = $assets[0]
  if ($asset.name -notmatch '^[A-Za-z0-9._-]+$' -or $asset.browser_download_url -notmatch '^https://') {
    throw 'PowerShell bundle has an invalid filename or HTTPS download URL.'
  }
  Write-Host "Release    : $($release.tag_name)"
  Write-Host "Asset      : $($asset.name)"
  New-Item -ItemType Directory -Path $stage | Out-Null
  $archive = Join-Path $stage $asset.name
  Invoke-WebRequest $asset.browser_download_url -OutFile $archive -ErrorAction Stop
  tar -xzf $archive -C $stage
  if ($LASTEXITCODE -ne 0) { throw 'PowerShell bundle extraction failed.' }
  $installer = Join-Path $stage 'InstallModule.ps1'
  if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) {
    throw 'PowerShell bundle does not contain InstallModule.ps1.'
  }
  $options = @{
    ModuleName = $ModuleName
    Scope = $Scope
    RepositoryName = $RepositoryName
  }
  if ($RequiredVersion) { $options.RequiredVersion = $RequiredVersion }
  & $installer @options
}
finally {
  # The bundled helper registers this staging directory as a local repository.
  try {
    $repository = Get-PSRepository -Name $RepositoryName -ErrorAction SilentlyContinue
    if ($repository -and ([string]$repository.SourceLocation).TrimEnd('/', '\') -eq $stage.TrimEnd('/', '\')) {
      Unregister-PSRepository -Name $RepositoryName -ErrorAction Stop
    }
  }
  finally {
    if (Test-Path -LiteralPath $stage) {
      Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction Stop
    }
  }
}
} @args
