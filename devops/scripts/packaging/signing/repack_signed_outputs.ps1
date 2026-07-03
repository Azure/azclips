param(
  [Parameter(Mandatory = $true)]
  [string]$WorkingRoot,

  [Parameter(Mandatory = $true)]
  [string]$ReleaseRoot
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $WorkingRoot)) {
  throw "Working root was not found: $WorkingRoot"
}

if (-not (Test-Path $ReleaseRoot)) {
  throw "Release root was not found: $ReleaseRoot"
}

$mapPath = Join-Path $WorkingRoot 'windows-zip-map.json'
if (Test-Path $mapPath) {
  $zipMap = Get-Content $mapPath -Raw | ConvertFrom-Json

  foreach ($entry in $zipMap) {
    $zipName = $entry.ZipName
    $extractDir = $entry.ExtractedDir
    $zipPath = Join-Path $ReleaseRoot $zipName

    if (-not (Test-Path $extractDir)) {
      throw "Expected extracted folder was not found: $extractDir"
    }

    if (Test-Path $zipPath) {
      Remove-Item $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $extractDir '*') -DestinationPath $zipPath -CompressionLevel Optimal
  }
}

$nupkgRoot = Join-Path $WorkingRoot 'nupkg'
if (Test-Path $nupkgRoot) {
  Get-ChildItem -Path $nupkgRoot -File -Filter '*.nupkg' | ForEach-Object {
    Copy-Item $_.FullName -Destination (Join-Path $ReleaseRoot $_.Name) -Force
  }
}

$manifestPath = Join-Path $ReleaseRoot 'signing-manifest.json'
$manifest = Get-ChildItem -Path $ReleaseRoot -File | ForEach-Object {
  $hash = Get-FileHash -Path $_.FullName -Algorithm SHA256
  [PSCustomObject]@{
    fileName = $_.Name
    size     = $_.Length
    sha256   = $hash.Hash.ToLowerInvariant()
  }
}

$manifest | ConvertTo-Json -Depth 5 | Out-File -FilePath $manifestPath -Encoding utf8
Write-Host "Wrote signing manifest: $manifestPath"
