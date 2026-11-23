# Spec Delta

## Purpose

Defines how the Network extension's subnet commands (create, show, list, delete) shape their Subnets_CreateOrUpdate request body on the CLI surface.

## ADDED Requirements

### Requirement: Address prefix single or plural
Subnet create SHALL send exactly one given address prefix as `properties.addressPrefix` without `properties.addressPrefixes`, SHALL send two or more given address prefixes as `properties.addressPrefixes` without `properties.addressPrefix`, and SHALL send neither field when no address prefix is given.

#### Scenario: F1 One address prefix
- **WHEN** subnet create runs on the CLI surface with address prefix `10.0.1.0/24` only
- **THEN** the Subnets_CreateOrUpdate body sends `properties.addressPrefix = "10.0.1.0/24"` and no `properties.addressPrefixes`

#### Scenario: F1 Several address prefixes
- **WHEN** subnet create runs with address prefixes `10.0.1.0/24` and `10.0.2.0/24`
- **THEN** the body sends `properties.addressPrefixes = ["10.0.1.0/24", "10.0.2.0/24"]` and no `properties.addressPrefix`

#### Scenario: F1 No address prefix
- **WHEN** subnet create runs without address prefixes
- **THEN** the body sends neither `properties.addressPrefix` nor `properties.addressPrefixes`

### Requirement: IPAM allocation values
On the CLI surface, subnet create SHALL turn each IPAM allocation value into one entry of `properties.ipamPoolPrefixAllocations`: `id=<pool-id> number-of-ip-addresses=<n>` sends `{ pool: { id: <pool-id> }, numberOfIpAddresses: "<n>" }`, and `id=<pool-id>` alone or a bare pool ID sends only `pool.id`.

#### Scenario: F3 Pool ID and number of IP addresses
- **WHEN** subnet create runs on the CLI surface with IPAM allocation `id=<pool-id> number-of-ip-addresses=256`
- **THEN** the body sends `properties.ipamPoolPrefixAllocations = [{ pool: { id: "<pool-id>" }, numberOfIpAddresses: "256" }]`

#### Scenario: F3 Pool ID key only
- **WHEN** subnet create runs on the CLI surface with IPAM allocation `id=<pool-id>`
- **THEN** the allocation entry sends `pool.id = "<pool-id>"` and no `numberOfIpAddresses`

#### Scenario: F3 Bare pool ID
- **WHEN** subnet create runs on the CLI surface with IPAM allocation `<pool-id>` in no `KEY=VALUE` form
- **THEN** the allocation entry sends `pool.id = "<pool-id>"` and no `numberOfIpAddresses`

### Requirement: Service endpoint names
Subnet create SHALL send `properties.serviceEndpoints` with one `{ service: <name> }` item per given service endpoint name, in input order.

#### Scenario: F4 Service endpoint names given
- **WHEN** subnet create runs with service endpoint names `Microsoft.Storage` and `Microsoft.Sql`
- **THEN** the body sends `properties.serviceEndpoints = [{ service: "Microsoft.Storage" }, { service: "Microsoft.Sql" }]`

### Requirement: Service endpoint policy name or ID
Subnet create SHALL send each given service endpoint policy as `{ id }` in `properties.serviceEndpointPolicies`, using a value that starts with `/` unchanged and expanding a name to `/subscriptions/<command subscription>/resourceGroups/<command resource group>/providers/Microsoft.Network/serviceEndpointPolicies/<name>`.

#### Scenario: F5 Policy resource ID
- **WHEN** subnet create runs with service endpoint policy `/subscriptions/<sub>/resourceGroups/<rg2>/providers/Microsoft.Network/serviceEndpointPolicies/<policy>`
- **THEN** the body sends `properties.serviceEndpointPolicies = [{ id: "/subscriptions/<sub>/resourceGroups/<rg2>/providers/Microsoft.Network/serviceEndpointPolicies/<policy>" }]`

#### Scenario: F5 Policy name
- **WHEN** subnet create runs in subscription `<sub>` and resource group `<rg>` with service endpoint policy `<policy>`
- **THEN** the body sends `properties.serviceEndpointPolicies = [{ id: "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Network/serviceEndpointPolicies/<policy>" }]`

