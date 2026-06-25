[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)][string]$SourceRoot,
  [Parameter(Mandatory = $true)][string]$WorkingRoot,
  [Parameter(Mandatory = $true)][string]$ReleaseRoot,
  [Parameter(Mandatory = $true)][string]$ArchitectureCsv,
  [Parameter(Mandatory = $true)][string]$EnableStrongNameSigning,
  [string]$StrongNamePatternCsv = '',
  [Parameter(Mandatory = $true)][string]$ExpectedStrongNameToken,
  [Parameter(Mandatory = $true)][string]$EnableNonCirrusDllSigning
)

$ErrorActionPreference = 'Stop'

$windowsRoot = Join-Path $WorkingRoot 'windows-extracted'
$nupkgRoot = Join-Path $WorkingRoot 'nupkg'
$nupkgExtractRoot = Join-Path $WorkingRoot 'nupkg-extracted'
$thirdPartyRoot = Join-Path $WorkingRoot 'thirdparty'
$strongNameRoot = Join-Path $WorkingRoot 'strongname'

Remove-Item $WorkingRoot, $ReleaseRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $WorkingRoot, $ReleaseRoot, $windowsRoot, $nupkgRoot, $nupkgExtractRoot, $thirdPartyRoot, $strongNameRoot -Force | Out-Null

if (-not (Test-Path $SourceRoot)) {
  throw "Unsigned artifact root was not found: $SourceRoot"
}

$architectures = $ArchitectureCsv.Split(',') |
  ForEach-Object { "$($_)".Trim().ToLowerInvariant() } |
  Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
if (-not $architectures) {
  throw 'windows_sign_architectures resolved to an empty list.'
}

$allWindowsAssets = Get-ChildItem -Path $SourceRoot -Recurse -File | Where-Object { $_.Name -like '*win-*.zip' }
$windowsAssets = foreach ($asset in $allWindowsAssets) {
  $name = $asset.Name.ToLowerInvariant()
  foreach ($arch in $architectures) {
    if ($name.Contains($arch)) {
      $asset
      break
    }
  }
}

$nupkgAssets = Get-ChildItem -Path $SourceRoot -Recurse -File | Where-Object {
  ($_.Name -like 'Microsoft.Azure.Cirrus*.nupkg' -or $_.Name -like 'Az.*.nupkg') -and
  $_.Name -notlike '*.snupkg'
} | Sort-Object FullName

