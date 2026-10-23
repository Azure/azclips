@{
RootModule = ''
ModuleVersion = '0.1.5'
CompatiblePSEditions = @('Core')
GUID = '7c7b69d5-7b18-4e6c-9c1c-22d0a14e86c5'
Author = 'Microsoft'
CompanyName = 'Microsoft'
Copyright = '(c) Microsoft. All rights reserved.'
Description = 'Rollup module for Azure PowerShell cmdlets'
PowerShellVersion = '7.6'
RequiredModules = @(
    @{ ModuleName = '../Accounts/Accounts.psd1'; ModuleVersion = '0.1.2' },
    @{ ModuleName = '../Network/Network.psd1'; ModuleVersion = '0.1.1' },
    @{ ModuleName = '../KeyVault/KeyVault.psd1'; ModuleVersion = '0.1.0' },
    @{ ModuleName = '../Compute/Compute.psd1'; ModuleVersion = '0.1.3' },
    @{ ModuleName = '../Resources/Resources.psd1'; ModuleVersion = '0.1.3' },
    @{ ModuleName = '../ManagedServiceIdentity/ManagedServiceIdentity.psd1'; ModuleVersion = '0.1.1' },
    @{ ModuleName = '../Config/Config.psd1'; ModuleVersion = '0.1.3' },
    @{ ModuleName = '../MsGraph/MsGraph.psd1'; ModuleVersion = '0.1.3' },
    @{ ModuleName = '../Storage/Storage.psd1'; ModuleVersion = '0.1.0' }
)
FunctionsToExport = @()
CmdletsToExport = @()
VariablesToExport = '*'
AliasesToExport = @()
PrivateData = @{
    PSData = @{
        ReleaseNotes = @'
0.1.4 - 2026-09-30 05:15 UTC
Az.Network 0.1.0
* Added 'New-AzPublicIpAddress', 'Get-AzPublicIpAddress' and 'Remove-AzPublicIpAddress'
    - 'Get-AzPublicIpAddress' serves both the single-address 'Get' set and the 'List' set; omitting '-ResourceGroupName' lists every public IP address in the subscription.

'@
    }
}

}
