param(
  [Parameter(Mandatory = $true)]
  [string]$WorkingRoot
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $WorkingRoot)) {
  throw "Working root was not found: $WorkingRoot"
}

$nupkgRoot = Join-Path $WorkingRoot 'nupkg'
if (Test-Path $nupkgRoot) {
  $nupkgs = Get-ChildItem -Path $nupkgRoot -File -Filter '*.nupkg'
  foreach ($pkg in $nupkgs) {
    dotnet nuget verify --all $pkg.FullName | Out-Host
    if ($LASTEXITCODE -ne 0) {
      throw "NuGet verification failed for $($pkg.Name)"
    }
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

  foreach ($file in $toVerify) {
    $signature = Get-AuthenticodeSignature -FilePath $file.FullName
    if ($signature.Status -ne 'Valid') {
      throw "Authenticode verification failed for $($file.FullName). Status: $($signature.Status)"
    }
  }
}

Write-Host 'Signed output verification completed successfully.'