### Requirement: Delegation values
Subnet create SHALL send `properties.delegations` with one item per given delegation value, in input order, whose `name` is the value as given and whose `properties.serviceName` is the value, except that a value with no `/` and exactly three dot-separated parts, such as `Microsoft.Sql.servers`, is sent as `serviceName` `Microsoft.<second part>/<third part>`.

#### Scenario: F6 Delegation service value
- **WHEN** subnet create runs with delegation `Microsoft.Web/serverFarms`
- **THEN** the body sends `properties.delegations = [{ name: "Microsoft.Web/serverFarms", properties: { serviceName: "Microsoft.Web/serverFarms" } }]`

#### Scenario: F6 Dotted delegation value
- **WHEN** subnet create runs with delegation `Microsoft.Sql.servers`
- **THEN** the delegation item sends `name = "Microsoft.Sql.servers"` and `properties.serviceName = "Microsoft.Sql/servers"`

### Requirement: Referenced resource name or ID
Subnet create SHALL send a given network security group, route table, or NAT gateway as `properties.networkSecurityGroup.id`, `properties.routeTable.id`, or `properties.natGateway.id`, using a value that starts with `/` unchanged and expanding a name to `/subscriptions/<command subscription>/resourceGroups/<command resource group>/providers/Microsoft.Network/<networkSecurityGroups|routeTables|natGateways>/<name>`, and SHALL send no field of the referenced resource other than `id`.

#### Scenario: F7 Names given
- **WHEN** subnet create runs in subscription `<sub>` and resource group `<rg>` with network security group `<nsg>`, route table `<rt>`, and NAT gateway `<nat>`
- **THEN** the body sends `properties.networkSecurityGroup.id`, `properties.routeTable.id`, and `properties.natGateway.id` as `/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Network/networkSecurityGroups/<nsg>`, `.../routeTables/<rt>`, and `.../natGateways/<nat>`

#### Scenario: F7 Resource ID given
- **WHEN** subnet create runs with network security group `/subscriptions/<sub>/resourceGroups/<rg2>/providers/Microsoft.Network/networkSecurityGroups/<nsg>`
- **THEN** the body sends that value unchanged as `properties.networkSecurityGroup.id`, and `properties.networkSecurityGroup` carries no field other than `id`

### Requirement: Network policy defaults
Subnet create SHALL send `properties.privateEndpointNetworkPolicies = "Disabled"` when private endpoint network policies is not given, and `properties.privateLinkServiceNetworkPolicies = "Enabled"` when private link service network policies is not given.

#### Scenario: F8 Policies not given
- **WHEN** subnet create runs without private endpoint and private link service network policies
- **THEN** the body sends `properties.privateEndpointNetworkPolicies = "Disabled"` and `properties.privateLinkServiceNetworkPolicies = "Enabled"`

#### Scenario: F8 Policies given
- **WHEN** subnet create runs with private endpoint network policies `Enabled` and private link service network policies `Disabled`
- **THEN** the body sends `properties.privateEndpointNetworkPolicies = "Enabled"` and `properties.privateLinkServiceNetworkPolicies = "Disabled"`

### Requirement: Deprecated disable network policy flags
On the CLI surface, subnet create SHALL map `--disable-private-endpoint-network-policies` and `--disable-private-link-service-network-policies` to `Disabled` when true or given with no value and to `Enabled` when false, SHALL let that value win over `--private-endpoint-network-policies` or `--private-link-service-network-policies`, and SHALL write a deprecation warning when either flag is given.

#### Scenario: F9 Disable private endpoint policies true
- **WHEN** subnet create runs on the CLI surface with `--disable-private-endpoint-network-policies true` and `--private-endpoint-network-policies Enabled`
- **THEN** the body sends `properties.privateEndpointNetworkPolicies = "Disabled"` and a deprecation warning is written

#### Scenario: F9 Disable private link service policies false
- **WHEN** subnet create runs on the CLI surface with `--disable-private-link-service-network-policies false`
- **THEN** the body sends `properties.privateLinkServiceNetworkPolicies = "Enabled"` and a deprecation warning is written

#### Scenario: F9 Flag given with no value
- **WHEN** subnet create runs on the CLI surface with `--disable-private-link-service-network-policies` and no value
- **THEN** the body sends `properties.privateLinkServiceNetworkPolicies = "Disabled"` and a deprecation warning is written
