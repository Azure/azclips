# Tasks

All tasks follow `Applying A Scenario-Test Change` in `.github/skills/azclips-scenario-test-converter/SKILL.md` and edit only its Target Locations. No product code changes.

## 1. Register the Network extension with the scenario host

- [x] 1.1 Add an `ExtensionUnderTest` entry for the `network` module, with the root leaf `""` among its leaves, to `tests/Scenario.Tests/Infrastructure/ScenarioHost.cs`, and the `ProjectReference` to `src/Extensions/Network` in `tests/Scenario.Tests/Scenario.Tests.csproj`; verify `dotnet build tests/Scenario.Tests --nologo -v q` reports no error in these two files.

## 2. Write the scenario tests

Write each case as one test method in `tests/Scenario.Tests/Network/VirtualNetworkScenarioTests.cs`. `<rg>` is a test resource group name; `<location>` is `westus` unless the case says otherwise. Each case creates its own resource group and deletes it at the end. Turn each `expect:` line into an assertion on that step's result, and drive the same commands with the same values in the record and playback branches. Verify each task by building the test project and confirming the method exists and compiles.

- [x] 2.1 T1: write the test method for scenario "T1 Virtual network lifecycle with DNS server and subnet" (test spec requirement "List scope by resource group presence") with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: both
      CLI:
      1. group create --name <rg> --location westus
      2. network vnet create --resource-group <rg> --name vnet1 --location westus --address-prefixes 10.0.0.0/16 --dns-servers 8.8.8.8 --subnet-names subnet1 --subnet-address-prefixes 10.0.1.0/24
         - expect: output is a VirtualNetwork with Name `vnet1`
         - expect: ProvisioningState is `Succeeded`
         - expect: AddressSpaceAddressPrefixes is exactly [`10.0.0.0/16`]
         - expect: DhcpOptionsDnsServers is exactly [`8.8.8.8`]
         - expect: Subnets has 1 item with Name `subnet1` and AddressPrefix `10.0.1.0/24`
         - expect: request body properties.subnets[0] has name `subnet1` and addressPrefix `10.0.1.0/24`
      3. network vnet show --resource-group <rg> --name vnet1
         - expect: Name is `vnet1` and Type is `Microsoft.Network/virtualNetworks`
         - expect: AddressSpaceAddressPrefixes[0] is `10.0.0.0/16`
         - expect: DhcpOptionsDnsServers[0] is `8.8.8.8`
         - expect: Subnets has 1 item with Name `subnet1` and AddressPrefix `10.0.1.0/24`
      4. network vnet list --resource-group <rg>
         - expect: output has exactly 1 VirtualNetwork, with Name `vnet1`
         - expect: every item has Type `Microsoft.Network/virtualNetworks`
         - expect: request URI path contains `/resourceGroups/<rg>/providers/Microsoft.Network/virtualNetworks`
      5. network vnet list
         - expect: output contains a VirtualNetwork with Name `vnet1`
         - expect: every item has Type `Microsoft.Network/virtualNetworks`
         - expect: request URI path is `/subscriptions/<subscription>/providers/Microsoft.Network/virtualNetworks` (no resourceGroups segment)
      6. network vnet delete --resource-group <rg> --name vnet1
         - expect: completes without error
      7. network vnet list --resource-group <rg>
         - expect: output is empty
      8. group delete --name <rg>
      PowerShell:
      1. New-AzResourceGroup -Name <rg> -Location westus
      2. New-AzVirtualNetwork -ResourceGroupName <rg> -Name vnet1 -Location westus -AddressPrefix 10.0.0.0/16 -DnsServer 8.8.8.8 -SubnetNames @('subnet1') -SubnetAddressPrefixes @(,@('10.0.1.0/24'))
         - expect: output is a VirtualNetwork with Name `vnet1`
         - expect: ProvisioningState is `Succeeded`
         - expect: AddressSpaceAddressPrefixes is exactly [`10.0.0.0/16`]
         - expect: DhcpOptionsDnsServers is exactly [`8.8.8.8`]
         - expect: Subnets has 1 item with Name `subnet1` and AddressPrefix `10.0.1.0/24`
      3. Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name vnet1
         - expect: Name is `vnet1` and Type is `Microsoft.Network/virtualNetworks`
         - expect: AddressSpaceAddressPrefixes[0] is `10.0.0.0/16`
         - expect: DhcpOptionsDnsServers[0] is `8.8.8.8`
         - expect: Subnets has 1 item with Name `subnet1` and AddressPrefix `10.0.1.0/24`
      4. Get-AzVirtualNetwork_List -ResourceGroupName <rg>
         - expect: output has exactly 1 VirtualNetwork, with Name `vnet1`
         - expect: every item has Type `Microsoft.Network/virtualNetworks`
      5. Get-AzVirtualNetwork_List
         - expect: output contains a VirtualNetwork with Name `vnet1`
         - expect: every item has Type `Microsoft.Network/virtualNetworks`
      6. Remove-AzVirtualNetwork -ResourceGroupName <rg> -Name vnet1
         - expect: completes without error
      7. Get-AzVirtualNetwork_List -ResourceGroupName <rg>
         - expect: output is empty
      8. Remove-AzResourceGroup -Name <rg>
      Reference: https://github.com/Azure/azure-powershell/blob/main/src/Network/Network.Test/ScenarioTests/VirtualNetworkTests.ps1 (Test-VirtualNetworkCRUD); https://github.com/Azure/azure-cli/blob/dev/src/azure-cli/azure/cli/command_modules/network/tests/latest/test_network_commands.py (test_network_vnet)

