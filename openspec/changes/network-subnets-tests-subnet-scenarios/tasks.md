# Tasks

All tasks follow `Applying A Scenario-Test Change` in `.github/skills/azclips-scenario-test-converter/SKILL.md` and edit only its Target Locations. No product code changes.

## 1. Register the Network extension with the scenario host

- [ ] 1.1 If no earlier change has added them, add an `ExtensionUnderTest` entry for the `network` module, with the root leaf `""` among its leaves, to `tests/Scenario.Tests/Infrastructure/ScenarioHost.cs`, and the `ProjectReference` to `src/Extensions/Network` in `tests/Scenario.Tests/Scenario.Tests.csproj`; verify `dotnet build tests/Scenario.Tests --nologo -v q` reports no error in these two files.

## 2. Write the scenario tests

Write each case as one test method in `tests/Scenario.Tests/Network/SubnetScenarioTests.cs`. Every case is CLI-only: the PowerShell surface exposes no Subnets command. Each case creates its own resource group. Turn each `expect:` line into an assertion on that step's result, and drive the same commands with the same values in the record and playback branches. Verify each task by building the test project and confirming the method exists and compiles.

- [ ] 2.1 T1: write the test method for scenario "T1 Subnet lifecycle with one and several address prefixes" (test spec requirement "Address prefix single or plural") with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: cli-only
      CLI:
      1. group create --name scenario-subnet-lifecycle-rg --location westus
      2. network vnet create -g scenario-subnet-lifecycle-rg -n vnet1 --location westus --address-prefixes 20.0.0.0/16 10.0.0.0/16
      3. network vnet subnet create -g scenario-subnet-lifecycle-rg --vnet-name vnet1 -n subnet1 --address-prefixes 20.0.0.0/24 --default-outbound false
         - expect: Name is `subnet1`
         - expect: AddressPrefix is `20.0.0.0/24`
         - expect: AddressPrefixes is null or empty
         - expect: DefaultOutboundAccess is false
         - expect: PrivateEndpointNetworkPolicies is `Disabled`
         - expect: PrivateLinkServiceNetworkPolicies is `Enabled`
      4. network vnet subnet create -g scenario-subnet-lifecycle-rg --vnet-name vnet1 -n subnet2 --address-prefixes 10.0.1.0/28 10.0.2.0/28 --default-outbound false
         - expect: Name is `subnet2`
         - expect: AddressPrefixes is [`10.0.1.0/28`, `10.0.2.0/28`] in that order
         - expect: AddressPrefix is null
      5. network vnet subnet list -g scenario-subnet-lifecycle-rg --vnet-name vnet1
         - expect: output is a list of Subnet items whose names include `subnet1` and `subnet2`
      6. network vnet subnet show -g scenario-subnet-lifecycle-rg --vnet-name vnet1 -n subnet1
         - expect: Name is `subnet1`
         - expect: AddressPrefix is `20.0.0.0/24`
      7. network vnet subnet show -g scenario-subnet-lifecycle-rg --vnet-name vnet1 -n subnet2
         - expect: AddressPrefixes is [`10.0.1.0/28`, `10.0.2.0/28`]
      8. network vnet subnet delete -g scenario-subnet-lifecycle-rg --vnet-name vnet1 -n subnet1
      9. network vnet subnet delete -g scenario-subnet-lifecycle-rg --vnet-name vnet1 -n subnet2
      10. network vnet subnet list -g scenario-subnet-lifecycle-rg --vnet-name vnet1
         - expect: no item is named `subnet1` or `subnet2`
      11. network vnet delete -g scenario-subnet-lifecycle-rg -n vnet1
      Reference: https://github.com/Azure/azure-cli/blob/dev/src/azure-cli/azure/cli/command_modules/network/tests/latest/test_network_commands.py (test_network_vnet); https://github.com/Azure/azure-powershell/blob/main/src/Network/Network.Test/ScenarioTests/VirtualNetworkTests.ps1 (Test-subnetCRUD, Test-multiPrefixSubnetCRUD)

