[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string]$WorkingRoot,

  [Parameter()]
  [bool]$RequireNupkg = $false,

  [Parameter()]
  [bool]$RequireWindowsAuthenticode = $false,

  [Parameter()]
  [bool]$RequireStrongName = $false,

  [Parameter()]
  [string]$ExpectedStrongNameToken = '31bf3856ad364e35',

  [Parameter()]
  [bool]$RequirePowerShellAuthenticode = $false,

  [Parameter()]
  [bool]$RequirePackagePayloadAuthenticode = $false
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $WorkingRoot)) {
  throw "Working root was not found: $WorkingRoot"
}

$failures = [System.Collections.Generic.List[string]]::new()
$nupkgCount = 0
$windowsCount = 0
$windowsDllCount = 0
$packageDllCount = 0
$strongNameVerifiedCount = 0
$powerShellCount = 0
$packagePayloadCount = 0
$expectedToken = $ExpectedStrongNameToken.Trim().ToLowerInvariant()

function Add-SigningFailure {
  param([Parameter(Mandatory = $true)][string]$Message)
  $script:failures.Add($Message)
}

function Test-IsFirstPartyPayload {
  param([Parameter(Mandatory = $true)][System.IO.FileInfo]$File)

  return $File.Name -ieq 'az.exe' -or
    $File.Name -ieq 'az.dll' -or
    $File.Name -ieq 'azclips-agent.dll' -or
    $File.Name -like 'Core.*.dll' -or
    $File.Name -like 'Microsoft.Azure.Azclips*.dll' -or
    $File.Name -like 'Microsoft.Azure.Azclips*.exe'
}

function Test-AuthenticodeValid {
  param([Parameter(Mandatory = $true)][System.IO.FileInfo]$File)

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
      return
    }

    $tokenText = -join ($token | ForEach-Object { $_.ToString('x2') })
    if ($tokenText -ne $expectedToken) {
      Add-SigningFailure "STRONGNAME-UNEXPECTED-TOKEN: $($File.FullName) (Token=$tokenText, Expected=$expectedToken)"
    }

    $bytes = [System.IO.File]::ReadAllBytes($File.FullName)
    $peHeaderOffset = [System.BitConverter]::ToInt32($bytes, 0x3C)
    $optionalHeaderOffset = $peHeaderOffset + 24
    $optionalHeaderMagic = [System.BitConverter]::ToUInt16($bytes, $optionalHeaderOffset)
    $dataDirectoryOffset = if ($optionalHeaderMagic -eq 0x20b) { $optionalHeaderOffset + 112 } else { $optionalHeaderOffset + 96 }
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
    else {
      $script:strongNameVerifiedCount++
    }
  }
  catch {
    Add-SigningFailure "STRONGNAME-UNREADABLE: $($File.FullName) ($($_.Exception.Message))"
  }
}

$nupkgRoot = Join-Path $WorkingRoot 'nupkg'
if (Test-Path $nupkgRoot) {
  Push-Location $WorkingRoot
  try {
    $nupkgs = Get-ChildItem -Path $nupkgRoot -File -Filter '*.nupkg'
    $nupkgCount = $nupkgs.Count
    foreach ($pkg in $nupkgs) {
      dotnet nuget verify --all $pkg.FullName | Out-Host
      if ($LASTEXITCODE -ne 0) {
        Add-SigningFailure "NUGET-SIGNATURE-INVALID: $($pkg.FullName)"
      }
    }
  }
  finally {
    Pop-Location
  }
}

$windowsRoot = Join-Path $WorkingRoot 'windows-extracted'
if (Test-Path $windowsRoot) {
  $toVerify = Get-ChildItem -Path $windowsRoot -Recurse -File | Where-Object { Test-IsFirstPartyPayload -File $_ }
  $windowsCount = $toVerify.Count
  $windowsDlls = $toVerify | Where-Object {
    $_.Name -like 'Microsoft.Azure.Azclips*.dll' -or
    $_.Name -like 'Core.*.dll' -or
    $_.Name -ieq 'az.dll' -or
    $_.Name -ieq 'azclips-agent.dll'
  }
  $windowsDllCount = $windowsDlls.Count

  if ($RequireWindowsAuthenticode) {
    foreach ($file in @($toVerify)) {
      Test-AuthenticodeValid -File $file
    }
  }

  if ($RequireStrongName) {
    foreach ($dll in @($windowsDlls)) {
      Test-StrongNameValid -File $dll
    }
  }
}

