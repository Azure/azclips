# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Build and import src/PS/Network/Network.psd1 before running.
$subnet = New-AzVirtualNetworkSubnetConfig `
    -Name 'web' `
    -AddressPrefix '10.20.1.0/24' `
    -ServiceEndpoint 'Microsoft.Storage' `
    -DelegatedService 'Microsoft.Web/serverFarms' `
    -DefaultOutboundAccess $false

if ($subnet.Name -ne 'web' -or $subnet.Properties.AddressPrefix -ne '10.20.1.0/24')
{
    throw 'New-AzVirtualNetworkSubnetConfig returned an unexpected subnet.'
}

$subnet
