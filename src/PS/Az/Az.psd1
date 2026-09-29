@{
RootModule = './Az.psm1'
ModuleVersion = '0.1.3'
CompatiblePSEditions = @('Core')
GUID = '7c7b69d5-7b18-4e6c-9c1c-22d0a14e86c5'
Author = 'Microsoft'
CompanyName = 'Microsoft'
Copyright = '(c) Microsoft. All rights reserved.'
Description = 'Rollup module for Azure PowerShell cmdlets'
PowerShellVersion = '7.6'
RequiredModules = @(
    @{ ModuleName = '../Microsoft.Azure.Azclips.Extensions.Accounts/Accounts.psd1'; ModuleVersion = '0.1.0' },
    @{ ModuleName = '../Microsoft.Azure.Azclips.Extensions.Network/Network.psd1'; ModuleVersion = '0.1.0' },
    @{ ModuleName = '../Microsoft.Azure.Azclips.Extensions.Compute/Compute.psd1'; ModuleVersion = '0.1.1' },
    @{ ModuleName = '../Microsoft.Azure.Azclips.Extensions.Resources/Resources.psd1'; ModuleVersion = '0.1.1' },
    @{ ModuleName = '../Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity/ManagedServiceIdentity.psd1'; ModuleVersion = '0.1.0' },
    @{ ModuleName = '../Microsoft.Azure.Azclips.Extensions.Config/Config.psd1'; ModuleVersion = '0.1.1' },
    @{ ModuleName = '../Microsoft.Azure.Azclips.Extensions.MsGraph/MsGraph.psd1'; ModuleVersion = '0.1.1' }
)
FunctionsToExport = @()
CmdletsToExport = @()
VariablesToExport = '*'
AliasesToExport = @()
PrivateData = @{
    PSData = @{
        ReleaseNotes = @'
0.1.3 - 2026-09-14 23:15 UTC
Az.MsGraph 0.1.1
* Refactored Microsoft Graph command customization and extension integration.

'@
    }
}

}
