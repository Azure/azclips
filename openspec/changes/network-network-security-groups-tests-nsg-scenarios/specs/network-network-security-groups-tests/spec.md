# Spec Delta

## Purpose

Record/playback scenario tests for the Network extension's network security group commands on the CLI surface (`network nsg create`, `network nsg show`, `network nsg list`, `network nsg delete`) and the PowerShell surface (`New-AzNetworkSecurityGroup`, `Get-AzNetworkSecurityGroup_Get`, `Get-AzNetworkSecurityGroup_List`, `Remove-AzNetworkSecurityGroup`). Each test replays its recording and verifies the behavior stated in the `network-network-security-groups` spec.

## ADDED Requirements

### Requirement: List scope by resource group presence
The scenario tests SHALL verify the behavior requirement "List scope by resource group presence".

#### Scenario: T1 Network security group lifecycle: create, show, list, delete
- **WHEN** (CLI) step 2 runs `network nsg create --resource-group <rg> --name <nsg> --location <location> --tags foo=doo`
- **THEN** no resource-group GET is sent before the NetworkSecurityGroups_CreateOrUpdate PUT
- **AND** the PUT body has location = "<location>" and tags = {"foo": "doo"}
- **AND** output name = "<nsg>", type = "Microsoft.Network/networkSecurityGroups", location = "<location>"
- **AND** output tags = {"foo": "doo"}
- **AND** output securityRules is empty and defaultSecurityRules has 6 items named AllowVnetInBound, AllowAzureLoadBalancerInBound, DenyAllInBound, AllowVnetOutBound, AllowInternetOutBound, DenyAllOutBound (any order)
- **WHEN** (CLI) step 3 runs `network nsg show --resource-group <rg> --name <nsg>`
- **THEN** output name = "<nsg>", type = "Microsoft.Network/networkSecurityGroups"
- **AND** output id ends with "/resourceGroups/<rg>/providers/Microsoft.Network/networkSecurityGroups/<nsg>"
- **AND** output provisioningState = "Succeeded", resourceGuid is not empty, defaultSecurityRules has 6 items
- **WHEN** (CLI) step 4 runs `network nsg list --resource-group <rg>`
- **THEN** request is GET .../resourceGroups/<rg>/providers/Microsoft.Network/networkSecurityGroups (NetworkSecurityGroups_List)
- **AND** output has exactly 1 item, its name = "<nsg>" and type = "Microsoft.Network/networkSecurityGroups"
- **WHEN** (CLI) step 5 runs `network nsg list`
- **THEN** request is GET /subscriptions/<sub>/providers/Microsoft.Network/networkSecurityGroups (NetworkSecurityGroups_ListAll)
- **AND** every output item has type = "Microsoft.Network/networkSecurityGroups"
- **AND** output contains an item whose id = <step 3: id>
- **WHEN** (CLI) step 6 runs `network nsg delete --resource-group <rg> --name <nsg>`
- **THEN** request is DELETE .../resourceGroups/<rg>/providers/Microsoft.Network/networkSecurityGroups/<nsg> (NetworkSecurityGroups_Delete) and the command completes without error
- **WHEN** (CLI) step 7 runs `network nsg list --resource-group <rg>`
- **THEN** output is empty
- **WHEN** (CLI) step 8 runs `network nsg list`
- **THEN** output contains no item whose id = <step 3: id>
- **WHEN** (PowerShell) step 2 runs `New-AzNetworkSecurityGroup -Name <nsg> -ResourceGroupName <rg> -Location <location> -Tag @{ foo = "doo" }`
- **THEN** no resource-group GET is sent before the NetworkSecurityGroups_CreateOrUpdate PUT
- **AND** the PUT body has location = "<location>" and tags = {"foo": "doo"}
- **AND** output Name = "<nsg>", Type = "Microsoft.Network/networkSecurityGroups", Location = "<location>"
- **AND** output Tags = {"foo": "doo"}
- **AND** output SecurityRules is empty and DefaultSecurityRules has 6 items named AllowVnetInBound, AllowAzureLoadBalancerInBound, DenyAllInBound, AllowVnetOutBound, AllowInternetOutBound, DenyAllOutBound (any order)
- **WHEN** (PowerShell) step 3 runs `Get-AzNetworkSecurityGroup_Get -ResourceGroupName <rg> -Name <nsg>`
- **THEN** output Name = "<nsg>", Type = "Microsoft.Network/networkSecurityGroups"
- **AND** output Id ends with "/resourceGroups/<rg>/providers/Microsoft.Network/networkSecurityGroups/<nsg>"
- **AND** output ProvisioningState = "Succeeded", ResourceGuid is not empty, DefaultSecurityRules has 6 items
- **WHEN** (PowerShell) step 4 runs `Get-AzNetworkSecurityGroup_List -ResourceGroupName <rg>`
- **THEN** request is GET .../resourceGroups/<rg>/providers/Microsoft.Network/networkSecurityGroups (NetworkSecurityGroups_List)
- **AND** output has exactly 1 item, its Name = "<nsg>" and Type = "Microsoft.Network/networkSecurityGroups"
- **WHEN** (PowerShell) step 5 runs `Get-AzNetworkSecurityGroup_List`
- **THEN** request is GET /subscriptions/<sub>/providers/Microsoft.Network/networkSecurityGroups (NetworkSecurityGroups_ListAll)
- **AND** every output item has Type = "Microsoft.Network/networkSecurityGroups"
- **AND** output contains an item whose Id = <step 3: Id>
- **WHEN** (PowerShell) step 6 runs `Remove-AzNetworkSecurityGroup -ResourceGroupName <rg> -Name <nsg>`
- **THEN** request is DELETE .../resourceGroups/<rg>/providers/Microsoft.Network/networkSecurityGroups/<nsg> (NetworkSecurityGroups_Delete) and the command completes without error
- **WHEN** (PowerShell) step 7 runs `Get-AzNetworkSecurityGroup_List -ResourceGroupName <rg>`
- **THEN** output is empty
- **WHEN** (PowerShell) step 8 runs `Get-AzNetworkSecurityGroup_List`
- **THEN** output contains no item whose Id = <step 3: Id>

### Requirement: Default location from the resource group
The scenario tests SHALL verify the behavior requirement "Default location from the resource group".

#### Scenario: T2 Create without location takes the resource group location
- **WHEN** (CLI) step 2 runs `network nsg create --resource-group <rg> --name <nsg>`
- **THEN** a GET /subscriptions/<sub>/resourceGroups/<rg> (ResourceGroups_Get) is sent before the NetworkSecurityGroups_CreateOrUpdate PUT
- **AND** the PUT body has location = "<location>"
- **AND** output name = "<nsg>" and location = "<location>"