- [x] 2.2 T2: write the test method for scenario "T2 Create defaults the address space to 10.0.0.0/16" (test spec requirement "Default virtual network address prefix") with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: both
      CLI:
      1. group create --name <rg> --location westus
      2. network vnet create --resource-group <rg> --name vnet1 --location westus --subnet-names default --subnet-address-prefixes 10.0.0.0/24
         - expect: request body properties.addressSpace.addressPrefixes is exactly [`10.0.0.0/16`]
         - expect: ProvisioningState is `Succeeded`
         - expect: AddressSpaceAddressPrefixes is exactly [`10.0.0.0/16`]
         - expect: Subnets[0].Name is `default` and Subnets[0].PrivateEndpointNetworkPolicies is `Disabled`
      3. network vnet show --resource-group <rg> --name vnet1
         - expect: AddressSpaceAddressPrefixes is exactly [`10.0.0.0/16`]
      4. group delete --name <rg>
      PowerShell:
      1. New-AzResourceGroup -Name <rg> -Location westus
      2. New-AzVirtualNetwork -ResourceGroupName <rg> -Name vnet1 -Location westus -SubnetNames @('default') -SubnetAddressPrefixes @(,@('10.0.0.0/24'))
         - expect: request body properties.addressSpace.addressPrefixes is exactly [`10.0.0.0/16`]
         - expect: ProvisioningState is `Succeeded`
         - expect: AddressSpaceAddressPrefixes is exactly [`10.0.0.0/16`]
         - expect: Subnets[0].Name is `default` and Subnets[0].PrivateEndpointNetworkPolicies is `Disabled`
      3. Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name vnet1
         - expect: AddressSpaceAddressPrefixes is exactly [`10.0.0.0/16`]
      4. Remove-AzResourceGroup -Name <rg>
      Reference: https://github.com/Azure/azure-cli/blob/dev/src/azure-cli/azure/cli/command_modules/network/tests/latest/test_network_commands.py (test_network_vnet create step)

