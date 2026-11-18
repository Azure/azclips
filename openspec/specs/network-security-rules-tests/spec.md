# network-security-rules-tests Specification

## Purpose
Record/playback scenario tests for the Azclips Network security rule commands: the CLI `network nsg rule ...` commands and the PowerShell `*-AzNetworkSecurityRule` cmdlets. Each test replays its committed recording on both surfaces and verifies the behavior stated in the `network-security-rules` spec.

## Requirements
### Requirement: Security rule update replaces given address and port lists
The scenario tests SHALL verify the behavior requirement "Security rule update replaces given address and port lists".

#### Scenario: T1 Rule address and port lists switch between singular and plural on create and update
- **WHEN** (CLI) step 3 runs `network nsg rule create --name <rule> --nsg-name <nsg> --resource-group <resource-group> --priority 1000 --access allow --direction inbound --destination-address-prefixes 10.0.0.0/24 11.0.0.0/24 --source-address-prefixes * --source-port-ranges 700-900 1000-1100`
- **THEN** the PUT to .../networkSecurityGroups/<nsg>/securityRules/<rule> has body name = <rule>
- **AND** the PUT body has properties.destinationAddressPrefixes = [10.0.0.0/24, 11.0.0.0/24] and no properties.destinationAddressPrefix
- **AND** the PUT body has properties.sourceAddressPrefix = * and no properties.sourceAddressPrefixes
- **AND** the PUT body has properties.sourcePortRanges = [700-900, 1000-1100] and no properties.sourcePortRange
- **AND** the PUT body has properties.destinationPortRange = 80 (default) and no properties.destinationPortRanges
- **AND** the PUT body has properties.protocol = * (default)
- **AND** output is a SecurityRule named <rule>
- **AND** output destinationAddressPrefixes contains exactly 10.0.0.0/24 and 11.0.0.0/24, and destinationAddressPrefix is null
- **AND** output sourceAddressPrefix = * and sourceAddressPrefixes has no items
- **AND** output sourcePortRanges contains exactly 700-900 and 1000-1100, and sourcePortRange is null
- **AND** output destinationPortRange = 80 and destinationPortRanges has no items
- **WHEN** (CLI) step 4 runs `network nsg rule update --name <rule> --nsg-name <nsg> --resource-group <resource-group> --destination-address-prefixes Internet --source-address-prefixes 10.0.0.0/24 11.0.0.0/24 --source-port-ranges * --destination-port-ranges 500-1000 2000 3000`
- **THEN** a GET of .../securityRules/<rule> is sent before the PUT to the same URL
- **AND** the PUT body has properties.destinationAddressPrefix = Internet and no properties.destinationAddressPrefixes
- **AND** the PUT body has properties.sourceAddressPrefixes = [10.0.0.0/24, 11.0.0.0/24] and no properties.sourceAddressPrefix
- **AND** the PUT body has properties.sourcePortRange = * and no properties.sourcePortRanges
- **AND** the PUT body has properties.destinationPortRanges = [500-1000, 2000, 3000] and no properties.destinationPortRange
- **AND** output destinationAddressPrefix = Internet and destinationAddressPrefixes has no items
- **AND** output sourceAddressPrefixes contains exactly 10.0.0.0/24 and 11.0.0.0/24, and sourceAddressPrefix is null
- **AND** output sourcePortRange = * and sourcePortRanges has no items
- **AND** output destinationPortRanges contains exactly 500-1000, 2000 and 3000, and destinationPortRange is null
- **AND** output priority = 1000 (not given, kept from the existing rule)
- **WHEN** (PowerShell) step 3 runs `New-AzNetworkSecurityRule -Name <rule> -NetworkSecurityGroupName <nsg> -ResourceGroupName <resource-group> -Priority 1000 -Access allow -Direction inbound -DestinationAddressPrefixes 10.0.0.0/24,11.0.0.0/24 -SourceAddressPrefixes '*' -SourcePortRanges 700-900,1000-1100`
- **THEN** the PUT to .../networkSecurityGroups/<nsg>/securityRules/<rule> has body name = <rule>
- **AND** the PUT body has properties.destinationAddressPrefixes = [10.0.0.0/24, 11.0.0.0/24] and no properties.destinationAddressPrefix
- **AND** the PUT body has properties.sourceAddressPrefix = * and no properties.sourceAddressPrefixes
- **AND** the PUT body has properties.sourcePortRanges = [700-900, 1000-1100] and no properties.sourcePortRange
- **AND** the PUT body has properties.destinationPortRange = 80 (default) and no properties.destinationPortRanges
- **AND** the PUT body has properties.protocol = * (default)
- **AND** output is a SecurityRule named <rule>
- **AND** output destinationAddressPrefixes contains exactly 10.0.0.0/24 and 11.0.0.0/24, and destinationAddressPrefix is null
- **AND** output sourceAddressPrefix = * and sourceAddressPrefixes has no items
- **AND** output sourcePortRanges contains exactly 700-900 and 1000-1100, and sourcePortRange is null
- **AND** output destinationPortRange = 80 and destinationPortRanges has no items
- **WHEN** (PowerShell) step 4 runs `Set-AzNetworkSecurityRule -Name <rule> -NetworkSecurityGroupName <nsg> -ResourceGroupName <resource-group> -DestinationAddressPrefixes Internet -SourceAddressPrefixes 10.0.0.0/24,11.0.0.0/24 -SourcePortRanges '*' -DestinationPortRanges 500-1000,2000,3000`
- **THEN** a GET of .../securityRules/<rule> is sent before the PUT to the same URL
- **AND** the PUT body has properties.destinationAddressPrefix = Internet and no properties.destinationAddressPrefixes
- **AND** the PUT body has properties.sourceAddressPrefixes = [10.0.0.0/24, 11.0.0.0/24] and no properties.sourceAddressPrefix
- **AND** the PUT body has properties.sourcePortRange = * and no properties.sourcePortRanges
- **AND** the PUT body has properties.destinationPortRanges = [500-1000, 2000, 3000] and no properties.destinationPortRange
- **AND** output destinationAddressPrefix = Internet and destinationAddressPrefixes has no items
- **AND** output sourceAddressPrefixes contains exactly 10.0.0.0/24 and 11.0.0.0/24, and sourceAddressPrefix is null
- **AND** output sourcePortRange = * and sourcePortRanges has no items
- **AND** output destinationPortRanges contains exactly 500-1000, 2000 and 3000, and destinationPortRange is null
- **AND** output priority = 1000 (not given, kept from the existing rule)