if (-not $windowsAssets -and -not $nupkgAssets) {
  throw "No Windows zip or target nupkg artifacts found under $SourceRoot"
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

$packageMap = @()
foreach ($pkg in @($nupkgAssets)) {
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

function Test-IsFirstPartyCirrusDll {
  param([Parameter(Mandatory = $true)][System.IO.FileInfo]$File)
  return $File.Name -like 'Microsoft.Azure.Cirrus*.dll' -or
    $File.Name -like 'Core.*.dll' -or
    $File.Name -ieq 'az.dll' -or
    $File.Name -ieq 'cirrus-agent.dll'
}

$cirrusPayloads = Get-ChildItem -Path $windowsRoot, $nupkgExtractRoot -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
  $(Test-IsFirstPartyCirrusDll -File $_) -or $_.Name -like 'Microsoft.Azure.Cirrus*.exe' -or $_.Name -like 'az.exe'
}
$psPayloads = Get-ChildItem -Path $nupkgExtractRoot -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
  $_.Extension -in '.ps1', '.psm1', '.psd1', '.ps1xml'
}
$strongNamePayloads = @()
if ([System.Convert]::ToBoolean($EnableStrongNameSigning)) {
  $strongNamePatterns = $StrongNamePatternCsv.Split('|') |
    ForEach-Object { "$($_)".Trim() } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

  if ($strongNamePatterns) {
    Write-Host "Strong-name candidate pattern filter: $($strongNamePatterns -join ', ')"
  }
  else {
    Write-Host 'Strong-name candidate pattern filter is empty; auto-detecting delay-signed managed Cirrus DLLs with the expected public key token.'
  }

  $expectedToken = $ExpectedStrongNameToken.Trim().ToLowerInvariant()
  if ([string]::IsNullOrWhiteSpace($expectedToken)) {
    throw 'esrp_strong_name_public_key_token must not be empty when strong-name signing is enabled.'
  }

  $strongNameCandidates = Get-ChildItem -Path $windowsRoot, $nupkgExtractRoot -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
    Test-IsFirstPartyCirrusDll -File $_
  } | Sort-Object FullName

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

  if (@($strongNameCandidates).Count -eq 0 -and @($cirrusPayloads).Count -gt 0) {
    throw 'Strong-name signing is enabled, but no first-party Cirrus DLL candidates were found. Check artifact layout and Test-IsFirstPartyCirrusDll patterns.'
  }
  if (@($strongNameCandidates).Count -gt 0 -and @($strongNameMap).Count -eq 0) {
    throw 'Strong-name signing is enabled, but zero first-party Cirrus DLLs were staged for ESRP. Check strong-name patterns, expected token, and pre-existing signatures.'
  }

  $strongNameMap | ConvertTo-Json -Depth 8 | Out-File -FilePath (Join-Path $WorkingRoot 'strongname-map.json') -Encoding utf8
}
$nonCirrusDllPayloads = @()
if ([System.Convert]::ToBoolean($EnableNonCirrusDllSigning)) {
  $nonCirrusCandidates = Get-ChildItem -Path $windowsRoot, $nupkgExtractRoot -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
    $_.Extension -ieq '.dll' -and -not (Test-IsFirstPartyCirrusDll -File $_)
  } | Sort-Object FullName

  $thirdPartyMap = @()
  $thirdPartyIndex = 0
  foreach ($dll in @($nonCirrusCandidates)) {
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
    $nonCirrusDllPayloads += $dll
  }

  $thirdPartyMap | ConvertTo-Json -Depth 8 | Out-File -FilePath (Join-Path $WorkingRoot 'thirdparty-map.json') -Encoding utf8
}

$hasWindows = if (@($windowsAssets).Count -gt 0) { 'true' } else { 'false' }
$hasNupkg = if (@($nupkgAssets).Count -gt 0) { 'true' } else { 'false' }
$hasCirrusPayload = if (@($cirrusPayloads).Count -gt 0) { 'true' } else { 'false' }
$hasPowerShellPayload = if (@($psPayloads).Count -gt 0) { 'true' } else { 'false' }
$hasStrongNamePayload = if (@($strongNamePayloads).Count -gt 0) { 'true' } else { 'false' }
$hasNonCirrusDllPayload = if (@($nonCirrusDllPayloads).Count -gt 0) { 'true' } else { 'false' }

Write-Host "Prepared Windows zip artifacts: $(@($windowsAssets).Count)"
Write-Host "Prepared target nupkg artifacts: $(@($nupkgAssets).Count)"
Write-Host "Prepared Cirrus binary payloads: $(@($cirrusPayloads).Count)"
Write-Host "Prepared PowerShell script/manifest payloads: $(@($psPayloads).Count)"
Write-Host "Prepared explicit strong-name payloads: $(@($strongNamePayloads).Count)"
Write-Host "Prepared unsigned non-Cirrus DLL payloads for co-signing: $(@($nonCirrusDllPayloads).Count)"
Write-Host "##vso[task.setvariable variable=HasWindowsPayload]$hasWindows"
Write-Host "##vso[task.setvariable variable=HasNupkg]$hasNupkg"
Write-Host "##vso[task.setvariable variable=HasCirrusPayload]$hasCirrusPayload"
Write-Host "##vso[task.setvariable variable=HasPowerShellPayload]$hasPowerShellPayload"
Write-Host "##vso[task.setvariable variable=HasStrongNamePayload]$hasStrongNamePayload"
Write-Host "##vso[task.setvariable variable=HasNonCirrusDllPayload]$hasNonCirrusDllPayload"
