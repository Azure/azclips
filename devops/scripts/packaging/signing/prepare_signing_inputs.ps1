[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [Alias('SourceRoot')]
  [string]$UnsignedRoot,

  [Parameter(Mandatory = $true)]
  [string]$WorkingRoot,

  [Parameter(Mandatory = $true)]
  [string]$ReleaseRoot,

  [Parameter()]
  [string]$ArchitectureCsv = '',

  [Parameter()]
  [string]$EnableStrongNameSigning = 'false',

  [Parameter()]
  [string]$StrongNamePatternCsv = '',

  [Parameter()]
  [string]$ExpectedStrongNameToken = '31bf3856ad364e35',

  [Parameter()]
  [string]$EnableNonCirrusDllSigning = 'false'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $UnsignedRoot)) {
  throw "Unsigned artifact root was not found: $UnsignedRoot"
}

$windowsRoot = Join-Path $WorkingRoot 'windows-extracted'
$nupkgRoot = Join-Path $WorkingRoot 'nupkg'
$nupkgExtractRoot = Join-Path $WorkingRoot 'nupkg-extracted'
$thirdPartyRoot = Join-Path $WorkingRoot 'thirdparty'
$strongNameRoot = Join-Path $WorkingRoot 'strongname'

Remove-Item $WorkingRoot, $ReleaseRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $WorkingRoot, $ReleaseRoot, $windowsRoot, $nupkgRoot, $nupkgExtractRoot, $thirdPartyRoot, $strongNameRoot -Force | Out-Null

function Test-IsFirstPartyDll {
  param([Parameter(Mandatory = $true)][System.IO.FileInfo]$File)

  return $File.Name -like 'Microsoft.Azure.Azclips*.dll' -or
    $File.Name -like 'Core.*.dll' -or
    $File.Name -ieq 'az.dll' -or
    $File.Name -ieq 'cirrus-agent.dll'
}

$assets = Get-ChildItem -Path $UnsignedRoot -Recurse -File | Where-Object {
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

$windowsAssets = $assets | Where-Object { $_.Name -match '-(x64|arm64)\.zip$' }
if (-not [string]::IsNullOrWhiteSpace($ArchitectureCsv)) {
  $architectures = $ArchitectureCsv.Split(',') |
    ForEach-Object { "$($_)".Trim().ToLowerInvariant() } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

  if (-not $architectures) {
    throw 'windows_sign_architectures resolved to an empty list.'
  }

  $windowsAssets = foreach ($asset in @($windowsAssets)) {
    $name = $asset.Name.ToLowerInvariant()
    foreach ($arch in $architectures) {
      if ($name.Contains($arch)) {
        $asset
        break
      }
    }
  }
}

$zipMap = @()
foreach ($zip in @($windowsAssets)) {
  $extractFolder = Join-Path $windowsRoot ([System.IO.Path]::GetFileNameWithoutExtension($zip.Name))
  New-Item -ItemType Directory -Path $extractFolder -Force | Out-Null
  Expand-Archive -Path $zip.FullName -DestinationPath $extractFolder -Force
  $zipMap += [PSCustomObject]@{
    ZipName = $zip.Name
    ExtractedDir = $extractFolder
  }
}
$zipMap | ConvertTo-Json -Depth 8 | Out-File -FilePath (Join-Path $WorkingRoot 'windows-zip-map.json') -Encoding utf8

$nupkgs = $assets | Where-Object { $_.Name -like '*.nupkg' -and $_.Name -notlike '*.snupkg' }
$packageMap = @()
foreach ($pkg in @($nupkgs)) {
  $targetPackage = Join-Path $nupkgRoot $pkg.Name
  Copy-Item $pkg.FullName -Destination $targetPackage -Force

  $safeName = $pkg.Name -replace '[^A-Za-z0-9_.-]', '_'
  $extractFolder = Join-Path $nupkgExtractRoot $safeName
  New-Item -ItemType Directory -Path $extractFolder -Force | Out-Null
  Expand-Archive -Path $targetPackage -DestinationPath $extractFolder -Force
  $packageMap += [PSCustomObject]@{
    PackageName = $pkg.Name
    PackagePath = $targetPackage
    ExtractedDir = $extractFolder
  }
}
$packageMap | ConvertTo-Json -Depth 8 | Out-File -FilePath (Join-Path $WorkingRoot 'nupkg-map.json') -Encoding utf8

$firstPartyPayloads = Get-ChildItem -Path $windowsRoot, $nupkgExtractRoot -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
  (Test-IsFirstPartyDll -File $_) -or
  $_.Name -like 'Microsoft.Azure.Azclips*.exe' -or
  $_.Name -ieq 'az.exe'
}

$psPayloads = Get-ChildItem -Path $nupkgExtractRoot -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
  $_.Extension -in '.ps1', '.psm1', '.psd1', '.ps1xml'
}

