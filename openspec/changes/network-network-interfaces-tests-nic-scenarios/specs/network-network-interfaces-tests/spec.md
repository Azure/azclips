# Spec Delta

## Purpose

Record/playback scenario tests for the Network extension's network interface commands on the CLI surface (`network nic create`, `network nic show`, `network nic list`, `network nic delete`) and the PowerShell surface (`New-AzNetworkInterface`, `Get-AzNetworkInterface_Get`, `Get-AzNetworkInterface_List`, `Remove-AzNetworkInterface`). Each test replays its recording and verifies the behavior stated in the `network-network-interfaces` spec.

## ADDED Requirements

### Requirement: List scope by resource group presence
The scenario tests SHALL verify the behavior requirement "List scope by resource group presence".

#### Scenario: T1 Create with defaults, read, list and delete a network interface
- **WHEN** (CLI) step 4 runs `network nic create -g <rg> -n <nic> -l <location> --subnet <subnet> --vnet-name <vnet>`
- **THEN** Name == <nic>
- **AND** ProvisioningState == "Succeeded"
- **AND** IpConfigurations has 1 item and IpConfigurations[0].Name == "ipconfig1"
- **AND** IpConfigurations[0].PrivateIPAllocationMethod == "Dynamic"
- **AND** IpConfigurations[0].SubnetId ends with "/virtualNetworks/<vnet>/subnets/<subnet>" (case-insensitive)
- **AND** request body properties.ipConfigurations[0].properties.subnet.id ends with "/resourceGroups/<rg>/providers/Microsoft.Network/virtualNetworks/<vnet>/subnets/<subnet>"
- **AND** IpConfigurations[0].PublicIPAddress is null
- **WHEN** (CLI) step 5 runs `network nic show -g <rg> -n <nic>`
- **THEN** Name == <nic>
- **AND** Location == <location>
- **AND** Type == "Microsoft.Network/networkInterfaces"
- **AND** IpConfigurations[0].PrivateIPAllocationMethod == "Dynamic"
- **WHEN** (CLI) step 6 runs `network nic list -g <rg>`
- **THEN** request URL path contains "/resourceGroups/<rg>/providers/Microsoft.Network/networkInterfaces"
- **AND** every item has Type == "Microsoft.Network/networkInterfaces"
- **AND** exactly one item has Name == <nic>
- **WHEN** (CLI) step 7 runs `network nic list`
- **THEN** request URL path is "/subscriptions/<subscription>/providers/Microsoft.Network/networkInterfaces" (no resourceGroups segment)
- **AND** every item Id contains "/networkInterfaces/"
- **AND** one item has Name == <nic>
- **WHEN** (CLI) step 8 runs `network nic delete -g <rg> -n <nic>`
- **THEN** request is DELETE on ".../resourceGroups/<rg>/providers/Microsoft.Network/networkInterfaces/<nic>" and completes without error
- **WHEN** (CLI) step 9 runs `network nic list -g <rg>`
- **THEN** result is empty
- **WHEN** (CLI) step 10 runs `network nic show -g <rg> -n <nic>`
- **THEN** fails with a not-found error (ResourceNotFound)
- **WHEN** (PowerShell) step 3 runs `New-AzNetworkInterface -ResourceGroupName <rg> -Name <nic> -Location <location> -SubnetId <step 2: Subnets[0].Id>`
- **THEN** Name == <nic>
- **AND** ProvisioningState == "Succeeded"
- **AND** IpConfigurations has 1 item and IpConfigurations[0].Name == "ipconfig1"
- **AND** IpConfigurations[0].PrivateIPAllocationMethod == "Dynamic"
- **AND** IpConfigurations[0].SubnetId equals <step 2: Subnets[0].Id> (case-insensitive)
- **AND** IpConfigurations[0].PublicIPAddress is null
- **WHEN** (PowerShell) step 4 runs `Get-AzNetworkInterface_Get -ResourceGroupName <rg> -Name <nic>`
- **THEN** Name == <nic>
- **AND** Location == <location>
- **AND** Type == "Microsoft.Network/networkInterfaces"
- **AND** IpConfigurations[0].PrivateIPAllocationMethod == "Dynamic"
- **WHEN** (PowerShell) step 5 runs `Get-AzNetworkInterface_List -ResourceGroupName <rg>`
- **THEN** request URL path contains "/resourceGroups/<rg>/providers/Microsoft.Network/networkInterfaces"
- **AND** exactly one item has Name == <nic> and ProvisioningState == "Succeeded"
- **WHEN** (PowerShell) step 6 runs `Get-AzNetworkInterface_List`
- **THEN** request URL path is "/subscriptions/<subscription>/providers/Microsoft.Network/networkInterfaces" (no resourceGroups segment)
- **AND** one item has Name == <nic>
- **WHEN** (PowerShell) step 7 runs `Remove-AzNetworkInterface -ResourceGroupName <rg> -Name <nic>`
- **THEN** request is DELETE on ".../resourceGroups/<rg>/providers/Microsoft.Network/networkInterfaces/<nic>" and completes without error
- **WHEN** (PowerShell) step 8 runs `Get-AzNetworkInterface_List -ResourceGroupName <rg>`
- **THEN** result is empty
- **WHEN** (PowerShell) step 9 runs `Get-AzNetworkInterface_Get -ResourceGroupName <rg> -Name <nic>`
- **THEN** fails with a not-found error (ResourceNotFound)

