# Extension test coverage

Generated from `1b3026f0543a` on 2026-10-19 07:05:36 UTC.

This report shows conservative mapped test signals for public extension commands. It is not a precise line-coverage or full static-analysis report; unresolved evidence is listed separately instead of being counted as covered or missing.

Docs presence is reported separately from docs/help validation tests. Docs/help test attribution is currently not analyzed.

## Summary

| Extension | Commands | Good | Partial | Gaps | Scenario gaps | CLI bench | PS bench |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| Accounts | 16 | 0 | 8 | 8 | 16 | no | no |
| Compute | 17 | 0 | 13 | 4 | 5 | 1 | 1 |
| Config | 10 | 0 | 0 | 10 | 10 | no | no |
| Extension | 7 | 0 | 0 | 7 | 7 | no | no |
| KeyVault | 13 | 4 | 9 | 0 | 9 | no | no |
| ManagedServiceIdentity | 10 | 5 | 3 | 2 | 5 | 1 | 1 |
| MsGraph | 27 | 0 | 21 | 6 | 27 | no | no |
| Network | 21 | 19 | 2 | 0 | 0 | no | no |
| Resources | 36 | 5 | 26 | 5 | 29 | 1 | 1 |

## Highest priority gaps

| Extension | Surface | Command | Gaps |
| --- | --- | --- | --- |
| Accounts | CLI | `profile list` | missing-unit, missing-scenario, missing-benchmark |
| Accounts | CLI | `profile set` | missing-unit, missing-scenario, missing-benchmark |
| Accounts | CLI | `profile show` | missing-unit, missing-scenario, missing-benchmark |
| Accounts | CLI | `survey` | missing-unit, missing-scenario, missing-benchmark |
| Accounts | PowerShell | `Get-AzProfile_List` | missing-unit, missing-scenario, missing-benchmark |
| Accounts | PowerShell | `Get-AzProfile_Show` | missing-unit, missing-scenario, missing-benchmark |
| Accounts | PowerShell | `Open-AzSurvey` | missing-unit, missing-scenario, missing-benchmark |
| Accounts | PowerShell | `Set-AzProfile` | missing-unit, missing-scenario, missing-benchmark |
| Compute | CLI | `vm list` | missing-unit, missing-scenario |
| Compute | PowerShell | `Get-AzVM_Id` | missing-unit, missing-scenario, missing-benchmark |
| Compute | PowerShell | `Get-AzVM_ListByName` | missing-unit, missing-scenario, missing-benchmark |
| Compute | PowerShell | `Get-AzVM_ListLocation` | missing-unit, missing-scenario, missing-benchmark |
| Config | CLI | `config delete` | missing-unit, missing-scenario, missing-benchmark |
| Config | CLI | `config get` | missing-unit, missing-scenario, missing-benchmark |
| Config | CLI | `config info` | missing-unit, missing-scenario, missing-benchmark |
| Config | CLI | `config list` | missing-unit, missing-scenario, missing-benchmark |
| Config | CLI | `config set` | missing-unit, missing-scenario, missing-benchmark |
| Config | PowerShell | `Get-AzConfig` | missing-unit, missing-scenario, missing-benchmark |
| Config | PowerShell | `Get-AzConfigInfo` | missing-unit, missing-scenario, missing-benchmark |
| Config | PowerShell | `Get-AzConfigList` | missing-unit, missing-scenario, missing-benchmark |
| Config | PowerShell | `Remove-AzConfig` | missing-unit, missing-scenario, missing-benchmark |
| Config | PowerShell | `Set-AzConfig` | missing-unit, missing-scenario, missing-benchmark |
| Extension | CLI | `extension add` | missing-unit, missing-scenario, missing-benchmark |
| Extension | CLI | `extension list` | missing-unit, missing-scenario, missing-benchmark |
| Extension | CLI | `extension pin` | missing-unit, missing-scenario, missing-benchmark |
| Extension | CLI | `extension remove` | missing-unit, missing-scenario, missing-benchmark |
| Extension | CLI | `extension show` | missing-unit, missing-scenario, missing-benchmark |
| Extension | CLI | `extension unpin` | missing-unit, missing-scenario, missing-benchmark |
| Extension | CLI | `extension update` | missing-unit, missing-scenario, missing-benchmark |
| ManagedServiceIdentity | PowerShell | `Get-AzUserAssignedIdentity_Get` | missing-unit, missing-scenario, missing-benchmark |
| ManagedServiceIdentity | PowerShell | `Remove-AzUserAssignedIdentity` | missing-unit, missing-scenario, missing-benchmark |
| MsGraph | CLI | `msgraph app create` | missing-unit, missing-scenario, missing-benchmark |
| MsGraph | CLI | `msgraph app delete` | missing-unit, missing-scenario, missing-benchmark |
| MsGraph | CLI | `msgraph app show` | missing-unit, missing-scenario, missing-benchmark |
| MsGraph | PowerShell | `Get-AzMsGraphApplication_Get` | missing-unit, missing-scenario, missing-benchmark |
| MsGraph | PowerShell | `New-AzMsGraphApplication` | missing-unit, missing-scenario, missing-benchmark |
| MsGraph | PowerShell | `Remove-AzMsGraphApplication` | missing-unit, missing-scenario, missing-benchmark |
| Resources | CLI | `deployment group export` | missing-unit, missing-scenario, missing-benchmark |
| Resources | CLI | `group list` | missing-unit, missing-scenario |
| Resources | CLI | `group show` | missing-unit, missing-scenario |
| Resources | PowerShell | `Get-AzResourceGroup_Get` | missing-unit, missing-scenario, missing-benchmark |
| Resources | PowerShell | `Get-AzResourceGroup_List` | missing-unit, missing-scenario, missing-benchmark |
| Accounts | CLI | `account get-access-token` | missing-scenario, missing-benchmark |
| Accounts | CLI | `login` | missing-scenario, missing-benchmark |
| Accounts | CLI | `logout` | missing-scenario, missing-benchmark |
| Accounts | CLI | `profile remove` | missing-scenario, missing-benchmark |
| Accounts | PowerShell | `Connect-AzAccount` | missing-scenario, missing-benchmark |
| Accounts | PowerShell | `Disconnect-AzAccount` | missing-scenario, missing-benchmark |
| Accounts | PowerShell | `Get-AzAccessToken` | missing-scenario, missing-benchmark |
| Accounts | PowerShell | `Remove-AzProfile` | missing-scenario, missing-benchmark |

