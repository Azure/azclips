# Tasks

These tasks change product code only. Scenario tests come later in a separate change. Generated models and atomic processors under `Processors/` and `Models/` are read-only. Before the extension moves to `src/Extensions/Network` (migration skill step 12), the extension root is the codegen output folder; afterwards it is `src/Extensions/Network`. Do not compile before migration step 13.

## 1. Create request shaping (shared customization)

- [x] 1.1 Add a NetworkInterfaces create customization class under `<extension root>/Customization` that inherits `NewNetworkInterfaceProcessor`, with no `Method`, `CreateRequestUri()`, api-version, or URL, and shapes the body through the smallest request-construction entry of `eng-docs/howto/Extension-Development-For-Private-Preview.md`; verify by review that the file exists, inherits the processor, and no file under `Processors/` or `Models/` changed
- [x] 1.2 Implement F1: add flat inputs for subnet ID, public IP address ID, private IP address, and IP configuration name (default `ipconfig1`); build `IpConfigurations` with exactly one `NetworkInterfaceIPConfiguration` named by that input, with `SubnetId`, `PublicIPAddress` carrying only the ID when given (unset otherwise), and `PrivateIPAddress` + `PrivateIPAllocationMethod = Static` when a private IP address is given, else `PrivateIPAllocationMethod = Dynamic` and no `PrivateIPAddress`; do not set `Primary`. Verify by review against "Single IP configuration from flat create inputs"
- [x] 1.3 Implement F8: add a private IP address version input; set `PrivateIPAddressVersion` on the IP configuration only when it has a value. Verify by review against "CLI private IP address version"
- [x] 1.4 Implement F3 with one private helper: add a virtual network name input; keep a subnet value that starts with `/`; turn a subnet name plus virtual network name into `/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/virtualNetworks/{vnet}/subnets/{name}`; turn a public IP address name or `NetworkSecurityGroupId` name into the `publicIPAddresses` or `networkSecurityGroups` resource ID; send no GET for any of them. Verify by review against "CLI name-or-ID expansion for create references"
- [x] 1.5 Implement F4: add an edge-zone input; when it has a value set `ExtendedLocationName` to it and `ExtendedLocationType = EdgeZone`, else set neither. Verify by review against "Edge zone as extended location"

## 2. List routing (shared customization)

- [x] 2.1 Add a NetworkInterfaces list customization class under `<extension root>/Customization` that inherits `ListNetworkInterfaceProcessor`, keeps its route when a resource group is given, and composes `ListAllNetworkInterfaceProcessor` with `CreateExtendedProcessor<T>` when it is not (F17), following `src/Extensions/Compute/Customization/ListVMProcessor_Orchestrator.cs` and leaving paging to the processor base; verify by review against "List scope by resource group presence" and that no route, method, or api-version is declared in the class

## 3. Surface bindings (PowerShell-only and CLI-only)

- [x] 3.1 In the `Remove-AzNetworkInterface` wrapper under `src/PS/Network/Cmdlets`, add F22: confirm with ShouldProcess before the delete unless `-Force` is given, send no delete when declined, and output `true` with `-PassThru` after a completed delete (nothing otherwise), following `src/PS/Resources/Cmdlets/RemoveSubDeploymentCmdlet.cs`. The PowerShell module is created at migration step 14: if it does not exist when this change is applied, leave this task unchecked and carry it out at step 14; verify by review against "PowerShell remove confirmation and PassThru" and that the CLI delete path is unchanged (S14b: done in the shared NetworkRemoveCmdletBase under src/PS/Network/Cmdlets; -WhatIf still wins over -Force; $true only when no error was written.)
- [x] 3.2 Expose the IP configuration name input from task 1.2 only on PowerShell `New-AzNetworkInterface` as `-IpConfigurationName` (F9), and the virtual network name (F3) and private IP address version (F8) inputs only on the CLI `network nic create` as `--vnet-name` and `--private-ip-address-version`. These bindings live in `ExtensionIndex.cs` and the PowerShell module, which are created at migration step 14: if they do not exist when this change is applied, leave this task unchecked and carry it out at step 14; verify by review against "PowerShell IP configuration name", "CLI name-or-ID expansion for create references", and "CLI private IP address version" (S14a: ExtensionIndex.cs bindings done; the PowerShell cmdlet parameter `-IpConfigurationName` is added in the PS module at S14b.) (S14b: -IpConfigurationName added to New-AzNetworkInterface.)

## Workflow follow-up

- Compile at migration step 13 and fix errors in the customization files only.
- Scenario tests for these requirements are proposed separately with `/integrate-scenario-test` and `/scenario-test-spec`.
- Archive the change after code and scenario tests are reviewed.