### Requirement: Single IP configuration from flat create inputs
The scenario tests SHALL verify the behavior requirement "Single IP configuration from flat create inputs".

#### Scenario: T2 Create with static private IP, public IP by name, and optional settings; show with expand
- **WHEN** (CLI) step 5 runs `network nic create -g <rg> -n <nic> -l <location> --subnet <step 3: id> --private-ip-address 10.0.1.5 --public-ip-address <pip> --ip-forwarding true --accelerated-networking true --internal-dns-name test --dns-servers 100.1.2.3 --tags foo=doo`
- **THEN** ProvisioningState == "Succeeded"
- **AND** IpConfigurations[0].PrivateIPAllocationMethod == "Static"
- **AND** IpConfigurations[0].PrivateIPAddress == "10.0.1.5"
- **AND** IpConfigurations[0].PublicIPAddress.Id ends with "/publicIPAddresses/<pip>" (case-insensitive)
- **AND** request body properties.ipConfigurations[0].properties.publicIPAddress.id ends with "/resourceGroups/<rg>/providers/Microsoft.Network/publicIPAddresses/<pip>"
- **AND** EnableIPForwarding == true
- **AND** EnableAcceleratedNetworking == true
- **AND** DnsSettingsInternalDnsNameLabel == "test"
- **AND** DnsSettingsDnsServers == ["100.1.2.3"]
- **AND** Tags["foo"] == "doo"
- **WHEN** (CLI) step 6 runs `network nic show -g <rg> -n <nic>`
- **THEN** IpConfigurations[0].PrivateIPAllocationMethod == "Static"
- **AND** IpConfigurations[0].PrivateIPAddress == "10.0.1.5"
- **AND** DnsSettingsInternalDnsNameLabel == "test"
- **AND** IpConfigurations[0].PublicIPAddress.Name is null
- **WHEN** (CLI) step 7 runs `network nic show -g <rg> -n <nic> --expand ipConfigurations/publicIPAddress`
- **THEN** request URL query has $expand == "ipConfigurations/publicIPAddress"
- **AND** IpConfigurations[0].PublicIPAddress.Name == <pip>
- **WHEN** (PowerShell) step 4 runs `New-AzNetworkInterface -ResourceGroupName <rg> -Name <nic> -Location <location> -SubnetId <step 2: Subnets[0].Id> -PublicIpAddressId <step 3: Id> -PrivateIpAddress 10.0.1.5 -IpConfigurationName createipconf -EnableIPForwarding $true -EnableAcceleratedNetworking $true -InternalDnsNameLabel test -DnsServer @("100.1.2.3") -Tag @{ foo = "doo" }`
- **THEN** ProvisioningState == "Succeeded"
- **AND** IpConfigurations[0].Name == "createipconf"
- **AND** IpConfigurations[0].PrivateIPAllocationMethod == "Static"
- **AND** IpConfigurations[0].PrivateIPAddress == "10.0.1.5"
- **AND** IpConfigurations[0].PublicIPAddress.Id equals <step 3: Id> (case-insensitive)
- **AND** EnableIPForwarding == true
- **AND** EnableAcceleratedNetworking == true
- **AND** DnsSettingsInternalDnsNameLabel == "test"
- **AND** DnsSettingsDnsServers == ["100.1.2.3"]
- **AND** Tags["foo"] == "doo"
- **WHEN** (PowerShell) step 5 runs `Get-AzNetworkInterface_Get -ResourceGroupName <rg> -Name <nic>`
- **THEN** IpConfigurations[0].Name == "createipconf"
- **AND** IpConfigurations[0].PrivateIPAddress == "10.0.1.5"
- **AND** DnsSettingsInternalDnsNameLabel == "test"
- **AND** IpConfigurations[0].PublicIPAddress.Name is null
- **WHEN** (PowerShell) step 6 runs `Get-AzNetworkInterface_Get -ResourceGroupName <rg> -Name <nic> -ExpandResource "IpConfigurations/PublicIPAddress"`
- **THEN** request URL query has $expand == "IpConfigurations/PublicIPAddress"
- **AND** IpConfigurations[0].PublicIPAddress.Name == <pip>

