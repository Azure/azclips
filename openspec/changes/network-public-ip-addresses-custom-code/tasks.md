# Tasks

These tasks change product code only. Scenario tests come later in a separate change. Generated models and atomic processors under `Processors/` and `Models/` are read-only. Before the extension moves to `src/Extensions/Network` (migration skill step 12), the extension root is the codegen output folder; afterwards it is `src/Extensions/Network`. Do not compile before migration step 13.

## 1. Create request shaping (shared customization)

- [x] 1.1 Add a PublicIPAddresses create customization class under `<extension root>/Customization` that inherits `CreateOrUpdatePublicIPAddressesProcessor`, with no `Method`, `CreateRequestUri()`, api-version, or URL, and shapes the body through the smallest request-construction entry of `eng-docs/howto/Extension-Development-For-Private-Preview.md`; verify by review that the file exists, inherits the processor, and no file under `Processors/` or `Models/` changed
- [x] 1.2 Implement the defaults in this order: F1 (`SkuName` empty -> `Standard`), then F2 (`PublicIPAllocationMethod` empty -> `Dynamic` when the effective `SkuName` is `Basic` ignoring case, else `Static`), F3 (`PublicIPAddressVersion` empty -> `IPv4`), F4 (`IdleTimeoutInMinutes` unset or <= 0 -> 4); given values stay unchanged. Verify by review against "SKU default on create", "Allocation method default from the SKU", "IP version default on create", and "Idle timeout default on create"
- [x] 1.3 Implement F7: add an edge-zone input; when it has a value set `ExtendedLocationName` to it and `ExtendedLocationType = EdgeZone`, else set neither. Verify by review against "Edge zone as extended location"
- [x] 1.4 Implement F8, F9, F10 with one private helper: for `DdosProtectionPlanId`, `DdosCustomPolicyId`, and `PublicIpPrefixId`, keep a value that starts with `/`, and replace any other non-empty value with the `Microsoft.Network` resource ID (`ddosProtectionPlans`, `ddosCustomPolicies`, `publicIPPrefixes`) built from `SubscriptionId` and `ResourceGroupName`; send the DDoS plan whether or not `DdosSettingsProtectionMode` is set; send no GET for any of them. Verify by review against "DDoS protection mode and plan", "DDoS custom policy name or ID", and "Public IP prefix name or ID"
- [x] 1.5 Implement F12: add an IP tag input of ordered type/value pairs and a CLI input of `TYPE=VALUE` strings split into those pairs; build `IpTags = [{IpTagType, Tag}]` in the given order, and leave `IpTags` unset when none are given. The PowerShell `-IpTag` hashtable is mapped to the same pairs by the PowerShell wrapper at migration step 14. Verify by review against "IP tags on create"

## 2. List routing (shared customization)

- [x] 2.1 Add a PublicIPAddresses list customization class under `<extension root>/Customization` that inherits `ListPublicIPAddressesProcessor`, keeps its route when a resource group is given, and composes `ListAllPublicIPAddressesProcessor` with `CreateExtendedProcessor<T>` when it is not (F25), following `src/Extensions/Compute/Customization/ListVMProcessor_Orchestrator.cs` and leaving paging to the processor base; verify by review against "List scope by resource group presence" and that no route, method, or api-version is declared in the class

## 3. PowerShell remove (PowerShell-only)

- [x] 3.1 In the `Remove-AzPublicIpAddress` wrapper under `src/PS/Network/Cmdlets`, add F28: confirm with ShouldProcess before the delete unless `-Force` is given, send no delete when declined, and output `true` with `-PassThru` after a completed delete (nothing otherwise), following `src/PS/Resources/Cmdlets/RemoveSubDeploymentCmdlet.cs`. The PowerShell module is created at migration step 14: if it does not exist when this change is applied, leave this task unchecked and carry it out at step 14; verify by review against "PowerShell remove confirmation and PassThru" and that the CLI delete path is unchanged (S14b: done in the shared NetworkRemoveCmdletBase under src/PS/Network/Cmdlets; -WhatIf still wins over -Force; $true only when no error was written.)

## Workflow follow-up

- Compile at migration step 13 and fix errors in the customization files only.
- Scenario tests for these requirements are proposed separately with `/integrate-scenario-test` and `/scenario-test-spec`.
- Archive the change after code and scenario tests are reviewed.
