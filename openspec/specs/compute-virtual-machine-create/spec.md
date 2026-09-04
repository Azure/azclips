## Purpose

Define Azclips Compute VM create behavior for simple VM creation across CLI `vm create` and PowerShell `New-AzVM`, including dependent resource orchestration, request-body defaults, selected network behavior, identity mapping, and CLI-only integrity monitoring.

## Requirements

### Requirement: VM create orchestrates dependent resources
`vm create` and `New-AzVM` SHALL create the dependent resources needed by a simple VM create request before creating the virtual machine. The orchestrated resources SHALL include the virtual network, subnet, network security group, network interface, and any requested public IP address.

#### Scenario: Simple create builds dependencies before VM
- **WHEN** a VM create request does not supply an existing network interface or VM configuration object
- **THEN** Azclips SHALL create or prepare the virtual network, subnet, network security group, and network interface before creating the VM
- **AND** the VM request SHALL reference the orchestrated primary network interface

#### Scenario: Explicit supporting resource values are preserved
- **WHEN** a VM create request supplies explicit names, prefixes, rules, or IDs for supporting resources
- **THEN** Azclips SHALL use those explicit values when orchestrating dependent resources

### Requirement: VM create controls public IP attachment
`vm create` and `New-AzVM` SHALL support VM creation with or without a public IP address. Public IP creation SHALL be opt-in through public IP intent such as a public IP name, domain label, or equivalent surface parameter.

#### Scenario: Public IP is omitted
- **WHEN** a VM create request does not request a public IP address
- **THEN** Azclips SHALL create the VM network interface without a public IP address reference

#### Scenario: Public IP is requested
- **WHEN** a VM create request requests a public IP address
- **THEN** Azclips SHALL create or use the requested public IP address before creating the network interface
- **AND** the network interface SHALL reference that public IP address
- **AND** an orchestrated public IP address SHALL default to Standard SKU and static allocation unless an explicit supported value overrides the default

#### Scenario: Public IP is explicitly disabled
- **WHEN** a VM create request explicitly disables public IP creation
- **THEN** Azclips SHALL create the VM network interface without a public IP address reference

### Requirement: VM create applies default image and TrustedLaunch security
`vm create` and `New-AzVM` SHALL apply a default Windows Server image and TrustedLaunch security profile when the caller omits image and security settings.

#### Scenario: Image and security are omitted
- **WHEN** a VM create request does not supply image publisher, offer, SKU, version, image ID, or security type
- **THEN** the VM request SHALL use publisher `MicrosoftWindowsServer`, offer `WindowsServer`, SKU `2022-datacenter-azure-edition`, and version `latest`
- **AND** the VM request SHALL set security type `TrustedLaunch`
- **AND** secure boot SHALL be enabled
- **AND** vTPM SHALL be enabled

#### Scenario: Image is supplied explicitly
- **WHEN** a VM create request supplies explicit image publisher, offer, SKU, version, or image ID
- **THEN** Azclips SHALL use the explicit image values unchanged

### Requirement: VM create honors Standard security opt-out
`vm create` and `New-AzVM` SHALL allow callers to explicitly request Standard security type. A Standard security request SHALL prevent TrustedLaunch security settings from being added while preserving image defaults when image settings are otherwise omitted.

#### Scenario: Standard security is requested without explicit image
- **WHEN** a VM create request sets security type to `Standard`
- **AND** the request does not supply image publisher, offer, SKU, version, or image ID
- **THEN** the VM request SHALL use publisher `MicrosoftWindowsServer`, offer `WindowsServer`, SKU `2022-datacenter-azure-edition`, and version `latest`
- **AND** the VM request SHALL omit the security profile

#### Scenario: Standard security is requested with explicit image
- **WHEN** a VM create request sets security type to `Standard`
- **AND** the request supplies explicit image values
- **THEN** Azclips SHALL use the explicit image values unchanged
- **AND** the VM request SHALL omit the security profile

### Requirement: VM create controls boot diagnostics
`vm create` and `New-AzVM` SHALL map boot diagnostics input into the VM request body. Boot diagnostics SHALL be enabled with the requested storage URI or storage account endpoint when enabled, and SHALL be disabled when explicitly disabled.

#### Scenario: Boot diagnostics is enabled with storage
- **WHEN** a VM create request enables boot diagnostics with a storage URI or storage account endpoint
- **THEN** the VM request SHALL set boot diagnostics enabled to `true`
- **AND** the VM request SHALL include the requested boot diagnostics storage URI

#### Scenario: Boot diagnostics is explicitly disabled
- **WHEN** a VM create request disables boot diagnostics
- **THEN** the VM request SHALL set boot diagnostics enabled to `false`

#### Scenario: Boot diagnostics input is omitted
- **WHEN** a VM create request does not include boot diagnostics input
- **THEN** Azclips SHALL use the VM create default established by the Compute processor contract

### Requirement: VM create sets accelerated networking conditionally
`vm create` and `New-AzVM` SHALL not unconditionally enable accelerated networking. Azclips SHALL honor explicit accelerated networking input and otherwise enable accelerated networking only when support is known for the selected VM size and image.

#### Scenario: Accelerated networking is explicitly enabled
- **WHEN** a VM create request explicitly enables accelerated networking
- **THEN** the network interface request SHALL set accelerated networking to `true`

#### Scenario: Accelerated networking is explicitly disabled
- **WHEN** a VM create request explicitly disables accelerated networking
- **THEN** the network interface request SHALL set accelerated networking to `false`

