# network-network-interfaces-tests Specification

## Purpose
Record/playback scenario tests for the Azclips Network network interface commands: the CLI `network nic ...` commands and the PowerShell `*-AzNetworkInterface` cmdlets. Each test replays its committed recording on both surfaces and verifies the behavior stated in the `network-network-interfaces` spec.

## Requirements
### Requirement: Create location defaults to the resource group location
The scenario tests SHALL verify the behavior requirement "Create location defaults to the resource group location".

#### Scenario: T1 Network interface lifecycle on both surfaces
- **WHEN** (CLI) step 6 runs `network nic create --name <nic> --resource-group <rg> --subnet <subnet> --vnet-name <vnet>`
- **THEN** a GET for resource group <rg> is sent before the network interface PUT
- **AND** Location equals <location>
- **AND** ProvisioningState equals Succeeded
- **AND** IpConfigurations has exactly one entry
- **AND** IpConfigurations[0].Name equals ipconfig1
- **AND** IpConfigurations[0].Primary is true
- **AND** IpConfigurations[0].PrivateIPAllocationMethod equals Dynamic
- **AND** IpConfigurations[0].PrivateIPAddressVersion equals IPv4
- **AND** IpConfigurations[0].SubnetId equals <step 2: Subnets[0].Id>
- **AND** IpConfigurations[0].PublicIPAddress is null
- **AND** NetworkSecurityGroupId is null
- **WHEN** (CLI) step 7 runs `network nic create --name <nic> --resource-group <rg> --subnet <step 2: Subnets[0].Id> --ip-forwarding true --private-ip-address 10.0.0.15 --public-ip-address <pip> --internal-dns-name test --dns-servers 100.1.2.3 --accelerated-networking true --tags foo=doo`
- **THEN** IpConfigurations[0].PrivateIPAllocationMethod equals Static
- **AND** IpConfigurations[0].PrivateIPAddress equals 10.0.0.15
- **AND** IpConfigurations[0].SubnetId equals <step 2: Subnets[0].Id>
- **AND** IpConfigurations[0].PublicIPAddress.Id equals <step 3: Id>
- **AND** EnableIPForwarding is true
- **AND** EnableAcceleratedNetworking is true
- **AND** DnsSettingsInternalDnsNameLabel equals test
- **AND** DnsSettingsDnsServers equals [100.1.2.3]
- **AND** Tags contains foo=doo
- **WHEN** (CLI) step 8 runs `network nic create --name <nic> --resource-group <rg> --subnet <subnet> --vnet-name <vnet> --network-security-group <nsg>`
- **THEN** IpConfigurations[0].PrivateIPAllocationMethod equals Dynamic
- **AND** EnableIPForwarding is false
- **AND** NetworkSecurityGroupId equals <step 4: Id>
- **AND** IpConfigurations[0].PublicIPAddress is null
- **WHEN** (CLI) step 9 runs `network nic create --name <nic> --resource-group <rg> --subnet <subnet> --vnet-name <vnet> --network-security-group <step 5: Id> --public-ip-address <step 3: Id>`
- **THEN** NetworkSecurityGroupId equals <step 5: Id>
- **AND** IpConfigurations[0].PublicIPAddress.Id equals <step 3: Id>
- **WHEN** (CLI) step 10 runs `network nic create --name <nic2> --resource-group <rg> --location <location> --subnet <step 2: Subnets[0].Id> --ip-config-name createipconf --private-ip-address-version IPv4`
- **THEN** no resource group GET is sent before the network interface PUT
- **AND** Location equals <location>
- **AND** IpConfigurations has exactly one entry
- **AND** IpConfigurations[0].Name equals createipconf
- **AND** IpConfigurations[0].Primary is true
- **AND** IpConfigurations[0].PrivateIPAllocationMethod equals Dynamic
- **AND** IpConfigurations[0].PrivateIPAddressVersion equals IPv4
- **AND** EnableIPForwarding is false
- **AND** EnableAcceleratedNetworking is false
- **WHEN** (CLI) step 11 runs `network nic show --name <nic> --resource-group <rg>`
- **THEN** Name equals <nic>
- **AND** IpConfigurations[0].PublicIPAddress.Id equals <step 3: Id>
- **AND** IpConfigurations[0].PublicIPAddress.Name is null
- **WHEN** (CLI) step 12 runs `network nic show --name <nic> --resource-group <rg> --expand ipConfigurations/publicIPAddress`
- **THEN** the request carries $expand=ipConfigurations/publicIPAddress
- **AND** IpConfigurations[0].PublicIPAddress.Name equals <pip>
- **WHEN** (CLI) step 13 runs `network nic list --resource-group <rg>`
- **THEN** the request targets the networkInterfaces list of resource group <rg>
- **AND** the output list has exactly two NetworkInterface items, named <nic> and <nic2>, in any order
- **WHEN** (CLI) step 14 runs `network nic list`
- **THEN** the request targets the subscription-scope networkInterfaces list, with no resourceGroups segment
- **AND** the output list contains NetworkInterface items named <nic> and <nic2>
- **WHEN** (CLI) step 15 runs `network nic delete --name <nic> --resource-group <rg>`
- **THEN** no output
- **WHEN** (CLI) step 16 runs `network nic delete --name <nic2> --resource-group <rg>`
- **THEN** no output
- **WHEN** (CLI) step 17 runs `network nic show --name <nic> --resource-group <rg>`
- **THEN** fails with a service error, HTTP 404 (ResourceNotFound)
- **WHEN** (CLI) step 18 runs `network nic list --resource-group <rg>`
- **THEN** the output list is empty
- **WHEN** (PowerShell) step 6 runs `New-AzNetworkInterface -Name <nic> -ResourceGroupName <rg> -Subnet <subnet> -VirtualNetworkName <vnet>`
- **THEN** a GET for resource group <rg> is sent before the network interface PUT
- **AND** Location equals <location>
- **AND** ProvisioningState equals Succeeded
- **AND** IpConfigurations has exactly one entry
- **AND** IpConfigurations[0].Name equals ipconfig1
- **AND** IpConfigurations[0].Primary is true
- **AND** IpConfigurations[0].PrivateIPAllocationMethod equals Dynamic
- **AND** IpConfigurations[0].PrivateIPAddressVersion equals IPv4
- **AND** IpConfigurations[0].SubnetId equals <step 2: Subnets[0].Id>
- **AND** IpConfigurations[0].PublicIPAddress is null
- **AND** NetworkSecurityGroupId is null
- **WHEN** (PowerShell) step 7 runs `New-AzNetworkInterface -Name <nic> -ResourceGroupName <rg> -Subnet <step 2: Subnets[0].Id> -EnableIPForwarding $true -PrivateIpAddress 10.0.0.15 -PublicIpAddress <pip> -InternalDnsNameLabel test -DnsServers 100.1.2.3 -EnableAcceleratedNetworking $true -Tag @{ foo = 'doo' }`
- **THEN** IpConfigurations[0].PrivateIPAllocationMethod equals Static
- **AND** IpConfigurations[0].PrivateIPAddress equals 10.0.0.15
- **AND** IpConfigurations[0].SubnetId equals <step 2: Subnets[0].Id>
- **AND** IpConfigurations[0].PublicIPAddress.Id equals <step 3: Id>
- **AND** EnableIPForwarding is true
- **AND** EnableAcceleratedNetworking is true
- **AND** DnsSettingsInternalDnsNameLabel equals test
- **AND** DnsSettingsDnsServers equals [100.1.2.3]
- **AND** Tags contains foo=doo
- **WHEN** (PowerShell) step 8 runs `New-AzNetworkInterface -Name <nic> -ResourceGroupName <rg> -Subnet <subnet> -VirtualNetworkName <vnet> -NetworkSecurityGroup <nsg>`
- **THEN** IpConfigurations[0].PrivateIPAllocationMethod equals Dynamic
- **AND** EnableIPForwarding is false
- **AND** NetworkSecurityGroupId equals <step 4: Id>
- **AND** IpConfigurations[0].PublicIPAddress is null
- **WHEN** (PowerShell) step 9 runs `New-AzNetworkInterface -Name <nic> -ResourceGroupName <rg> -Subnet <subnet> -VirtualNetworkName <vnet> -NetworkSecurityGroup <step 5: Id> -PublicIpAddress <step 3: Id>`
- **THEN** NetworkSecurityGroupId equals <step 5: Id>
- **AND** IpConfigurations[0].PublicIPAddress.Id equals <step 3: Id>
- **WHEN** (PowerShell) step 10 runs `New-AzNetworkInterface -Name <nic2> -ResourceGroupName <rg> -Location <location> -Subnet <step 2: Subnets[0].Id> -IpConfigurationName createipconf -PrivateIpAddressVersion IPv4`
- **THEN** no resource group GET is sent before the network interface PUT
- **AND** Location equals <location>
- **AND** IpConfigurations has exactly one entry
- **AND** IpConfigurations[0].Name equals createipconf
- **AND** IpConfigurations[0].Primary is true
- **AND** IpConfigurations[0].PrivateIPAllocationMethod equals Dynamic
- **AND** IpConfigurations[0].PrivateIPAddressVersion equals IPv4
- **AND** EnableIPForwarding is false
- **AND** EnableAcceleratedNetworking is false
- **WHEN** (PowerShell) step 11 runs `Get-AzNetworkInterface_Get -Name <nic> -ResourceGroupName <rg>`
- **THEN** Name equals <nic>
- **AND** IpConfigurations[0].PublicIPAddress.Id equals <step 3: Id>
- **AND** IpConfigurations[0].PublicIPAddress.Name is null
- **WHEN** (PowerShell) step 12 runs `Get-AzNetworkInterface_Get -Name <nic> -ResourceGroupName <rg> -ExpandResource ipConfigurations/publicIPAddress`
- **THEN** the request carries $expand=ipConfigurations/publicIPAddress
- **AND** IpConfigurations[0].PublicIPAddress.Name equals <pip>
- **WHEN** (PowerShell) step 13 runs `Get-AzNetworkInterface_List -ResourceGroupName <rg>`
- **THEN** the request targets the networkInterfaces list of resource group <rg>
- **AND** the output list has exactly two NetworkInterface items, named <nic> and <nic2>, in any order
- **WHEN** (PowerShell) step 14 runs `Get-AzNetworkInterface_List`
- **THEN** the request targets the subscription-scope networkInterfaces list, with no resourceGroups segment
- **AND** the output list contains NetworkInterface items named <nic> and <nic2>
- **WHEN** (PowerShell) step 15 runs `Remove-AzNetworkInterface -Name <nic> -ResourceGroupName <rg>`
- **THEN** no output
- **WHEN** (PowerShell) step 16 runs `Remove-AzNetworkInterface -Name <nic2> -ResourceGroupName <rg>`
- **THEN** no output
- **WHEN** (PowerShell) step 17 runs `Get-AzNetworkInterface_Get -Name <nic> -ResourceGroupName <rg>`
- **THEN** fails with a service error, HTTP 404 (ResourceNotFound)
- **WHEN** (PowerShell) step 18 runs `Get-AzNetworkInterface_List -ResourceGroupName <rg>`
- **THEN** the output list is empty

