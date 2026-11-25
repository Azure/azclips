# Spec Delta

## Purpose

Defines how the Network extension's virtual network commands (create, show, list, delete) shape their requests and route between REST operations on the CLI and PowerShell surfaces.

## ADDED Requirements

### Requirement: Default virtual network address prefix
Virtual network create SHALL send `properties.addressSpace.addressPrefixes = ["10.0.0.0/16"]` in the VirtualNetworks_CreateOrUpdate body when neither address prefixes nor IPAM pool allocations are given, and SHALL send exactly the given prefixes when address prefixes are given.

#### Scenario: F1 No address prefixes and no IPAM allocations
- **WHEN** virtual network create runs on the CLI or PowerShell surface without address prefixes and without IPAM pool allocations
- **THEN** the VirtualNetworks_CreateOrUpdate body sends `properties.addressSpace.addressPrefixes = ["10.0.0.0/16"]`

#### Scenario: F1 Address prefixes given
- **WHEN** virtual network create runs with address prefixes `10.1.0.0/16` and `10.2.0.0/16`
- **THEN** the body sends `properties.addressSpace.addressPrefixes = ["10.1.0.0/16", "10.2.0.0/16"]` and no other prefix

### Requirement: IPAM pool allocations replace address prefixes
When IPAM pool allocations are given, virtual network create SHALL send `properties.addressSpace.ipamPoolPrefixAllocations` with one entry per allocation and SHALL send no address prefixes and no default prefix.

#### Scenario: F2 IPAM allocations given
- **WHEN** virtual network create runs with IPAM pool allocations
- **THEN** the body sends `properties.addressSpace.ipamPoolPrefixAllocations` with one entry per allocation, carrying `pool.id`, and `numberOfIpAddresses` when given

#### Scenario: F2 IPAM allocations and address prefixes both given
- **WHEN** virtual network create runs with IPAM pool allocations and also with address prefixes
- **THEN** the body sends no `properties.addressSpace.addressPrefixes` and no default prefix

### Requirement: Edge zone maps to extended location
Virtual network create SHALL send `extendedLocation = { name: <edge zone>, type: "EdgeZone" }` when an edge zone name is given, and SHALL send no `extendedLocation` otherwise.

#### Scenario: F3 Edge zone given
- **WHEN** virtual network create runs with edge zone name `<edge-zone>`
- **THEN** the body sends `extendedLocation = { name: "<edge-zone>", type: "EdgeZone" }`

#### Scenario: F3 No edge zone
- **WHEN** virtual network create runs without an edge zone
- **THEN** the body sends no `extendedLocation`

### Requirement: Inline subnet definitions
Virtual network create SHALL send `properties.subnets` with one entry per given subnet definition, each carrying its `name` and the subnet properties given for it, and SHALL send no `properties.subnets` when no subnet definitions are given.

#### Scenario: F4 Subnet definitions given
- **WHEN** virtual network create runs with subnet definitions
- **THEN** the body sends `properties.subnets` with one entry per definition, each carrying its `name` and the subnet properties given for it (address prefix or prefixes, NSG, route table, NAT gateway, delegations, service endpoints, network policies, default outbound access)

#### Scenario: F4 No subnet definitions
- **WHEN** virtual network create runs without subnet definitions
- **THEN** the body sends no `properties.subnets`

### Requirement: Subnet from a name plus prefixes
When a subnet name is given with subnet prefixes, virtual network create SHALL send one subnet carrying `properties.addressPrefix` for exactly one prefix, or `properties.addressPrefixes` for several prefixes.

#### Scenario: F5 One subnet prefix
- **WHEN** virtual network create runs with subnet name `<subnet>` and exactly one subnet prefix `10.0.0.0/24`
- **THEN** the body sends one subnet `{ name: "<subnet>", properties.addressPrefix: "10.0.0.0/24" }`

#### Scenario: F5 Several subnet prefixes
- **WHEN** virtual network create runs with subnet name `<subnet>` and subnet prefixes `10.0.0.0/24` and `10.0.1.0/24`
- **THEN** the body sends one subnet `{ name: "<subnet>", properties.addressPrefixes: ["10.0.0.0/24", "10.0.1.0/24"] }`

### Requirement: IP allocation references
Virtual network create SHALL send `properties.ipAllocations = [{ id: <each ID> }]` when IP allocation IDs are given.

#### Scenario: F22 IP allocation IDs given
- **WHEN** virtual network create runs on the CLI or PowerShell surface with IP allocation IDs `<id1>` and `<id2>`
- **THEN** the body sends `properties.ipAllocations = [{ id: "<id1>" }, { id: "<id2>" }]`

### Requirement: List scope by resource group presence
Virtual network list SHALL call VirtualNetworks_List for the given resource group when a resource group is given, and VirtualNetworks_ListAll for the subscription otherwise, returning all pages in both cases.

#### Scenario: F10 List with a resource group
- **WHEN** virtual network list runs on the CLI or PowerShell (list) surface with resource group `<rg>`
- **THEN** Azclips calls VirtualNetworks_List for `<rg>` and returns the virtual networks of all pages

#### Scenario: F10 List without a resource group
- **WHEN** virtual network list runs without a resource group
- **THEN** Azclips calls VirtualNetworks_ListAll for the subscription and returns the virtual networks of all pages

### Requirement: PowerShell remove confirmation and PassThru
On the PowerShell surface, virtual network remove SHALL ask the user to confirm before VirtualNetworks_Delete unless `-Force` is given, SHALL send no DELETE when the prompt is declined, and SHALL write `$true` when `-PassThru` is given and the delete succeeds.

#### Scenario: F18 Remove without Force, prompt declined
- **WHEN** `Remove-AzVirtualNetwork` runs without `-Force` and the user declines the confirmation
- **THEN** no VirtualNetworks_Delete request is sent

#### Scenario: F18 Remove with PassThru
- **WHEN** `Remove-AzVirtualNetwork` runs with `-PassThru` and the delete succeeds
- **THEN** it writes `$true`