#### Scenario: Accelerated networking support is not known
- **WHEN** a VM create request omits accelerated networking input
- **AND** Azclips cannot determine that the selected VM size and image support accelerated networking
- **THEN** the network interface request SHALL not enable accelerated networking

#### Scenario: Accelerated networking support is known
- **WHEN** a VM create request omits accelerated networking input
- **AND** Azclips determines that the selected VM size and image support accelerated networking
- **THEN** the network interface request SHALL enable accelerated networking

### Requirement: VM create derives omitted simple-create names
`vm create` and `New-AzVM` SHALL derive omitted simple-create resource names from the VM name when a surface allows those values to be omitted. Derived names SHALL remain deterministic, and explicit caller-supplied names SHALL take precedence.

#### Scenario: Supporting names are omitted
- **WHEN** a VM create request supplies VM name `myvm`
- **AND** a supported surface omits one or more derived supporting resource names
- **THEN** Azclips SHALL derive the omitted supporting names from `myvm`
- **AND** Azclips SHALL preserve the derived network names required by the base `compute-virtual-machine-create` spec

#### Scenario: Supporting names are supplied
- **WHEN** a VM create request supplies explicit supporting resource names
- **THEN** Azclips SHALL use the explicit names unchanged

### Requirement: VM create maps managed identity input
`vm create` and `New-AzVM` SHALL include managed identity settings in the VM request when identity input is supplied. When identity input is omitted, the VM request SHALL omit identity settings.

#### Scenario: System-assigned identity is requested
- **WHEN** a VM create request requests a system-assigned managed identity
- **THEN** the VM request SHALL include system-assigned identity settings

#### Scenario: User-assigned identity is requested
- **WHEN** a VM create request requests one or more user-assigned managed identities
- **THEN** the VM request SHALL include user-assigned identity settings for each requested identity resource ID

#### Scenario: Identity input is omitted
- **WHEN** a VM create request does not include identity input
- **THEN** the VM request SHALL omit identity settings

### Requirement: CLI VM create supports integrity monitoring
CLI `vm create` SHALL support integrity monitoring as a CLI-only VM create behavior. When requested, Azclips SHALL configure the VM for integrity monitoring by creating the required guest attestation extension after VM creation. This behavior SHALL NOT change `New-AzVM` behavior.

#### Scenario: CLI integrity monitoring is requested
- **WHEN** a CLI `vm create` request enables integrity monitoring
- **THEN** Azclips SHALL create the VM with TrustedLaunch-compatible security settings
- **AND** Azclips SHALL create the guest attestation extension for the VM after the VM exists
- **AND** the CLI command result SHALL still represent the VM create operation

#### Scenario: Integrity monitoring is omitted
- **WHEN** a CLI `vm create` request omits integrity monitoring input
- **THEN** Azclips SHALL NOT create the guest attestation extension as part of VM create

#### Scenario: PowerShell VM create is used
- **WHEN** `New-AzVM` creates a VM
- **THEN** Azclips SHALL NOT add CLI-only integrity monitoring behavior to the PowerShell command

### Requirement: VM create derives omitted network resource names

`vm create` and `New-AzVM` SHALL allow callers to omit the orchestrated network resource names that can be derived from the VM name.

#### Scenario: Caller omits supporting network resource names

- **GIVEN** a VM create request with `Name` set to `myvm`
- **AND** no network interface name, virtual network name, network security group name, or subnet name is supplied
- **WHEN** Azclips orchestrates the supporting resources
- **THEN** the virtual network name is `myvmVNET`
- **AND** the subnet name is `myvmSubnet`
- **AND** the network security group name is `myvmNSG`
- **AND** the network interface name is `myvmVMNic`

#### Scenario: Caller supplies supporting network resource names

- **GIVEN** a VM create request with explicit network interface, virtual network, network security group, and subnet names
- **WHEN** Azclips orchestrates the supporting resources
- **THEN** the explicit names are used unchanged

### Requirement: VM create applies default network prefixes

`vm create` and `New-AzVM` SHALL fill omitted VNet and subnet address prefixes with shared Azclips defaults.

#### Scenario: Caller omits network prefixes

- **GIVEN** a VM create request with no virtual network address prefixes
- **AND** no subnet address prefixes
- **WHEN** Azclips creates the virtual network
- **THEN** the virtual network address prefixes include `10.0.0.0/16`
- **AND** the derived or supplied subnet includes address prefix `10.0.0.0/24`

#### Scenario: Caller supplies network prefixes

- **GIVEN** a VM create request with explicit virtual network and subnet address prefixes
- **WHEN** Azclips creates the virtual network
- **THEN** the explicit prefixes are used unchanged

### Requirement: VM create supplies Linux SSH defaults when password authentication is not used

`vm create` and `New-AzVM` SHALL support a Linux/SSH creation path from shared orchestration when callers do not provide password authentication.

#### Scenario: Caller creates a Linux SSH VM without explicit image values

- **GIVEN** a VM create request without an admin password
- **AND** no explicit image publisher, offer, SKU, or version values are supplied
- **WHEN** Azclips creates the virtual machine
- **THEN** the VM image defaults to a Linux image supported by the current Compute processor contract
- **AND** the NSG defaults include an inbound allow rule for TCP port 22

#### Scenario: Caller supplies explicit image values

- **GIVEN** a VM create request with explicit image publisher, offer, SKU, or version values
- **WHEN** Azclips creates the virtual machine
- **THEN** the explicit image values are used unchanged