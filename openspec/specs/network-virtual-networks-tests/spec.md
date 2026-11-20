# network-virtual-networks-tests Specification

## Purpose
Record/playback scenario tests for the Azclips Network virtual network commands: the CLI `network vnet ...` commands and the PowerShell `*-AzVirtualNetwork` cmdlets. Each test replays its committed recording on both surfaces and verifies the behavior stated in the `network-virtual-networks` spec.

## Requirements
### Requirement: Default the location to the resource group location
The scenario tests SHALL verify the behavior requirement "Default the location to the resource group location".

#### Scenario: T6 Resource group location lookup failure stops the create
- **WHEN** (CLI) step 1 runs `network vnet create --resource-group <resource-group> --name <vnet>`
- **THEN** fails with service error code ResourceGroupNotFound
- **AND** the only request sent is GET /subscriptions/{sub}/resourceGroups/<resource-group>; no PUT is sent
- **WHEN** (PowerShell) step 1 runs `New-AzVirtualNetwork -ResourceGroupName <resource-group> -Name <vnet>`
- **THEN** fails with service error code ResourceGroupNotFound
- **AND** no PUT is sent

#### Scenario: T7 Explicit location is sent without a resource group lookup
- **WHEN** (CLI) step 1 runs `network vnet create --resource-group <resource-group> --name <vnet> --location <location>`
- **THEN** the first request is the PUT to /subscriptions/{sub}/resourceGroups/<resource-group>/providers/Microsoft.Network/virtualNetworks/<vnet>?api-version=2026-03-01; no GET on the resource group is sent
- **AND** the PUT body has location "<location>" and properties.addressSpace.addressPrefixes ["10.0.0.0/16"]
- **AND** fails with service error code ResourceGroupNotFound
- **WHEN** (PowerShell) step 1 runs `New-AzVirtualNetwork -ResourceGroupName <resource-group> -Name <vnet> -Location <location>`
- **THEN** the first request is the PUT to virtualNetworks/<vnet>?api-version=2026-03-01; no GET on the resource group is sent
- **AND** fails with service error code ResourceGroupNotFound

### Requirement: Accept a DDoS protection plan by name or resource ID
The scenario tests SHALL verify the behavior requirement "Accept a DDoS protection plan by name or resource ID".

#### Scenario: T5 DDoS protection plan by name or resource ID
- **WHEN** (CLI) step 2 runs `network vnet create --resource-group <resource-group> --name <vnet-ddos-name> --ddos-protection-plan <ddos-plan>`
- **THEN** the PUT body has properties.ddosProtectionPlan.id "/subscriptions/{sub}/resourceGroups/<resource-group>/providers/Microsoft.Network/ddosProtectionPlans/<ddos-plan>" (playback only)
- **AND** fails with the service error for the nonexistent DDoS protection plan
- **WHEN** (CLI) step 3 runs `network vnet create --resource-group <resource-group> --name <vnet-ddos-id> --ddos-protection-plan /subscriptions/{sub}/resourceGroups/<other-resource-group>/providers/Microsoft.Network/ddosProtectionPlans/<ddos-plan>`
- **THEN** the PUT body has properties.ddosProtectionPlan.id equal to the --ddos-protection-plan value, unchanged (playback only)
- **AND** fails with the service error for the nonexistent DDoS protection plan
- **WHEN** (PowerShell) step 2 runs `New-AzVirtualNetwork -ResourceGroupName <resource-group> -Name <vnet-ddos-name> -DdosProtectionPlan <ddos-plan>`
- **THEN** the PUT body has properties.ddosProtectionPlan.id "/subscriptions/{sub}/resourceGroups/<resource-group>/providers/Microsoft.Network/ddosProtectionPlans/<ddos-plan>" (playback only)
- **AND** fails with the service error for the nonexistent DDoS protection plan
- **WHEN** (PowerShell) step 3 runs `New-AzVirtualNetwork -ResourceGroupName <resource-group> -Name <vnet-ddos-id> -DdosProtectionPlanId /subscriptions/{sub}/resourceGroups/<other-resource-group>/providers/Microsoft.Network/ddosProtectionPlans/<ddos-plan>`
- **THEN** the PUT body has properties.ddosProtectionPlan.id equal to the -DdosProtectionPlanId value, unchanged (playback only)
- **AND** fails with the service error for the nonexistent DDoS protection plan

