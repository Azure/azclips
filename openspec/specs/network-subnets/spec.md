# network-subnets Specification

## Purpose
Defines how Azclips handles the Network `Subnets` operation group (`Microsoft.Network/virtualNetworks/subnets`): the inputs subnet commands accept and the request shapes, request sequences and outputs they produce.

## Requirements
### Requirement: Create sets the subnet name in the body
Subnet create SHALL send the subnet PUT with body `name` set to the Name input.

#### Scenario: Body name equals the Name input
- **WHEN** subnet create runs
- **THEN** Azclips sends `PUT .../virtualNetworks/{VirtualNetworkName}/subnets/{Name}?api-version=2026-03-01` with body `name` equal to the Name input.

### Requirement: Address prefix singular/plural mapping
Subnet create and update SHALL take one AddressPrefixes list and send it as `properties.addressPrefix` when it has one item or `properties.addressPrefixes` when it has more, omitting or removing the other property; update without AddressPrefixes SHALL keep both properties as the GET returned them.

#### Scenario: Create with one prefix
- **WHEN** create receives AddressPrefixes with one item
- **THEN** the PUT body sets `properties.addressPrefix` to that item and omits `properties.addressPrefixes`.

#### Scenario: Create with several prefixes
- **WHEN** create receives AddressPrefixes with more than one item
- **THEN** the PUT body sets `properties.addressPrefixes` to the items in input order and omits `properties.addressPrefix`.

#### Scenario: Update with one prefix
- **WHEN** update receives AddressPrefixes with one item
- **THEN** the PUT body sets `properties.addressPrefix` to that item and removes `properties.addressPrefixes` taken from the GET result.

#### Scenario: Update with several prefixes
- **WHEN** update receives AddressPrefixes with more than one item
- **THEN** the PUT body sets `properties.addressPrefixes` to the items and removes `properties.addressPrefix` taken from the GET result.

#### Scenario: Update without prefixes
- **WHEN** update does not receive AddressPrefixes
- **THEN** the PUT body keeps `properties.addressPrefix` and `properties.addressPrefixes` exactly as the GET returned them.

### Requirement: Resource reference name-or-ID
Subnet create and update SHALL accept NetworkSecurityGroup, RouteTable and NatGateway as a name or a full ARM resource ID, SHALL format a name into an ID in the same subscription and resource group without an extra ARM call, and SHALL send each given reference as an object carrying only `id`.

#### Scenario: NSG given by name
- **WHEN** NetworkSecurityGroup is a name
- **THEN** the PUT body sets `properties.networkSecurityGroup.id` to `/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkSecurityGroups/{name}`, with no extra ARM call.

#### Scenario: Route table given by name
- **WHEN** RouteTable is a name
- **THEN** the PUT body sets `properties.routeTable.id` to `/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/routeTables/{name}`.

#### Scenario: NAT gateway given by name
- **WHEN** NatGateway is a name
- **THEN** the PUT body sets `properties.natGateway.id` to `/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/natGateways/{name}`.

#### Scenario: Reference given as a full ARM ID
- **WHEN** any of these inputs is a full ARM resource ID
- **THEN** the PUT body uses that ID unchanged, including IDs in another subscription or resource group.

#### Scenario: Reference object carries only the ID
- **WHEN** a reference input is given
- **THEN** the referenced object in the PUT body carries only `id`, with no nested NSG or route table properties.

#### Scenario: Create without references
- **WHEN** create receives none of these inputs
- **THEN** the PUT body omits `networkSecurityGroup`, `routeTable`, and `natGateway`.

### Requirement: Service endpoints from service names
Subnet create and update SHALL take ServiceEndpoints as a list of service names and SHALL send one `{ "service": "<item>" }` entry per item in input order; on update the given list SHALL replace the GET list.

#### Scenario: Service endpoints given
- **WHEN** ServiceEndpoints is given
- **THEN** the PUT body sets `properties.serviceEndpoints` to one `{ "service": "<item>" }` entry per item, in input order.

#### Scenario: Update replaces service endpoints
- **WHEN** update receives ServiceEndpoints
- **THEN** that list replaces the `serviceEndpoints` list from the GET result.

#### Scenario: Create without service endpoints
- **WHEN** create does not receive ServiceEndpoints
- **THEN** the PUT body omits `properties.serviceEndpoints`.

