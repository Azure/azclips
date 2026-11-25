# Tasks

All tasks follow `Applying A Scenario-Test Change` in `.github/skills/azclips-scenario-test-converter/SKILL.md` and edit only its Target Locations. No product code changes.

## 1. Register the Network extension with the scenario host

- [x] 1.1 If no earlier change has added them, add an `ExtensionUnderTest` entry for the `network` module, with the root leaf `""` among its leaves, to `tests/Scenario.Tests/Infrastructure/ScenarioHost.cs`, and the `ProjectReference` to `src/Extensions/Network` in `tests/Scenario.Tests/Scenario.Tests.csproj`; verify `dotnet build tests/Scenario.Tests --nologo -v q` reports no error in these two files.

## 2. Write the scenario tests

Write each case as one test method in `tests/Scenario.Tests/Network/NetworkSecurityGroupScenarioTests.cs`. Each case creates its own resource group. Turn each `expect:` line into an assertion on that step's result, and drive the same commands with the same values in the record and playback branches. Verify each task by building the test project and confirming the method exists and compiles.

- [x] 2.1 T1: write the test method for scenario "T1 Network security group lifecycle: create, show, list, delete" (test spec requirement "List scope by resource group presence") with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: both
      CLI:
      1. group create --name scenario-nsg-rg --location westus
      2. network nsg create --resource-group scenario-nsg-rg --name test-nsg1 --location westus --tags foo=doo
         - expect: no resource-group GET is sent before the NetworkSecurityGroups_CreateOrUpdate PUT
         - expect: the PUT body has location = "westus" and tags = {"foo": "doo"}
         - expect: output name = "test-nsg1", type = "Microsoft.Network/networkSecurityGroups", location = "westus"
         - expect: output tags = {"foo": "doo"}
         - expect: output securityRules is empty and defaultSecurityRules has 6 items named AllowVnetInBound, AllowAzureLoadBalancerInBound, DenyAllInBound, AllowVnetOutBound, AllowInternetOutBound, DenyAllOutBound (any order)
      3. network nsg show --resource-group scenario-nsg-rg --name test-nsg1
         - expect: output name = "test-nsg1", type = "Microsoft.Network/networkSecurityGroups"
         - expect: output id ends with "/resourceGroups/scenario-nsg-rg/providers/Microsoft.Network/networkSecurityGroups/test-nsg1"
         - expect: output provisioningState = "Succeeded", resourceGuid is not empty, defaultSecurityRules has 6 items
      4. network nsg list --resource-group scenario-nsg-rg
         - expect: request is GET .../resourceGroups/scenario-nsg-rg/providers/Microsoft.Network/networkSecurityGroups (NetworkSecurityGroups_List)
         - expect: output has exactly 1 item, its name = "test-nsg1" and type = "Microsoft.Network/networkSecurityGroups"
      5. network nsg list
         - expect: request is GET /subscriptions/<sub>/providers/Microsoft.Network/networkSecurityGroups (NetworkSecurityGroups_ListAll)
         - expect: every output item has type = "Microsoft.Network/networkSecurityGroups"
         - expect: output contains an item whose id = <step 3: id>
      6. network nsg delete --resource-group scenario-nsg-rg --name test-nsg1
         - expect: request is DELETE .../resourceGroups/scenario-nsg-rg/providers/Microsoft.Network/networkSecurityGroups/test-nsg1 (NetworkSecurityGroups_Delete) and the command completes without error
      7. network nsg list --resource-group scenario-nsg-rg
         - expect: output is empty
      8. network nsg list
         - expect: output contains no item whose id = <step 3: id>
      9. group delete --name scenario-nsg-rg
      PowerShell:
      1. New-AzResourceGroup -Name scenario-nsg-rg -Location westus
      2. New-AzNetworkSecurityGroup -Name test-nsg1 -ResourceGroupName scenario-nsg-rg -Location westus -Tag @{ foo = "doo" }
         - expect: no resource-group GET is sent before the NetworkSecurityGroups_CreateOrUpdate PUT
         - expect: the PUT body has location = "westus" and tags = {"foo": "doo"}
         - expect: output Name = "test-nsg1", Type = "Microsoft.Network/networkSecurityGroups", Location = "westus"
         - expect: output Tags = {"foo": "doo"}
         - expect: output SecurityRules is empty and DefaultSecurityRules has 6 items named AllowVnetInBound, AllowAzureLoadBalancerInBound, DenyAllInBound, AllowVnetOutBound, AllowInternetOutBound, DenyAllOutBound (any order)
      3. Get-AzNetworkSecurityGroup_Get -ResourceGroupName scenario-nsg-rg -Name test-nsg1
         - expect: output Name = "test-nsg1", Type = "Microsoft.Network/networkSecurityGroups"
         - expect: output Id ends with "/resourceGroups/scenario-nsg-rg/providers/Microsoft.Network/networkSecurityGroups/test-nsg1"
         - expect: output ProvisioningState = "Succeeded", ResourceGuid is not empty, DefaultSecurityRules has 6 items
      4. Get-AzNetworkSecurityGroup_List -ResourceGroupName scenario-nsg-rg
         - expect: request is GET .../resourceGroups/scenario-nsg-rg/providers/Microsoft.Network/networkSecurityGroups (NetworkSecurityGroups_List)
         - expect: output has exactly 1 item, its Name = "test-nsg1" and Type = "Microsoft.Network/networkSecurityGroups"
      5. Get-AzNetworkSecurityGroup_List
         - expect: request is GET /subscriptions/<sub>/providers/Microsoft.Network/networkSecurityGroups (NetworkSecurityGroups_ListAll)
         - expect: every output item has Type = "Microsoft.Network/networkSecurityGroups"
         - expect: output contains an item whose Id = <step 3: Id>
      6. Remove-AzNetworkSecurityGroup -ResourceGroupName scenario-nsg-rg -Name test-nsg1
         - expect: request is DELETE .../resourceGroups/scenario-nsg-rg/providers/Microsoft.Network/networkSecurityGroups/test-nsg1 (NetworkSecurityGroups_Delete) and the command completes without error
      7. Get-AzNetworkSecurityGroup_List -ResourceGroupName scenario-nsg-rg
         - expect: output is empty
      8. Get-AzNetworkSecurityGroup_List
         - expect: output contains no item whose Id = <step 3: Id>
      9. Remove-AzResourceGroup -Name scenario-nsg-rg
      Reference: https://github.com/Azure/azure-cli/blob/dev/src/azure-cli/azure/cli/command_modules/network/tests/latest/test_network_commands.py (NetworkSecurityGroupScenarioTest.test_network_nsg); https://github.com/Azure/azure-powershell/blob/main/src/Network/Network.Test/ScenarioTests/NetworkSecurityGroupTests.ps1 (Test-NetworkSecurityGroupCRUD)