### Requirement: Send EdgeZone as an extended location
The scenario tests SHALL verify the behavior requirement "Send EdgeZone as an extended location".

#### Scenario: T4 EdgeZone is sent as an extended location
- **WHEN** (CLI) step 2 runs `network vnet create --resource-group <resource-group> --name <vnet> --location <location> --edge-zone <edge-zone> --subnet-name <subnet>`
- **THEN** fails with service error code UnsupportedEdgeZone, because the recording subscription has no edge-zone access
- **AND** the PUT body has extendedLocation.name "<edge-zone>" and extendedLocation.type "EdgeZone"
- **WHEN** (PowerShell) step 2 runs `New-AzVirtualNetwork -ResourceGroupName <resource-group> -Name <vnet> -Location <location> -EdgeZone <edge-zone> -SubnetName <subnet>`
- **THEN** fails with service error code UnsupportedEdgeZone
- **AND** the PUT body has extendedLocation.name "<edge-zone>"

### Requirement: Send encryption only with EnableEncryption
The scenario tests SHALL verify the behavior requirement "Send encryption only with EnableEncryption".

#### Scenario: T3 Encryption is sent only with EnableEncryption
- **WHEN** (CLI) step 2 runs `network vnet create --resource-group <resource-group> --name <vnet1> --address-prefixes 10.0.0.0/16 --subnet-name <subnet> --subnet-prefixes 10.0.0.0/24 --enable-encryption true --encryption-enforcement-policy allowUnencrypted`
- **THEN** the PUT body has properties.encryption.enabled true and properties.encryption.enforcement "allowUnencrypted"
- **AND** EncryptionEnabled == true
- **AND** EncryptionEnforcement == "AllowUnencrypted"
- **WHEN** (CLI) step 3 runs `network vnet create --resource-group <resource-group> --name <vnet2> --address-prefixes 10.0.0.0/16 --subnet-name <subnet> --subnet-prefixes 10.0.0.0/24 --enable-encryption true --encryption-enforcement-policy dropUnencrypted`
- **THEN** fails with service error code CannotUseDropUnencryptedForVnet, because the recording subscription lacks the AllowDropUnecryptedVnet feature
- **AND** the PUT body has properties.encryption.enabled true and properties.encryption.enforcement "dropUnencrypted"
- **WHEN** (CLI) step 4 runs `network vnet create --resource-group <resource-group> --name <vnet3> --address-prefixes 10.0.0.0/16 --subnet-name <subnet> --subnet-prefixes 10.0.0.0/24 --enable-encryption false`
- **THEN** ProvisioningState == "Succeeded"
- **AND** the PUT body has properties.encryption.enabled false and no properties.encryption.enforcement
- **WHEN** (CLI) step 5 runs `network vnet create --resource-group <resource-group> --name <vnet4> --address-prefixes 10.0.0.0/16 --subnet-name <subnet> --subnet-prefixes 10.0.0.0/24 --enable-encryption true`
- **THEN** fails with service error code MustSpecifyBothEncryptionIntentAndEnforcementPolicy
- **AND** the PUT body has properties.encryption.enabled true and no properties.encryption.enforcement
- **WHEN** (CLI) step 6 runs `network vnet create --resource-group <resource-group> --name <vnet5> --encryption-enforcement-policy DropUnencrypted`
- **THEN** ProvisioningState == "Succeeded"
- **AND** the PUT body has no properties.encryption
- **AND** the PUT body has no properties.subnets
- **WHEN** (CLI) step 7 runs `network vnet create --resource-group <resource-group> --name <vnet6> --enable-encryption true --encryption-enforcement-policy Invalid`
- **THEN** fails because 'Invalid' is not an allowed value for --encryption-enforcement-policy
- **AND** no request is sent
- **WHEN** (PowerShell) step 2 runs `New-AzVirtualNetwork -ResourceGroupName <resource-group> -Name <vnet1> -AddressPrefixes 10.0.0.0/16 -SubnetName <subnet> -SubnetPrefixes 10.0.0.0/24 -EnableEncryption -EncryptionEnforcement allowUnencrypted`
- **THEN** the PUT body has properties.encryption.enabled true and properties.encryption.enforcement "allowUnencrypted"
- **AND** EncryptionEnabled == true
- **AND** EncryptionEnforcement == "AllowUnencrypted"
- **WHEN** (PowerShell) step 3 runs `New-AzVirtualNetwork -ResourceGroupName <resource-group> -Name <vnet2> -AddressPrefixes 10.0.0.0/16 -SubnetName <subnet> -SubnetPrefixes 10.0.0.0/24 -EnableEncryption -EncryptionEnforcementPolicy dropUnencrypted`
- **THEN** fails with service error code CannotUseDropUnencryptedForVnet
- **AND** the PUT body has properties.encryption.enforcement "dropUnencrypted"
- **WHEN** (PowerShell) step 4 runs `New-AzVirtualNetwork -ResourceGroupName <resource-group> -Name <vnet3> -AddressPrefixes 10.0.0.0/16 -SubnetName <subnet> -SubnetPrefixes 10.0.0.0/24 -EnableEncryption:$false`
- **THEN** ProvisioningState == "Succeeded"
- **AND** the PUT body has properties.encryption.enabled false and no properties.encryption.enforcement
- **WHEN** (PowerShell) step 5 runs `New-AzVirtualNetwork -ResourceGroupName <resource-group> -Name <vnet4> -AddressPrefixes 10.0.0.0/16 -SubnetName <subnet> -SubnetPrefixes 10.0.0.0/24 -EnableEncryption`
- **THEN** fails with service error code MustSpecifyBothEncryptionIntentAndEnforcementPolicy
- **WHEN** (PowerShell) step 6 runs `New-AzVirtualNetwork -ResourceGroupName <resource-group> -Name <vnet5> -EncryptionEnforcement DropUnencrypted`
- **THEN** ProvisioningState == "Succeeded"
- **AND** the PUT body has no properties.encryption
- **AND** the PUT body has no properties.subnets
- **WHEN** (PowerShell) step 7 runs `New-AzVirtualNetwork -ResourceGroupName <resource-group> -Name <vnet6> -EnableEncryption -EncryptionEnforcement Invalid`
- **THEN** fails with a parameter binding error from the EncryptionEnforcement ValidateSet
- **AND** no request is sent

