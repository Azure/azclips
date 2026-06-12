param(
  [Parameter(Mandatory = $true)]
  [string]$UnsignedRoot,

  [Parameter(Mandatory = $true)]
  [string]$WorkingRoot,

  [Parameter(Mandatory = $true)]
  [string]$ReleaseRoot
)

$ErrorActionPreference = 'Stop'

function Assert-SafeResetPath {
  param(
    [Parameter(Mandatory = $true)]
    [string]$Path,

    [Parameter(Mandatory = $true)]
    [string]$Name
  )

  if ([string]::IsNullOrWhiteSpace($Path)) {
    throw "$Name must not be empty."
  }

  $fullPath = [System.IO.Path]::GetFullPath($Path)
  $rootPath = [System.IO.Path]::GetPathRoot($fullPath)
  $trimmedFullPath = $fullPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
  $trimmedRootPath = $rootPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)

  if ($trimmedFullPath -eq $trimmedRootPath) {
    throw "$Name resolves to a filesystem root and will not be reset: $fullPath"
  }

  return $fullPath
}

if (-not (Test-Path $UnsignedRoot)) {
  throw "Unsigned artifact root was not found: $UnsignedRoot"
}

$WorkingRoot = Assert-SafeResetPath -Path $WorkingRoot -Name 'WorkingRoot'
$ReleaseRoot = Assert-SafeResetPath -Path $ReleaseRoot -Name 'ReleaseRoot'

Remove-Item $WorkingRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $ReleaseRoot -Recurse -Force -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Path $WorkingRoot, $ReleaseRoot -Force | Out-Null

$nupkgRoot = Join-Path $WorkingRoot 'nupkg'
$windowsExtractRoot = Join-Path $WorkingRoot 'windows-extracted'
New-Item -ItemType Directory -Path $nupkgRoot, $windowsExtractRoot -Force | Out-Null

$assets = Get-ChildItem -Path $UnsignedRoot -Recurse -File |
  Where-Object {
    $_.Name -like '*.tar.gz' -or
    $_.Name -like '*.zip' -or
    ($_.Name -like '*.nupkg' -and $_.Name -notlike '*.snupkg')
  }

if (-not $assets) {
  throw "No release assets (.tar.gz, .zip, .nupkg) found under $UnsignedRoot"
}

foreach ($asset in $assets) {
  Copy-Item $asset.FullName -Destination (Join-Path $ReleaseRoot $asset.Name) -Force
}

$nupkgs = $assets | Where-Object { $_.Name -like '*.nupkg' -and $_.Name -notlike '*.snupkg' }
foreach ($pkg in $nupkgs) {
  Copy-Item $pkg.FullName -Destination (Join-Path $nupkgRoot $pkg.Name) -Force
}

$windowsZips = $assets | Where-Object { $_.Name -like '*win-*.zip' }
$zipMap = @()

foreach ($zip in $windowsZips) {
  $extractFolder = Join-Path $windowsExtractRoot ([System.IO.Path]::GetFileNameWithoutExtension($zip.Name))
  New-Item -ItemType Directory -Path $extractFolder -Force | Out-Null
  Expand-Archive -Path $zip.FullName -DestinationPath $extractFolder -Force

  $zipMap += [PSCustomObject]@{
    ZipName      = $zip.Name
    ExtractedDir = $extractFolder
  }
}

$mapPath = Join-Path $WorkingRoot 'windows-zip-map.json'
$zipMap | ConvertTo-Json -Depth 5 | Out-File -FilePath $mapPath -Encoding utf8

$hasNupkg = if ($nupkgs.Count -gt 0) { 'true' } else { 'false' }
$hasWindows = if ($windowsZips.Count -gt 0) { 'true' } else { 'false' }

Write-Host "Prepared $($assets.Count) release assets"
Write-Host "Prepared $($nupkgs.Count) nupkg files for NuGet signing"
Write-Host "Prepared $($windowsZips.Count) Windows zip payloads for binary signing"
Write-Host "##vso[task.setvariable variable=HasNupkg]$hasNupkg"
Write-Host "##vso[task.setvariable variable=HasWindowsPayload]$hasWindows"
