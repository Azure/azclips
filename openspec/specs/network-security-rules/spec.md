# network-security-rules Specification

## Purpose
Defines how Azclips creates and updates Network security rules of a network security group on CLI and PowerShell: the request body each command sends, its defaults, and its output.

## Requirements
### Requirement: Security rule create sends the rule name in the body
Security rule create SHALL send `PUT` to `/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkSecurityGroups/{NetworkSecurityGroupName}/securityRules/{Name}?api-version=2026-03-01` with body `name` set to `Name`, SHALL wait for the long-running operation to finish, and SHALL output the bare `SecurityRule` the service returns.

#### Scenario: Rule name is sent in the PUT body
- **WHEN** a security rule is created on CLI or PowerShell with `Name` `<rule>`
- **THEN** Azclips SHALL send `PUT` to the rule URL with body `name` = `<rule>`
- **AND** Azclips SHALL wait for the long-running operation to finish

#### Scenario: Create outputs the bare security rule
- **WHEN** a security rule create finishes
- **THEN** Azclips SHALL output the bare `SecurityRule` returned by the service

### Requirement: Security rule create defaults access, direction and protocol
Security rule create SHALL send `properties.access` = `Allow`, `properties.direction` = `Inbound` and `properties.protocol` = `*` for each of `Access`, `Direction` and `Protocol` that is not given, and SHALL send a given value unchanged.

#### Scenario: Access defaults to Allow
- **WHEN** `Access` is not given on create
- **THEN** the PUT body SHALL have `properties.access` = `Allow`

#### Scenario: Direction defaults to Inbound
- **WHEN** `Direction` is not given on create
- **THEN** the PUT body SHALL have `properties.direction` = `Inbound`

#### Scenario: Protocol defaults to asterisk
- **WHEN** `Protocol` is not given on create
- **THEN** the PUT body SHALL have `properties.protocol` = `*`

#### Scenario: Given access, direction or protocol is sent unchanged
- **WHEN** `Access`, `Direction` or `Protocol` is given on create
- **THEN** the PUT body SHALL carry the given value unchanged

### Requirement: Security rule create defaults addresses and ports
Security rule create SHALL default the source port to `*`, the destination port to `80`, and the source and destination address prefixes to `*`. It SHALL NOT apply the address prefix default on a side whose application security group input is given.

#### Scenario: Source port defaults to asterisk
- **WHEN** `SourcePortRanges` is not given on create
- **THEN** the PUT body SHALL have `properties.sourcePortRange` = `*`

#### Scenario: Destination port defaults to 80
- **WHEN** `DestinationPortRanges` is not given on create
- **THEN** the PUT body SHALL have `properties.destinationPortRange` = `80`

#### Scenario: Source address prefix defaults to asterisk
- **WHEN** neither `SourceAddressPrefixes` nor `SourceAsgs` is given on create
- **THEN** the PUT body SHALL have `properties.sourceAddressPrefix` = `*`

#### Scenario: Destination address prefix defaults to asterisk
- **WHEN** neither `DestinationAddressPrefixes` nor `DestinationAsgs` is given on create
- **THEN** the PUT body SHALL have `properties.destinationAddressPrefix` = `*`

#### Scenario: No address prefix default on a side with application security groups
- **WHEN** `SourceAsgs` is given and `SourceAddressPrefixes` is not on create
- **THEN** the PUT body SHALL have no `sourceAddressPrefix` or `sourceAddressPrefixes`, only `sourceApplicationSecurityGroups`
- **AND** the same SHALL hold for the destination side when `DestinationAsgs` is given and `DestinationAddressPrefixes` is not

#### Scenario: Address prefix and application security groups on the same side
- **WHEN** both an address prefix input and an application security group input are given for the same side on create
- **THEN** the PUT body SHALL carry both as given
- **AND** Azclips SHALL return the service's error

### Requirement: Security rule create maps address and port lists to singular or plural properties
Security rule create SHALL send each of `SourceAddressPrefixes`, `SourcePortRanges`, `DestinationAddressPrefixes` and `DestinationPortRanges` on the matching REST singular property when it has exactly one item, and on the matching plural property when it has more than one item.

#### Scenario: One item goes to the singular property
- **WHEN** `SourceAddressPrefixes`, `SourcePortRanges`, `DestinationAddressPrefixes` or `DestinationPortRanges` has exactly one item on create
- **THEN** the PUT body SHALL set the matching singular property (`sourceAddressPrefix`, `sourcePortRange`, `destinationAddressPrefix`, `destinationPortRange`) to that item
- **AND** the PUT body SHALL omit the matching plural property

