<#
.SYNOPSIS
  Downloads the public CLI extensions bundle into the current directory.
.DESCRIPTION
  Saves azcli-extensions.tar.gz without extracting or installing extensions.
  No GitHub CLI, authentication, or external JSON parser is required.
.EXAMPLE
  ./azcli-extensions-download.ps1

  Downloads the latest release bundle without extracting or installing it.

.EXAMPLE
  ./azcli-extensions-download.ps1 -Release v0.1.7.20261104T200741Z

  Pins the GitHub release tag with -Release; this is not a module/package version.
#>
& {
  [CmdletBinding()]
  param(
    [ValidatePattern('^[A-Za-z0-9._-]+$')]
    [string]$Release
  )

  $ErrorActionPreference = 'Stop'
  if (Test-Path -LiteralPath './azcli-extensions.tar.gz') {
    $destination = Get-Item -LiteralPath './azcli-extensions.tar.gz' -Force
    if ($destination.PSIsContainer -or ($destination.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
      throw 'Refusing to replace a non-file destination: ./azcli-extensions.tar.gz.'
    }
  }
  $endpoint = 'https://api.github.com/repos/Azure/azclips/releases/latest'
  if ($Release) { $endpoint = "https://api.github.com/repos/Azure/azclips/releases/tags/$Release" }
  $stage = Join-Path (Get-Location).ProviderPath ".azcli-extensions-download-$([guid]::NewGuid().ToString('N'))"
  try {
    $metadata = Invoke-RestMethod $endpoint -ErrorAction Stop
    if ($metadata.tag_name -cnotmatch '^[A-Za-z0-9._-]+$' -or
        ($Release -and $metadata.tag_name -cne $Release)) {
      throw 'Release metadata has no valid matching tag_name.'
    }
    $assets = @($metadata.assets | Where-Object { $_.name -cmatch '^azcli-extensions-[0-9][A-Za-z0-9._-]*[.]tar[.]gz$' })
    if ($assets.Count -ne 1) { throw "Expected exactly one extensions bundle; found $($assets.Count)." }
    $asset = $assets[0]
    $url = "https://github.com/Azure/azclips/releases/download/$($metadata.tag_name)/$($asset.name)"
    if ($asset.browser_download_url -cne $url) { throw 'Extensions bundle has an invalid GitHub download URL.' }
    New-Item -ItemType Directory -Path $stage | Out-Null
    $archive = Join-Path $stage 'azcli-extensions.tar.gz'
    Invoke-WebRequest $url -OutFile $archive -ErrorAction Stop
    if (-not (Test-Path -LiteralPath $archive -PathType Leaf)) { throw 'Download did not produce the expected archive.' }
    Move-Item -LiteralPath $archive -Destination './azcli-extensions.tar.gz' -Force
    Write-Host "Downloaded: $url"
    Write-Host 'Saved to: ./azcli-extensions.tar.gz (not extracted or installed)'
  }
  finally {
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction Stop }
  }
} @args
