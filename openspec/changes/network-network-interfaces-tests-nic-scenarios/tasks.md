# Tasks

All tasks follow `Applying A Scenario-Test Change` in `.github/skills/azclips-scenario-test-converter/SKILL.md` and edit only its Target Locations. No product code changes.

## 1. Register the Network extension with the scenario host

- [ ] 1.1 If no earlier change has added them, add an `ExtensionUnderTest` entry for the `network` module, with the root leaf `""` among its leaves, to `tests/Scenario.Tests/Infrastructure/ScenarioHost.cs`, and the `ProjectReference` to `src/Extensions/Network` in `tests/Scenario.Tests/Scenario.Tests.csproj`; verify `dotnet build tests/Scenario.Tests --nologo -v q` reports no error in these two files.

## 2. Write the scenario tests

Write each case as one test method under `tests/Scenario.Tests/Network/`, in the file the task names; each case has its own resource group and recording. The steps below are carried as given: replace each placeholder (`<rg>`, `<location>`, `<vnet>`, `<subnet>`, `<nic>`, `<nsg>`, `<pip>`, `<subscription>`) with one fixed literal per test method, using a distinct resource group per case (for example `scenario-nic-lifecycle-rg`, `scenario-nic-options-rg`, `scenario-nic-refs-rg`, `scenario-nic-edgezone-rg`), and `<edge-zone>` with `microsoftlosangeles1`. Turn each `expect:` line into an assertion on that step's result, and drive the same commands with the same values on both surfaces so they send identical requests. Verify each task by building the test project and confirming the method exists and compiles.