### Requirement: Delegations from service names
Subnet create and update SHALL take Delegations as a list of service names and SHALL send one delegation per item named by its zero-based index with `properties.serviceName` set to the item as given; on update the given list SHALL replace the GET list.

#### Scenario: Delegations given
- **WHEN** Delegations is given
- **THEN** the PUT body sets `properties.delegations` to one entry per item with `name` equal to the item's zero-based index as a string (`"0"`, `"1"`, …) and `properties.serviceName` equal to the item as given.

#### Scenario: Update replaces delegations
- **WHEN** update receives Delegations
- **THEN** that list replaces the `delegations` list from the GET result.

#### Scenario: Create without delegations
- **WHEN** create does not receive Delegations
- **THEN** the PUT body omits `properties.delegations`.

### Requirement: Create-time network policy defaults
Subnet create SHALL default `properties.privateEndpointNetworkPolicies` to `Disabled` and `properties.privateLinkServiceNetworkPolicies` to `Enabled` when those inputs are not given, and SHALL send a given value as is.

#### Scenario: Private endpoint policies default
- **WHEN** create does not receive PrivateEndpointNetworkPolicies
- **THEN** the PUT body sets `properties.privateEndpointNetworkPolicies` to `Disabled`.

#### Scenario: Private link service policies default
- **WHEN** create does not receive PrivateLinkServiceNetworkPolicies
- **THEN** the PUT body sets `properties.privateLinkServiceNetworkPolicies` to `Enabled`.

#### Scenario: Given policy values are sent
- **WHEN** create receives either input
- **THEN** the PUT body sends the given value.

### Requirement: Update as GET → apply given inputs → PUT
Subnet update SHALL GET the subnet, change only the properties whose inputs were given, keep every other property and apply no create defaults, then PUT the merged subnet, wait for the long-running operation and output the final subnet as the bare REST resource. A failed GET SHALL return the service error and send no PUT.

#### Scenario: Update reads the subnet first
- **WHEN** subnet update runs
- **THEN** Azclips first sends `GET .../virtualNetworks/{VirtualNetworkName}/subnets/{Name}?api-version=2026-03-01`.

#### Scenario: GET failure stops the update
- **WHEN** that GET fails (for example 404 NotFound)
- **THEN** Azclips returns the service error and sends no PUT.

#### Scenario: Only given inputs change
- **WHEN** the GET succeeds
- **THEN** Azclips changes only the properties whose inputs were given (AddressPrefixes, NetworkSecurityGroup, RouteTable, NatGateway, ServiceEndpoints, Delegations, PrivateEndpointNetworkPolicies, PrivateLinkServiceNetworkPolicies, DefaultOutboundAccess) and keeps every other property as the GET returned it.

#### Scenario: Update applies no create defaults
- **WHEN** update does not receive PrivateEndpointNetworkPolicies or PrivateLinkServiceNetworkPolicies
- **THEN** the PUT body keeps the GET values and applies no create defaults.

#### Scenario: Merged subnet is written and returned
- **WHEN** the merged subnet is ready
- **THEN** Azclips sends `PUT .../subnets/{Name}?api-version=2026-03-01`, waits for the long-running operation, and outputs the final subnet as the bare REST resource.

### Requirement: Update detaches references with an empty string
Subnet update SHALL detach the NSG, route table or NAT gateway when that input is an empty string by omitting the reference from the PUT body, and SHALL replace the reference when the input is non-empty.

#### Scenario: Empty NSG detaches it
- **WHEN** update receives NetworkSecurityGroup as an empty string
- **THEN** the PUT body omits `properties.networkSecurityGroup` entirely, so the NSG is detached.

#### Scenario: Empty route table detaches it
- **WHEN** update receives RouteTable as an empty string
- **THEN** the PUT body omits `properties.routeTable` entirely, so the route table is detached.

#### Scenario: Empty NAT gateway detaches it
- **WHEN** update receives NatGateway as an empty string
- **THEN** the PUT body omits `properties.natGateway` entirely, so the NAT gateway is detached.

#### Scenario: Non-empty value replaces the reference
- **WHEN** update receives a non-empty value for any of these
- **THEN** the reference is replaced as described in requirement "Resource reference name-or-ID".
