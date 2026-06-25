[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)][string]$WorkingRoot,
  [Parameter(Mandatory = $true)][string]$ReleaseRoot,
  [Parameter(Mandatory = $true)][string]$RequirePayloadVerification,
  [Parameter(Mandatory = $true)][string]$RequireStrongName,
  [Parameter(Mandatory = $true)][string]$ExpectedStrongNameToken,
  [Parameter(Mandatory = $true)][string]$SourceProject,
  [Parameter(Mandatory = $true)][string]$SourcePipelineName,
  [Parameter(Mandatory = $true)][string]$SourceArtifactName,
  [Parameter(Mandatory = $true)][string]$SourceArtifactSource,
  [Parameter(Mandatory = $true)][string]$SigningProject,
  [Parameter(Mandatory = $true)][string]$SigningPipeline,
  [Parameter(Mandatory = $true)][string]$SigningBuildId,
  [Parameter(Mandatory = $true)][string]$SigningBuildNumber,
  [Parameter(Mandatory = $true)][string]$SigningBranch,
  [Parameter(Mandatory = $true)][string]$SigningCommit,
  [Parameter(Mandatory = $true)][string]$AuthenticodeKeyCode,
  [Parameter(Mandatory = $true)][string]$NuGetKeyCode,
  [Parameter(Mandatory = $true)][string]$StrongNameKeyCode,
  [Parameter(Mandatory = $true)][string]$ThirdPartyKeyCode,
  [Parameter(Mandatory = $true)][string]$TimestampEnabled,
  [Parameter(Mandatory = $true)][string]$StrongNameEnabled,
  [string]$StrongNameCandidatePatterns = '',
  [Parameter(Mandatory = $true)][string]$ThirdPartySigningEnabled,
  [Parameter(Mandatory = $true)][string]$NuGetVerifyEnabled
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$workingRoot = $WorkingRoot
$releaseRoot = $ReleaseRoot
$verifyRoot = Join-Path $workingRoot 'verify-extracted-release'
$requirePayloadVerification = [System.Convert]::ToBoolean($RequirePayloadVerification)
$requireStrongName = [System.Convert]::ToBoolean($RequireStrongName)
$expectedStrongNameToken = $ExpectedStrongNameToken.Trim().ToLowerInvariant()
Remove-Item $verifyRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $verifyRoot -Force | Out-Null

$failures = [System.Collections.Generic.List[string]]::new()
$payloadsChecked = 0
$nupkgSignaturesVerified = 0
$strongNameFileNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$strongNameMapPath = Join-Path $workingRoot 'strongname-map.json'
if ($requireStrongName -and (Test-Path $strongNameMapPath)) {
  foreach ($entry in @(Get-Content $strongNameMapPath -Raw | ConvertFrom-Json)) {
    [void]$strongNameFileNames.Add([string]$entry.FileName)
  }
}

function Add-SigningFailure {
  param([Parameter(Mandatory = $true)][string]$Message)
  $script:failures.Add($Message)
}

function Test-AuthenticodeValid {
  param([Parameter(Mandatory = $true)][System.IO.FileInfo]$File)
  $script:payloadsChecked++
  $signature = Get-AuthenticodeSignature -FilePath $File.FullName
  if ($signature.Status -ne 'Valid') {
    Add-SigningFailure "AUTHENTICODE-INVALID: $($File.FullName) (Status=$($signature.Status))"
  }
}