- [x] 2.2 T2: write the test method for scenario "T2 Create without location takes the resource group location" (test spec requirement "Default location from the resource group") with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: cli-only
      CLI:
      1. group create --name scenario-nsg-default-location-rg --location westus
      2. network nsg create --resource-group scenario-nsg-default-location-rg --name test-nsg1
         - expect: a GET /subscriptions/<sub>/resourceGroups/scenario-nsg-default-location-rg (ResourceGroups_Get) is sent before the NetworkSecurityGroups_CreateOrUpdate PUT
         - expect: the PUT body has location = "westus"
         - expect: output name = "test-nsg1" and location = "westus"
      3. network nsg delete --resource-group scenario-nsg-default-location-rg --name test-nsg1
      4. group delete --name scenario-nsg-default-location-rg
      Reference: https://github.com/Azure/azure-cli/blob/dev/src/azure-cli/azure/cli/command_modules/network/tests/latest/test_network_commands.py (NetworkSecurityGroupScenarioTest.test_network_nsg, `network nsg create --name {nsg} -g {rg} --tags foo=doo` without --location)

## 3. Replay, review, and record

- [x] 3.1 Build the test project and run the new scenarios in playback mode (`dotnet test tests/Scenario.Tests --filter FullyQualifiedName~NetworkSecurityGroupScenarioTests`); verify each scenario either passes or fails only as `recording-required`, and report any other failure.
- [ ] 3.2 Stop for user review of the scenario code with the Apply Report; apply the corrections the user asks for and show the result again, repeating until the user confirms. Verify by the user's explicit confirmation.
- [ ] 3.3 After that confirmation, ask the user whether to record. Record only the scenarios the user approves, one `--filter` at a time, after confirming the user is signed in with the azclips CLI, following `Record vs. playback` in `tests/Scenario.Tests/README.md`; verify the approved scenarios pass in playback against the new recordings, and report the recordings for review and any resources still needing cleanup. The subscription-wide list in T1 records every network security group in the subscription; report its size and whether a list sanitiser is needed.

## Workflow follow-up

- Archive the change with `/opsx:archive network-network-security-groups-tests-nsg-scenarios` only after the user has reviewed the recordings and chosen to retain the change.