## Accounts

Extension-level benchmarks: CLI=no, PowerShell=no.

| Surface | Command | Unit | Scenario | Script scenario | Docs present | Docs test | Bench | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CLI | `account get-access-token` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `login` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `logout` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `profile list` | no | no | no | yes | not analyzed | no | Gap |
| CLI | `profile remove` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `profile set` | no | no | no | yes | not analyzed | no | Gap |
| CLI | `profile show` | no | no | no | yes | not analyzed | no | Gap |
| CLI | `survey` | no | no | no | yes | not analyzed | no | Gap |
| PowerShell | `Connect-AzAccount` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Disconnect-AzAccount` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzAccessToken` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzProfile_List` | no | no | no | yes | not analyzed | no | Gap |
| PowerShell | `Get-AzProfile_Show` | no | no | no | yes | not analyzed | no | Gap |
| PowerShell | `Open-AzSurvey` | no | no | no | yes | not analyzed | no | Gap |
| PowerShell | `Remove-AzProfile` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Set-AzProfile` | no | no | no | yes | not analyzed | no | Gap |

## Compute

Extension-level benchmarks: CLI=1, PowerShell=1.

| Surface | Command | Unit | Scenario | Script scenario | Docs present | Docs test | Bench | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CLI | `disk list` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `vm create` | no | yes | yes | yes | not analyzed | yes | Partial |
| CLI | `vm deallocate` | no | yes | no | yes | not analyzed | no | Partial |
| CLI | `vm delete` | no | yes | no | yes | not analyzed | no | Partial |
| CLI | `vm list` | no | no | no | yes | not analyzed | yes | Gap |
| CLI | `vm show` | no | yes | no | yes | not analyzed | yes | Partial |
| CLI | `vm start` | no | yes | no | yes | not analyzed | no | Partial |
| CLI | `vm stop` | no | yes | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzVM_Get` | no | yes | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzVM_Id` | no | no | no | yes | not analyzed | no | Gap |
| PowerShell | `Get-AzVM_List` | no | yes | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzVM_ListByName` | no | no | no | yes | not analyzed | no | Gap |
| PowerShell | `Get-AzVM_ListLocation` | no | no | no | yes | not analyzed | no | Gap |
| PowerShell | `New-AzVM` | no | yes | yes | yes | not analyzed | yes | Partial |
| PowerShell | `Remove-AzVM` | no | yes | no | yes | not analyzed | no | Partial |
| PowerShell | `Start-AzVM` | no | yes | no | yes | not analyzed | no | Partial |
| PowerShell | `Stop-AzVM` | no | yes | no | yes | not analyzed | no | Partial |

## Config

Extension-level benchmarks: CLI=no, PowerShell=no.

| Surface | Command | Unit | Scenario | Script scenario | Docs present | Docs test | Bench | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CLI | `config delete` | no | no | no | yes | not analyzed | no | Gap |
| CLI | `config get` | no | no | no | yes | not analyzed | no | Gap |
| CLI | `config info` | no | no | no | yes | not analyzed | no | Gap |
| CLI | `config list` | no | no | no | yes | not analyzed | no | Gap |
| CLI | `config set` | no | no | no | yes | not analyzed | no | Gap |
| PowerShell | `Get-AzConfig` | no | no | no | yes | not analyzed | no | Gap |
| PowerShell | `Get-AzConfigInfo` | no | no | no | yes | not analyzed | no | Gap |
| PowerShell | `Get-AzConfigList` | no | no | no | yes | not analyzed | no | Gap |
| PowerShell | `Remove-AzConfig` | no | no | no | yes | not analyzed | no | Gap |
| PowerShell | `Set-AzConfig` | no | no | no | yes | not analyzed | no | Gap |

## Extension

Extension-level benchmarks: CLI=no, PowerShell=no.

| Surface | Command | Unit | Scenario | Script scenario | Docs present | Docs test | Bench | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CLI | `extension add` | no | no | no | yes | not analyzed | no | Gap |
| CLI | `extension list` | no | no | no | yes | not analyzed | no | Gap |
| CLI | `extension pin` | no | no | no | yes | not analyzed | no | Gap |
| CLI | `extension remove` | no | no | no | yes | not analyzed | no | Gap |
| CLI | `extension show` | no | no | no | yes | not analyzed | no | Gap |
| CLI | `extension unpin` | no | no | no | yes | not analyzed | no | Gap |
| CLI | `extension update` | no | no | no | yes | not analyzed | no | Gap |

## KeyVault

Extension-level benchmarks: CLI=no, PowerShell=no.

| Surface | Command | Unit | Scenario | Script scenario | Docs present | Docs test | Bench | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CLI | `keyvault secret delete` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `keyvault secret list` | yes | yes | no | yes | not analyzed | no | Good |
| CLI | `keyvault secret list-versions` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `keyvault secret set` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `keyvault secret set-attributes` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `keyvault secret show` | yes | yes | no | yes | not analyzed | no | Good |
| PowerShell | `Get-AzKeyVaultSecret_Get` | yes | yes | no | yes | not analyzed | no | Good |
| PowerShell | `Get-AzKeyVaultSecret_List` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzKeyVaultSecret_Versions` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Remove-AzKeyVaultSecret` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Set-AzKeyVaultSecret` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Undo-AzKeyVaultSecretRemoval` | yes | yes | no | yes | not analyzed | no | Good |
| PowerShell | `Update-AzKeyVaultSecret` | yes | no | no | yes | not analyzed | no | Partial |

## ManagedServiceIdentity

Extension-level benchmarks: CLI=1, PowerShell=1.

| Surface | Command | Unit | Scenario | Script scenario | Docs present | Docs test | Bench | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CLI | `identity create` | yes | yes | no | yes | not analyzed | yes | Good |
| CLI | `identity delete` | yes | yes | no | yes | not analyzed | no | Good |
| CLI | `identity list` | yes | yes | no | yes | not analyzed | yes | Good |
| CLI | `identity show` | yes | yes | no | yes | not analyzed | yes | Good |
| CLI | `identity update` | yes | yes | no | yes | not analyzed | yes | Good |
| PowerShell | `Get-AzUserAssignedIdentity_Get` | no | no | no | yes | not analyzed | no | Gap |
| PowerShell | `Get-AzUserAssignedIdentity_List` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `New-AzUserAssignedIdentity` | yes | no | no | yes | not analyzed | yes | Partial |
| PowerShell | `Remove-AzUserAssignedIdentity` | no | no | no | yes | not analyzed | no | Gap |
| PowerShell | `Update-AzUserAssignedIdentity` | yes | no | no | yes | not analyzed | yes | Partial |

## MsGraph

Extension-level benchmarks: CLI=no, PowerShell=no.

| Surface | Command | Unit | Scenario | Script scenario | Docs present | Docs test | Bench | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CLI | `msgraph app create` | no | no | no | yes | not analyzed | no | Gap |
| CLI | `msgraph app delete` | no | no | no | yes | not analyzed | no | Gap |
| CLI | `msgraph app list` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `msgraph app show` | no | no | no | yes | not analyzed | no | Gap |
| CLI | `msgraph group list` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `msgraph group member check` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `msgraph group member list` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `msgraph group show` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `msgraph user list` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `msgraph user show` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzMsGraphApplication_DisplayName` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzMsGraphApplication_Get` | no | no | no | yes | not analyzed | no | Gap |
| PowerShell | `Get-AzMsGraphApplication_List` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzMsGraphGroupMember_DisplayName` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzMsGraphGroupMember_ObjectId` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzMsGraphGroup_DisplayName` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzMsGraphGroup_DisplayNameStartsWith` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzMsGraphGroup_Get` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzMsGraphGroup_List` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzMsGraphUser_DisplayName` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzMsGraphUser_Get` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzMsGraphUser_List` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzMsGraphUser_Mail` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzMsGraphUser_SignedIn` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzMsGraphUser_StartsWith` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `New-AzMsGraphApplication` | no | no | no | yes | not analyzed | no | Gap |
| PowerShell | `Remove-AzMsGraphApplication` | no | no | no | yes | not analyzed | no | Gap |

## Network

Extension-level benchmarks: CLI=no, PowerShell=no.

| Surface | Command | Unit | Scenario | Script scenario | Docs present | Docs test | Bench | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CLI | `network public-ip create` | yes | yes | no | yes | not analyzed | no | Good |
| CLI | `network public-ip delete` | yes | yes | no | yes | not analyzed | no | Good |
| CLI | `network public-ip list` | yes | yes | no | yes | not analyzed | no | Good |
| CLI | `network public-ip show` | yes | yes | no | yes | not analyzed | no | Good |
| CLI | `network vnet create` | yes | yes | no | yes | not analyzed | no | Good |
| CLI | `network vnet delete` | yes | yes | no | yes | not analyzed | no | Good |
| CLI | `network vnet list` | yes | yes | no | yes | not analyzed | no | Good |
| CLI | `network vnet show` | yes | yes | no | yes | not analyzed | no | Good |
| CLI | `network vnet subnet create` | yes | yes | no | yes | not analyzed | no | Good |
| CLI | `network vnet subnet delete` | no | yes | no | yes | not analyzed | no | Partial |
| CLI | `network vnet subnet list` | yes | yes | no | yes | not analyzed | no | Good |
| CLI | `network vnet subnet show` | no | yes | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzPublicIpAddress_Get` | yes | yes | no | yes | not analyzed | no | Good |
| PowerShell | `Get-AzPublicIpAddress_List` | yes | yes | no | yes | not analyzed | no | Good |
| PowerShell | `Get-AzVirtualNetwork_Get` | yes | yes | no | yes | not analyzed | no | Good |
| PowerShell | `Get-AzVirtualNetwork_List` | yes | yes | no | yes | not analyzed | no | Good |
| PowerShell | `New-AzPublicIpAddress` | yes | yes | no | yes | not analyzed | no | Good |
| PowerShell | `New-AzVirtualNetwork` | yes | yes | no | yes | not analyzed | no | Good |
| PowerShell | `New-AzVirtualNetworkSubnetConfig` | yes | yes | no | yes | not analyzed | no | Good |
| PowerShell | `Remove-AzPublicIpAddress` | yes | yes | no | yes | not analyzed | no | Good |
| PowerShell | `Remove-AzVirtualNetwork` | yes | yes | no | yes | not analyzed | no | Good |

