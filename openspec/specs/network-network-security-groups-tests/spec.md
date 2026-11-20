# network-network-security-groups-tests Specification

## Purpose
Scenario tests that replay recorded Azure sessions to verify the behavior the `network-network-security-groups` spec defines, on both the CLI and the PowerShell surfaces.

## Requirements
### Requirement: Create a network security group with location and tags only
The scenario tests SHALL verify the behavior requirement "Create a network security group with location and tags only".

#### Scenario: T1 Network security group lifecycle: default and explicit location, tags, show, list by group and subscription, attached references, delete
- **WHEN** (CLI) step 3 runs `network nsg create --name <nsg1> --resource-group <resource-group> --tags foo=doo`
- **THEN** one bare NetworkSecurityGroup output with no wrapper key: name <nsg1>, type Microsoft.Network/networkSecurityGroups, provisioningState Succeeded
- **AND** location equals <location>, the resource group's location
- **AND** tags equal exactly {foo: doo}
- **AND** request: a GET of /subscriptions/{subscription}/resourcegroups/<resource-group> is sent before the NSG PUT
- **AND** request: the PUT to /subscriptions/{subscription}/resourceGroups/<resource-group>/providers/Microsoft.Network/networkSecurityGroups/<nsg1>?api-version=2026-03-01 has a body with only location and tags: no id and no properties (so no flushConnection and no securityRules)
- **WHEN** (CLI) step 4 runs `network nsg list`
- **THEN** request: GET /subscriptions/{subscription}/providers/Microsoft.Network/networkSecurityGroups?api-version=2026-03-01, with no resourceGroups segment
- **AND** every listed item has type Microsoft.Network/networkSecurityGroups
- **AND** the listed items include <nsg1>
- **WHEN** (CLI) step 5 runs `network nsg list --resource-group <resource-group>`
- **THEN** request: GET /subscriptions/{subscription}/resourceGroups/<resource-group>/providers/Microsoft.Network/networkSecurityGroups?api-version=2026-03-01
- **AND** exactly one item, named <nsg1>, type Microsoft.Network/networkSecurityGroups
- **WHEN** (CLI) step 6 runs `network nsg show --resource-group <resource-group> --name <nsg1>`
- **THEN** one NetworkSecurityGroup: name <nsg1>, type Microsoft.Network/networkSecurityGroups, location <location>, tags {foo: doo}
- **AND** id ends with /resourceGroups/<resource-group>/providers/Microsoft.Network/networkSecurityGroups/<nsg1> (case-insensitive)
- **AND** securityRules is empty
- **AND** defaultSecurityRules has 6 entries named AllowVnetInBound, AllowAzureLoadBalancerInBound, DenyAllInBound, AllowVnetOutBound, AllowInternetOutBound, DenyAllOutBound, in any order
- **WHEN** (CLI) step 7 runs `network nsg create --name <nsg2> --resource-group <resource-group> --location <location>`
- **THEN** one NetworkSecurityGroup: name <nsg2>, location <location>, provisioningState Succeeded, no tags
- **AND** request: no resource-group GET is sent before this PUT; the PUT body carries only location
- **WHEN** (CLI) step 8 runs `network nsg show --resource-group <resource-group> --name <nsg2>`
- **THEN** securityRules is empty
- **AND** defaultSecurityRules has 6 entries named AllowVnetInBound, AllowAzureLoadBalancerInBound, DenyAllInBound, AllowVnetOutBound, AllowInternetOutBound, DenyAllOutBound, in any order
- **AND** subnets and networkInterfaces are empty
- **WHEN** (CLI) step 10 runs `network nsg show --resource-group <resource-group> --name <nsg2>`
- **THEN** subnets has exactly one entry, whose id ends with /virtualNetworks/<vnet>/subnets/<subnet> (case-insensitive)
- **WHEN** (CLI) step 12 runs `network nsg show --resource-group <resource-group> --name <nsg2>`
- **THEN** networkInterfaces has exactly one entry, whose id ends with /networkInterfaces/<nic> (case-insensitive)
- **WHEN** (CLI) step 15 runs `network nsg delete --resource-group <resource-group> --name <nsg1>`
- **THEN** no output
- **WHEN** (CLI) step 16 runs `network nsg delete --resource-group <resource-group> --name <nsg2>`
- **THEN** no output
- **WHEN** (CLI) step 17 runs `network nsg list --resource-group <resource-group>`
- **THEN** no NetworkSecurityGroup is listed
- **WHEN** (CLI) step 18 runs `network nsg list`
- **THEN** no listed item has an id under /resourceGroups/<resource-group>/
- **WHEN** (PowerShell) step 3 runs `New-AzNetworkSecurityGroup -Name <nsg1> -ResourceGroupName <resource-group> -Tag @{ foo = 'doo' }` through the real New-AzNetworkSecurityGroup cmdlet, so the hashtable is converted
- **THEN** one bare NetworkSecurityGroup output: name <nsg1>, type Microsoft.Network/networkSecurityGroups, provisioningState Succeeded
- **AND** location equals <location>, the resource group's location
- **AND** tags equal exactly {foo: doo}
- **AND** request: a GET of /subscriptions/{subscription}/resourcegroups/<resource-group> is sent before the NSG PUT
- **AND** request: the NSG PUT body has only location and tags, the same as the CLI body (body-matched playback)
- **WHEN** (PowerShell) step 4 runs `Get-AzNetworkSecurityGroup_List` through the real Get-AzNetworkSecurityGroup cmdlet with no ResourceGroupName, so the default List set is used
- **THEN** request: GET /subscriptions/{subscription}/providers/Microsoft.Network/networkSecurityGroups?api-version=2026-03-01
- **AND** the output is individual NetworkSecurityGroup objects, each of type Microsoft.Network/networkSecurityGroups
- **AND** the output includes <nsg1>
- **WHEN** (PowerShell) step 5 runs `Get-AzNetworkSecurityGroup_List -ResourceGroupName <resource-group>` through the real Get-AzNetworkSecurityGroup cmdlet
- **THEN** request: GET /subscriptions/{subscription}/resourceGroups/<resource-group>/providers/Microsoft.Network/networkSecurityGroups?api-version=2026-03-01
- **AND** exactly one NetworkSecurityGroup object, named <nsg1>
- **WHEN** (PowerShell) step 6 runs `Get-AzNetworkSecurityGroup_Get -ResourceGroupName <resource-group> -Name <nsg1>` through the real Get-AzNetworkSecurityGroup cmdlet, so the Get set is chosen
- **THEN** one NetworkSecurityGroup: name <nsg1>, type Microsoft.Network/networkSecurityGroups, location <location>, tags {foo: doo}
- **AND** Id ends with /resourceGroups/<resource-group>/providers/Microsoft.Network/networkSecurityGroups/<nsg1> (case-insensitive)
- **AND** SecurityRules is empty
- **AND** DefaultSecurityRules has 6 entries named AllowVnetInBound, AllowAzureLoadBalancerInBound, DenyAllInBound, AllowVnetOutBound, AllowInternetOutBound, DenyAllOutBound, in any order
- **WHEN** (PowerShell) step 7 runs `New-AzNetworkSecurityGroup -Name <nsg2> -ResourceGroupName <resource-group> -Location <location>`
- **THEN** one NetworkSecurityGroup: name <nsg2>, location <location>, provisioningState Succeeded, no tags
- **AND** request: no resource-group GET is sent before this PUT; the PUT body carries only location
- **WHEN** (PowerShell) step 8 runs `Get-AzNetworkSecurityGroup_Get -ResourceGroupName <resource-group> -Name <nsg2>`
- **THEN** SecurityRules is empty
- **AND** DefaultSecurityRules has 6 entries named AllowVnetInBound, AllowAzureLoadBalancerInBound, DenyAllInBound, AllowVnetOutBound, AllowInternetOutBound, DenyAllOutBound, in any order
- **AND** Subnets and NetworkInterfaces are empty
- **WHEN** (PowerShell) step 10 runs `Get-AzNetworkSecurityGroup_Get -ResourceGroupName <resource-group> -Name <nsg2>`
- **THEN** Subnets has exactly one entry, whose Id ends with /virtualNetworks/<vnet>/subnets/<subnet> (case-insensitive)
- **WHEN** (PowerShell) step 12 runs `Get-AzNetworkSecurityGroup_Get -ResourceGroupName <resource-group> -Name <nsg2>`
- **THEN** NetworkInterfaces has exactly one entry, whose Id ends with /networkInterfaces/<nic> (case-insensitive)
- **WHEN** (PowerShell) step 15 runs `Remove-AzNetworkSecurityGroup -Name <nsg1> -ResourceGroupName <resource-group> -Force` through the real Remove-AzNetworkSecurityGroup cmdlet
- **THEN** no output, because without -PassThru nothing is written
- **WHEN** (PowerShell) step 16 runs `Remove-AzNetworkSecurityGroup -Name <nsg2> -ResourceGroupName <resource-group> -Force -PassThru` through the real Remove-AzNetworkSecurityGroup cmdlet
- **THEN** exactly one output, the boolean true
- **WHEN** (PowerShell) step 17 runs `Get-AzNetworkSecurityGroup_List -ResourceGroupName <resource-group>` through the real Get-AzNetworkSecurityGroup cmdlet
- **THEN** no output
- **WHEN** (PowerShell) step 18 runs `Get-AzNetworkSecurityGroup_List` through the real Get-AzNetworkSecurityGroup cmdlet with no ResourceGroupName
- **THEN** no output object has an Id under /resourceGroups/<resource-group>/