- [ ] 2.1 T1: write the test method for scenario "T1 Create with defaults, read, list and delete a network interface" (test spec requirement "List scope by resource group presence") in `tests/Scenario.Tests/Network/NetworkInterfaceLifecycleScenarioTests.cs` with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: both
      CLI:
      1. group create --name <rg> --location <location>
      2. network vnet create -g <rg> -n <vnet> -l <location> --address-prefixes 10.0.0.0/16
      3. network vnet subnet create -g <rg> --vnet-name <vnet> -n <subnet> --address-prefixes 10.0.1.0/24
      4. network nic create -g <rg> -n <nic> -l <location> --subnet <subnet> --vnet-name <vnet>
         - expect: Name == <nic>
         - expect: ProvisioningState == "Succeeded"
         - expect: IpConfigurations has 1 item and IpConfigurations[0].Name == "ipconfig1"
         - expect: IpConfigurations[0].PrivateIPAllocationMethod == "Dynamic"
         - expect: IpConfigurations[0].SubnetId ends with "/virtualNetworks/<vnet>/subnets/<subnet>" (case-insensitive)
         - expect: request body properties.ipConfigurations[0].properties.subnet.id ends with "/resourceGroups/<rg>/providers/Microsoft.Network/virtualNetworks/<vnet>/subnets/<subnet>"
         - expect: IpConfigurations[0].PublicIPAddress is null
      5. network nic show -g <rg> -n <nic>
         - expect: Name == <nic>
         - expect: Location == <location>
         - expect: Type == "Microsoft.Network/networkInterfaces"
         - expect: IpConfigurations[0].PrivateIPAllocationMethod == "Dynamic"
      6. network nic list -g <rg>
         - expect: request URL path contains "/resourceGroups/<rg>/providers/Microsoft.Network/networkInterfaces"
         - expect: every item has Type == "Microsoft.Network/networkInterfaces"
         - expect: exactly one item has Name == <nic>
      7. network nic list
         - expect: request URL path is "/subscriptions/<subscription>/providers/Microsoft.Network/networkInterfaces" (no resourceGroups segment)
         - expect: every item Id contains "/networkInterfaces/"
         - expect: one item has Name == <nic>
      8. network nic delete -g <rg> -n <nic>
         - expect: request is DELETE on ".../resourceGroups/<rg>/providers/Microsoft.Network/networkInterfaces/<nic>" and completes without error
      9. network nic list -g <rg>
         - expect: result is empty
      10. network nic show -g <rg> -n <nic>
         - expect: fails with a not-found error (ResourceNotFound)
      11. group delete --name <rg>
      PowerShell:
      1. New-AzResourceGroup -Name <rg> -Location <location>
      2. New-AzVirtualNetwork -ResourceGroupName <rg> -Name <vnet> -Location <location> -AddressPrefix @("10.0.0.0/16") -SubnetNames @("<subnet>") -SubnetAddressPrefixes @(,@("10.0.1.0/24"))
      3. New-AzNetworkInterface -ResourceGroupName <rg> -Name <nic> -Location <location> -SubnetId <step 2: Subnets[0].Id>
         - expect: Name == <nic>
         - expect: ProvisioningState == "Succeeded"
         - expect: IpConfigurations has 1 item and IpConfigurations[0].Name == "ipconfig1"
         - expect: IpConfigurations[0].PrivateIPAllocationMethod == "Dynamic"
         - expect: IpConfigurations[0].SubnetId equals <step 2: Subnets[0].Id> (case-insensitive)
         - expect: IpConfigurations[0].PublicIPAddress is null
      4. Get-AzNetworkInterface_Get -ResourceGroupName <rg> -Name <nic>
         - expect: Name == <nic>
         - expect: Location == <location>
         - expect: Type == "Microsoft.Network/networkInterfaces"
         - expect: IpConfigurations[0].PrivateIPAllocationMethod == "Dynamic"
      5. Get-AzNetworkInterface_List -ResourceGroupName <rg>
         - expect: request URL path contains "/resourceGroups/<rg>/providers/Microsoft.Network/networkInterfaces"
         - expect: exactly one item has Name == <nic> and ProvisioningState == "Succeeded"
      6. Get-AzNetworkInterface_List
         - expect: request URL path is "/subscriptions/<subscription>/providers/Microsoft.Network/networkInterfaces" (no resourceGroups segment)
         - expect: one item has Name == <nic>
      7. Remove-AzNetworkInterface -ResourceGroupName <rg> -Name <nic>
         - expect: request is DELETE on ".../resourceGroups/<rg>/providers/Microsoft.Network/networkInterfaces/<nic>" and completes without error
      8. Get-AzNetworkInterface_List -ResourceGroupName <rg>
         - expect: result is empty
      9. Get-AzNetworkInterface_Get -ResourceGroupName <rg> -Name <nic>
         - expect: fails with a not-found error (ResourceNotFound)
      10. Remove-AzResourceGroup -Name <rg>
      Reference: azure-cli test_network_commands.py::test_network_nic; azure-powershell NetworkInterfaceTests.ps1 Test-NetworkInterfaceCRUDUsingId, Test-NetworkInterfaceNoPublicIpAddress; NetworkInterfaceTestsGenerated.ps1 Test-NetworkInterfaceCRUDMinimalParameters

