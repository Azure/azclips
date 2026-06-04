param(
  [Parameter(Mandatory = $true)]
  [string]$WorkingRoot,

  [Parameter()]
  [bool]$RequireNupkg = $false,

  [Parameter()]
  [bool]$RequireWindowsAuthenticode = $false,

  [Parameter()]
  [bool]$RequireStrongName = $false
)

$ErrorActionPreference = 'Stop'

$strongNameExcludeNames = @(
  'Microsoft.Azure.Cirrus.ExtensionRuntime.dll'
)

if (-not (Test-Path $WorkingRoot)) {
  throw "Working root was not found: $WorkingRoot"
}

$nupkgCount = 0
$windowsCount = 0
$windowsDllCount = 0
$strongNameVerifiedCount = 0
$strongNameTargetCount = 0

$nupkgRoot = Join-Path $WorkingRoot 'nupkg'
if (Test-Path $nupkgRoot) {
  Push-Location $WorkingRoot
  try {
    $nupkgs = Get-ChildItem -Path $nupkgRoot -File -Filter '*.nupkg'
    $nupkgCount = $nupkgs.Count
    foreach ($pkg in $nupkgs) {
      dotnet nuget verify --all $pkg.FullName | Out-Host
      if ($LASTEXITCODE -ne 0) {
        throw "NuGet verification failed for $($pkg.Name)"
      }
    }
  }
  finally {
    Pop-Location
  }
}

$windowsRoot = Join-Path $WorkingRoot 'windows-extracted'
if (Test-Path $windowsRoot) {
  $toVerify = Get-ChildItem -Path $windowsRoot -Recurse -File |
    Where-Object {
      $_.Name -ieq 'azv2.exe' -or
      $_.Name -like 'Microsoft.Azure.Cirrus*.dll' -or
      $_.Name -like 'Microsoft.Azure.Cirrus*.exe'
    }

  $windowsCount = $toVerify.Count
  $windowsDlls = $toVerify | Where-Object { $_.Name -like 'Microsoft.Azure.Cirrus*.dll' }
  $windowsDllCount = $windowsDlls.Count

  foreach ($file in $toVerify) {
    $signature = Get-AuthenticodeSignature -FilePath $file.FullName
    if ($signature.Status -ne 'Valid') {
      throw "Authenticode verification failed for $($file.FullName). Status: $($signature.Status)"
    }
  }

  if ($RequireStrongName) {
    $strongNameTargets = $windowsDlls | Where-Object { $_.Name -notin $strongNameExcludeNames }
    $strongNameTargetCount = $strongNameTargets.Count
    foreach ($dll in $windowsDlls | Where-Object { $_.Name -in $strongNameExcludeNames }) {
      Write-Host "Skipping strong-name verification for excluded DLL: $($dll.FullName)"
    }

    foreach ($dll in $strongNameTargets) {
      try {
        $assemblyName = [System.Reflection.AssemblyName]::GetAssemblyName($dll.FullName)
      }
      catch {
        throw "Strong-name verification failed for $($dll.FullName). Unable to read managed assembly metadata."
      }

      $token = $assemblyName.GetPublicKeyToken()
      if (-not $token -or $token.Length -eq 0) {
        throw "Strong-name verification failed for $($dll.FullName). Public key token is missing."
      }

      $strongNameVerifiedCount++
    }
  }
}

if ($RequireNupkg -and $nupkgCount -eq 0) {
  throw "Verification matrix failed: expected at least one .nupkg under $nupkgRoot"
}

if ($RequireWindowsAuthenticode -and $windowsCount -eq 0) {
  throw "Verification matrix failed: expected at least one Windows binary under $windowsRoot"
}

if ($RequireStrongName -and $strongNameTargetCount -eq 0) {
  throw "Verification matrix failed: strong-name policy requires at least one non-excluded managed Cirrus DLL to verify."
}

Write-Host 'Verification matrix summary:'
Write-Host "  NuGet packages verified: $nupkgCount"
Write-Host "  Windows binaries Authenticode verified: $windowsCount"
Write-Host "  Windows DLLs discovered: $windowsDllCount"
if ($RequireStrongName) {
  Write-Host "  Windows DLLs requiring strong-name verification: $strongNameTargetCount"
  Write-Host "  Windows DLLs strong-name verified: $strongNameVerifiedCount"
} else {
  Write-Host '  Windows DLLs strong-name verified: skipped (policy disabled)'
}

Write-Host 'Signed output verification completed successfully.'
