# network-network-security-groups Specification

## Purpose
Defines how the Network extension creates, lists and removes network security groups, beyond the plain REST operations: location defaulting, the create request surface, list scope routing and the PowerShell remove experience.

## Requirements
### Requirement: Default the location from the resource group on create
Network security group create SHALL use the given Location as the NSG `location`. When Location is omitted, it SHALL read the resource group and use the resource group's location.

#### Scenario: Location is given
- **WHEN** network security group create runs with Location
- **THEN** Azclips SHALL send that value as `location` in the PUT body
- **AND** Azclips SHALL NOT read the resource group

#### Scenario: Location is omitted
- **WHEN** network security group create runs without Location
- **THEN** Azclips SHALL first send `GET /subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroupName}` (Microsoft.Resources)
- **AND** Azclips SHALL send the returned resource-group `location` as `location` in the NSG PUT body

#### Scenario: Resource group read fails
- **WHEN** network security group create runs without Location and the resource-group GET fails (for example, the resource group does not exist)
- **THEN** Azclips SHALL raise that service error
- **AND** Azclips SHALL send no NSG PUT

### Requirement: Create a network security group with location and tags only
Network security group create SHALL send one long-running PUT whose body carries only `location` and, when Tags are given, `tags`, and SHALL output the final NSG as the bare REST resource.

#### Scenario: Request carries only location and tags
- **WHEN** network security group create runs
- **THEN** Azclips SHALL send `PUT /subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkSecurityGroups/{Name}?api-version=2026-03-01`
- **AND** the body SHALL carry `location`, plus `tags` only when Tags are given
- **AND** the body SHALL carry no `id`, no `properties.flushConnection`, and no `properties.securityRules`

#### Scenario: Long-running create outputs the bare final resource
- **WHEN** the PUT starts a long-running operation
- **THEN** Azclips SHALL poll it to a terminal state
- **AND** Azclips SHALL output the final NSG read from the resource URI as the bare REST resource, including `properties.*` such as the service-generated `defaultSecurityRules`, with no wrapper key

#### Scenario: An NSG with the same name already exists
- **WHEN** network security group create runs and an NSG with that name already exists
- **THEN** Azclips SHALL send the same PUT without an existence check or a prompt, so the service applies its create-or-replace semantics

#### Scenario: Create fails
- **WHEN** the PUT or the long-running operation fails
- **THEN** Azclips SHALL raise the service error

### Requirement: List network security groups by resource group or subscription
Network security group list SHALL list the given resource group when ResourceGroupName is given and the whole subscription otherwise, and SHALL output every NSG from every page as a bare resource.

#### Scenario: Resource group is given
- **WHEN** network security group list runs with ResourceGroupName
- **THEN** Azclips SHALL send `GET /subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkSecurityGroups?api-version=2026-03-01`
- **AND** Azclips SHALL NOT call the subscription-wide list

#### Scenario: Resource group is omitted
- **WHEN** network security group list runs without ResourceGroupName
- **THEN** Azclips SHALL send `GET /subscriptions/{SubscriptionId}/providers/Microsoft.Network/networkSecurityGroups?api-version=2026-03-01`
- **AND** Azclips SHALL NOT call the resource-group list

#### Scenario: Results span several pages
- **WHEN** a list response carries `nextLink`
- **THEN** Azclips SHALL send a GET to each `nextLink` in turn
- **AND** Azclips SHALL output every NSG from every page as a bare resource

#### Scenario: Scope contains no NSGs
- **WHEN** network security group list runs and the scope contains no NSGs
- **THEN** Azclips SHALL output no items and raise no error

### Requirement: Confirm before removing a network security group in PowerShell
`Remove-AzNetworkSecurityGroup` SHALL support `-WhatIf` and `-Confirm`, and SHALL prompt for confirmation before deleting unless `-Force` is given. The CLI delete command SHALL NOT gain this behavior.

#### Scenario: WhatIf sends no delete
- **WHEN** `Remove-AzNetworkSecurityGroup` runs with `-WhatIf`
- **THEN** Azclips SHALL send no DELETE
- **AND** Azclips SHALL only report what would be removed

#### Scenario: Prompt without Force
- **WHEN** `Remove-AzNetworkSecurityGroup` runs without `-Force`
- **THEN** Azclips SHALL prompt for confirmation
- **AND** Azclips SHALL send the DELETE only if the user confirms

#### Scenario: Force skips the prompt
- **WHEN** `Remove-AzNetworkSecurityGroup` runs with `-Force`
- **THEN** Azclips SHALL send the DELETE without prompting

### Requirement: Write true after removal with PassThru in PowerShell
`Remove-AzNetworkSecurityGroup` SHALL write `$true` after a successful delete when `-PassThru` is given, and SHALL write nothing otherwise. The CLI delete command SHALL NOT gain this behavior.

#### Scenario: PassThru after a successful delete
- **WHEN** `Remove-AzNetworkSecurityGroup -PassThru` completes the delete successfully
- **THEN** Azclips SHALL write `$true`

#### Scenario: No PassThru
- **WHEN** `Remove-AzNetworkSecurityGroup` completes the delete without `-PassThru`
- **THEN** Azclips SHALL write nothing