- [x] 2.3 T3: write the test method for scenario "T3 Create with flow timeout" (test spec requirement "Virtual network create") with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: both
      CLI:
      1. group create --name <rg> --location westus
      2. network vnet create --resource-group <rg> --name vnet --location westus --flowtimeout 10
         - expect: FlowTimeoutInMinutes is 10
      3. network vnet show --resource-group <rg> --name vnet
         - expect: FlowTimeoutInMinutes is 10
      4. group delete --name <rg>
      PowerShell:
      1. New-AzResourceGroup -Name <rg> -Location westus
      2. New-AzVirtualNetwork -ResourceGroupName <rg> -Name vnet -Location westus -FlowTimeout 10
         - expect: FlowTimeoutInMinutes is 10
      3. Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name vnet
         - expect: FlowTimeoutInMinutes is 10
      4. Remove-AzResourceGroup -Name <rg>
      Reference: test_network_vnet_flowtimeout (azure-cli); Test-VirtualNetworkCRUD-FlowTimeout (azure-powershell)

- [x] 2.4 T4: write the test method for scenario "T4 Create with BGP community" (test spec requirement "Virtual network create") with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: both
      CLI:
      1. group create --name <rg> --location westus
      2. network vnet create --resource-group <rg> --name vnet1 --location westus --bgp-community 12076:20000
         - expect: BgpCommunitiesVirtualNetworkCommunity is `12076:20000`
      3. network vnet show --resource-group <rg> --name vnet1
         - expect: BgpCommunitiesVirtualNetworkCommunity is `12076:20000`
      4. group delete --name <rg>
      PowerShell:
      1. New-AzResourceGroup -Name <rg> -Location westus
      2. New-AzVirtualNetwork -ResourceGroupName <rg> -Name vnet1 -Location westus -BgpCommunity 12076:20000
         - expect: BgpCommunitiesVirtualNetworkCommunity is `12076:20000`
      3. Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name vnet1
         - expect: BgpCommunitiesVirtualNetworkCommunity is `12076:20000`
      4. Remove-AzResourceGroup -Name <rg>
      Reference: test_network_vnet_with_bgp_community (azure-cli); Test-bgpCommunitiesCRUD (azure-powershell)

- [x] 2.5 T5: write the test method for scenario "T5 Create with encryption settings" (test spec requirement "Virtual network create") with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: both
      CLI:
      1. group create --name <rg> --location westus
      2. network vnet create --resource-group <rg> --name MyVnet1 --location westus --address-prefixes 10.0.0.0/16 --subnet-names Mysubnet --subnet-address-prefixes 10.0.0.0/24 --enable-encryption true --encryption-policy AllowUnencrypted
      3. network vnet create --resource-group <rg> --name MyVnet2 --location westus --address-prefixes 10.1.0.0/16 --subnet-names MySubnet --subnet-address-prefixes 10.1.0.0/24 --enable-encryption true --encryption-policy DropUnencrypted
      4. network vnet create --resource-group <rg> --name MyVnet3 --location westus --address-prefixes 10.2.0.0/16 --subnet-names Mysubnet --subnet-address-prefixes 10.2.0.0/24 --enable-encryption false
      5. network vnet show --resource-group <rg> --name MyVnet1
         - expect: EncryptionEnabled is true
         - expect: EncryptionEnforcement is `AllowUnencrypted`
      6. network vnet show --resource-group <rg> --name MyVnet2
         - expect: EncryptionEnabled is true
         - expect: EncryptionEnforcement is `DropUnencrypted`
      7. network vnet show --resource-group <rg> --name MyVnet3
         - expect: EncryptionEnabled is false
      8. group delete --name <rg>
      PowerShell:
      1. New-AzResourceGroup -Name <rg> -Location westus
      2. New-AzVirtualNetwork -ResourceGroupName <rg> -Name MyVnet1 -Location westus -AddressPrefix 10.0.0.0/16 -SubnetNames @('Mysubnet') -SubnetAddressPrefixes @(,@('10.0.0.0/24')) -EnableEncryption $true -EncryptionEnforcementPolicy AllowUnencrypted
      3. New-AzVirtualNetwork -ResourceGroupName <rg> -Name MyVnet2 -Location westus -AddressPrefix 10.1.0.0/16 -SubnetNames @('MySubnet') -SubnetAddressPrefixes @(,@('10.1.0.0/24')) -EnableEncryption $true -EncryptionEnforcementPolicy DropUnencrypted
      4. New-AzVirtualNetwork -ResourceGroupName <rg> -Name MyVnet3 -Location westus -AddressPrefix 10.2.0.0/16 -SubnetNames @('Mysubnet') -SubnetAddressPrefixes @(,@('10.2.0.0/24')) -EnableEncryption $false
      5. Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name MyVnet1
         - expect: EncryptionEnabled is true
         - expect: EncryptionEnforcement is `AllowUnencrypted`
      6. Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name MyVnet2
         - expect: EncryptionEnabled is true
         - expect: EncryptionEnforcement is `DropUnencrypted`
      7. Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name MyVnet3
         - expect: EncryptionEnabled is false
      8. Remove-AzResourceGroup -Name <rg>
      Reference: test_network_vnet_with_encryption (azure-cli); Test-VirtualNetworkEncryption (azure-powershell)