#### Scenario: More items go to the plural property
- **WHEN** one of these inputs has more than one item on create
- **THEN** the PUT body SHALL set the matching plural property (`sourceAddressPrefixes`, `sourcePortRanges`, `destinationAddressPrefixes`, `destinationPortRanges`) to the list
- **AND** the PUT body SHALL omit the matching singular property

### Requirement: Security rule application security groups by name or ID
Security rule create and update SHALL accept each `SourceAsgs` and `DestinationAsgs` item as an application security group name or full ARM resource ID, and SHALL send each item as an entry carrying only `id`, in the order given.

#### Scenario: ARM ID is sent unchanged
- **WHEN** a `SourceAsgs` or `DestinationAsgs` item is a full ARM resource ID on create or update
- **THEN** the request body's `properties.sourceApplicationSecurityGroups` / `properties.destinationApplicationSecurityGroups` SHALL contain `{ "id": "<item>" }` unchanged

#### Scenario: Name is expanded to an ID without an API call
- **WHEN** a `SourceAsgs` or `DestinationAsgs` item is a name on create or update
- **THEN** Azclips SHALL build `/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/applicationSecurityGroups/{name}` without an API call
- **AND** the body SHALL contain `{ "id": "<built ID>" }`

#### Scenario: Entries carry only the ID in the given order
- **WHEN** application security groups are sent on create or update
- **THEN** each entry SHALL carry only `id`, with no `location` or `tags`
- **AND** the entries SHALL be in the order the user gave

### Requirement: Security rule update applies only the given inputs
Security rule update SHALL send `GET` to the rule URL, apply only the given inputs to the GET result, send `PUT` to the same URL with that result, wait for the long-running operation, and output the bare `SecurityRule`. It SHALL apply no create defaults, SHALL treat `Priority` as optional, and SHALL send no PUT when the GET fails.

#### Scenario: Update sends GET then PUT
- **WHEN** a security rule is updated on CLI or PowerShell
- **THEN** Azclips SHALL first send `GET` to the rule URL
- **AND** then send `PUT` to the same URL with the GET result after applying the given inputs
- **AND** wait for the long-running operation and output the bare `SecurityRule`

#### Scenario: Inputs not given keep the existing values
- **WHEN** an input is not given on update
- **THEN** the PUT body SHALL keep the value from the GET result
- **AND** no create defaults SHALL be applied, and `Priority` SHALL be optional

#### Scenario: Given scalar inputs replace the existing values
- **WHEN** `Priority`, `Access`, `Direction`, `Protocol` or `Description` is given on update
- **THEN** the PUT body SHALL replace that property with the given value

#### Scenario: Given application security groups replace that side's list
- **WHEN** `SourceAsgs` or `DestinationAsgs` is given on update
- **THEN** the PUT body SHALL replace that side's application security group list with the given list, built as for create
- **AND** the address prefix on that side SHALL be left as GET returned it

#### Scenario: Missing rule fails at GET
- **WHEN** the rule to update does not exist
- **THEN** the GET SHALL return 404 and Azclips SHALL return that error
- **AND** Azclips SHALL send no PUT

### Requirement: Security rule update replaces given address and port lists
Security rule update SHALL put a given `SourceAddressPrefixes`, `SourcePortRanges`, `DestinationAddressPrefixes` or `DestinationPortRanges` on the matching singular property (one item) or plural property (more items), clearing the other, and SHALL keep both properties as GET returned them when the input is not given.

#### Scenario: One item replaces the singular property
- **WHEN** `SourceAddressPrefixes`, `SourcePortRanges`, `DestinationAddressPrefixes` or `DestinationPortRanges` is given with exactly one item on update
- **THEN** the PUT body SHALL set the matching singular property to that item
- **AND** the matching plural property SHALL carry no values

#### Scenario: More items replace the plural property
- **WHEN** one of these inputs is given with more than one item on update
- **THEN** the PUT body SHALL set the matching plural property to the list
- **AND** the matching singular property SHALL NOT be sent

#### Scenario: List not given keeps both properties
- **WHEN** one of these inputs is not given on update
- **THEN** the PUT body SHALL keep both the singular and the plural property as GET returned them
