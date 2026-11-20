# network-virtual-networks Specification

## Purpose
Defines how Azclips creates, shows, lists, and deletes Azure virtual networks from the CLI and PowerShell, including the create defaults and input surface, list scope routing, and PowerShell confirmation behavior.

## Requirements
### Requirement: Default the location to the resource group location
Virtual network create SHALL accept an optional `Location`. When `Location` is omitted, it SHALL read the resource group and send the virtual network PUT with the resource group's location. When `Location` is given, it SHALL send no resource group request.

#### Scenario: Location omitted uses the resource group location
- **WHEN** virtual network create runs without `Location`
- **THEN** Azclips SHALL first send `GET /subscriptions/{sub}/resourceGroups/{rg}` (Microsoft.Resources)
- **AND** the virtual network PUT SHALL carry `location` set to the resource group's `location`

#### Scenario: Location given is sent unchanged
- **WHEN** virtual network create runs with `Location`
- **THEN** Azclips SHALL send no resource group GET
- **AND** the PUT SHALL carry the given `location` unchanged

#### Scenario: Resource group lookup fails
- **WHEN** virtual network create runs without `Location` and the resource group GET fails, for example because the resource group does not exist
- **THEN** Azclips SHALL surface that service error
- **AND** Azclips SHALL send no virtual network PUT

### Requirement: Default the address prefixes
Virtual network create SHALL send `properties.addressSpace.addressPrefixes` as `["10.0.0.0/16"]` when `AddressPrefixes` is omitted, and as exactly the given list otherwise.

#### Scenario: AddressPrefixes omitted
- **WHEN** virtual network create runs without `AddressPrefixes`
- **THEN** the PUT body SHALL carry `properties.addressSpace.addressPrefixes = ["10.0.0.0/16"]`

#### Scenario: AddressPrefixes given
- **WHEN** virtual network create runs with `AddressPrefixes`
- **THEN** the PUT body SHALL carry exactly the given list in `properties.addressSpace.addressPrefixes`

### Requirement: Create one inline subnet from SubnetName and SubnetPrefixes
Virtual network create SHALL accept optional `SubnetName` and `SubnetPrefixes`. When `SubnetName` is given, the PUT body SHALL carry exactly one subnet with that name and an address prefix taken from `SubnetPrefixes` or derived from the first virtual network prefix. When `SubnetName` is not given, the PUT body SHALL carry no subnets.

#### Scenario: SubnetName adds one subnet
- **WHEN** virtual network create runs with `SubnetName`
- **THEN** the PUT body SHALL carry `properties.subnets` with exactly one subnet whose `name` is `SubnetName`

#### Scenario: Default subnet prefix from the first virtual network prefix
- **WHEN** virtual network create runs with `SubnetName` and without `SubnetPrefixes`
- **THEN** the subnet's `properties.addressPrefix` SHALL be the address part of the first virtual network address prefix, after the address prefix default is applied, with mask `/24` when the virtual network mask is shorter than 24, else with the virtual network's own mask
- **AND** `10.0.0.0/16` SHALL give `10.0.0.0/24`, and `10.1.2.0/26` SHALL give `10.1.2.0/26`

#### Scenario: One subnet prefix maps to addressPrefix
- **WHEN** virtual network create runs with `SubnetName` and exactly one `SubnetPrefixes` value
- **THEN** the subnet SHALL carry it in `properties.addressPrefix`
- **AND** the subnet SHALL carry no `addressPrefixes`

#### Scenario: Several subnet prefixes map to addressPrefixes
- **WHEN** virtual network create runs with `SubnetName` and more than one `SubnetPrefixes` value
- **THEN** the subnet SHALL carry them in `properties.addressPrefixes`
- **AND** the subnet SHALL carry no `addressPrefix`

#### Scenario: No SubnetName sends no subnets
- **WHEN** virtual network create runs without `SubnetName`, even with `SubnetPrefixes`
- **THEN** the PUT body SHALL carry no `properties.subnets`

### Requirement: Accept a DDoS protection plan by name or resource ID
Virtual network create SHALL accept an optional `DdosProtectionPlan` as a plain name or a full ARM resource ID and send it as `properties.ddosProtectionPlan.id`. A plain name SHALL be expanded to a DDoS protection plan ID in the virtual network's subscription and resource group.

#### Scenario: Plan name is expanded to an ID
- **WHEN** virtual network create runs with `DdosProtectionPlan` set to a plain name
- **THEN** the PUT body SHALL carry `properties.ddosProtectionPlan.id = /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/ddosProtectionPlans/{name}`, using the virtual network's subscription and resource group

#### Scenario: Plan ID is sent unchanged
- **WHEN** virtual network create runs with `DdosProtectionPlan` set to a full ARM resource ID
- **THEN** the PUT body SHALL carry it unchanged in `properties.ddosProtectionPlan.id`

#### Scenario: No plan sends no ddosProtectionPlan
- **WHEN** virtual network create runs without `DdosProtectionPlan`
- **THEN** the PUT body SHALL carry no `properties.ddosProtectionPlan`

### Requirement: Send EdgeZone as an extended location
Virtual network create SHALL accept an optional `EdgeZone` and send it as `extendedLocation` with type `EdgeZone`. The extended location type SHALL NOT be a user input.

#### Scenario: EdgeZone given
- **WHEN** virtual network create runs with `EdgeZone`
- **THEN** the PUT body SHALL carry `extendedLocation = { "name": <EdgeZone>, "type": "EdgeZone" }`

#### Scenario: EdgeZone omitted
- **WHEN** virtual network create runs without `EdgeZone`
- **THEN** the PUT body SHALL carry no `extendedLocation`

### Requirement: Send encryption only with EnableEncryption
Virtual network create SHALL send `properties.encryption` only when `EnableEncryption` is given, with `EncryptionEnforcement` included only alongside it.

