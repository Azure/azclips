# network-network-interfaces Specification

## Purpose
Defines how Azclips creates, shows, lists and deletes Azure network interfaces on the CLI and PowerShell surfaces, including the request each command sends and the output it writes.

## Requirements
### Requirement: Create location defaults to the resource group location
Network interface create SHALL accept an optional Location. When Location is omitted, Azclips SHALL read the resource group and use its location in the PUT body before sending the network interface PUT. When Location is given, Azclips SHALL send no resource group request.

#### Scenario: Location omitted uses the resource group location
- **WHEN** create runs without Location
- **THEN** Azclips sends `GET /subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroupName}` (Microsoft.Resources) before the NIC PUT, and sets the PUT body `location` to the returned resource group `location`

#### Scenario: Location given sends no resource group request
- **WHEN** create runs with Location
- **THEN** Azclips sends no resource group GET and sets the PUT body `location` to the given value

#### Scenario: Resource group lookup failure stops create
- **WHEN** the resource group GET fails (for example 404 `ResourceGroupNotFound`)
- **THEN** Azclips surfaces the service error and sends no NIC PUT

### Requirement: Create builds a single primary IP configuration
Network interface create SHALL send exactly one IP configuration, marked primary, named from IpConfigurationName or `ipconfig1` by default.

#### Scenario: One primary IP configuration
- **WHEN** create runs
- **THEN** the PUT body `properties.ipConfigurations` contains exactly one element, and that element has `properties.primary = true`

#### Scenario: Default IP configuration name
- **WHEN** IpConfigurationName is not given
- **THEN** that element's `name` is `ipconfig1`

#### Scenario: Given IP configuration name
- **WHEN** IpConfigurationName is given
- **THEN** that element's `name` is the given value

### Requirement: Create resolves the subnet from a name or ID
Network interface create SHALL accept Subnet as a full ARM resource ID or as a name. A name SHALL be formatted into a subnet ID with VirtualNetworkName in the NIC's subscription and resource group, without an ARM call, and SHALL require VirtualNetworkName.

#### Scenario: Subnet given as a resource ID
- **WHEN** Subnet is a full ARM resource ID (it starts with `/subscriptions/`)
- **THEN** Azclips sends it unchanged as `properties.ipConfigurations[0].properties.subnet.id`

#### Scenario: Subnet given as a name with a virtual network name
- **WHEN** Subnet is a name and VirtualNetworkName is given
- **THEN** Azclips sends `/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/virtualNetworks/{VirtualNetworkName}/subnets/{Subnet}` as `properties.ipConfigurations[0].properties.subnet.id`
- **AND** It makes no extra ARM call
- **AND** `{SubscriptionId}` and `{ResourceGroupName}` are the NIC's own (the subscription is the active profile's when not given)

#### Scenario: Subnet name without a virtual network name
- **WHEN** Subnet is a name and VirtualNetworkName is not given
- **THEN** Azclips raises an error saying that VirtualNetworkName is required when Subnet is a name, and sends no ARM request

### Requirement: Create derives the private IP allocation method
Network interface create SHALL set the IP configuration's private IP allocation method to `Static` when PrivateIpAddress is given and to `Dynamic` otherwise.

#### Scenario: Private IP address given
- **WHEN** PrivateIpAddress is given
- **THEN** `properties.ipConfigurations[0].properties.privateIPAddress` is the given value and `privateIPAllocationMethod` is `Static`

#### Scenario: Private IP address not given
- **WHEN** PrivateIpAddress is not given
- **THEN** `properties.ipConfigurations[0].properties.privateIPAllocationMethod` is `Dynamic` and `privateIPAddress` is omitted

### Requirement: Create defaults the private IP address version
Network interface create SHALL always send the IP configuration's private IP address version, using PrivateIpAddressVersion when given and `IPv4` otherwise.

#### Scenario: Private IP address version not given
- **WHEN** PrivateIpAddressVersion is not given
- **THEN** `properties.ipConfigurations[0].properties.privateIPAddressVersion` is `IPv4`

#### Scenario: Private IP address version given
- **WHEN** PrivateIpAddressVersion is given (`IPv4` or `IPv6`)
- **THEN** `properties.ipConfigurations[0].properties.privateIPAddressVersion` is the given value

