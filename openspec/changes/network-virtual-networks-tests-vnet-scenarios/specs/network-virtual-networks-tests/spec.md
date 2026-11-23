# Spec Delta

## Purpose

Record/playback scenario tests for the Network extension's virtual network commands: the CLI `network vnet create/show/list/delete` commands and the PowerShell `New-AzVirtualNetwork`, `Get-AzVirtualNetwork_Get`, `Get-AzVirtualNetwork_List`, and `Remove-AzVirtualNetwork` cmdlets. Each test replays its recording on both surfaces and verifies the behavior stated in the `network-virtual-networks` spec.

## ADDED Requirements

### Requirement: List scope by resource group presence
The scenario tests SHALL verify the behavior requirement "List scope by resource group presence".

#### Scenario: T1 Virtual network lifecycle with DNS server and subnet
- **WHEN** (CLI) step 2 runs `network vnet create --resource-group <rg> --name <vnet> --location <location> --address-prefixes 10.0.0.0/16 --dns-servers 8.8.8.8 --subnet-names <subnet> --subnet-address-prefixes 10.0.1.0/24`
- **THEN** output is a VirtualNetwork with Name `<vnet>`
- **AND** ProvisioningState is `Succeeded`
- **AND** AddressSpaceAddressPrefixes is exactly [`10.0.0.0/16`]
- **AND** DhcpOptionsDnsServers is exactly [`8.8.8.8`]
- **AND** Subnets has 1 item with Name `<subnet>` and AddressPrefix `10.0.1.0/24`
- **AND** request body properties.subnets[0] has name `<subnet>` and addressPrefix `10.0.1.0/24`
- **WHEN** (CLI) step 3 runs `network vnet show --resource-group <rg> --name <vnet>`
- **THEN** Name is `<vnet>` and Type is `Microsoft.Network/virtualNetworks`
- **AND** AddressSpaceAddressPrefixes[0] is `10.0.0.0/16`
- **AND** DhcpOptionsDnsServers[0] is `8.8.8.8`
- **AND** Subnets has 1 item with Name `<subnet>` and AddressPrefix `10.0.1.0/24`
- **WHEN** (CLI) step 4 runs `network vnet list --resource-group <rg>`
- **THEN** output has exactly 1 VirtualNetwork, with Name `<vnet>`
- **AND** every item has Type `Microsoft.Network/virtualNetworks`
- **AND** request URI path contains `/resourceGroups/<rg>/providers/Microsoft.Network/virtualNetworks`
- **WHEN** (CLI) step 5 runs `network vnet list`
- **THEN** output contains a VirtualNetwork with Name `<vnet>`
- **AND** every item has Type `Microsoft.Network/virtualNetworks`
- **AND** request URI path is `/subscriptions/<subscription>/providers/Microsoft.Network/virtualNetworks` (no resourceGroups segment)
- **WHEN** (CLI) step 6 runs `network vnet delete --resource-group <rg> --name <vnet>`
- **THEN** completes without error
- **WHEN** (CLI) step 7 runs `network vnet list --resource-group <rg>`
- **THEN** output is empty
- **WHEN** (PowerShell) step 2 runs `New-AzVirtualNetwork -ResourceGroupName <rg> -Name <vnet> -Location <location> -AddressPrefix 10.0.0.0/16 -DnsServer 8.8.8.8 -SubnetNames @('<subnet>') -SubnetAddressPrefixes @(,@('10.0.1.0/24'))`
- **THEN** output is a VirtualNetwork with Name `<vnet>`
- **AND** ProvisioningState is `Succeeded`
- **AND** AddressSpaceAddressPrefixes is exactly [`10.0.0.0/16`]
- **AND** DhcpOptionsDnsServers is exactly [`8.8.8.8`]
- **AND** Subnets has 1 item with Name `<subnet>` and AddressPrefix `10.0.1.0/24`
- **WHEN** (PowerShell) step 3 runs `Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name <vnet>`
- **THEN** Name is `<vnet>` and Type is `Microsoft.Network/virtualNetworks`
- **AND** AddressSpaceAddressPrefixes[0] is `10.0.0.0/16`
- **AND** DhcpOptionsDnsServers[0] is `8.8.8.8`
- **AND** Subnets has 1 item with Name `<subnet>` and AddressPrefix `10.0.1.0/24`
- **WHEN** (PowerShell) step 4 runs `Get-AzVirtualNetwork_List -ResourceGroupName <rg>`
- **THEN** output has exactly 1 VirtualNetwork, with Name `<vnet>`
- **AND** every item has Type `Microsoft.Network/virtualNetworks`
- **WHEN** (PowerShell) step 5 runs `Get-AzVirtualNetwork_List`
- **THEN** output contains a VirtualNetwork with Name `<vnet>`
- **AND** every item has Type `Microsoft.Network/virtualNetworks`
- **WHEN** (PowerShell) step 6 runs `Remove-AzVirtualNetwork -ResourceGroupName <rg> -Name <vnet>`
- **THEN** completes without error
- **WHEN** (PowerShell) step 7 runs `Get-AzVirtualNetwork_List -ResourceGroupName <rg>`
- **THEN** output is empty