$nupkgExtractRoot = Join-Path $WorkingRoot 'nupkg-extracted'
if (Test-Path $nupkgExtractRoot) {
  $packagePayloads = Get-ChildItem -Path $nupkgExtractRoot -Recurse -File | Where-Object { Test-IsFirstPartyPayload -File $_ }
  $packagePayloadCount = $packagePayloads.Count

  if ($RequirePackagePayloadAuthenticode) {
    foreach ($file in @($packagePayloads)) {
      Test-AuthenticodeValid -File $file
    }
  }

  if ($RequireStrongName) {
    $packageDlls = $packagePayloads | Where-Object {
      $_.Name -like 'Microsoft.Azure.Azclips*.dll' -or
      $_.Name -like 'Core.*.dll' -or
      $_.Name -ieq 'az.dll' -or
      $_.Name -ieq 'azclips-agent.dll'
    }
    $packageDllCount = $packageDlls.Count
    foreach ($dll in @($packageDlls)) {
      Test-StrongNameValid -File $dll
    }
  }
}

if ($RequirePowerShellAuthenticode -and (Test-Path $nupkgExtractRoot)) {
  $psFiles = Get-ChildItem -Path $nupkgExtractRoot -Recurse -File | Where-Object {
    $_.Extension -in '.ps1', '.psm1', '.psd1', '.ps1xml'
  }
  $powerShellCount = $psFiles.Count
  foreach ($file in @($psFiles)) {
    Test-AuthenticodeValid -File $file
  }
}

if ($RequireNupkg -and $nupkgCount -eq 0) {
  Add-SigningFailure "Verification matrix failed: expected at least one .nupkg under $nupkgRoot"
}

if ($RequireWindowsAuthenticode -and $windowsCount -eq 0) {
  Add-SigningFailure "Verification matrix failed: expected at least one Windows binary under $windowsRoot"
}

if ($RequireStrongName -and ($windowsDllCount + $packageDllCount) -eq 0) {
  Add-SigningFailure 'Verification matrix failed: strong-name policy requires at least one managed first-party DLL to verify.'
}

if ($RequirePowerShellAuthenticode -and $powerShellCount -eq 0) {
  Add-SigningFailure "Verification matrix failed: expected at least one PowerShell payload under $nupkgExtractRoot"
}

if ($failures.Count -gt 0) {
  Write-Host "##vso[task.logissue type=error]Signed output verification failed with $($failures.Count) issue(s)."
  $failures | Select-Object -First 75 | ForEach-Object { Write-Host "  $_" }
  throw 'Signed output verification failed.'
}

Write-Host 'Verification matrix summary:'
Write-Host "  NuGet packages verified: $nupkgCount"
if ($RequireWindowsAuthenticode) {
  Write-Host "  Windows binaries Authenticode verified: $windowsCount"
} else {
  Write-Host "  Windows binaries discovered (Authenticode check skipped): $windowsCount"
}
Write-Host "  Windows DLLs discovered: $windowsDllCount"
Write-Host "  Package DLLs discovered: $packageDllCount"
if ($RequirePackagePayloadAuthenticode) {
  Write-Host "  Package first-party payloads Authenticode verified: $packagePayloadCount"
} else {
  Write-Host "  Package first-party payloads discovered (Authenticode check skipped): $packagePayloadCount"
}
if ($RequireStrongName) {
  Write-Host "  Windows DLLs strong-name verified: $strongNameVerifiedCount"
} else {
  Write-Host '  Windows DLLs strong-name verified: skipped (policy disabled)'
}
Write-Host "  PowerShell payloads Authenticode verified: $powerShellCount"
Write-Host 'Signed output verification completed successfully.'