## Resources

Extension-level benchmarks: CLI=1, PowerShell=1.

| Surface | Command | Unit | Scenario | Script scenario | Docs present | Docs test | Bench | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CLI | `deployment group cancel` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `deployment group create` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `deployment group delete` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `deployment group export` | no | no | no | yes | not analyzed | no | Gap |
| CLI | `deployment group list` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `deployment group show` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `deployment group validate` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `deployment group what-if` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `group create` | no | yes | yes | yes | not analyzed | yes | Partial |
| CLI | `group delete` | yes | yes | no | yes | not analyzed | no | Good |
| CLI | `group exists` | yes | yes | no | yes | not analyzed | no | Good |
| CLI | `group list` | no | no | no | yes | not analyzed | yes | Gap |
| CLI | `group show` | no | no | no | yes | not analyzed | yes | Gap |
| CLI | `group update` | yes | yes | no | yes | not analyzed | no | Good |
| CLI | `rest` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `role assignment create` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `role assignment delete` | yes | no | no | yes | not analyzed | no | Partial |
| CLI | `role assignment list` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzResourceGroupDeployment_Get` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzResourceGroupDeployment_GetById` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzResourceGroupDeployment_List` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Get-AzResourceGroup_Get` | no | no | no | yes | not analyzed | no | Gap |
| PowerShell | `Get-AzResourceGroup_List` | no | no | no | yes | not analyzed | no | Gap |
| PowerShell | `Get-AzRoleAssignment` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Invoke-AzRestMethod` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `New-AzResourceGroup` | no | yes | yes | yes | not analyzed | yes | Partial |
| PowerShell | `New-AzResourceGroupDeployment` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `New-AzResourceGroupDeploymentWhatIf` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `New-AzRoleAssignment` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Remove-AzResourceGroup` | yes | yes | no | yes | not analyzed | no | Good |
| PowerShell | `Remove-AzResourceGroupDeployment` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Remove-AzRoleAssignment` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Save-AzResourceGroupDeploymentTemplate` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Set-AzResourceGroup` | yes | yes | no | yes | not analyzed | no | Good |
| PowerShell | `Stop-AzResourceGroupDeployment` | yes | no | no | yes | not analyzed | no | Partial |
| PowerShell | `Test-AzResourceGroupDeployment` | yes | no | no | yes | not analyzed | no | Partial |

## Unmapped test evidence

| Tier | Test | Evidence |
| --- | --- | --- |
| scenario | `tests/Scenario.Tests/DeploymentScenarioTests.cs` | `RunCli([.. run.Cli, "--subscription-id", SubscriptionId])` |
| scenario | `tests/Scenario.Tests/KeyVaultSecretScenarioTests.cs` | `CLI:keyvault secret` |
| scenario | `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` | `RunCli(ArgsWithImage(LinuxImage)` |
| scenario | `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` | `RunCli(ArgsWithImage(image)` |
| scenario | `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` | `RunCli([.. ArgsWithImage(LinuxImage)` |
| scenario | `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` | `RunCli([.. s_cliArgs,
                "--security-type", "ConfidentialVM",
                "--os-disk-security-encryption-type", "DiskWithVMGuestState",
                "--os-disk-secure-vm-disk-encryption-set", NotADiskEncryptionSetId])` |
| scenario | `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` | `RunCli([.. s_cliArgs, "--accelerated-networking", "true"])` |
| scenario | `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` | `RunCli([.. s_cliArgs, "--os-disk-security-encryption-type", "VMGuestStateOnly"])` |
| scenario | `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` | `RunCli([.. s_cliArgs, "--security-type", "ConfidentialVM", "--os-disk-security-encryption-type", "VMGuestStateOnly", "--os-disk-secure-vm-disk-encryption-set", "mydes"])` |
| scenario | `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` | `RunCli([.. s_cliArgs, "--security-type", "ConfidentialVM"])` |
| scenario | `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` | `RunCli([.. s_cliArgs, "--ssh-key-values", PublicKey])` |
| scenario | `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` | `RunCli(args)` |
| scenario | `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` | `RunCli(s_cliArgs)` |
| scenario | `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` | `RunCli(s_cliArgs.Where(arg => arg != "--admin-password" && arg != Password)` |
| scenario | `tests/Scenario.Tests/VmCreateOrchestrationTests.cs` | `RunCli(vmCreateArgs)` |
| scenario | `tests/Scenario.Tests/VmLifecycleScenarioTests.cs` | `RunCli(args.ToArray()` |
| unit | `tests/Cli.Tests/AgentFallbackSkipTelemetryTests.cs` | `processor:DefaultActivityNameFilterProcessor` |
| unit | `tests/Cli.Tests/AgentFallbackSkipTelemetryTests.cs` | `processor:DefaultTelemetryPropertyFilterProcessor` |
| unit | `tests/Cli.Tests/AgentFallbackSkipTelemetryTests.cs` | `processor:SimpleActivityExportProcessor` |
| unit | `tests/Cli.Tests/Configuration/ConfigCliTests.cs` | `processor:BoolFlagProcessor` |
| unit | `tests/Cli.Tests/GetAccessTokenProcessorTests.cs` | `processor:ProcessorContext` |
| unit | `tests/Cli.Tests/GroupDeleteTests.cs` | `processor:ProcessorContext` |
| unit | `tests/Cli.Tests/GroupExistsTests.cs` | `processor:ProcessorContext` |
| unit | `tests/Cli.Tests/GroupUpdateTests.cs` | `processor:ProcessorContext` |
| unit | `tests/Cli.Tests/Help/RuntimeHelpRendererTests.cs` | `processor:RuntimeHelpCommand` |
| unit | `tests/Cli.Tests/IdentityExtensionTests.cs` | `processor:ProcessorContext` |
| unit | `tests/Cli.Tests/ListDiskProcessorTests.cs` | `processor:ProcessorContext` |
| unit | `tests/Cli.Tests/ListVMProcessorTests.cs` | `processor:ListVMProcessor` |
| unit | `tests/Cli.Tests/ListVMProcessorTests.cs` | `processor:ProcessorContext` |
| unit | `tests/Cli.Tests/ListVirtualNetworkProcessorTests.cs` | `processor:ProcessorContext` |
| unit | `tests/Cli.Tests/ProfileSubscriptionResolutionTests.cs` | `processor:TestComputeProcessor` |
| unit | `tests/Cli.Tests/ProfileSubscriptionResolutionTests.cs` | `processor:TestResourceProcessor` |
| unit | `tests/Cli.Tests/PublicIpAddressParameterTests.cs` | `processor:ProcessorContext` |
| unit | `tests/Cli.Tests/ResourceGroupDeploymentCliTests.cs` | `processor:ProcessorContext` |
| unit | `tests/Cli.Tests/ResourceGroupDeploymentDeleteTests.cs` | `processor:RemoveResourceGroupDeploymentProcessor` |
| unit | `tests/Cli.Tests/RestProcessorTests.cs` | `processor:ProcessorContext` |
| unit | `tests/Cli.Tests/RoleAssignmentCliTests.cs` | `processor:ListRoleAssignmentProcessor` |
| unit | `tests/Cli.Tests/RoleAssignmentCliTests.cs` | `processor:ProcessorContext` |
| unit | `tests/Cli.Tests/RoleAssignmentCreateTests.cs` | `processor:ProcessorContext` |
| unit | `tests/Cli.Tests/RoleAssignmentDeleteTests.cs` | `processor:ProcessorContext` |
| unit | `tests/Cli.Tests/SubnetParameterTests.cs` | `processor:ProcessorContext` |
| unit | `tests/Cli.Tests/VirtualNetworkParameterTests.cs` | `processor:ProcessorContext` |
| unit | `tests/Core.Engine.Tests/ExecutionSessionSeamTests.cs` | `processor:ProcessorContext` |
| unit | `tests/Core.Telemetry.Tests/ActivityNameFilterProcessorTests.cs` | `processor:DefaultActivityNameFilterProcessor` |
| unit | `tests/Core.Telemetry.Tests/AriaActivityExporterTests.cs` | `processor:SimpleActivityExportProcessor` |
| unit | `tests/Core.Telemetry.Tests/FileExporterBatchTests.cs` | `processor:BatchActivityExportProcessor` |
| unit | `tests/Core.Telemetry.Tests/FileExporterBatchTests.cs` | `processor:SimpleActivityExportProcessor` |
| unit | `tests/Core.Telemetry.Tests/RecoveryTelemetryTests.cs` | `processor:SimpleActivityExportProcessor` |
| unit | `tests/Core.Telemetry.Tests/TelemetryPropertyFilterProcessorTests.cs` | `processor:DefaultTelemetryPropertyFilterProcessor` |
| unit | `tests/Extensions.KeyVault.Tests/KeyVaultDataPlaneProcessorBaseTests.cs` | `processor:ProcessorContext` |
| unit | `tests/Extensions.KeyVault.Tests/KeyVaultDataPlaneProcessorBaseTests.cs` | `processor:TestProcessor` |
| unit | `tests/Extensions.KeyVault.Tests/KeyVaultEndpointTests.cs` | `processor:EndpointProcessor` |
| unit | `tests/Extensions.KeyVault.Tests/KeyVaultEndpointTests.cs` | `processor:ProcessorContext` |
| unit | `tests/Extensions.KeyVault.Tests/SecretCommandTests.cs` | `processor:ProcessorContext` |
| unit | `tests/Extensions.MsGraph.Tests/CheckGroupMemberProcessorTests.cs` | `processor:TestableCheckGroupMemberProcessor` |
| unit | `tests/Extensions.MsGraph.Tests/DeleteApplicationProcessorTests.cs` | `processor:TestableDeleteApplicationProcessor` |
| unit | `tests/Extensions.MsGraph.Tests/GetApplicationProcessorTests.cs` | `processor:TestableGetApplicationProcessor` |
| unit | `tests/Extensions.MsGraph.Tests/GetGroupProcessorTests.cs` | `processor:TestableGetGroupProcessor` |
| unit | `tests/Extensions.MsGraph.Tests/GetSignedInUserProcessorTests.cs` | `processor:TestableGetSignedInUserProcessor` |
| unit | `tests/Extensions.MsGraph.Tests/GetUserProcessorTests.cs` | `processor:TestableGetUserProcessor` |
| unit | `tests/Extensions.MsGraph.Tests/GraphProcessorBaseAuthScopeTests.cs` | `processor:GetUserProcessor` |
| unit | `tests/Extensions.MsGraph.Tests/ListApplicationProcessorTests.cs` | `processor:TestableListApplicationProcessor` |
| unit | `tests/Extensions.MsGraph.Tests/ListGroupMemberProcessorTests.cs` | `processor:TestableListGroupMemberProcessor` |
| unit | `tests/Extensions.MsGraph.Tests/ListGroupProcessorTests.cs` | `processor:TestableListGroupProcessor` |
| unit | `tests/Extensions.MsGraph.Tests/ListUserProcessorTests.cs` | `processor:TestableListUserProcessor` |
| unit | `tests/Extensions.MsGraph.Tests/NewApplicationProcessorTests.cs` | `processor:TestableNewApplicationProcessor` |