function Test-StrongNameValid {
  param([Parameter(Mandatory = $true)][System.IO.FileInfo]$File)
  try {
    $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($File.FullName)
    $token = $assemblyName.GetPublicKeyToken()
    if (-not $token -or $token.Length -eq 0) {
      Add-SigningFailure "STRONGNAME-MISSING: $($File.FullName)"
    }
    else {
      $tokenText = -join ($token | ForEach-Object { $_.ToString('x2') })
      if ($tokenText -ne $expectedStrongNameToken) {
        Add-SigningFailure "STRONGNAME-UNEXPECTED-TOKEN: $($File.FullName) (Token=$tokenText, Expected=$expectedStrongNameToken)"
      }
    }

    $bytes = [System.IO.File]::ReadAllBytes($File.FullName)
    $peHeaderOffset = [System.BitConverter]::ToInt32($bytes, 0x3C)
    $optionalHeaderOffset = $peHeaderOffset + 24
    $optionalHeaderMagic = [System.BitConverter]::ToUInt16($bytes, $optionalHeaderOffset)
    if ($optionalHeaderMagic -eq 0x20b) {
      $dataDirectoryOffset = $optionalHeaderOffset + 112
    }
    else {
      $dataDirectoryOffset = $optionalHeaderOffset + 96
    }
    $clrHeaderRva = [System.BitConverter]::ToInt32($bytes, $dataDirectoryOffset + 14 * 8)
    if ($clrHeaderRva -eq 0) {
      Add-SigningFailure "STRONGNAME-NO-CLR-HEADER: $($File.FullName)"
      return
    }

    $sectionCount = [System.BitConverter]::ToUInt16($bytes, $peHeaderOffset + 6)
    $optionalHeaderSize = [System.BitConverter]::ToUInt16($bytes, $peHeaderOffset + 20)
    $sectionOffset = $optionalHeaderOffset + $optionalHeaderSize
    $clrHeaderOffset = $null
    for ($index = 0; $index -lt $sectionCount; $index++) {
      $currentSectionOffset = $sectionOffset + $index * 40
      $virtualAddress = [System.BitConverter]::ToInt32($bytes, $currentSectionOffset + 12)
      $virtualSize = [System.BitConverter]::ToInt32($bytes, $currentSectionOffset + 8)
      $rawPointer = [System.BitConverter]::ToInt32($bytes, $currentSectionOffset + 20)
      if ($clrHeaderRva -ge $virtualAddress -and $clrHeaderRva -lt ($virtualAddress + $virtualSize)) {
        $clrHeaderOffset = $clrHeaderRva - $virtualAddress + $rawPointer
        break
      }
    }

    if ($null -eq $clrHeaderOffset) {
      Add-SigningFailure "STRONGNAME-CLR-HEADER-UNMAPPED: $($File.FullName)"
      return
    }

    $clrFlags = [System.BitConverter]::ToInt32($bytes, $clrHeaderOffset + 16)
    if (-not ($clrFlags -band 0x8)) {
      Add-SigningFailure "STRONGNAME-NOT-COMPLETED: $($File.FullName)"
    }
  }
  catch {
    Add-SigningFailure "STRONGNAME-UNREADABLE: $($File.FullName) ($($_.Exception.Message))"
  }
}

function Test-PayloadTree {
  param([Parameter(Mandatory = $true)][string]$Root)

  $payloadFiles = Get-ChildItem -Path $Root -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -ieq 'az.exe' -or
    $_.Name -ieq 'az.dll' -or
    $_.Name -ieq 'cirrus-agent.dll' -or
    $_.Name -like 'Core.*.dll' -or
    $_.Name -like 'Microsoft.Azure.Cirrus*.dll' -or
    $_.Name -like 'Microsoft.Azure.Cirrus*.exe' -or
    $_.Name -like 'Microsoft.Azure.Cirrus.Cmdlets*.dll' -or
    $_.Extension -in '.ps1', '.psm1', '.psd1', '.ps1xml'
  }

  foreach ($payload in @($payloadFiles)) {
    Test-AuthenticodeValid -File $payload
    if ($requireStrongName -and $strongNameFileNames.Contains($payload.Name)) {
      Test-StrongNameValid -File $payload
    }
  }
}