- [ ] 2.2 T2: write the test method for scenario "T2 Create with static private IP, public IP by name, and optional settings; show with expand" (test spec requirement "Single IP configuration from flat create inputs") in `tests/Scenario.Tests/Network/NetworkInterfaceCreateOptionsScenarioTests.cs` with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: both
      CLI:
      1. group create --name <rg> --location <location>
      2. network vnet create -g <rg> -n <vnet> -l <location> --address-prefixes 10.0.0.0/16
      3. network vnet subnet create -g <rg> --vnet-name <vnet> -n <subnet> --address-prefixes 10.0.1.0/24
      4. network public-ip create -g <rg> -n <pip> -l <location> --sku Standard --allocation-method Static
      5. network nic create -g <rg> -n <nic> -l <location> --subnet <step 3: id> --private-ip-address 10.0.1.5 --public-ip-address <pip> --ip-forwarding true --accelerated-networking true --internal-dns-name test --dns-servers 100.1.2.3 --tags foo=doo
         - expect: ProvisioningState == "Succeeded"
         - expect: IpConfigurations[0].PrivateIPAllocationMethod == "Static"
         - expect: IpConfigurations[0].PrivateIPAddress == "10.0.1.5"
         - expect: IpConfigurations[0].PublicIPAddress.Id ends with "/publicIPAddresses/<pip>" (case-insensitive)
         - expect: request body properties.ipConfigurations[0].properties.publicIPAddress.id ends with "/resourceGroups/<rg>/providers/Microsoft.Network/publicIPAddresses/<pip>"
         - expect: EnableIPForwarding == true
         - expect: EnableAcceleratedNetworking == true
         - expect: DnsSettingsInternalDnsNameLabel == "test"
         - expect: DnsSettingsDnsServers == ["100.1.2.3"]
         - expect: Tags["foo"] == "doo"
      6. network nic show -g <rg> -n <nic>
         - expect: IpConfigurations[0].PrivateIPAllocationMethod == "Static"
         - expect: IpConfigurations[0].PrivateIPAddress == "10.0.1.5"
         - expect: DnsSettingsInternalDnsNameLabel == "test"
         - expect: IpConfigurations[0].PublicIPAddress.Name is null
      7. network nic show -g <rg> -n <nic> --expand ipConfigurations/publicIPAddress
         - expect: request URL query has $expand == "ipConfigurations/publicIPAddress"
         - expect: IpConfigurations[0].PublicIPAddress.Name == <pip>
      8. group delete --name <rg>
      PowerShell:
      1. New-AzResourceGroup -Name <rg> -Location <location>
      2. New-AzVirtualNetwork -ResourceGroupName <rg> -Name <vnet> -Location <location> -AddressPrefix @("10.0.0.0/16") -SubnetNames @("<subnet>") -SubnetAddressPrefixes @(,@("10.0.1.0/24"))
      3. New-AzPublicIpAddress -ResourceGroupName <rg> -Name <pip> -Location <location> -Sku Standard -AllocationMethod Static
      4. New-AzNetworkInterface -ResourceGroupName <rg> -Name <nic> -Location <location> -SubnetId <step 2: Subnets[0].Id> -PublicIpAddressId <step 3: Id> -PrivateIpAddress 10.0.1.5 -IpConfigurationName createipconf -EnableIPForwarding $true -EnableAcceleratedNetworking $true -InternalDnsNameLabel test -DnsServer @("100.1.2.3") -Tag @{ foo = "doo" }
         - expect: ProvisioningState == "Succeeded"
         - expect: IpConfigurations[0].Name == "createipconf"
         - expect: IpConfigurations[0].PrivateIPAllocationMethod == "Static"
         - expect: IpConfigurations[0].PrivateIPAddress == "10.0.1.5"
         - expect: IpConfigurations[0].PublicIPAddress.Id equals <step 3: Id> (case-insensitive)
         - expect: EnableIPForwarding == true
         - expect: EnableAcceleratedNetworking == true
         - expect: DnsSettingsInternalDnsNameLabel == "test"
         - expect: DnsSettingsDnsServers == ["100.1.2.3"]
         - expect: Tags["foo"] == "doo"
      5. Get-AzNetworkInterface_Get -ResourceGroupName <rg> -Name <nic>
         - expect: IpConfigurations[0].Name == "createipconf"
         - expect: IpConfigurations[0].PrivateIPAddress == "10.0.1.5"
         - expect: DnsSettingsInternalDnsNameLabel == "test"
         - expect: IpConfigurations[0].PublicIPAddress.Name is null
      6. Get-AzNetworkInterface_Get -ResourceGroupName <rg> -Name <nic> -ExpandResource "IpConfigurations/PublicIPAddress"
         - expect: request URL query has $expand == "IpConfigurations/PublicIPAddress"
         - expect: IpConfigurations[0].PublicIPAddress.Name == <pip>
      7. Remove-AzResourceGroup -Name <rg>
      Reference: azure-cli test_network_commands.py::test_network_nic (optional-parameters create), ::test_network_nic_auxiliary; azure-powershell NetworkInterfaceTests.ps1 Test-NetworkInterfaceCRUD, Test-NetworkInterfaceCRUDStaticAllocation, Test-NetworkInterfaceIDns, Test-NetworkInterfaceWithAcceleratedNetworking, Test-NetworkInterfaceExpandResource; NetworkInterfaceTestsGenerated.ps1 Test-NetworkInterfaceCRUDAllParameters

