# Spec Delta

## Purpose

Defines how the Network extension's network security group commands (create, show, list, delete) shape their requests and route between REST operations on the CLI and PowerShell surfaces.

## ADDED Requirements

### Requirement: Default location from the resource group
Network security group create SHALL, when no location is given, send ResourceGroups_Get for the target resource group and use its `location` as the `location` of the NetworkSecurityGroups_CreateOrUpdate body. When a location is given, it SHALL send no resource-group GET and SHALL put the given location in the body. When the resource-group GET fails, it SHALL report that error and SHALL send no PUT.

#### Scenario: F1 No location given
- **WHEN** network security group create runs without a location for resource group `<rg>`
- **THEN** Azclips sends `GET /subscriptions/<sub>/resourceGroups/<rg>` (ResourceGroups_Get)
- **AND** the NetworkSecurityGroups_CreateOrUpdate body sends `location` equal to the `location` returned by that GET

#### Scenario: F1 Location given
- **WHEN** network security group create runs with location `<location>`
- **THEN** Azclips sends no resource-group GET
- **AND** the NetworkSecurityGroups_CreateOrUpdate body sends `location = "<location>"`

#### Scenario: F1 Resource-group GET fails
- **WHEN** network security group create runs without a location and the resource-group GET fails, for example with 404
- **THEN** Azclips reports that error
- **AND** sends no NetworkSecurityGroups_CreateOrUpdate request

### Requirement: List scope by resource group presence
Network security group list SHALL call NetworkSecurityGroups_List for the resource group when a resource group is given, and NetworkSecurityGroups_ListAll for the subscription when it is not, and SHALL output every item of every page.

#### Scenario: F2 Resource group given
- **WHEN** network security group list runs on the CLI or PowerShell surface with resource group `<rg>`
- **THEN** Azclips sends `GET /subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Network/networkSecurityGroups` (NetworkSecurityGroups_List)
- **AND** outputs every item of every page

#### Scenario: F2 No resource group
- **WHEN** network security group list runs on the CLI or PowerShell surface without a resource group
- **THEN** Azclips sends `GET /subscriptions/<sub>/providers/Microsoft.Network/networkSecurityGroups` (NetworkSecurityGroups_ListAll)
- **AND** outputs every item of every page

### Requirement: CLI tags parsing on create
On the CLI surface, network security group create SHALL parse `--tags` values of the form `key[=value]` into the `tags` dictionary of the NetworkSecurityGroups_CreateOrUpdate body, with an empty string value for a key given without `=value`.

#### Scenario: F12 Tags with and without values
- **WHEN** network security group create runs on the CLI with `--tags a=b c`
- **THEN** the NetworkSecurityGroups_CreateOrUpdate body sends `tags = {"a": "b", "c": ""}`

### Requirement: PowerShell remove confirmation and PassThru
On the PowerShell surface, `Remove-AzNetworkSecurityGroup` SHALL ask for confirmation before NetworkSecurityGroups_Delete unless `-Force` is given, SHALL send no DELETE when the confirmation is declined, and SHALL output `true` after a completed delete only when `-PassThru` is given.

#### Scenario: F8 Confirmation declined
- **WHEN** `Remove-AzNetworkSecurityGroup` runs without `-Force` and the user declines the confirmation
- **THEN** Azclips sends no NetworkSecurityGroups_Delete request

#### Scenario: F8 PassThru after delete
- **WHEN** the NetworkSecurityGroups_Delete request completes and `-PassThru` is given
- **THEN** Azclips outputs `true`

#### Scenario: F8 No PassThru
- **WHEN** the NetworkSecurityGroups_Delete request completes and `-PassThru` is not given
- **THEN** Azclips outputs nothing
