# Installs build-time PowerShell dependencies pinned to known-good versions.
# Idempotent: skips modules that are already present at the pinned version.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$dependencies = @(
    @{ Name = 'Microsoft.PowerShell.PlatyPS'; RequiredVersion = '1.0.1' }
)

foreach ($dep in $dependencies)
{
    $existing = Get-Module -Name $dep.Name -ListAvailable |
        Where-Object { $_.Version -eq [version]$dep.RequiredVersion }

    if ($existing)
    {
        Write-Host "$($dep.Name) $($dep.RequiredVersion) already installed at $($existing[0].Path)"
        continue
    }

    Write-Host "Installing $($dep.Name) $($dep.RequiredVersion) (CurrentUser scope)..."
    Install-PSResource -Name $dep.Name `
        -Version $dep.RequiredVersion `
        -Repository PSGallery `
        -Scope CurrentUser `
        -TrustRepository `
        -Quiet
}