### Requirement: Create maps the edge zone to the extended location
The scenario tests SHALL verify the behavior requirement "Create maps the edge zone to the extended location".

#### Scenario: T2 Network interface in an edge zone
- **WHEN** (CLI) step 2 runs `network vnet create --name <vnet> --resource-group <rg> --location <location> --subnet-name <subnet> --subnet-prefixes 10.0.0.0/24`, with no edge zone
- **AND** (CLI) step 3 runs `network nic create --name <nic> --resource-group <rg> --location <location> --edge-zone <edge-zone> --subnet <subnet> --vnet-name <vnet>`
- **THEN** fails with service error code UnsupportedEdgeZone, because the recording subscription has no edge-zone access
- **AND** the PUT body carries extendedLocation with name <edge-zone> and type EdgeZone
- **WHEN** (PowerShell) step 3 runs `New-AzNetworkInterface -Name <nic> -ResourceGroupName <rg> -Location <location> -EdgeZone <edge-zone> -Subnet <subnet> -VirtualNetworkName <vnet>`
- **THEN** fails with service error code UnsupportedEdgeZone
- **AND** the PUT body carries extendedLocation with name <edge-zone> and type EdgeZone

### Requirement: PowerShell remove confirms and supports PassThru
The scenario tests SHALL verify the behavior requirement "PowerShell remove confirms and supports PassThru".