### Requirement: Security rule update applies only the given inputs
The scenario tests SHALL verify the behavior requirement "Security rule update applies only the given inputs".

#### Scenario: T2 Security rule create, list, show, update, partial update and delete
- **WHEN** (CLI) step 3 runs `network nsg rule create --name <rule> --nsg-name <nsg> --resource-group <resource-group> --priority 1000 --access allow --direction inbound --protocol * --destination-address-prefixes 1234 --source-address-prefixes 789 --source-port-ranges * --destination-port-ranges 4444`
- **THEN** the PUT to .../networkSecurityGroups/<nsg>/securityRules/<rule> has body name = <rule>
- **AND** the PUT body has properties.priority = 1000, properties.protocol = *, properties.access = allow and properties.direction = inbound, as given
- **AND** the PUT body has properties.sourceAddressPrefix = 789, properties.destinationAddressPrefix = 1234, properties.sourcePortRange = * and properties.destinationPortRange = 4444
- **AND** the PUT body has no sourceAddressPrefixes, destinationAddressPrefixes, sourcePortRanges or destinationPortRanges property
- **AND** output is a SecurityRule named <rule> with priority = 1000
- **WHEN** (CLI) step 4 runs `network nsg rule list --nsg-name <nsg> --resource-group <resource-group>`
- **THEN** output is a list of exactly 1 SecurityRule, named <rule>
- **WHEN** (CLI) step 5 runs `network nsg rule show --name <rule> --nsg-name <nsg> --resource-group <resource-group>`
- **THEN** output is a SecurityRule named <rule>
- **AND** output priority = 1000 and destinationPortRange = 4444
- **WHEN** (CLI) step 6 runs `network nsg rule update --name <rule> --nsg-name <nsg> --resource-group <resource-group> --direction Outbound --access DENY --destination-address-prefixes 111 --protocol Tcp --source-address-prefixes 111 --source-port-ranges 1234-1235 --destination-port-ranges 1234-1235 --priority 888 --description greatrule`
- **THEN** a GET of .../securityRules/<rule> is sent before the PUT to the same URL
- **AND** the PUT body has properties.access = DENY (as given), properties.direction = Outbound, properties.protocol = Tcp, properties.priority = 888 and properties.description = greatrule
- **AND** the PUT body has properties.sourceAddressPrefix = 111, properties.destinationAddressPrefix = 111, properties.sourcePortRange = 1234-1235 and properties.destinationPortRange = 1234-1235
- **AND** output direction = Outbound, protocol = Tcp, priority = 888 and description = greatrule
- **AND** output sourceAddressPrefix = 111, destinationAddressPrefix = 111 and sourcePortRange = 1234-1235
- **WHEN** (CLI) step 7 runs `network nsg rule update --name <rule> --nsg-name <nsg> --resource-group <resource-group> --description cool`
- **THEN** a GET of .../securityRules/<rule> is sent before the PUT to the same URL
- **AND** the PUT body has properties.description = cool, and keeps properties.priority = 888, properties.direction = Outbound, properties.protocol = Tcp and properties.destinationPortRange = 1234-1235 from the GET (no create defaults applied)
- **AND** output description = cool
- **AND** output priority = 888, direction = Outbound, protocol = Tcp and sourcePortRange = 1234-1235, unchanged from step 6
- **WHEN** (CLI) step 8 runs `network nsg rule delete --name <rule> --nsg-name <nsg> --resource-group <resource-group>`
- **THEN** output is empty
- **WHEN** (CLI) step 9 runs `network nsg rule list --nsg-name <nsg> --resource-group <resource-group>`
- **THEN** output is a list with no items
- **WHEN** (PowerShell) step 3 runs `New-AzNetworkSecurityRule -Name <rule> -NetworkSecurityGroupName <nsg> -ResourceGroupName <resource-group> -Priority 1000 -Access allow -Direction inbound -Protocol '*' -DestinationAddressPrefixes 1234 -SourceAddressPrefixes 789 -SourcePortRanges '*' -DestinationPortRanges 4444`
- **THEN** the PUT to .../networkSecurityGroups/<nsg>/securityRules/<rule> has body name = <rule>
- **AND** the PUT body has properties.priority = 1000, properties.protocol = *, properties.access = allow and properties.direction = inbound, as given
- **AND** the PUT body has properties.sourceAddressPrefix = 789, properties.destinationAddressPrefix = 1234, properties.sourcePortRange = * and properties.destinationPortRange = 4444
- **AND** the PUT body has no sourceAddressPrefixes, destinationAddressPrefixes, sourcePortRanges or destinationPortRanges property
- **AND** output is a SecurityRule named <rule> with priority = 1000
- **WHEN** (PowerShell) step 4 runs `Get-AzNetworkSecurityRule_List -NetworkSecurityGroupName <nsg> -ResourceGroupName <resource-group>`
- **THEN** output is a list of exactly 1 SecurityRule, named <rule>
- **WHEN** (PowerShell) step 5 runs `Get-AzNetworkSecurityRule_Get -Name <rule> -NetworkSecurityGroupName <nsg> -ResourceGroupName <resource-group>`
- **THEN** output is a SecurityRule named <rule>
- **AND** output priority = 1000 and destinationPortRange = 4444
- **WHEN** (PowerShell) step 6 runs `Set-AzNetworkSecurityRule -Name <rule> -NetworkSecurityGroupName <nsg> -ResourceGroupName <resource-group> -Direction Outbound -Access DENY -DestinationAddressPrefixes 111 -Protocol Tcp -SourceAddressPrefixes 111 -SourcePortRanges 1234-1235 -DestinationPortRanges 1234-1235 -Priority 888 -Description greatrule`
- **THEN** a GET of .../securityRules/<rule> is sent before the PUT to the same URL
- **AND** the PUT body has properties.access = DENY (as given), properties.direction = Outbound, properties.protocol = Tcp, properties.priority = 888 and properties.description = greatrule
- **AND** the PUT body has properties.sourceAddressPrefix = 111, properties.destinationAddressPrefix = 111, properties.sourcePortRange = 1234-1235 and properties.destinationPortRange = 1234-1235
- **AND** output direction = Outbound, protocol = Tcp, priority = 888 and description = greatrule
- **AND** output sourceAddressPrefix = 111, destinationAddressPrefix = 111 and sourcePortRange = 1234-1235
- **WHEN** (PowerShell) step 7 runs `Set-AzNetworkSecurityRule -Name <rule> -NetworkSecurityGroupName <nsg> -ResourceGroupName <resource-group> -Description cool`
- **THEN** a GET of .../securityRules/<rule> is sent before the PUT to the same URL
- **AND** the PUT body has properties.description = cool, and keeps properties.priority = 888, properties.direction = Outbound, properties.protocol = Tcp and properties.destinationPortRange = 1234-1235 from the GET (no create defaults applied)
- **AND** output description = cool
- **AND** output priority = 888, direction = Outbound, protocol = Tcp and sourcePortRange = 1234-1235, unchanged from step 6
- **WHEN** (PowerShell) step 8 runs `Remove-AzNetworkSecurityRule -Name <rule> -NetworkSecurityGroupName <nsg> -ResourceGroupName <resource-group>`
- **THEN** output is empty
- **WHEN** (PowerShell) step 9 runs `Get-AzNetworkSecurityRule_List -NetworkSecurityGroupName <nsg> -ResourceGroupName <resource-group>`
- **THEN** output is a list with no items
