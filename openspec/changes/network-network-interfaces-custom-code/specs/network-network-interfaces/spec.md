# Spec Delta

## Purpose

Defines how the Network extension's network interface commands (create, show, list, delete) shape their requests and route between REST operations on the CLI and PowerShell surfaces.

## ADDED Requirements

### Requirement: Single IP configuration from flat create inputs
Network interface create SHALL send NetworkInterfaces_CreateOrUpdate with exactly one IP configuration named `ipconfig1`, built from the given subnet ID, the public IP address ID when given, and the private IP address with `Static` allocation when given, else `Dynamic` allocation.

#### Scenario: F1 One IP configuration named ipconfig1
- **WHEN** network interface create runs with subnet ID `<subnet-id>` and no IP configuration name
- **THEN** the NetworkInterfaces_CreateOrUpdate body sends `properties.ipConfigurations` with exactly one entry, named `ipconfig1`
- **AND** that entry sends `properties.subnet.id = "<subnet-id>"`

#### Scenario: F1 Public IP address given
- **WHEN** network interface create runs with public IP address ID `<pip-id>`
- **THEN** the IP configuration sends `properties.publicIPAddress.id = "<pip-id>"`

#### Scenario: F1 No public IP address
- **WHEN** network interface create runs without a public IP address
- **THEN** the IP configuration sends no `publicIPAddress`

#### Scenario: F1 Private IP address given
- **WHEN** network interface create runs with private IP address `<ip>`
- **THEN** the IP configuration sends `properties.privateIPAddress = "<ip>"` and `properties.privateIPAllocationMethod = "Static"`

#### Scenario: F1 No private IP address
- **WHEN** network interface create runs without a private IP address
- **THEN** the IP configuration sends `properties.privateIPAllocationMethod = "Dynamic"` and no `privateIPAddress`

### Requirement: CLI name-or-ID expansion for create references
The CLI network interface create SHALL use a `--subnet` value starting with `/` unchanged, and SHALL expand a `--subnet` name given with `--vnet-name`, a `--public-ip-address` name, or a `--network-security-group` name into its `Microsoft.Network` resource ID in the command's subscription and resource group, sending no extra REST call.

#### Scenario: F3 Subnet given as an ID
- **WHEN** CLI network interface create runs with `--subnet <subnet-id>` where the value starts with `/`
- **THEN** the IP configuration sends `properties.subnet.id = "<subnet-id>"` unchanged

#### Scenario: F3 Subnet given as a name with the virtual network name
- **WHEN** CLI network interface create runs in subscription `<sub>` and resource group `<rg>` with `--subnet <subnet>` and `--vnet-name <vnet>`
- **THEN** the IP configuration sends `properties.subnet.id = "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Network/virtualNetworks/<vnet>/subnets/<subnet>"`
- **AND** no request other than NetworkInterfaces_CreateOrUpdate is sent

#### Scenario: F3 Public IP address given as a name
- **WHEN** CLI network interface create runs in subscription `<sub>` and resource group `<rg>` with `--public-ip-address <pip>`
- **THEN** the IP configuration sends `properties.publicIPAddress.id = "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Network/publicIPAddresses/<pip>"`

#### Scenario: F3 Network security group given as a name
- **WHEN** CLI network interface create runs in subscription `<sub>` and resource group `<rg>` with `--network-security-group <nsg>`
- **THEN** the NetworkInterfaces_CreateOrUpdate body sends `properties.networkSecurityGroup.id = "/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Network/networkSecurityGroups/<nsg>"`

### Requirement: Edge zone as extended location
Network interface create SHALL send `extendedLocation = { "name": <edge zone>, "type": "EdgeZone" }` when an edge zone is given, and SHALL send no `extendedLocation` when none is given.

#### Scenario: F4 Edge zone given
- **WHEN** network interface create runs with edge zone `<edge-zone>`
- **THEN** the NetworkInterfaces_CreateOrUpdate body sends `extendedLocation = { "name": "<edge-zone>", "type": "EdgeZone" }`

#### Scenario: F4 No edge zone
- **WHEN** network interface create runs without an edge zone
- **THEN** the NetworkInterfaces_CreateOrUpdate body sends no `extendedLocation`

### Requirement: CLI private IP address version
The CLI network interface create SHALL send the given `--private-ip-address-version` (`IPv4` or `IPv6`) as `properties.privateIPAddressVersion` of the IP configuration.

#### Scenario: F8 Private IP address version given
- **WHEN** CLI network interface create runs with `--private-ip-address-version <version>` where `<version>` is `IPv4` or `IPv6`
- **THEN** the IP configuration sends `properties.privateIPAddressVersion = "<version>"`

### Requirement: PowerShell IP configuration name
PowerShell `New-AzNetworkInterface` SHALL name the single IP configuration with the given `-IpConfigurationName` instead of `ipconfig1`.

#### Scenario: F9 IP configuration name given
- **WHEN** `New-AzNetworkInterface` runs with `-IpConfigurationName <ipconfig>`
- **THEN** the NetworkInterfaces_CreateOrUpdate body sends exactly one IP configuration, named `<ipconfig>`

### Requirement: List scope by resource group presence
Network interface list SHALL send NetworkInterfaces_List for the given resource group when a resource group is given, SHALL send NetworkInterfaces_ListAll for the subscription when none is given, and SHALL follow every `nextLink` page and return all network interfaces as one list.

#### Scenario: F17 Resource group given
- **WHEN** network interface list runs with resource group `<rg>`
- **THEN** Azclips sends NetworkInterfaces_List for `<rg>`

#### Scenario: F17 No resource group
- **WHEN** network interface list runs without a resource group
- **THEN** Azclips sends NetworkInterfaces_ListAll for the subscription

#### Scenario: F17 Multiple pages
- **WHEN** a list response has a `nextLink`
- **THEN** Azclips follows every page and returns all network interfaces as one list

### Requirement: PowerShell remove confirmation and PassThru
PowerShell `Remove-AzNetworkInterface` SHALL ask the user to confirm unless `-Force` is given and SHALL send no NetworkInterfaces_Delete when the user declines; after a completed delete it SHALL write `$true` when `-PassThru` is given and nothing otherwise.

#### Scenario: F22 Declined confirmation
- **WHEN** `Remove-AzNetworkInterface` runs without `-Force` and the user declines the confirmation
- **THEN** no NetworkInterfaces_Delete request is sent

#### Scenario: F22 Force skips confirmation
- **WHEN** `Remove-AzNetworkInterface` runs with `-Force`
- **THEN** the user is not asked to confirm and NetworkInterfaces_Delete is sent

#### Scenario: F22 PassThru after delete
- **WHEN** the delete completes and `-PassThru` is given
- **THEN** the cmdlet writes `$true`

#### Scenario: F22 No PassThru
- **WHEN** the delete completes and `-PassThru` is not given
- **THEN** the cmdlet writes nothing