- [ ] 2.2 T2: write the test method for scenario "T2 Subnet with a network security group by name and by ID" (test spec requirement "Referenced resource name or ID") with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: cli-only
      CLI:
      1. group create --name scenario-subnet-nsg-rg --location westus
      2. network nsg create -g scenario-subnet-nsg-rg -n nsg --location westus
      3. network vnet create -g scenario-subnet-nsg-rg -n vnet1 --location westus --address-prefixes 20.0.0.0/16 10.0.0.0/16
      4. network vnet subnet create -g scenario-subnet-nsg-rg --vnet-name vnet1 -n subnet1 --address-prefixes 20.0.0.0/24 --network-security-group nsg --default-outbound false
         - expect: NetworkSecurityGroupId ends with `/networkSecurityGroups/nsg` (case-insensitive)
      5. network vnet subnet create -g scenario-subnet-nsg-rg --vnet-name vnet1 -n subnet2 --address-prefixes 20.0.1.0/24 --network-security-group <step 2: id> --default-outbound false
         - expect: NetworkSecurityGroupId ends with `/networkSecurityGroups/nsg` (case-insensitive)
      6. network vnet subnet list -g scenario-subnet-nsg-rg --vnet-name vnet1
         - expect: output is a list of Subnet items whose names include `subnet1` and `subnet2`
      7. network vnet subnet show -g scenario-subnet-nsg-rg --vnet-name vnet1 -n subnet1
         - expect: Name is `subnet1`
         - expect: NetworkSecurityGroupId ends with `/networkSecurityGroups/nsg` (case-insensitive)
      8. network vnet subnet delete -g scenario-subnet-nsg-rg --vnet-name vnet1 -n subnet1
      9. network vnet subnet delete -g scenario-subnet-nsg-rg --vnet-name vnet1 -n subnet2
      Reference: test_network_commands.py (test_network_vnet_with_subnet_nsg); VirtualNetworkTests.ps1 (Test-subnetNetworkSecurityGroupCRUD)

- [ ] 2.3 T3: write the test method for scenario "T3 Subnet with a service endpoint" (test spec requirement "Service endpoint names") with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: cli-only
      CLI:
      1. group create --name scenario-subnet-endpoint-rg --location eastus2
      2. network vnet create -g scenario-subnet-endpoint-rg -n vnet1 --location eastus2
      3. network vnet subnet create -g scenario-subnet-endpoint-rg --vnet-name vnet1 -n subnet1 --address-prefixes 10.0.1.0/24 --service-endpoints Microsoft.Storage --default-outbound false
         - expect: ServiceEndpoints has one item whose Service is `Microsoft.Storage`
      Reference: test_network_commands.py (test_network_subnet_endpoint_service); VirtualNetworkTests.ps1 (Test-VirtualNetworkSubnetServiceEndpoint)

- [ ] 2.4 T4: write the test method for scenario "T4 Subnet delegation in dotted and slash form" (test spec requirement "Delegation values") with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: cli-only
      CLI:
      1. group create --name scenario-subnet-delegation-rg --location westcentralus
      2. network vnet create -g scenario-subnet-delegation-rg -n vnet1 --location westcentralus
      3. network vnet subnet create -g scenario-subnet-delegation-rg --vnet-name vnet1 -n subnet1 --address-prefixes 10.0.0.0/24 --delegations Microsoft.Web.serverFarms --default-outbound false
         - expect: Delegations has one item whose ServiceName is `Microsoft.Web/serverFarms`
      4. network vnet subnet create -g scenario-subnet-delegation-rg --vnet-name vnet1 -n subnet2 --address-prefixes 10.0.1.0/24 --delegations Microsoft.Sql/managedInstances --default-outbound false
         - expect: Delegations has one item whose ServiceName is `Microsoft.Sql/managedInstances`
      Reference: test_network_commands.py (test_network_subnet_delegation); VirtualNetworkTests.ps1 (Test-subnetDelegationCRUD)