#### Scenario: EnableEncryption given
- **WHEN** virtual network create runs with `EnableEncryption`
- **THEN** the PUT body SHALL carry `properties.encryption.enabled = <EnableEncryption>`
- **AND** the PUT body SHALL also carry `properties.encryption.enforcement = <EncryptionEnforcement>` when `EncryptionEnforcement` is given

#### Scenario: EncryptionEnforcement without EnableEncryption
- **WHEN** virtual network create runs with `EncryptionEnforcement` and without `EnableEncryption`
- **THEN** the PUT body SHALL carry no `properties.encryption`, so the enforcement value is not sent

#### Scenario: Neither encryption input given
- **WHEN** virtual network create runs without `EnableEncryption` and without `EncryptionEnforcement`
- **THEN** the PUT body SHALL carry no `properties.encryption`

### Requirement: Virtual network create input surface
Virtual network create SHALL take the virtual network name from `Name` and map `Tags`, `DnsServers`, `FlowTimeoutInMinutes`, `BgpCommunity`, and `EnableDdosProtection` to their body paths, each sent only when given. It SHALL NOT accept generator artifacts or create properties outside the approved shape as inputs, and SHALL output the bare virtual network resource.

#### Scenario: Tags
- **WHEN** virtual network create runs with `Tags`
- **THEN** the PUT body SHALL carry them in `tags`
- **AND** without `Tags` the PUT body SHALL carry no `tags`

#### Scenario: DnsServers
- **WHEN** virtual network create runs with `DnsServers`
- **THEN** the PUT body SHALL carry them in `properties.dhcpOptions.dnsServers`
- **AND** without `DnsServers` the PUT body SHALL carry no `properties.dhcpOptions`

#### Scenario: FlowTimeoutInMinutes
- **WHEN** virtual network create runs with `FlowTimeoutInMinutes`
- **THEN** the PUT body SHALL carry it in `properties.flowTimeoutInMinutes`
- **AND** without `FlowTimeoutInMinutes` nothing SHALL be sent for it

#### Scenario: BgpCommunity
- **WHEN** virtual network create runs with `BgpCommunity`
- **THEN** the PUT body SHALL carry it in `properties.bgpCommunities.virtualNetworkCommunity`
- **AND** without `BgpCommunity` the PUT body SHALL carry no `properties.bgpCommunities`

#### Scenario: EnableDdosProtection
- **WHEN** virtual network create runs with `EnableDdosProtection`
- **THEN** the PUT body SHALL carry it in `properties.enableDdosProtection`
- **AND** without `EnableDdosProtection` nothing SHALL be sent for it

#### Scenario: Name and inputs outside the approved shape
- **WHEN** virtual network create is invoked
- **THEN** the `Name` input SHALL be used as the `virtualNetworks/{name}` path segment
- **AND** body `id`, `virtualNetworkPeerings`, `extendedLocation.type`, IPAM pool prefix allocations, VM protection, IP allocations, private-endpoint VNet policies, summarized gateway prefixes, and full subnet objects SHALL NOT be accepted as inputs

#### Scenario: Create output
- **WHEN** the virtual network PUT long-running operation completes
- **THEN** Azclips SHALL output the bare `VirtualNetwork` resource returned by the final GET

### Requirement: List virtual networks by resource group or subscription
Virtual network list SHALL list the virtual networks of a resource group when `ResourceGroupName` is given, and of the subscription otherwise. It SHALL follow every page and output each virtual network item, not the list envelope.

#### Scenario: List in a resource group
- **WHEN** virtual network list runs with `ResourceGroupName`
- **THEN** Azclips SHALL send `GET /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Network/virtualNetworks?api-version=2026-03-01`

#### Scenario: List in the subscription
- **WHEN** virtual network list runs without `ResourceGroupName`
- **THEN** Azclips SHALL send `GET /subscriptions/{sub}/providers/Microsoft.Network/virtualNetworks?api-version=2026-03-01`

#### Scenario: Every page is followed
- **WHEN** a list response carries `nextLink`
- **THEN** Azclips SHALL follow every page
- **AND** Azclips SHALL output each `VirtualNetwork` item, not the list envelope

### Requirement: PowerShell confirmation on virtual network create and remove
The PowerShell virtual network create and remove cmdlets SHALL support `-WhatIf` and `-Confirm`. The remove cmdlet SHALL ask for confirmation before the DELETE unless `-Force` is given. This behavior SHALL NOT be added to the CLI.

#### Scenario: Remove without Force asks first
- **WHEN** the PowerShell virtual network remove cmdlet runs without `-Force`
- **THEN** PowerShell SHALL ask for confirmation before the DELETE is sent
- **AND** when the confirmation is declined, no DELETE SHALL be sent

#### Scenario: Remove with Force
- **WHEN** the PowerShell virtual network remove cmdlet runs with `-Force` and without `-WhatIf`
- **THEN** the DELETE SHALL be sent without a prompt

#### Scenario: WhatIf sends nothing
- **WHEN** the PowerShell virtual network create or remove cmdlet runs with `-WhatIf`
- **THEN** no PUT or DELETE SHALL be sent

### Requirement: PowerShell remove PassThru
The PowerShell virtual network remove cmdlet SHALL accept a `-PassThru` switch and write `true` when the removal completes with it. This behavior SHALL NOT be added to the CLI.

#### Scenario: Remove with PassThru
- **WHEN** the PowerShell virtual network remove cmdlet completes with `-PassThru`
- **THEN** the cmdlet SHALL write `true`

#### Scenario: Remove without PassThru
- **WHEN** the PowerShell virtual network remove cmdlet completes without `-PassThru`
- **THEN** the cmdlet SHALL write nothing
