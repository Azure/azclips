[CmdletBinding()]
param(
    [string]$RepositoryName = 'LocalAzclips',

    [string]$ModuleName = 'Az',

    [string]$RequiredVersion,

    [ValidateSet('CurrentUser', 'AllUsers')]
    [string]$Scope = 'CurrentUser'
)

$ErrorActionPreference = 'Stop'

$minimumPowerShellVersion = [version]'7.6'
$currentPowerShellVersion = [version]::new($PSVersionTable.PSVersion.Major, $PSVersionTable.PSVersion.Minor)
if ($currentPowerShellVersion -lt $minimumPowerShellVersion)
{
    throw "Azclips PowerShell modules require PowerShell $minimumPowerShellVersion or later. Current PowerShell version is $($PSVersionTable.PSVersion). Install or update PowerShell from https://aka.ms/powershell."
}

$repositoryRoot = $PSScriptRoot
if (-not $RequiredVersion)
{
    $packageNamePattern = '^' + [regex]::Escape($ModuleName) + '\.(?<Version>\d+(?:\.\d+){1,3})$'
    $moduleVersion = Get-ChildItem -Path $repositoryRoot -Filter "$ModuleName.*.nupkg" -File |
        Where-Object { $_.BaseName -match $packageNamePattern } |
        ForEach-Object {
            $null = $_.BaseName -match $packageNamePattern
            [version]$Matches.Version
        } |
        Sort-Object -Descending |
        Select-Object -First 1

    if (-not $moduleVersion)
    {
        throw "Could not infer $ModuleName module version. No $ModuleName.<version>.nupkg package was found under $repositoryRoot."
    }

    $RequiredVersion = $moduleVersion.ToString()
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