- [ ] 2.5 T5: write the test method for scenario "T5 Deprecated disable network policy flags" (test spec requirement "Deprecated disable network policy flags") with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: cli-only
      CLI:
      1. group create --name scenario-subnet-policies-rg --location westus
      2. network vnet create -g scenario-subnet-policies-rg -n MyVnet --location westus
      3. network vnet subnet create -g scenario-subnet-policies-rg --vnet-name MyVnet --address-prefixes 10.0.1.0/24 --name MySubnet1 --disable-private-endpoint-network-policies true --default-outbound false
         - expect: AddressPrefix is `10.0.1.0/24`
         - expect: PrivateEndpointNetworkPolicies is `Disabled`
         - expect: PrivateLinkServiceNetworkPolicies is `Enabled`
      4. network vnet subnet create -g scenario-subnet-policies-rg --vnet-name MyVnet --address-prefixes 10.0.2.0/24 --name MySubnet2 --disable-private-link-service-network-policies true --default-outbound false
         - expect: AddressPrefix is `10.0.2.0/24`
         - expect: PrivateEndpointNetworkPolicies is `Disabled`
         - expect: PrivateLinkServiceNetworkPolicies is `Disabled`
      5. network vnet subnet create -g scenario-subnet-policies-rg --vnet-name MyVnet --address-prefixes 10.0.3.0/24 --name MySubnet3 --disable-private-endpoint-network-policies true --disable-private-link-service-network-policies true --default-outbound false
         - expect: AddressPrefix is `10.0.3.0/24`
         - expect: PrivateEndpointNetworkPolicies is `Disabled`
         - expect: PrivateLinkServiceNetworkPolicies is `Disabled`
      Reference: test_network_commands.py (test_subnet_with_private_endpoint_and_private_link_options)

- [ ] 2.6 T6: write the test method for scenario "T6 Subnet default outbound access true and false" (test spec requirement "Subnet create") with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: cli-only
      CLI:
      1. group create --name scenario-subnet-outbound-rg --location westcentralus
      2. network vnet create -g scenario-subnet-outbound-rg -n vnet1 --location westcentralus
      3. network vnet subnet create -g scenario-subnet-outbound-rg --vnet-name vnet1 -n subnet1 --address-prefixes 10.0.3.0/24 --default-outbound-access false
         - expect: DefaultOutboundAccess is false
      4. network vnet subnet create -g scenario-subnet-outbound-rg --vnet-name vnet1 -n subnet2 --address-prefixes 10.0.4.0/24 --default-outbound-access true
         - expect: DefaultOutboundAccess is true
      5. network vnet subnet show -g scenario-subnet-outbound-rg --vnet-name vnet1 -n subnet2
         - expect: DefaultOutboundAccess is true
      Reference: test_network_commands.py (test_network_subnet_default_outbound_access); VirtualNetworkTests.ps1 (Test-subnetWithDefaultOutboundAccessCRUD)

- [ ] 2.7 T7: write the test method for scenario "T7 Subnet sharing scope Tenant" (test spec requirement "Subnet create") with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: cli-only
      CLI:
      1. group create --name scenario-subnet-sharing-rg --location westcentralus
      2. network vnet create -g scenario-subnet-sharing-rg -n vnet1 --location westcentralus --address-prefixes 10.0.0.0/16
      3. network vnet subnet create -g scenario-subnet-sharing-rg --vnet-name vnet1 -n subnet1 --address-prefixes 10.0.0.0/16 --default-outbound false --sharing-scope Tenant
      4. network vnet subnet show -g scenario-subnet-sharing-rg --vnet-name vnet1 -n subnet1
         - expect: SharingScope is `Tenant`
      Reference: test_network_commands.py (test_network_subnet_with_sharing_scope)

## 3. Replay, review, and record

- [ ] 3.1 Build the test project and run the new scenarios in playback mode (`dotnet test tests/Scenario.Tests --filter FullyQualifiedName~SubnetScenarioTests`); verify each scenario either passes or fails only as `recording-required`, and report any other failure.
- [ ] 3.2 Stop for user review of the scenario code with the Apply Report; apply the corrections the user asks for and show the result again, repeating until the user confirms. Verify by the user's explicit confirmation.
- [ ] 3.3 After that confirmation, ask the user whether to record. Record only the scenarios the user approves, one `--filter` at a time, after confirming the user is signed in with the azclips CLI, following `Record vs. playback` in `tests/Scenario.Tests/README.md`; verify the approved scenarios pass in playback against the new recordings, and report the recordings for review and any resources still needing cleanup. T4 step 4 may be rejected by the service because the delegation name contains `/`; report it if so.

## Workflow follow-up

- Archive the change with `/opsx:archive network-subnets-tests-subnet-scenarios` only after the user has reviewed the recordings and chosen to retain the change.