### Requirement: Create resolves the public IP address from a name or ID
Network interface create SHALL accept an optional PublicIpAddress as a full ARM resource ID or as a name formatted into an ID in the NIC's subscription and resource group, and SHALL send it as an IP configuration `publicIPAddress` reference carrying only `id`.

#### Scenario: Public IP address given as a resource ID
- **WHEN** PublicIpAddress is a full ARM resource ID
- **THEN** `properties.ipConfigurations[0].properties.publicIPAddress` is `{"id": <value>}`, with no other public IP fields in the body

#### Scenario: Public IP address given as a name
- **WHEN** PublicIpAddress is a name
- **THEN** `properties.ipConfigurations[0].properties.publicIPAddress` is `{"id": "/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/publicIPAddresses/{PublicIpAddress}"}` (the NIC's subscription and resource group), with no extra ARM call

#### Scenario: Public IP address not given
- **WHEN** PublicIpAddress is not given
- **THEN** `publicIPAddress` is omitted from the IP configuration

### Requirement: Create resolves the network security group from a name or ID
Network interface create SHALL accept an optional NetworkSecurityGroup as a full ARM resource ID or as a name formatted into an ID in the NIC's subscription and resource group, and SHALL send it as `properties.networkSecurityGroup` carrying only `id`.

#### Scenario: Network security group given as a resource ID
- **WHEN** NetworkSecurityGroup is a full ARM resource ID
- **THEN** the PUT body `properties.networkSecurityGroup` is `{"id": <value>}`, with no other NSG fields

#### Scenario: Network security group given as a name
- **WHEN** NetworkSecurityGroup is a name
- **THEN** `properties.networkSecurityGroup` is `{"id": "/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkSecurityGroups/{NetworkSecurityGroup}"}` (the NIC's subscription and resource group), with no extra ARM call

#### Scenario: Network security group not given
- **WHEN** NetworkSecurityGroup is not given
- **THEN** `properties.networkSecurityGroup` is omitted

### Requirement: Create maps the edge zone to the extended location
Network interface create SHALL accept an optional EdgeZone and send it as an `extendedLocation` of type `EdgeZone`.

#### Scenario: Edge zone given
- **WHEN** EdgeZone is given
- **THEN** the PUT body `extendedLocation` is `{"name": <EdgeZone>, "type": "EdgeZone"}`

#### Scenario: Edge zone not given
- **WHEN** EdgeZone is not given
- **THEN** `extendedLocation` is omitted

### Requirement: Create passes through optional network interface inputs
Network interface create SHALL send Tags, DnsServers, InternalDnsNameLabel, EnableIPForwarding and EnableAcceleratedNetworking to their PUT body properties only when given, with no default value.

#### Scenario: Tags given
- **WHEN** Tags is given
- **THEN** the PUT body `tags` is the given dictionary

#### Scenario: DNS servers given
- **WHEN** DnsServers is given
- **THEN** `properties.dnsSettings.dnsServers` is the given list

#### Scenario: Internal DNS name label given
- **WHEN** InternalDnsNameLabel is given
- **THEN** `properties.dnsSettings.internalDnsNameLabel` is the given value

#### Scenario: Neither DnsServers nor InternalDnsNameLabel given
- **WHEN** neither is given
- **THEN** `properties.dnsSettings` is omitted

#### Scenario: IP forwarding given
- **WHEN** EnableIPForwarding is given
- **THEN** `properties.enableIPForwarding` is the given bool

#### Scenario: Accelerated networking given
- **WHEN** EnableAcceleratedNetworking is given
- **THEN** `properties.enableAcceleratedNetworking` is the given bool

#### Scenario: Pass-through input not given
- **WHEN** any of these inputs is not given
- **THEN** its property is omitted from the PUT body, with no `false` or empty default

### Requirement: Create exposes only the approved inputs
Network interface create SHALL bind only the approved inputs on both surfaces and SHALL reject any other generated body input as an unknown parameter without sending an ARM request.

#### Scenario: Only approved inputs are bindable
- **WHEN** create is invoked on either surface
- **THEN** the only bindable inputs are the ones in the approved shape: SubscriptionId, ResourceGroupName, Name, Location, Tags, Subnet, VirtualNetworkName, IpConfigurationName, PrivateIpAddress, PrivateIpAddressVersion, PublicIpAddress, NetworkSecurityGroup, DnsServers, InternalDnsNameLabel, EnableIPForwarding, EnableAcceleratedNetworking, EdgeZone

#### Scenario: Generated-only input is rejected
- **WHEN** a user passes a generated-only input (body `Id`, `IpConfigurations`, `ExtendedLocationName`/`ExtendedLocationType`, `NetworkSecurityGroupLocation`/`NetworkSecurityGroupTags`/`FlushConnection`/`SecurityRules`, any `PrivateLinkService*`, `LoadBalancerFrontendIpConfigurations`, `DestinationIPAddress`, `AccessMode`, `Fqdns`, `EnableProxyProtocol`, `VisibilitySubscriptions`, `AutoApprovalSubscriptions`, `WorkloadType`, `NicType`, `MigrationPhase`, `AuxiliaryMode`, `AuxiliarySku`, `DisableTcpStateTracking`)
- **THEN** Azclips rejects it as an unknown parameter and sends no ARM request

### Requirement: List routes by resource group
Network interface list SHALL list the network interfaces of the given resource group when ResourceGroupName is given and of the whole subscription otherwise, following every page and writing each network interface as a bare `NetworkInterface`.

#### Scenario: List in a resource group
- **WHEN** list runs with ResourceGroupName
- **THEN** Azclips sends `GET /subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkInterfaces?api-version=2026-03-01`, follows every `nextLink`, and outputs each NIC as a bare `NetworkInterface`

#### Scenario: List in the subscription
- **WHEN** list runs without ResourceGroupName
- **THEN** Azclips sends `GET /subscriptions/{SubscriptionId}/providers/Microsoft.Network/networkInterfaces?api-version=2026-03-01`, follows every `nextLink`, and outputs each NIC as a bare `NetworkInterface`

#### Scenario: Empty scope
- **WHEN** a scope has no NICs
- **THEN** Azclips outputs nothing and raises no error

### Requirement: Show a network interface
Network interface show SHALL get one network interface by ResourceGroupName and Name, with an optional Expand, and SHALL write it as a bare `NetworkInterface`.

#### Scenario: Show a network interface
- **WHEN** show runs
- **THEN** Azclips sends `GET .../resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkInterfaces/{Name}?api-version=2026-03-01` and outputs the bare `NetworkInterface`

#### Scenario: Show with expand
- **WHEN** show runs with Expand
- **THEN** the GET adds `$expand=<Expand>`

#### Scenario: Show a missing network interface
- **WHEN** the NIC does not exist
- **THEN** show surfaces the service 404 error

### Requirement: Delete a network interface
Network interface delete SHALL delete one network interface by ResourceGroupName and Name, wait for the long-running operation to complete, and write no resource output.

#### Scenario: Delete a network interface
- **WHEN** delete runs
- **THEN** Azclips sends `DELETE .../networkInterfaces/{Name}?api-version=2026-03-01`, polls the LRO to completion, and writes no resource output

### Requirement: PowerShell remove confirms and supports PassThru
`Remove-AzNetworkInterface` SHALL ask for confirmation unless `-Force` is given, SHALL send no DELETE under `-WhatIf`, and SHALL write `$true` after a completed delete when `-PassThru` is given. The CLI delete SHALL NOT prompt.

#### Scenario: Remove without Force prompts
- **WHEN** `Remove-AzNetworkInterface` runs without `-Force`
- **THEN** PowerShell prompts for confirmation, and declining sends no DELETE

#### Scenario: Remove with WhatIf
- **WHEN** `-WhatIf` is given
- **THEN** no DELETE is sent, even with `-Force`

#### Scenario: Remove with PassThru
- **WHEN** `-PassThru` is given and the delete completes
- **THEN** the cmdlet writes `$true`

#### Scenario: CLI delete does not prompt
- **WHEN** the CLI delete runs
- **THEN** no prompt is shown