$strongNamePayloads = @()
if ([System.Convert]::ToBoolean($EnableStrongNameSigning)) {
  $strongNamePatterns = $StrongNamePatternCsv.Split('|') |
    ForEach-Object { "$($_)".Trim() } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

  $expectedToken = $ExpectedStrongNameToken.Trim().ToLowerInvariant()
  if ([string]::IsNullOrWhiteSpace($expectedToken)) {
    throw 'ExpectedStrongNameToken must not be empty when strong-name signing is enabled.'
  }

  $strongNameCandidates = Get-ChildItem -Path $windowsRoot, $nupkgExtractRoot -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { Test-IsFirstPartyDll -File $_ } |
    Sort-Object FullName

  $strongNameMap = @()
  $strongNameIndex = 0
  foreach ($dll in @($strongNameCandidates)) {
    $relativePath = $dll.FullName.Substring($WorkingRoot.Length).TrimStart('\', '/') -replace '\\', '/'
    if ($strongNamePatterns) {
      $matched = $false
      foreach ($pattern in $strongNamePatterns) {
        if ($relativePath -like $pattern -or $dll.Name -like $pattern) {
          $matched = $true
          break
        }
      }
      if (-not $matched) {
        continue
      }
    }

    try {
      $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($dll.FullName)
    }
    catch {
      Write-Host "Skipping non-managed strong-name candidate: $relativePath"
      continue
    }

    $token = $assemblyName.GetPublicKeyToken()
    if (-not $token -or $token.Length -eq 0) {
      throw "Strong-name candidate is not delay-signed with a public key token: $relativePath"
    }

    $tokenText = -join ($token | ForEach-Object { $_.ToString('x2') })
    if ($tokenText -ne $expectedToken) {
      throw "Strong-name candidate has unexpected public key token '$tokenText'. Expected '$expectedToken': $relativePath"
    }

    $signature = Get-AuthenticodeSignature -FilePath $dll.FullName
    if ($signature.Status -eq 'Valid') {
      Write-Host "Skipping already Authenticode-signed strong-name candidate: $relativePath"
      continue
    }

    $strongNameIndex++
    $flatName = 'strongname_{0:d5}_{1}' -f $strongNameIndex, $dll.Name
    Copy-Item $dll.FullName -Destination (Join-Path $strongNameRoot $flatName) -Force
    $strongNameMap += [PSCustomObject]@{
      FlatName = $flatName
      OriginalPath = $dll.FullName
      RelativePath = $relativePath
      FileName = $dll.Name
      PublicKeyToken = $tokenText
    }
    $strongNamePayloads += $dll
  }

  if (@($strongNameCandidates).Count -eq 0 -and @($firstPartyPayloads).Count -gt 0) {
    throw 'Strong-name signing is enabled, but no first-party DLL candidates were found. Check artifact layout and signing patterns.'
  }
  if (@($strongNameCandidates).Count -gt 0 -and @($strongNameMap).Count -eq 0) {
    throw 'Strong-name signing is enabled, but zero first-party DLLs were staged for ESRP. Check strong-name patterns, expected token, and pre-existing signatures.'
  }

  $strongNameMap | ConvertTo-Json -Depth 8 | Out-File -FilePath (Join-Path $WorkingRoot 'strongname-map.json') -Encoding utf8
}

$thirdPartyDllPayloads = @()
if ([System.Convert]::ToBoolean($EnableNonCirrusDllSigning)) {
  $thirdPartyCandidates = Get-ChildItem -Path $windowsRoot, $nupkgExtractRoot -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -ieq '.dll' -and -not (Test-IsFirstPartyDll -File $_) } |
    Sort-Object FullName

  $thirdPartyMap = @()
  $thirdPartyIndex = 0
  foreach ($dll in @($thirdPartyCandidates)) {
    $signature = Get-AuthenticodeSignature -FilePath $dll.FullName
    if ($signature.Status -eq 'Valid') {
      continue
    }

    $thirdPartyIndex++
    $flatName = 'thirdparty_{0:d5}_{1}' -f $thirdPartyIndex, $dll.Name
    Copy-Item $dll.FullName -Destination (Join-Path $thirdPartyRoot $flatName) -Force
    $thirdPartyMap += [PSCustomObject]@{
      FlatName = $flatName
      OriginalPath = $dll.FullName
    }
    $thirdPartyDllPayloads += $dll
  }

  $thirdPartyMap | ConvertTo-Json -Depth 8 | Out-File -FilePath (Join-Path $WorkingRoot 'thirdparty-map.json') -Encoding utf8
}

$hasNupkg = if (@($nupkgs).Count -gt 0) { 'true' } else { 'false' }
$hasWindows = if (@($windowsAssets).Count -gt 0) { 'true' } else { 'false' }
$hasFirstPartyPayload = if (@($firstPartyPayloads).Count -gt 0) { 'true' } else { 'false' }
$hasPowerShellPayload = if (@($psPayloads).Count -gt 0) { 'true' } else { 'false' }
$hasStrongNamePayload = if (@($strongNamePayloads).Count -gt 0) { 'true' } else { 'false' }
$hasThirdPartyDllPayload = if (@($thirdPartyDllPayloads).Count -gt 0) { 'true' } else { 'false' }

Write-Host "Prepared $(@($assets).Count) release assets"
Write-Host "Prepared $(@($nupkgs).Count) nupkg files for NuGet signing"
Write-Host "Prepared $(@($windowsAssets).Count) Windows zip payloads for binary signing"
Write-Host "Prepared $(@($firstPartyPayloads).Count) first-party binary payloads"
Write-Host "Prepared $(@($psPayloads).Count) PowerShell script/manifest payloads"
Write-Host "Prepared $(@($strongNamePayloads).Count) explicit strong-name payloads"
Write-Host "Prepared $(@($thirdPartyDllPayloads).Count) unsigned third-party DLL payloads for co-signing"
Write-Host "##vso[task.setvariable variable=HasNupkg]$hasNupkg"
Write-Host "##vso[task.setvariable variable=HasWindowsPayload]$hasWindows"
Write-Host "##vso[task.setvariable variable=HasFirstPartyPayload]$hasFirstPartyPayload"
Write-Host "##vso[task.setvariable variable=HasPowerShellPayload]$hasPowerShellPayload"
Write-Host "##vso[task.setvariable variable=HasStrongNamePayload]$hasStrongNamePayload"
Write-Host "##vso[task.setvariable variable=HasThirdPartyDllPayload]$hasThirdPartyDllPayload"
