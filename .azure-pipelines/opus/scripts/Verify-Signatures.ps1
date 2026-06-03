<#
.SYNOPSIS
  Verify Authenticode (and NuGet) signatures across signed artifacts.

.PARAMETER Mode
  WindowsPayload : verify DLL/EXE/PS* signatures inside the extracted Windows trees.
  ReleaseDrop    : verify the final release drop (DLL/EXE in zips, .nupkg, .msi).

.NOTES
  Modeled on azure-powershell `tools/CheckSignature.ps1` plus `Check-NupkgAndMsi.ps1`.
  Throws if any signature is invalid.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Root,
    [Parameter(Mandatory)] [ValidateSet('WindowsPayload', 'ReleaseDrop')] [string] $Mode
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$failures = New-Object System.Collections.Generic.List[string]

function Test-Authenticode([string]$Path) {
    $sig = Get-AuthenticodeSignature -FilePath $Path
    if ($sig.Status -ne 'Valid') {
        $failures.Add("AUTHENTICODE [$($sig.Status)] $Path")
    }
}

function Test-NugetPackage([string]$Path) {
    # Use the dotnet CLI's verifier so we don't depend on the legacy nuget.exe
    # (matches the validation stages and the current cirrus signing template).
    $output = & dotnet nuget verify --all $Path 2>&1
    if ($LASTEXITCODE -ne 0) {
        $failures.Add("NUGET [exit=$LASTEXITCODE] $Path`n$output")
    }
}

if ($Mode -eq 'WindowsPayload') {
    $patterns = '*.dll', '*.exe', '*.ps1', '*.psm1', '*.psd1', '*.ps1xml'
    foreach ($pattern in $patterns) {
        Get-ChildItem -Path $Root -Recurse -Filter $pattern -File -ErrorAction SilentlyContinue |
            ForEach-Object { Test-Authenticode $_.FullName }
    }
}
elseif ($Mode -eq 'ReleaseDrop') {
    Get-ChildItem -Path $Root -Filter '*.msi' -File | ForEach-Object { Test-Authenticode $_.FullName }
    Get-ChildItem -Path $Root -Filter '*.nupkg' -File | Where-Object { $_.Name -notlike '*.snupkg' } |
        ForEach-Object { Test-NugetPackage $_.FullName }
}

if ($failures.Count -gt 0) {
    Write-Error ("Signature verification failed for {0} item(s):`n{1}" -f $failures.Count, ($failures -join "`n"))
    throw 'Signature verification failed.'
}

Write-Host "Signature verification ($Mode) passed."
