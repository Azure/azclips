@{
RootModule = ''
ModuleVersion = '0.1.7'
CompatiblePSEditions = @('Core')
GUID = '7c7b69d5-7b18-4e6c-9c1c-22d0a14e86c5'
Author = 'Microsoft'
CompanyName = 'Microsoft'
Copyright = '(c) Microsoft. All rights reserved.'
Description = 'Rollup module for Azure PowerShell cmdlets'
PowerShellVersion = '7.6'
RequiredModules = @(
    @{ ModuleName = '../Accounts/Accounts.psd1'; ModuleVersion = '0.1.4' },
    @{ ModuleName = '../Network/Network.psd1'; ModuleVersion = '0.1.3' },
    @{ ModuleName = '../KeyVault/KeyVault.psd1'; ModuleVersion = '0.1.2' },
    @{ ModuleName = '../Compute/Compute.psd1'; ModuleVersion = '0.1.5' },
    @{ ModuleName = '../Resources/Resources.psd1'; ModuleVersion = '0.1.5' },
    @{ ModuleName = '../ManagedServiceIdentity/ManagedServiceIdentity.psd1'; ModuleVersion = '0.1.3' },
    @{ ModuleName = '../Config/Config.psd1'; ModuleVersion = '0.1.5' },
    @{ ModuleName = '../MsGraph/MsGraph.psd1'; ModuleVersion = '0.1.5' },
    @{ ModuleName = '../Storage/Storage.psd1'; ModuleVersion = '0.1.1' }
)
FunctionsToExport = @()
CmdletsToExport = @()
VariablesToExport = '*'
AliasesToExport = @()
PrivateData = @{
    PSData = @{
        ReleaseNotes = @'
0.1.7 - 2026-11-04 19:14 UTC
Az.Accounts 0.1.4
* Added service principal client-secret sign-in with '-ClientSecret <SecureString>' and backward-compatible '-ServicePrincipal -Credential <PSCredential>' syntax.

Az.Network 0.1.3
* Removed 'New-AzVirtualNetworkSubnetConfig'
    - Pass hashtables to 'New-AzVirtualNetwork -Subnet' instead.
* Fixed 'Get-AzPublicIpAddress' output exposing a blank 'Properties' value instead of the assigned IP address.
    - Resource properties are available at the top level.
    - The attached IP configuration is flattened into 'IpConfigurationId', 'PrivateIPAddress' and 'SubnetId' (the last two with '-ExpandResource'); the nested 'IpConfiguration' object is no longer exposed.
    - 'ServicePublicIPAddress' and 'LinkedPublicIPAddress' are no longer exposed.
* Updated 'New-AzVirtualNetwork', 'Get-AzVirtualNetwork' and 'Remove-AzVirtualNetwork'
    - '-Name' accepts the 'ResourceName' alias.
    - '-Subnet' hashtables accept array values for multi-valued keys such as 'AddressPrefixes'.
    - Subnets no longer send private endpoint / private link service network policies unless requested.
* Virtual network, subnet, network security group, network interface and public IP address outputs now use the TypeSpec-generated models
    - Resource properties are exposed directly on the output object (for example '$vnet.Subnets', '$subnet.AddressPrefixes') instead of under '.Properties'.
    - The public IP address output type is now 'PublicIPAddress'.

'@
    }
}

}
