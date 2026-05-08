<#
.SYNOPSIS
    Builds the Cirrus solution and updates PowerShell module manifests for the specified configuration.

.PARAMETER Configuration
    The build configuration. Valid values are 'Debug' and 'Release'. Defaults to 'Debug'.

.EXAMPLE
    ./build.ps1 -Configuration Release
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

dotnet build "$PSScriptRoot/../cirrus.slnx" --configuration $Configuration

# Update .psd1 module manifests to reference the correct artifact paths
& "$PSScriptRoot/Set-ModuleConfiguration.ps1" -Configuration $Configuration