### Requirement: Default virtual network address prefix
The scenario tests SHALL verify the behavior requirement "Default virtual network address prefix".

#### Scenario: T2 Create defaults the address space to 10.0.0.0/16
- **WHEN** (CLI) step 2 runs `network vnet create --resource-group <rg> --name <vnet> --location <location> --subnet-names <subnet> --subnet-address-prefixes 10.0.0.0/24`
- **THEN** request body properties.addressSpace.addressPrefixes is exactly [`10.0.0.0/16`]
- **AND** ProvisioningState is `Succeeded`
- **AND** AddressSpaceAddressPrefixes is exactly [`10.0.0.0/16`]
- **AND** Subnets[0].Name is `<subnet>` and Subnets[0].PrivateEndpointNetworkPolicies is `Disabled`
- **WHEN** (CLI) step 3 runs `network vnet show --resource-group <rg> --name <vnet>`
- **THEN** AddressSpaceAddressPrefixes is exactly [`10.0.0.0/16`]
- **WHEN** (PowerShell) step 2 runs `New-AzVirtualNetwork -ResourceGroupName <rg> -Name <vnet> -Location <location> -SubnetNames @('<subnet>') -SubnetAddressPrefixes @(,@('10.0.0.0/24'))`
- **THEN** request body properties.addressSpace.addressPrefixes is exactly [`10.0.0.0/16`]
- **AND** ProvisioningState is `Succeeded`
- **AND** AddressSpaceAddressPrefixes is exactly [`10.0.0.0/16`]
- **AND** Subnets[0].Name is `<subnet>` and Subnets[0].PrivateEndpointNetworkPolicies is `Disabled`
- **WHEN** (PowerShell) step 3 runs `Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name <vnet>`
- **THEN** AddressSpaceAddressPrefixes is exactly [`10.0.0.0/16`]

### Requirement: Edge zone maps to extended location
The scenario tests SHALL verify the behavior requirement "Edge zone maps to extended location".

#### Scenario: T8 Create in an edge zone
- **WHEN** (CLI) step 2 runs `network vnet create --resource-group <rg> --name <vnet> --location <location> --address-prefixes 10.0.0.0/16 --dns-servers 8.8.8.8 --subnet-names <subnet> --subnet-address-prefixes 10.0.1.0/24 --extended-location <edge-zone>`
- **THEN** request body extendedLocation has name `<edge-zone>` and type `EdgeZone`
- **WHEN** (CLI) step 3 runs `network vnet show --resource-group <rg> --name <vnet>`
- **THEN** ExtendedLocationName is `<edge-zone>`
- **AND** ExtendedLocationType is `EdgeZone`
- **WHEN** (PowerShell) step 2 runs `New-AzVirtualNetwork -ResourceGroupName <rg> -Name <vnet> -Location <location> -AddressPrefix 10.0.0.0/16 -DnsServer 8.8.8.8 -SubnetNames @('<subnet>') -SubnetAddressPrefixes @(,@('10.0.1.0/24')) -EdgeZone <edge-zone>`
- **THEN** request body extendedLocation has name `<edge-zone>` and type `EdgeZone`
- **WHEN** (PowerShell) step 3 runs `Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name <vnet>`
- **THEN** ExtendedLocationName is `<edge-zone>`
- **AND** ExtendedLocationType is `EdgeZone`

### Requirement: Virtual network create
The scenario tests SHALL verify the commands `network vnet create` and `New-AzVirtualNetwork`, whose create properties no behavior requirement covers.

#### Scenario: T3 Create with flow timeout
- **WHEN** (CLI) step 2 runs `network vnet create --resource-group <rg> --name <vnet> --location <location> --flowtimeout 10`
- **THEN** FlowTimeoutInMinutes is 10
- **WHEN** (CLI) step 3 runs `network vnet show --resource-group <rg> --name <vnet>`
- **THEN** FlowTimeoutInMinutes is 10
- **WHEN** (PowerShell) step 2 runs `New-AzVirtualNetwork -ResourceGroupName <rg> -Name <vnet> -Location <location> -FlowTimeout 10`
- **THEN** FlowTimeoutInMinutes is 10
- **WHEN** (PowerShell) step 3 runs `Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name <vnet>`
- **THEN** FlowTimeoutInMinutes is 10