#### Scenario: T3 PowerShell cmdlets bind aliases, route parameter sets and honor -Force -PassThru
- **WHEN** (PowerShell) step 5 runs `New-AzNetworkInterface` (real cmdlet) `-ResourceName <nic> -ResourceGroupName <rg> -Location <location> -SubnetId <step 2: Subnets[0].Id> -PublicIpAddressId <step 3: Id> -NetworkSecurityGroupId <step 4: Id> -DnsServer 100.1.2.3 -EnableAcceleratedNetworking $true -Tag @{ fastpathenabled = 'true' }`
- **THEN** no error records
- **AND** exactly one pipeline object, a NetworkInterface named <nic>
- **AND** IpConfigurations[0].SubnetId equals <step 2: Subnets[0].Id>
- **AND** IpConfigurations[0].PublicIPAddress.Id equals <step 3: Id>
- **AND** NetworkSecurityGroupId equals <step 4: Id>
- **AND** DnsSettingsDnsServers equals [100.1.2.3]
- **AND** EnableAcceleratedNetworking is true
- **AND** Tags contains fastpathenabled=true
- **WHEN** (PowerShell) step 6 runs `Get-AzNetworkInterface` (real cmdlet, List set) `-ResourceGroupName <rg>`
- **THEN** exactly one pipeline object, a NetworkInterface named <nic>
- **WHEN** (PowerShell) step 7 runs `Get-AzNetworkInterface` (real cmdlet, Get set) `-ResourceGroupName <rg> -ResourceName <nic>`
- **THEN** exactly one pipeline object, a NetworkInterface named <nic>
- **WHEN** (PowerShell) step 8 runs `Remove-AzNetworkInterface` (real cmdlet) `-ResourceGroupName <rg> -ResourceName <nic> -Force -PassThru`
- **THEN** no error records and no confirmation prompt
- **AND** exactly one pipeline object, the boolean true
- **WHEN** (PowerShell) step 9 runs `Get-AzNetworkInterface` (real cmdlet, List set) `-ResourceGroupName <rg>`
- **THEN** no pipeline output