- [x] 2.6 T6: write the test method for scenario "T6 Create with private endpoint VNet policies" (test spec requirement "Virtual network create") with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: both
      CLI:
      1. group create --name <rg> --location westus
      2. network vnet create --resource-group <rg> --name vnet1 --location westus --address-prefixes 10.1.0.0/16
      3. network vnet show --resource-group <rg> --name vnet1
         - expect: PrivateEndpointVNetPolicies is `Disabled`
      4. network vnet create --resource-group <rg> --name vnet2 --location westus --address-prefixes 10.2.0.0/16 --pe-vnet-policies Disabled
         - expect: request body properties.privateEndpointVNetPolicies is `Disabled`
      5. network vnet show --resource-group <rg> --name vnet2
         - expect: PrivateEndpointVNetPolicies is `Disabled`
      6. network vnet create --resource-group <rg> --name vnet3 --location westus --address-prefixes 10.3.0.0/16 --pe-vnet-policies Basic
      7. network vnet show --resource-group <rg> --name vnet3
         - expect: PrivateEndpointVNetPolicies is `Basic`
      8. group delete --name <rg>
      PowerShell:
      1. New-AzResourceGroup -Name <rg> -Location westus
      2. New-AzVirtualNetwork -ResourceGroupName <rg> -Name vnet1 -Location westus -AddressPrefix 10.1.0.0/16
      3. Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name vnet1
         - expect: PrivateEndpointVNetPolicies is `Disabled`
      4. New-AzVirtualNetwork -ResourceGroupName <rg> -Name vnet2 -Location westus -AddressPrefix 10.2.0.0/16 -PrivateEndpointVNetPoliciesValue Disabled
         - expect: request body properties.privateEndpointVNetPolicies is `Disabled`
      5. Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name vnet2
         - expect: PrivateEndpointVNetPolicies is `Disabled`
      6. New-AzVirtualNetwork -ResourceGroupName <rg> -Name vnet3 -Location westus -AddressPrefix 10.3.0.0/16 -PrivateEndpointVNetPoliciesValue Basic
      7. Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name vnet3
         - expect: PrivateEndpointVNetPolicies is `Basic`
      8. Remove-AzResourceGroup -Name <rg>
      Reference: Test-VirtualNetworkPrivateEndpointVNetPolicies (azure-powershell)