#### Scenario: T4 Create with BGP community
- **WHEN** (CLI) step 2 runs `network vnet create --resource-group <rg> --name <vnet> --location <location> --bgp-community 12076:20000`
- **THEN** BgpCommunitiesVirtualNetworkCommunity is `12076:20000`
- **WHEN** (CLI) step 3 runs `network vnet show --resource-group <rg> --name <vnet>`
- **THEN** BgpCommunitiesVirtualNetworkCommunity is `12076:20000`
- **WHEN** (PowerShell) step 2 runs `New-AzVirtualNetwork -ResourceGroupName <rg> -Name <vnet> -Location <location> -BgpCommunity 12076:20000`
- **THEN** BgpCommunitiesVirtualNetworkCommunity is `12076:20000`
- **WHEN** (PowerShell) step 3 runs `Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name <vnet>`
- **THEN** BgpCommunitiesVirtualNetworkCommunity is `12076:20000`

#### Scenario: T5 Create with encryption settings
- **WHEN** (CLI) step 5 runs `network vnet show --resource-group <rg> --name <vnet1>`
- **THEN** EncryptionEnabled is true
- **AND** EncryptionEnforcement is `AllowUnencrypted`
- **WHEN** (CLI) step 6 runs `network vnet show --resource-group <rg> --name <vnet2>`
- **THEN** EncryptionEnabled is true
- **AND** EncryptionEnforcement is `DropUnencrypted`
- **WHEN** (CLI) step 7 runs `network vnet show --resource-group <rg> --name <vnet3>`
- **THEN** EncryptionEnabled is false
- **WHEN** (PowerShell) step 5 runs `Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name <vnet1>`
- **THEN** EncryptionEnabled is true
- **AND** EncryptionEnforcement is `AllowUnencrypted`
- **WHEN** (PowerShell) step 6 runs `Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name <vnet2>`
- **THEN** EncryptionEnabled is true
- **AND** EncryptionEnforcement is `DropUnencrypted`
- **WHEN** (PowerShell) step 7 runs `Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name <vnet3>`
- **THEN** EncryptionEnabled is false

#### Scenario: T6 Create with private endpoint VNet policies
- **WHEN** (CLI) step 3 runs `network vnet show --resource-group <rg> --name <vnet1>`
- **THEN** PrivateEndpointVNetPolicies is `Disabled`
- **WHEN** (CLI) step 4 runs `network vnet create --resource-group <rg> --name <vnet2> --location <location> --address-prefixes 10.2.0.0/16 --pe-vnet-policies Disabled`
- **THEN** request body properties.privateEndpointVNetPolicies is `Disabled`
- **WHEN** (CLI) step 5 runs `network vnet show --resource-group <rg> --name <vnet2>`
- **THEN** PrivateEndpointVNetPolicies is `Disabled`
- **WHEN** (CLI) step 7 runs `network vnet show --resource-group <rg> --name <vnet3>`
- **THEN** PrivateEndpointVNetPolicies is `Basic`
- **WHEN** (PowerShell) step 3 runs `Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name <vnet1>`
- **THEN** PrivateEndpointVNetPolicies is `Disabled`
- **WHEN** (PowerShell) step 4 runs `New-AzVirtualNetwork -ResourceGroupName <rg> -Name <vnet2> -Location <location> -AddressPrefix 10.2.0.0/16 -PrivateEndpointVNetPoliciesValue Disabled`
- **THEN** request body properties.privateEndpointVNetPolicies is `Disabled`
- **WHEN** (PowerShell) step 5 runs `Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name <vnet2>`
- **THEN** PrivateEndpointVNetPolicies is `Disabled`
- **WHEN** (PowerShell) step 7 runs `Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name <vnet3>`
- **THEN** PrivateEndpointVNetPolicies is `Basic`

#### Scenario: T7 Create with summarized gateway prefixes
- **WHEN** (CLI) step 2 runs `network vnet create --resource-group <rg> --name <vnet> --location <location> --address-prefixes 10.0.0.0/16 --sgp 10.0.0.0/16`
- **THEN** ProvisioningState is `Succeeded`
- **AND** SummarizedGatewayPrefixesAddressPrefixes[0] is `10.0.0.0/16`
- **WHEN** (CLI) step 3 runs `network vnet show --resource-group <rg> --name <vnet>`
- **THEN** SummarizedGatewayPrefixesAddressPrefixes[0] is `10.0.0.0/16`
- **WHEN** (PowerShell) step 2 runs `New-AzVirtualNetwork -ResourceGroupName <rg> -Name <vnet> -Location <location> -AddressPrefix 10.0.0.0/16 -SummarizedGatewayPrefix 10.0.0.0/16`
- **THEN** ProvisioningState is `Succeeded`
- **AND** SummarizedGatewayPrefixesAddressPrefixes[0] is `10.0.0.0/16`
- **WHEN** (PowerShell) step 3 runs `Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name <vnet>`
- **THEN** SummarizedGatewayPrefixesAddressPrefixes[0] is `10.0.0.0/16`