if ($requirePayloadVerification) {
  $releaseArchives = Get-ChildItem -Path $releaseRoot -File -ErrorAction SilentlyContinue | Where-Object {
    $_.Name -like '*.zip' -or
    $_.Name -like 'Microsoft.Azure.Cirrus*.nupkg' -or
    $_.Name -like 'Az.*.nupkg'
  }

  foreach ($archive in @($releaseArchives)) {
    $safeName = $archive.Name -replace '[^A-Za-z0-9_.-]', '_'
    $extractDir = Join-Path $verifyRoot $safeName
    New-Item -ItemType Directory -Path $extractDir -Force | Out-Null
    try {
      Expand-Archive -Path $archive.FullName -DestinationPath $extractDir -Force
      Test-PayloadTree -Root $extractDir
    }
    catch {
      Add-SigningFailure "ARCHIVE-VERIFY-FAILED: $($archive.FullName) ($($_.Exception.Message))"
    }
  }
  Write-Host "Payload verification sweep checked files: $payloadsChecked"
}
else {
  Write-Host 'Payload verification is disabled by require_payload_verification=false.'
}

$nupkgs = Get-ChildItem -Path $releaseRoot -File -Filter '*.nupkg' | Where-Object {
  $_.Name -like 'Microsoft.Azure.Cirrus*.nupkg' -or $_.Name -like 'Az.*.nupkg'
} | Sort-Object Name
foreach ($pkg in @($nupkgs)) {
  dotnet nuget verify --all $pkg.FullName | Out-Host
  if ($LASTEXITCODE -ne 0) {
    Add-SigningFailure "NUGET-SIGNATURE-INVALID: $($pkg.FullName)"
  }
  else {
    $nupkgSignaturesVerified++
  }
}
Write-Host "NuGet package signatures verified: $nupkgSignaturesVerified"

if ($failures.Count -gt 0) {
  Write-Host "##vso[task.logissue type=error]Holistic signing verification failed with $($failures.Count) issue(s)."
  $failures | Select-Object -First 75 | ForEach-Object { Write-Host "  $_" }
  throw 'Holistic signing verification failed.'
}

Write-Host 'Holistic signing verification passed.'

$manifestPath = Join-Path $releaseRoot 'signing-manifest.json'
$manifest = Get-ChildItem -Path $releaseRoot -File | ForEach-Object {
  $hash = Get-FileHash -Path $_.FullName -Algorithm SHA256
  [PSCustomObject]@{
    fileName = $_.Name
    size = $_.Length
    sha256 = $hash.Hash.ToLowerInvariant()
  }
}
$manifest | ConvertTo-Json -Depth 8 | Out-File -FilePath $manifestPath -Encoding utf8
Write-Host "Wrote signing manifest: $manifestPath"

$provenancePath = Join-Path $releaseRoot 'signing-provenance.json'
$provenance = [PSCustomObject]@{
  source = [PSCustomObject]@{
    project = $SourceProject
    pipeline = $SourcePipelineName
    artifact = $SourceArtifactName
    artifactSource = $SourceArtifactSource
  }
  signingRun = [PSCustomObject]@{
    project = $SigningProject
    pipeline = $SigningPipeline
    buildId = $SigningBuildId
    buildNumber = $SigningBuildNumber
    branch = $SigningBranch
    commit = $SigningCommit
  }
  policy = [PSCustomObject]@{
    authenticodeKeyCode = $AuthenticodeKeyCode
    nugetKeyCode = $NuGetKeyCode
    strongNameKeyCode = $StrongNameKeyCode
    thirdPartyKeyCode = $ThirdPartyKeyCode
    timestampEnabled = $TimestampEnabled
    strongNameEnabled = $StrongNameEnabled
    strongNameCandidatePatterns = $StrongNameCandidatePatterns
    strongNamePublicKeyToken = $ExpectedStrongNameToken
    thirdPartySigningEnabled = $ThirdPartySigningEnabled
    nugetVerifyEnabled = $NuGetVerifyEnabled
    payloadVerificationRequired = $RequirePayloadVerification
  }
}
$provenance | ConvertTo-Json -Depth 8 | Out-File -FilePath $provenancePath -Encoding utf8
Write-Host "Wrote signing provenance: $provenancePath"