### Requirement: Virtual network create input surface
The scenario tests SHALL verify the behavior requirement "Virtual network create input surface".

#### Scenario: T2 Create with explicit inputs and inline subnet prefixes
- **WHEN** (CLI) step 2 runs `network vnet create --resource-group <resource-group> --name <vnet-inputs> --location <location> --address-prefixes 20.0.0.0/16 10.0.0.0/16 --subnet-name <subnet> --subnet-prefixes 20.0.0.0/24 --dns-servers 8.8.8.8 --tags env=scenario owner --flowtimeout 10 --bgp-community 12076:20000 --ddos-protection false`
- **THEN** no GET on the resource group is sent; the first request is the PUT to virtualNetworks/<vnet-inputs>?api-version=2026-03-01
- **AND** the PUT body has location "<location>"
- **AND** Location == "<location>"
- **AND** AddressSpaceAddressPrefixes contains exactly "20.0.0.0/16" and "10.0.0.0/16"
- **AND** Subnets has one item with Name "<subnet>" and AddressPrefix "20.0.0.0/24"
- **AND** the PUT body has properties.subnets[0].addressPrefix "20.0.0.0/24" and no properties.subnets[0].addressPrefixes
- **AND** DhcpOptionsDnsServers == ["8.8.8.8"]
- **AND** Tags == { env: "scenario", owner: "" }
- **AND** FlowTimeoutInMinutes == 10
- **AND** BgpCommunitiesVirtualNetworkCommunity == "12076:20000"
- **AND** EnableDdosProtection == false and the PUT body has properties.enableDdosProtection false
- **WHEN** (CLI) step 3 runs `network vnet create --resource-group <resource-group> --name <vnet-small> --location <location> --address-prefixes 10.2.0.0/27 --subnet-name <subnet>`
- **THEN** Subnets has one item with Name "<subnet>" and AddressPrefix "10.2.0.0/27"
- **AND** the PUT body has properties.subnets[0].addressPrefix "10.2.0.0/27"
- **WHEN** (CLI) step 4 runs `network vnet create --resource-group <resource-group> --name <vnet-multi> --location <location> --address-prefixes 10.1.0.0/16 --subnet-name <subnet> --subnet-prefixes 10.1.0.0/24 10.1.1.0/24`
- **THEN** the PUT body has properties.subnets[0].addressPrefixes ["10.1.0.0/24", "10.1.1.0/24"] and no properties.subnets[0].addressPrefix
- **AND** Subnets has one item, named "<subnet>", whose AddressPrefixes contains exactly "10.1.0.0/24" and "10.1.1.0/24"
- **WHEN** (CLI) step 5 runs `network vnet delete --resource-group <resource-group> --name <vnet-inputs>`
- **THEN** writes no output
- **WHEN** (PowerShell) step 2 runs `New-AzVirtualNetwork -ResourceGroupName <resource-group> -Name <vnet-inputs> -Location <location> -AddressPrefixes 20.0.0.0/16,10.0.0.0/16 -SubnetName <subnet> -SubnetPrefixes 20.0.0.0/24 -DnsServers 8.8.8.8 -Tag @{ env = "scenario"; owner = "" } -FlowTimeoutInMinutes 10 -BgpCommunity 12076:20000 -EnableDdosProtection:$false`
- **THEN** no GET on the resource group is sent; the first request is the PUT to virtualNetworks/<vnet-inputs>?api-version=2026-03-01
- **AND** Location == "<location>"
- **AND** AddressSpaceAddressPrefixes contains exactly "20.0.0.0/16" and "10.0.0.0/16"
- **AND** Subnets has one item with Name "<subnet>" and AddressPrefix "20.0.0.0/24"
- **AND** DhcpOptionsDnsServers == ["8.8.8.8"]
- **AND** Tags == { env: "scenario", owner: "" }
- **AND** FlowTimeoutInMinutes == 10
- **AND** BgpCommunitiesVirtualNetworkCommunity == "12076:20000"
- **AND** EnableDdosProtection == false and the PUT body has properties.enableDdosProtection false
- **WHEN** (PowerShell) step 3 runs `New-AzVirtualNetwork -ResourceGroupName <resource-group> -Name <vnet-small> -Location <location> -AddressPrefixes 10.2.0.0/27 -SubnetName <subnet>`
- **THEN** Subnets has one item with Name "<subnet>" and AddressPrefix "10.2.0.0/27"
- **WHEN** (PowerShell) step 4 runs `New-AzVirtualNetwork -ResourceGroupName <resource-group> -Name <vnet-multi> -Location <location> -AddressPrefixes 10.1.0.0/16 -SubnetName <subnet> -SubnetPrefixes 10.1.0.0/24,10.1.1.0/24`
- **THEN** the PUT body has properties.subnets[0].addressPrefixes ["10.1.0.0/24", "10.1.1.0/24"] and no properties.subnets[0].addressPrefix
- **AND** Subnets has one item, named "<subnet>", whose AddressPrefixes contains exactly "10.1.0.0/24" and "10.1.1.0/24"
- **WHEN** (PowerShell) step 5 runs `Remove-AzVirtualNetwork -ResourceGroupName <resource-group> -Name <vnet-inputs> -Force`
- **THEN** writes no output

