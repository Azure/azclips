[CmdletBinding()]
param(
    [string]$RepositoryName = 'LocalAzclips',

    [string]$ModuleName = 'Az',

    [string]$RequiredVersion,

    [ValidateSet('CurrentUser', 'AllUsers')]
    [string]$Scope = 'CurrentUser'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = $PSScriptRoot
if (-not $RequiredVersion)
{
    $rollupPackage = Get-ChildItem -Path $repositoryRoot -Filter 'Az.*.nupkg' -File |
        Where-Object { $_.BaseName -match '^Az\.\d' } |
        Sort-Object -Property Name -Descending |
        Select-Object -First 1

    if (-not $rollupPackage)
    {
        throw "Could not infer Az module version. No Az.<version>.nupkg package was found under $repositoryRoot."
    }

    $RequiredVersion = $rollupPackage.BaseName.Substring('Az.'.Length)
}

$existingRepository = Get-PSRepository -Name $RepositoryName -ErrorAction SilentlyContinue
if ($existingRepository)
{
    Unregister-PSRepository -Name $RepositoryName
}

Register-PSRepository `
    -Name $RepositoryName `
    -SourceLocation $repositoryRoot `
    -PublishLocation $repositoryRoot `
    -InstallationPolicy Trusted `
    -PackageManagementProvider NuGet

Install-Module `
    -Name $ModuleName `
    -RequiredVersion $RequiredVersion `
    -Repository $RepositoryName `
    -Scope $Scope `
    -Force `
    -AllowClobber

Write-Host "Installed $ModuleName $RequiredVersion from $repositoryRoot."