- [ ] 2.3 T3: write the test method for scenario "T3 Create with network security group and public IP references, then re-create without IP forwarding" (test spec requirement "CLI name-or-ID expansion for create references") in `tests/Scenario.Tests/Network/NetworkInterfaceReferencesScenarioTests.cs` with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: both
      CLI:
      1. group create --name <rg> --location <location>
      2. network vnet create -g <rg> -n <vnet> -l <location> --address-prefixes 10.0.0.0/16
      3. network vnet subnet create -g <rg> --vnet-name <vnet> -n <subnet> --address-prefixes 10.0.1.0/24
      4. network nsg create -g <rg> -n <nsg> -l <location>
      5. network public-ip create -g <rg> -n <pip> -l <location> --sku Standard --allocation-method Static
      6. network nic create -g <rg> -n <nic> -l <location> --subnet <subnet> --vnet-name <vnet> --network-security-group <nsg> --ip-forwarding true
         - expect: NetworkSecurityGroupId ends with "/networkSecurityGroups/<nsg>" (case-insensitive)
         - expect: request body properties.networkSecurityGroup.id ends with "/resourceGroups/<rg>/providers/Microsoft.Network/networkSecurityGroups/<nsg>"
         - expect: EnableIPForwarding == true
         - expect: IpConfigurations[0].PrivateIPAllocationMethod == "Dynamic"
      7. network nic create -g <rg> -n <nic> -l <location> --subnet <step 3: id> --network-security-group <step 4: id> --public-ip-address <step 5: id>
         - expect: ProvisioningState == "Succeeded"
         - expect: request body properties.networkSecurityGroup.id equals <step 4: id>
         - expect: request body properties.ipConfigurations[0].properties.publicIPAddress.id equals <step 5: id>
         - expect: request body has no properties.enableIPForwarding
         - expect: EnableIPForwarding == false
         - expect: NetworkSecurityGroupId ends with "/networkSecurityGroups/<nsg>" (case-insensitive)
         - expect: IpConfigurations[0].PublicIPAddress.Id ends with "/publicIPAddresses/<pip>" (case-insensitive)
         - expect: IpConfigurations[0].PrivateIPAllocationMethod == "Dynamic"
      8. group delete --name <rg>
      PowerShell:
      1. New-AzResourceGroup -Name <rg> -Location <location>
      2. New-AzVirtualNetwork -ResourceGroupName <rg> -Name <vnet> -Location <location> -AddressPrefix @("10.0.0.0/16") -SubnetNames @("<subnet>") -SubnetAddressPrefixes @(,@("10.0.1.0/24"))
      3. New-AzNetworkSecurityGroup -ResourceGroupName <rg> -Name <nsg> -Location <location>
      4. New-AzPublicIpAddress -ResourceGroupName <rg> -Name <pip> -Location <location> -Sku Standard -AllocationMethod Static
      5. New-AzNetworkInterface -ResourceGroupName <rg> -Name <nic> -Location <location> -SubnetId <step 2: Subnets[0].Id> -NetworkSecurityGroupId <step 3: Id> -EnableIPForwarding $true
         - expect: NetworkSecurityGroupId equals <step 3: Id> (case-insensitive)
         - expect: EnableIPForwarding == true
         - expect: IpConfigurations[0].PrivateIPAllocationMethod == "Dynamic"
      6. New-AzNetworkInterface -ResourceGroupName <rg> -Name <nic> -Location <location> -SubnetId <step 2: Subnets[0].Id> -NetworkSecurityGroupId <step 3: Id> -PublicIpAddressId <step 4: Id>
         - expect: ProvisioningState == "Succeeded"
         - expect: request body has no properties.enableIPForwarding
         - expect: EnableIPForwarding == false
         - expect: IpConfigurations[0].PublicIPAddress.Id equals <step 4: Id> (case-insensitive)
         - expect: NetworkSecurityGroupId equals <step 3: Id> (case-insensitive)
      7. Remove-AzResourceGroup -Name <rg>
      Reference: azure-cli test_network_commands.py::test_network_nic (NSG by name, NSG and public IP by id); azure-powershell NetworkInterfaceTests.ps1 Test-NetworkInterfaceEnableIPForwarding, Test-NetworkInterfaceIpv6 (create part)