### Requirement: List virtual networks by resource group or subscription
The scenario tests SHALL verify the behavior requirement "List virtual networks by resource group or subscription".

#### Scenario: T1 Virtual network lifecycle with create defaults
- **WHEN** (CLI) step 1 runs `network vnet create --resource-group <resource-group> --name <vnet> --subnet-name <subnet>`
- **THEN** fails with service error code ResourceGroupNotFound
- **AND** the only request sent is GET /subscriptions/{sub}/resourceGroups/<resource-group>; no PUT to virtualNetworks is sent
- **WHEN** (CLI) step 3 runs `network vnet create --resource-group <resource-group> --name <vnet> --subnet-name <subnet>`
- **THEN** a GET on resource group <resource-group> is sent before the PUT to virtualNetworks/<vnet>?api-version=2026-03-01
- **AND** Location == "<location>"
- **AND** ProvisioningState == "Succeeded"
- **AND** AddressSpaceAddressPrefixes == ["10.0.0.0/16"]
- **AND** Subnets has one item with Name "<subnet>", AddressPrefix "10.0.0.0/24" and PrivateEndpointNetworkPolicies "Disabled"
- **AND** PrivateEndpointVNetPolicies == "Disabled"
- **AND** the PUT body has properties.subnets[0].addressPrefix "10.0.0.0/24" and no properties.subnets[0].addressPrefixes
- **AND** the PUT body has no tags, properties.dhcpOptions, properties.bgpCommunities, properties.ddosProtectionPlan, properties.encryption or extendedLocation
- **WHEN** (CLI) step 4 runs `network vnet show --resource-group <resource-group> --name <vnet>`
- **THEN** Name == "<vnet>"
- **AND** Type == "Microsoft.Network/virtualNetworks"
- **AND** Id == <step 3: Id> (playback only)
- **WHEN** (CLI) step 5 runs `network vnet list --resource-group <resource-group>`
- **THEN** the output holds exactly one VirtualNetwork, named "<vnet>"
- **AND** the request is GET /subscriptions/{sub}/resourceGroups/<resource-group>/providers/Microsoft.Network/virtualNetworks?api-version=2026-03-01
- **WHEN** (CLI) step 6 runs `network vnet list`
- **THEN** the output contains a VirtualNetwork with Id <step 3: Id>
- **AND** every output item has Type "Microsoft.Network/virtualNetworks"
- **AND** the request is GET /subscriptions/{sub}/providers/Microsoft.Network/virtualNetworks?api-version=2026-03-01
- **WHEN** (CLI) step 7 runs `network vnet delete --resource-group <resource-group> --name <vnet>`
- **THEN** writes no output
- **WHEN** (CLI) step 8 runs `network vnet list --resource-group <resource-group>`
- **THEN** the output is empty
- **WHEN** (PowerShell) step 1 runs `New-AzVirtualNetwork -ResourceGroupName <resource-group> -Name <vnet> -SubnetName <subnet>`
- **THEN** fails with service error code ResourceGroupNotFound
- **AND** the only request sent is GET /subscriptions/{sub}/resourceGroups/<resource-group>; no PUT to virtualNetworks is sent
- **WHEN** (PowerShell) step 3 runs `New-AzVirtualNetwork -ResourceGroupName <resource-group> -Name <vnet> -SubnetName <subnet> -WhatIf`
- **THEN** writes no output
- **AND** no request is sent
- **WHEN** (PowerShell) step 4 runs `New-AzVirtualNetwork -ResourceGroupName <resource-group> -Name <vnet> -SubnetName <subnet>`
- **THEN** a GET on resource group <resource-group> is sent before the PUT to virtualNetworks/<vnet>?api-version=2026-03-01
- **AND** Location == "<location>"
- **AND** ProvisioningState == "Succeeded"
- **AND** AddressSpaceAddressPrefixes == ["10.0.0.0/16"]
- **AND** Subnets has one item with Name "<subnet>", AddressPrefix "10.0.0.0/24" and PrivateEndpointNetworkPolicies "Disabled"
- **AND** PrivateEndpointVNetPolicies == "Disabled"
- **WHEN** (PowerShell) step 5 runs `Get-AzVirtualNetwork_Get -ResourceGroupName <resource-group> -Name <vnet>`
- **THEN** writes one VirtualNetwork with Name "<vnet>" and Type "Microsoft.Network/virtualNetworks"
- **WHEN** (PowerShell) step 6 runs `Get-AzVirtualNetwork_List -ResourceGroupName <resource-group>`
- **THEN** writes exactly one VirtualNetwork object (not a list), named "<vnet>"
- **AND** the request is GET /subscriptions/{sub}/resourceGroups/<resource-group>/providers/Microsoft.Network/virtualNetworks?api-version=2026-03-01
- **WHEN** (PowerShell) step 7 runs `Get-AzVirtualNetwork_List`
- **THEN** the output objects include a VirtualNetwork with Id <step 4: Id>
- **AND** every output object has Type "Microsoft.Network/virtualNetworks"
- **AND** the request is GET /subscriptions/{sub}/providers/Microsoft.Network/virtualNetworks?api-version=2026-03-01
- **WHEN** (PowerShell) step 8 runs `Remove-AzVirtualNetwork -ResourceGroupName <resource-group> -Name <vnet> -WhatIf`
- **THEN** writes no output
- **AND** no DELETE is sent
- **WHEN** (PowerShell) step 9 runs `Remove-AzVirtualNetwork -ResourceGroupName <resource-group> -Name <vnet>`
- **THEN** fails because the host cannot answer the confirmation prompt
- **AND** no DELETE is sent
- **WHEN** (PowerShell) step 10 runs `Remove-AzVirtualNetwork -ResourceGroupName <resource-group> -Name <vnet> -Force -PassThru`
- **THEN** writes exactly one output, the boolean true
- **AND** the DELETE to virtualNetworks/<vnet>?api-version=2026-03-01 is sent without a prompt
- **WHEN** (PowerShell) step 11 runs `Get-AzVirtualNetwork_List -ResourceGroupName <resource-group>`
- **THEN** writes no output
