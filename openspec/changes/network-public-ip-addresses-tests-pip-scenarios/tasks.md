# Tasks

All tasks follow `Applying A Scenario-Test Change` in `.github/skills/azclips-scenario-test-converter/SKILL.md` and edit only its Target Locations. No product code changes.

## 1. Register the Network extension with the scenario host

- [ ] 1.1 If no earlier change has added them, add an `ExtensionUnderTest` entry for the `network` module, with the root leaf `""` among its leaves, to `tests/Scenario.Tests/Infrastructure/ScenarioHost.cs`, and the `ProjectReference` to `src/Extensions/Network` in `tests/Scenario.Tests/Scenario.Tests.csproj`; verify `dotnet build tests/Scenario.Tests --nologo -v q` reports no error in these two files.

## 2. Write the scenario tests

Write each case as one test method under `tests/Scenario.Tests/Network/`, in the file the task names. T1 and T8 share one scenario and recording (T8 runs after T1 step 3, reusing T1's resource group and `pubip3`); T2 to T6 share the resource group `scenario-pip-sku-rg` and one recording; T7 has its own. Turn each `expect:` line into an assertion on that step's result, and drive the same commands with the same values on both surfaces so they send identical requests. Verify each task by building the test project and confirming the method exists and compiles.

- [ ] 2.1 T1: write the test method for scenario "T1 Public IP lifecycle: DNS label scope, IP tags, zones, list, show, delete" (test spec requirement "List scope by resource group presence") in `tests/Scenario.Tests/Network/PublicIpAddressLifecycleScenarioTests.cs` with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: both
      CLI:
      1. group create --name scenario-pip-lifecycle-rg --location eastus2
      2. network public-ip create -g scenario-pip-lifecycle-rg -n pubipdns -l eastus2 --dns-name azclipspipdns1 --dns-name-scope TenantReuse --allocation-method Static
         - expect: output is one PublicIPAddress with Name pubipdns
         - expect: PublicIPAllocationMethod is Static
         - expect: DnsSettingsDomainNameLabel is azclipspipdns1
         - expect: DnsSettingsDomainNameLabelScope is TenantReuse
         - expect: ProvisioningState is Succeeded
      3. network public-ip create -g scenario-pip-lifecycle-rg -n pubip3 -l eastus2 --version IPv4 --sku Standard --zone 1 2 3 --ip-tags RoutingPreference=Internet
         - expect: output is one PublicIPAddress with Name pubip3
         - expect: PublicIPAddressVersion is IPv4
         - expect: PublicIPAllocationMethod is Static
         - expect: Zones contains exactly 1, 2, 3 (any order)
         - expect: IpTags has one entry with IpTagType RoutingPreference and Tag Internet
         - expect: DnsSettingsDomainNameLabel is null
      4. network public-ip list -g scenario-pip-lifecycle-rg
         - expect: output is PublicIPAddress items whose names are exactly pubipdns and pubip3
      5. network public-ip show -g scenario-pip-lifecycle-rg -n pubipdns
         - expect: output is one PublicIPAddress with Name pubipdns and Location eastus2
         - expect: DnsSettingsDomainNameLabel is azclipspipdns1
      6. network public-ip delete -g scenario-pip-lifecycle-rg -n pubipdns
         - expect: request is DELETE on .../resourceGroups/scenario-pip-lifecycle-rg/providers/Microsoft.Network/publicIPAddresses/pubipdns and the step returns no error
      7. network public-ip list -g scenario-pip-lifecycle-rg
         - expect: output contains pubip3 and no PublicIPAddress named pubipdns
      PowerShell:
      1. New-AzResourceGroup -Name scenario-pip-lifecycle-rg -Location eastus2
      2. New-AzPublicIpAddress -ResourceGroupName scenario-pip-lifecycle-rg -Name pubipdns -Location eastus2 -DomainNameLabel azclipspipdns1 -DomainNameLabelScope TenantReuse -AllocationMethod Static
         - expect: output is one PublicIPAddress with Name pubipdns
         - expect: PublicIPAllocationMethod is Static
         - expect: DnsSettingsDomainNameLabel is azclipspipdns1
         - expect: DnsSettingsDomainNameLabelScope is TenantReuse
         - expect: ProvisioningState is Succeeded
      3. New-AzPublicIpAddress -ResourceGroupName scenario-pip-lifecycle-rg -Name pubip3 -Location eastus2 -IpAddressVersion IPv4 -Sku Standard -Zone @('1','2','3') -IpTag @{ RoutingPreference = 'Internet' }
         - expect: output is one PublicIPAddress with Name pubip3
         - expect: PublicIPAddressVersion is IPv4
         - expect: PublicIPAllocationMethod is Static
         - expect: Zones contains exactly 1, 2, 3 (any order)
         - expect: IpTags has one entry with IpTagType RoutingPreference and Tag Internet
         - expect: DnsSettingsDomainNameLabel is null
      4. Get-AzPublicIpAddress_List -ResourceGroupName scenario-pip-lifecycle-rg
         - expect: output is PublicIPAddress items whose names are exactly pubipdns and pubip3
      5. Get-AzPublicIpAddress_Get -ResourceGroupName scenario-pip-lifecycle-rg -Name pubipdns
         - expect: output is one PublicIPAddress with Name pubipdns and Location eastus2
         - expect: DnsSettingsDomainNameLabel is azclipspipdns1
      6. Remove-AzPublicIpAddress -ResourceGroupName scenario-pip-lifecycle-rg -Name pubipdns
         - expect: request is DELETE on .../resourceGroups/scenario-pip-lifecycle-rg/providers/Microsoft.Network/publicIPAddresses/pubipdns and the step returns no error
      7. Get-AzPublicIpAddress_List -ResourceGroupName scenario-pip-lifecycle-rg
         - expect: output contains pubip3 and no PublicIPAddress named pubipdns
      Reference: https://github.com/Azure/azure-cli/blob/dev/src/azure-cli/azure/cli/command_modules/network/tests/latest/test_network_commands.py (test_network_public_ip); https://github.com/Azure/azure-powershell/blob/main/src/Network/Network.Test/ScenarioTests/PublicIpAddressTests.ps1 (Test-PublicIpAddressCRUD)

- [ ] 2.2 T2: write the test method for scenario "T2 SKU defaults, tags, Global tier, and StandardV2" (test spec requirement "SKU default on create") in `tests/Scenario.Tests/Network/PublicIpAddressSkuScenarioTests.cs` with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: both
      CLI:
      1. group create --name scenario-pip-sku-rg --location eastus2
      2. network public-ip create -g scenario-pip-sku-rg -n pubip1 -l eastus2
         - expect: request body has sku.name Standard, properties.publicIPAllocationMethod Static, properties.publicIPAddressVersion IPv4, properties.idleTimeoutInMinutes 4
         - expect: output SkuName is Standard and SkuTier is Regional
         - expect: PublicIPAllocationMethod is Static
         - expect: DnsSettingsDomainNameLabel is null
      3. network public-ip show -g scenario-pip-sku-rg -n pubip1
         - expect: SkuName is Standard, SkuTier is Regional, PublicIPAllocationMethod is Static, PublicIPAddressVersion is IPv4
      4. network public-ip create -g scenario-pip-sku-rg -n pubip2 -l eastus2 --sku Standard --tags foo=doo
         - expect: request body tags is exactly {foo: doo}
      5. network public-ip show -g scenario-pip-sku-rg -n pubip2
         - expect: SkuName is Standard, SkuTier is Regional, PublicIPAllocationMethod is Static
         - expect: Tags has foo = doo
      6. network public-ip create -g scenario-pip-sku-rg -n pubip3 -l eastus2 --sku Standard --tier Global
         - expect: request body has sku.tier Global
      7. network public-ip show -g scenario-pip-sku-rg -n pubip3
         - expect: SkuName is Standard, SkuTier is Global, PublicIPAllocationMethod is Static
      8. network public-ip create -g scenario-pip-sku-rg -n pubip4 -l eastus2 --sku StandardV2 --allocation-method Static
         - expect: output SkuName is StandardV2
      PowerShell:
      1. New-AzResourceGroup -Name scenario-pip-sku-rg -Location eastus2
      2. New-AzPublicIpAddress -ResourceGroupName scenario-pip-sku-rg -Name pubip1 -Location eastus2
         - expect: request body has sku.name Standard, properties.publicIPAllocationMethod Static, properties.publicIPAddressVersion IPv4, properties.idleTimeoutInMinutes 4
         - expect: output SkuName is Standard and SkuTier is Regional
         - expect: PublicIPAllocationMethod is Static
         - expect: DnsSettingsDomainNameLabel is null
      3. Get-AzPublicIpAddress_Get -ResourceGroupName scenario-pip-sku-rg -Name pubip1
         - expect: SkuName is Standard, SkuTier is Regional, PublicIPAllocationMethod is Static, PublicIPAddressVersion is IPv4
      4. New-AzPublicIpAddress -ResourceGroupName scenario-pip-sku-rg -Name pubip2 -Location eastus2 -Sku Standard -Tag @{ foo = 'doo' }
         - expect: request body tags is exactly {foo: doo}
      5. Get-AzPublicIpAddress_Get -ResourceGroupName scenario-pip-sku-rg -Name pubip2
         - expect: SkuName is Standard, SkuTier is Regional, PublicIPAllocationMethod is Static
         - expect: Tags has foo = doo
      6. New-AzPublicIpAddress -ResourceGroupName scenario-pip-sku-rg -Name pubip3 -Location eastus2 -Sku Standard -Tier Global
         - expect: request body has sku.tier Global
      7. Get-AzPublicIpAddress_Get -ResourceGroupName scenario-pip-sku-rg -Name pubip3
         - expect: SkuName is Standard, SkuTier is Global, PublicIPAllocationMethod is Static
      8. New-AzPublicIpAddress -ResourceGroupName scenario-pip-sku-rg -Name pubip4 -Location eastus2 -Sku StandardV2 -AllocationMethod Static
         - expect: output SkuName is StandardV2
      Reference: test_network_commands.py (test_network_public_ip_sku); PublicIpAddressTests.ps1 (Test-PublicIpAddressCRUD-StandardSkuDefault, -StandardSku, -StandardSkuGlobalTier, Test-PublicIpAddressStandardV2UpgradedToV2)

- [ ] 2.3 T3: write the test method for scenario "T3 IPv6 public IP address" (test spec requirement "IP version default on create") in `tests/Scenario.Tests/Network/PublicIpAddressSkuScenarioTests.cs` with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: both
      CLI:
      1. group create --name scenario-pip-sku-rg --location eastus2
      2. network public-ip create -g scenario-pip-sku-rg -n pubipv6 -l eastus2 --allocation-method Static --version IPv6
         - expect: request body has properties.publicIPAddressVersion IPv6
         - expect: output PublicIPAddressVersion is IPv6 and PublicIPAllocationMethod is Static
      3. network public-ip show -g scenario-pip-sku-rg -n pubipv6
         - expect: PublicIPAddressVersion is IPv6 and ProvisioningState is Succeeded
      PowerShell:
      1. New-AzResourceGroup -Name scenario-pip-sku-rg -Location eastus2
      2. New-AzPublicIpAddress -ResourceGroupName scenario-pip-sku-rg -Name pubipv6 -Location eastus2 -AllocationMethod Static -IpAddressVersion IPv6
         - expect: request body has properties.publicIPAddressVersion IPv6
         - expect: output PublicIPAddressVersion is IPv6 and PublicIPAllocationMethod is Static
      3. Get-AzPublicIpAddress_Get -ResourceGroupName scenario-pip-sku-rg -Name pubipv6
         - expect: PublicIPAddressVersion is IPv6 and ProvisioningState is Succeeded
      Reference: PublicIpAddressTests.ps1 (Test-PublicIpAddressIpVersion)

- [ ] 2.4 T4: write the test method for scenario "T4 Explicit idle timeout" (test spec requirement "Idle timeout default on create") in `tests/Scenario.Tests/Network/PublicIpAddressSkuScenarioTests.cs` with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: both
      CLI:
      1. group create --name scenario-pip-sku-rg --location eastus2
      2. network public-ip create -g scenario-pip-sku-rg -n pubipidle -l eastus2 --idle-timeout 15 --allocation-method Static --dns-name azclipspipdns2
         - expect: request body has properties.idleTimeoutInMinutes 15
      3. network public-ip show -g scenario-pip-sku-rg -n pubipidle
         - expect: IdleTimeoutInMinutes is 15
         - expect: PublicIPAllocationMethod is Static and DnsSettingsDomainNameLabel is azclipspipdns2
      PowerShell:
      1. New-AzResourceGroup -Name scenario-pip-sku-rg -Location eastus2
      2. New-AzPublicIpAddress -ResourceGroupName scenario-pip-sku-rg -Name pubipidle -Location eastus2 -IdleTimeoutInMinutes 15 -AllocationMethod Static -DomainNameLabel azclipspipdns2
         - expect: request body has properties.idleTimeoutInMinutes 15
      3. Get-AzPublicIpAddress_Get -ResourceGroupName scenario-pip-sku-rg -Name pubipidle
         - expect: IdleTimeoutInMinutes is 15
         - expect: PublicIPAllocationMethod is Static and DnsSettingsDomainNameLabel is azclipspipdns2
      Reference: PublicIpAddressTests.ps1 (Test-PublicIpAddressCRUD-IdleTimeout)

- [ ] 2.5 T5: write the test method for scenario "T5 DDoS protection mode on create" (test spec requirement "DDoS protection mode and plan") in `tests/Scenario.Tests/Network/PublicIpAddressSkuScenarioTests.cs` with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: both
      CLI:
      1. group create --name scenario-pip-sku-rg --location eastus2
      2. network public-ip create -g scenario-pip-sku-rg -n pubipddos -l eastus2 --allocation-method Static --sku Standard --protection-mode Enabled
         - expect: request body has properties.ddosSettings.protectionMode Enabled and no ddosSettings.ddosProtectionPlan
      3. network public-ip show -g scenario-pip-sku-rg -n pubipddos
         - expect: DdosSettingsProtectionMode is Enabled
         - expect: SkuName is Standard and PublicIPAllocationMethod is Static
      PowerShell:
      1. New-AzResourceGroup -Name scenario-pip-sku-rg -Location eastus2
      2. New-AzPublicIpAddress -ResourceGroupName scenario-pip-sku-rg -Name pubipddos -Location eastus2 -AllocationMethod Static -Sku Standard -DdosProtectionMode Enabled
         - expect: request body has properties.ddosSettings.protectionMode Enabled and no ddosSettings.ddosProtectionPlan
      3. Get-AzPublicIpAddress_Get -ResourceGroupName scenario-pip-sku-rg -Name pubipddos
         - expect: DdosSettingsProtectionMode is Enabled
         - expect: SkuName is Standard and PublicIPAllocationMethod is Static
      Reference: PublicIpAddressTests.ps1 (Test-PublicIpAddressCRUD-DdosProtection, protection-mode part only)

- [ ] 2.6 T6: write the test method for scenario "T6 Basic SKU with Dynamic allocation" (test spec requirement "Allocation method default from the SKU") in `tests/Scenario.Tests/Network/PublicIpAddressSkuScenarioTests.cs` with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: both
      CLI:
      1. group create --name scenario-pip-sku-rg --location eastus2
      2. network public-ip create -g scenario-pip-sku-rg -n pubipbasic -l eastus2 --allocation-method Dynamic --sku Basic --dns-name azclipspipbasic
         - expect: request body has sku.name Basic and properties.publicIPAllocationMethod Dynamic
         - expect: output SkuName is Basic and PublicIPAllocationMethod is Dynamic (verify at record: the service may reject Basic SKU creation; see review_gaps)
      3. network public-ip show -g scenario-pip-sku-rg -n pubipbasic
         - expect: SkuName is Basic, PublicIPAllocationMethod is Dynamic, DnsSettingsDomainNameLabel is azclipspipbasic
      PowerShell:
      1. New-AzResourceGroup -Name scenario-pip-sku-rg -Location eastus2
      2. New-AzPublicIpAddress -ResourceGroupName scenario-pip-sku-rg -Name pubipbasic -Location eastus2 -AllocationMethod Dynamic -Sku Basic -DomainNameLabel azclipspipbasic
         - expect: request body has sku.name Basic and properties.publicIPAllocationMethod Dynamic
         - expect: output SkuName is Basic and PublicIPAllocationMethod is Dynamic (verify at record: the service may reject Basic SKU creation; see review_gaps)
      3. Get-AzPublicIpAddress_Get -ResourceGroupName scenario-pip-sku-rg -Name pubipbasic
         - expect: SkuName is Basic, PublicIPAllocationMethod is Dynamic, DnsSettingsDomainNameLabel is azclipspipbasic
      Reference: PublicIpAddressTests.ps1 (Test-PublicIpAddressCRUD-BasicSku)

- [ ] 2.7 T7: write the test method for scenario "T7 Public IP in an edge zone" (test spec requirement "Edge zone as extended location") in `tests/Scenario.Tests/Network/PublicIpAddressEdgeZoneScenarioTests.cs` with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: both
      CLI:
      1. group create --name scenario-pip-edgezone-rg --location westus
      2. network public-ip create -g scenario-pip-edgezone-rg -n pubipedge -l westus --edge-zone microsoftlosangeles1 --sku Standard
         - expect: request body has extendedLocation.name microsoftlosangeles1 and extendedLocation.type EdgeZone
      3. network public-ip show -g scenario-pip-edgezone-rg -n pubipedge
         - expect: ExtendedLocationName is microsoftlosangeles1 and ExtendedLocationType is EdgeZone
      PowerShell:
      1. New-AzResourceGroup -Name scenario-pip-edgezone-rg -Location westus
      2. New-AzPublicIpAddress -ResourceGroupName scenario-pip-edgezone-rg -Name pubipedge -Location westus -EdgeZone microsoftlosangeles1 -Sku Standard
         - expect: request body has extendedLocation.name microsoftlosangeles1 and extendedLocation.type EdgeZone
      3. Get-AzPublicIpAddress_Get -ResourceGroupName scenario-pip-edgezone-rg -Name pubipedge
         - expect: ExtendedLocationName is microsoftlosangeles1 and ExtendedLocationType is EdgeZone
      Reference: test_network_commands.py (test_network_public_ip_edge_zone); PublicIpAddressTests.ps1 (Test-PublicIpAddressInEdgeZone)

- [ ] 2.8 T8: write the test method for scenario "T8 List public IP addresses in the subscription (synthesized)" (test spec requirement "List scope by resource group presence") in `tests/Scenario.Tests/Network/PublicIpAddressLifecycleScenarioTests.cs` with these steps and expectations, naming the case ID and scenario in a comment; verify it compiles.
      Surface: both
      CLI:
      1. group create --name scenario-pip-lifecycle-rg --location eastus2
      2. network public-ip create -g scenario-pip-lifecycle-rg -n pubip3 -l eastus2 --version IPv4 --sku Standard --zone 1 2 3 --ip-tags RoutingPreference=Internet
      3. network public-ip list
         - expect: request is GET /subscriptions/<sub>/providers/Microsoft.Network/publicIPAddresses (no resourceGroups segment)
         - expect: output contains a PublicIPAddress named pubip3
      PowerShell:
      1. New-AzResourceGroup -Name scenario-pip-lifecycle-rg -Location eastus2
      2. New-AzPublicIpAddress -ResourceGroupName scenario-pip-lifecycle-rg -Name pubip3 -Location eastus2 -IpAddressVersion IPv4 -Sku Standard -Zone @('1','2','3') -IpTag @{ RoutingPreference = 'Internet' }
      3. Get-AzPublicIpAddress_List
         - expect: request is GET /subscriptions/<sub>/providers/Microsoft.Network/publicIPAddresses (no resourceGroups segment)
         - expect: output contains a PublicIPAddress named pubip3
      Reference: none (synthesized for the ListAll branch of ListPublicIPAddressProcessor_Orchestrator; place it inside the T1 scenario after T1 step 3 so it shares T1's recording)

## 3. Replay, review, and record

- [ ] 3.1 Build the test project and run the new scenarios in playback mode (`dotnet test tests/Scenario.Tests --filter FullyQualifiedName~PublicIpAddress`); verify each scenario either passes or fails only as `recording-required`, and report any other failure.
- [ ] 3.2 Stop for user review of the scenario code with the Apply Report; apply the corrections the user asks for and show the result again, repeating until the user confirms. Verify by the user's explicit confirmation.
- [ ] 3.3 After that confirmation, ask the user whether to record. Record only the scenarios the user approves, one `--filter` at a time, after confirming the user is signed in with the azclips CLI, following `Record vs. playback` in `tests/Scenario.Tests/README.md`; verify the approved scenarios pass in playback against the new recordings, and report the recordings for review and any resources still needing cleanup. Report whether T6 Basic SKU creation was rejected, whether the T7 edge zone is available to the subscription, and the size of the T8 subscription-wide list and whether a list sanitiser is needed; delete the T5 DDoS-protected public IP promptly.

## Workflow follow-up

- Archive the change with `/opsx:archive network-public-ip-addresses-tests-pip-scenarios` only after the user has reviewed the recordings and chosen to retain the change.