### Requirement: CLI name-or-ID expansion for create references
The scenario tests SHALL verify the behavior requirement "CLI name-or-ID expansion for create references".

#### Scenario: T3 Create with network security group and public IP references, then re-create without IP forwarding
- **WHEN** (CLI) step 6 runs `network nic create -g <rg> -n <nic> -l <location> --subnet <subnet> --vnet-name <vnet> --network-security-group <nsg> --ip-forwarding true`
- **THEN** NetworkSecurityGroupId ends with "/networkSecurityGroups/<nsg>" (case-insensitive)
- **AND** request body properties.networkSecurityGroup.id ends with "/resourceGroups/<rg>/providers/Microsoft.Network/networkSecurityGroups/<nsg>"
- **AND** EnableIPForwarding == true
- **AND** IpConfigurations[0].PrivateIPAllocationMethod == "Dynamic"
- **WHEN** (CLI) step 7 runs `network nic create -g <rg> -n <nic> -l <location> --subnet <step 3: id> --network-security-group <step 4: id> --public-ip-address <step 5: id>`
- **THEN** ProvisioningState == "Succeeded"
- **AND** request body properties.networkSecurityGroup.id equals <step 4: id>
- **AND** request body properties.ipConfigurations[0].properties.publicIPAddress.id equals <step 5: id>
- **AND** request body has no properties.enableIPForwarding
- **AND** EnableIPForwarding == false
- **AND** NetworkSecurityGroupId ends with "/networkSecurityGroups/<nsg>" (case-insensitive)
- **AND** IpConfigurations[0].PublicIPAddress.Id ends with "/publicIPAddresses/<pip>" (case-insensitive)
- **AND** IpConfigurations[0].PrivateIPAllocationMethod == "Dynamic"
- **WHEN** (PowerShell) step 5 runs `New-AzNetworkInterface -ResourceGroupName <rg> -Name <nic> -Location <location> -SubnetId <step 2: Subnets[0].Id> -NetworkSecurityGroupId <step 3: Id> -EnableIPForwarding $true`
- **THEN** NetworkSecurityGroupId equals <step 3: Id> (case-insensitive)
- **AND** EnableIPForwarding == true
- **AND** IpConfigurations[0].PrivateIPAllocationMethod == "Dynamic"
- **WHEN** (PowerShell) step 6 runs `New-AzNetworkInterface -ResourceGroupName <rg> -Name <nic> -Location <location> -SubnetId <step 2: Subnets[0].Id> -NetworkSecurityGroupId <step 3: Id> -PublicIpAddressId <step 4: Id>`
- **THEN** ProvisioningState == "Succeeded"
- **AND** request body has no properties.enableIPForwarding
- **AND** EnableIPForwarding == false
- **AND** IpConfigurations[0].PublicIPAddress.Id equals <step 4: Id> (case-insensitive)
- **AND** NetworkSecurityGroupId equals <step 3: Id> (case-insensitive)

### Requirement: Edge zone as extended location
The scenario tests SHALL verify the behavior requirement "Edge zone as extended location".

#### Scenario: T4 Create a network interface in an edge zone
- **WHEN** (CLI) step 4 runs `network nic create -g <rg> -n <nic> -l <location> --subnet <step 3: id> --edge-zone <edge-zone>`
- **THEN** request body extendedLocation.name == <edge-zone> and extendedLocation.type == "EdgeZone"
- **AND** ExtendedLocationName == <edge-zone>
- **AND** ExtendedLocationType == "EdgeZone"
- **WHEN** (CLI) step 5 runs `network nic show -g <rg> -n <nic>`
- **THEN** ExtendedLocationName == <edge-zone>
- **AND** ExtendedLocationType == "EdgeZone"
- **WHEN** (PowerShell) step 3 runs `New-AzNetworkInterface -ResourceGroupName <rg> -Name <nic> -Location <location> -EdgeZone <edge-zone> -SubnetId <step 2: Subnets[0].Id>`
- **THEN** request body extendedLocation.name == <edge-zone> and extendedLocation.type == "EdgeZone"
- **AND** ExtendedLocationName == <edge-zone>
- **AND** ExtendedLocationType == "EdgeZone"
- **WHEN** (PowerShell) step 4 runs `Get-AzNetworkInterface_Get -ResourceGroupName <rg> -Name <nic>`
- **THEN** ExtendedLocationName == <edge-zone>
- **AND** ExtendedLocationType == "EdgeZone"
