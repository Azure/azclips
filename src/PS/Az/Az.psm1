$ErrorActionPreference = 'Stop'

$moduleNames = @(
    'Accounts'
    'Network'
    'Compute'
    'Resources'
    'ManagedServiceIdentity'
    'Config'
    'MsGraph'
)

foreach ($moduleName in $moduleNames)
{
    $manifest = Join-Path $PSScriptRoot "../Microsoft.Azure.Azclips.Extensions.$moduleName/$moduleName.psd1"
    Import-Module $manifest -Global -ErrorAction Stop
}
