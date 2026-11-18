# network-public-ip-addresses Specification

## Purpose
Defines the custom behavior of the Network public IP address commands: input defaults and request body shaping on create, list scope routing, and the PowerShell remove confirmation.

## Requirements
### Requirement: Default the location from the resource group on create
Public IP address create SHALL use the resource group's location as the PUT body `location` when `Location` is omitted, reading the resource group before the PUT. When `Location` is given, it SHALL be sent as is and no resource group request SHALL be sent.

#### Scenario: Location omitted uses the resource group location
- **WHEN** public IP address create runs without `Location`
- **THEN** Azclips SHALL send `GET /subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}` (Microsoft.Resources) before the PUT
- **AND** the PUT body `location` SHALL be the resource group's `location`

#### Scenario: Location given sends no resource group request
- **WHEN** public IP address create runs with `Location`
- **THEN** Azclips SHALL send no resource group request
- **AND** the PUT body `location` SHALL be the given value

#### Scenario: Resource group lookup fails
- **WHEN** public IP address create runs without `Location` and the resource group GET fails (for example 404 `ResourceGroupNotFound`)
- **THEN** the command SHALL fail with that service error
- **AND** no PUT SHALL be sent

### Requirement: Default the SKU to Standard on create
Public IP address create SHALL send `sku.name = "Standard"` when `Sku` is omitted and the given value otherwise. `sku.tier` SHALL be sent only when `Tier` is given.

#### Scenario: Sku omitted
- **WHEN** public IP address create runs without `Sku`
- **THEN** the PUT body SHALL contain `sku.name = "Standard"`

#### Scenario: Sku given
- **WHEN** public IP address create runs with `Sku` set to `Basic`, `Standard`, or `StandardV2`
- **THEN** the PUT body `sku.name` SHALL be the given value

#### Scenario: Tier given
- **WHEN** public IP address create runs with `Tier`
- **THEN** the PUT body `sku.tier` SHALL be the given value alongside the effective `sku.name`

#### Scenario: Tier omitted
- **WHEN** public IP address create runs without `Tier`
- **THEN** the PUT body SHALL NOT contain `sku.tier`

### Requirement: Derive the allocation method from the SKU on create
When `AllocationMethod` is omitted, public IP address create SHALL send `properties.publicIPAllocationMethod = "Static"` if the effective SKU is `Standard` or `StandardV2` (case-insensitive) and `"Dynamic"` if it is `Basic`. A given `AllocationMethod` SHALL be sent as is, whatever the SKU.

#### Scenario: Standard or StandardV2 SKU without allocation method
- **WHEN** public IP address create runs without `AllocationMethod` and the effective SKU (after the SKU default) is `Standard` or `StandardV2` (case-insensitive)
- **THEN** the PUT body SHALL contain `properties.publicIPAllocationMethod = "Static"`

#### Scenario: Basic SKU without allocation method
- **WHEN** public IP address create runs without `AllocationMethod` and the effective SKU is `Basic`
- **THEN** the PUT body SHALL contain `properties.publicIPAllocationMethod = "Dynamic"`

#### Scenario: Allocation method given
- **WHEN** public IP address create runs with `AllocationMethod`
- **THEN** the PUT body `properties.publicIPAllocationMethod` SHALL be the given value, whatever the SKU
- **AND** an invalid SKU/method combination SHALL be reported by the service error

### Requirement: Default the IP version to IPv4 on create
Public IP address create SHALL send `properties.publicIPAddressVersion = "IPv4"` when `Version` is omitted and the given value otherwise.

#### Scenario: Version omitted
- **WHEN** public IP address create runs without `Version`
- **THEN** the PUT body SHALL contain `properties.publicIPAddressVersion = "IPv4"`

#### Scenario: Version given
- **WHEN** public IP address create runs with `Version`
- **THEN** the PUT body `properties.publicIPAddressVersion` SHALL be the given value

### Requirement: Send DNS settings only when a DNS input is given on create
Public IP address create SHALL send `properties.dnsSettings` only when at least one of `DomainNameLabel`, `DomainNameLabelScope`, `ReverseFqdn` is given, containing only the given fields. `fqdn` SHALL never be sent.

#### Scenario: No DNS input
- **WHEN** public IP address create runs with none of `DomainNameLabel`, `DomainNameLabelScope`, `ReverseFqdn`
- **THEN** the PUT body SHALL contain no `properties.dnsSettings` object

#### Scenario: Some DNS inputs given
- **WHEN** public IP address create runs with any of `DomainNameLabel`, `DomainNameLabelScope`, `ReverseFqdn`
- **THEN** the PUT body SHALL contain `properties.dnsSettings` with only the given fields (`domainNameLabel`, `domainNameLabelScope`, `reverseFqdn`)
- **AND** `fqdn` SHALL NOT be sent

### Requirement: Set an EdgeZone extended location from EdgeZone on create
Public IP address create SHALL send `extendedLocation` with the given name and type `EdgeZone` when `EdgeZone` is given, and no `extendedLocation` otherwise.

#### Scenario: EdgeZone given
- **WHEN** public IP address create runs with `EdgeZone`
- **THEN** the PUT body SHALL contain `extendedLocation = { "name": <EdgeZone>, "type": "EdgeZone" }`

#### Scenario: EdgeZone omitted
- **WHEN** public IP address create runs without `EdgeZone`
- **THEN** the PUT body SHALL contain no `extendedLocation`

### Requirement: Route the list scope by resource group
Public IP address list SHALL list the given resource group when `ResourceGroupName` is given and the whole subscription when it is omitted, following every `nextLink` and outputting each public IP address as a bare resource.

#### Scenario: Resource group given
- **WHEN** public IP address list runs with `ResourceGroupName`
- **THEN** Azclips SHALL send `GET /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/publicIPAddresses`
- **AND** Azclips SHALL follow every `nextLink`
- **AND** each public IP address SHALL be output as a bare resource

#### Scenario: Resource group omitted
- **WHEN** public IP address list runs without `ResourceGroupName`
- **THEN** Azclips SHALL send `GET /subscriptions/{subscriptionId}/providers/Microsoft.Network/publicIPAddresses`
- **AND** Azclips SHALL follow every `nextLink`
- **AND** each public IP address SHALL be output as a bare resource

### Requirement: Confirm before removing a public IP address in PowerShell
`Remove-AzPublicIpAddress` SHALL ask for confirmation before deleting unless `-Force` is given, and SHALL output `$true` after a successful delete when `-PassThru` is given. This behavior is PowerShell-only.

#### Scenario: Remove without Force, user declines
- **WHEN** `Remove-AzPublicIpAddress` runs without `-Force` and the user declines the confirmation
- **THEN** PowerShell SHALL have asked for confirmation
- **AND** no DELETE SHALL be sent

#### Scenario: Remove with Force
- **WHEN** `Remove-AzPublicIpAddress` runs with `-Force`
- **THEN** it SHALL delete without prompting

#### Scenario: Remove with PassThru
- **WHEN** `Remove-AzPublicIpAddress` runs with `-PassThru` and the delete succeeds
- **THEN** the cmdlet SHALL output `$true`