- [ ] 2.4 T4: write the test method for scenario "T4 Create a network interface in an edge zone" (test spec requirement "Edge zone as extended location") in `tests/Scenario.Tests/Network/NetworkInterfaceEdgeZoneScenarioTests.cs` with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: both
      CLI:
      1. group create --name <rg> --location westus
      2. network vnet create -g <rg> -n <vnet> -l westus --address-prefixes 10.0.0.0/16 --extended-location <edge-zone>
      3. network vnet subnet create -g <rg> --vnet-name <vnet> -n <subnet> --address-prefixes 10.0.0.0/24
      4. network nic create -g <rg> -n <nic> -l westus --subnet <step 3: id> --edge-zone <edge-zone>
         - expect: request body extendedLocation.name == <edge-zone> and extendedLocation.type == "EdgeZone"
         - expect: ExtendedLocationName == <edge-zone>
         - expect: ExtendedLocationType == "EdgeZone"
      5. network nic show -g <rg> -n <nic>
         - expect: ExtendedLocationName == <edge-zone>
         - expect: ExtendedLocationType == "EdgeZone"
      6. group delete --name <rg>
      PowerShell:
      1. New-AzResourceGroup -Name <rg> -Location westus
      2. New-AzVirtualNetwork -ResourceGroupName <rg> -Name <vnet> -Location westus -EdgeZone <edge-zone> -AddressPrefix @("10.0.0.0/16") -SubnetNames @("<subnet>") -SubnetAddressPrefixes @(,@("10.0.0.0/24"))
      3. New-AzNetworkInterface -ResourceGroupName <rg> -Name <nic> -Location westus -EdgeZone <edge-zone> -SubnetId <step 2: Subnets[0].Id>
         - expect: request body extendedLocation.name == <edge-zone> and extendedLocation.type == "EdgeZone"
         - expect: ExtendedLocationName == <edge-zone>
         - expect: ExtendedLocationType == "EdgeZone"
      4. Get-AzNetworkInterface_Get -ResourceGroupName <rg> -Name <nic>
         - expect: ExtendedLocationName == <edge-zone>
         - expect: ExtendedLocationType == "EdgeZone"
      5. Remove-AzResourceGroup -Name <rg>
      Reference: azure-cli test_network_commands.py::test_network_nic_edge_zone; azure-powershell NetworkInterfaceTests.ps1 Test-NetworkInterfaceInEdgeZone (edge zone microsoftlosangeles1, location westus)

## 3. Replay, review, and record

- [ ] 3.1 Build the test project and run the new scenarios in playback mode (`dotnet test tests/Scenario.Tests --filter FullyQualifiedName~NetworkInterface`); verify each scenario either passes or fails only as `recording-required`, and report any other failure.
- [ ] 3.2 Stop for user review of the scenario code with the Apply Report; apply the corrections the user asks for and show the result again, repeating until the user confirms. Verify by the user's explicit confirmation.
- [ ] 3.3 After that confirmation, ask the user whether to record. Record only the scenarios the user approves, one `--filter` at a time, after confirming the user is signed in with the azclips CLI, following `Record vs. playback` in `tests/Scenario.Tests/README.md`; verify the approved scenarios pass in playback against the new recordings, and report the recordings for review and any resources still needing cleanup. Report whether the T4 edge zone is available to the subscription (if creation fails with an edge-zone-not-supported error, report T4 as unrecordable rather than changing it) and whether the T2 region accepts accelerated networking on a NIC without a VM.

## Workflow follow-up

- Archive the change with `/opsx:archive network-network-interfaces-tests-nic-scenarios` only after the user has reviewed the recordings and chosen to retain the change.