- [x] 2.7 T7: write the test method for scenario "T7 Create with summarized gateway prefixes" (test spec requirement "Virtual network create") with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: both
      CLI:
      1. group create --name <rg> --location centraluseuap
      2. network vnet create --resource-group <rg> --name vnet1 --location centraluseuap --address-prefixes 10.0.0.0/16 --sgp 10.0.0.0/16
         - expect: ProvisioningState is `Succeeded`
         - expect: SummarizedGatewayPrefixesAddressPrefixes[0] is `10.0.0.0/16`
      3. network vnet show --resource-group <rg> --name vnet1
         - expect: SummarizedGatewayPrefixesAddressPrefixes[0] is `10.0.0.0/16`
      4. group delete --name <rg>
      PowerShell:
      1. New-AzResourceGroup -Name <rg> -Location centraluseuap
      2. New-AzVirtualNetwork -ResourceGroupName <rg> -Name vnet1 -Location centraluseuap -AddressPrefix 10.0.0.0/16 -SummarizedGatewayPrefix 10.0.0.0/16
         - expect: ProvisioningState is `Succeeded`
         - expect: SummarizedGatewayPrefixesAddressPrefixes[0] is `10.0.0.0/16`
      3. Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name vnet1
         - expect: SummarizedGatewayPrefixesAddressPrefixes[0] is `10.0.0.0/16`
      4. Remove-AzResourceGroup -Name <rg>
      Reference: test_network_vnet_with_summarized_gateway_prefixes (azure-cli)

- [x] 2.8 T8: write the test method for scenario "T8 Create in an edge zone" (test spec requirement "Edge zone maps to extended location") with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: both
      CLI:
      1. group create --name <rg> --location eastus2euap
      2. network vnet create --resource-group <rg> --name vnet1 --location eastus2euap --address-prefixes 10.0.0.0/16 --dns-servers 8.8.8.8 --subnet-names subnet1 --subnet-address-prefixes 10.0.1.0/24 --extended-location MicrosoftRRDCLab1
         - expect: request body extendedLocation has name `MicrosoftRRDCLab1` and type `EdgeZone`
      3. network vnet show --resource-group <rg> --name vnet1
         - expect: ExtendedLocationName is `MicrosoftRRDCLab1`
         - expect: ExtendedLocationType is `EdgeZone`
      4. group delete --name <rg>
      PowerShell:
      1. New-AzResourceGroup -Name <rg> -Location eastus2euap
      2. New-AzVirtualNetwork -ResourceGroupName <rg> -Name vnet1 -Location eastus2euap -AddressPrefix 10.0.0.0/16 -DnsServer 8.8.8.8 -SubnetNames @('subnet1') -SubnetAddressPrefixes @(,@('10.0.1.0/24')) -EdgeZone MicrosoftRRDCLab1
         - expect: request body extendedLocation has name `MicrosoftRRDCLab1` and type `EdgeZone`
      3. Get-AzVirtualNetwork_Get -ResourceGroupName <rg> -Name vnet1
         - expect: ExtendedLocationName is `MicrosoftRRDCLab1`
         - expect: ExtendedLocationType is `EdgeZone`
      4. Remove-AzResourceGroup -Name <rg>
      Reference: Test-VirtualNetworkEdgeZone (azure-powershell)

## 3. Replay, review, and record

- [x] 3.1 Build the test project and run the new scenarios in playback mode (`dotnet test tests/Scenario.Tests --filter FullyQualifiedName~VirtualNetworkScenarioTests`); verify each scenario either passes or fails only as `recording-required`, and report any other failure.
- [ ] 3.2 Stop for user review of the scenario code with the Apply Report; apply the corrections the user asks for and show the result again, repeating until the user confirms. Verify by the user's explicit confirmation.
- [ ] 3.3 After that confirmation, ask the user whether to record. Record only the scenarios the user approves, one `--filter` at a time, after confirming the user is signed in with the azclips CLI, following `Record vs. playback` in `tests/Scenario.Tests/README.md`; verify the approved scenarios pass in playback against the new recordings, and report the recordings for review and any resources still needing cleanup. T7 needs `centraluseuap` and T8 needs `eastus2euap` with edge zone `MicrosoftRRDCLab1`; report any that cannot be recorded.

## Workflow follow-up

- Archive the change with `/opsx:archive network-virtual-networks-tests-vnet-scenarios` only after the user has reviewed the recordings and chosen to retain the change.
