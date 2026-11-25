# Design

## Context

The Network extension is being built in the codegen output folder before it moves to `src/Extensions/Network` (migration skill step 12). The generated atomic processors for PublicIPAddresses are `CreateOrUpdatePublicIPAddressesProcessor`, `GetPublicIPAddressesProcessor`, `ListPublicIPAddressesProcessor`, `ListAllPublicIPAddressesProcessor`, and `DeletePublicIPAddressesProcessor`. They are read-only. `CreateOrUpdatePublicIPAddressesProcessor` already exposes flattened body inputs (`SkuName`, `PublicIPAllocationMethod`, `PublicIPAddressVersion`, `IdleTimeoutInMinutes`, `ExtendedLocationName`, `ExtendedLocationType`, `DdosSettingsProtectionMode`, `DdosProtectionPlanId`, `DdosCustomPolicyId`, `PublicIpPrefixId`, `IpTags`) plus `SubscriptionId` and `ResourceGroupName`. There is no `ExtensionIndex.cs` and no PowerShell module yet. No behavior in this change needs a non-Network REST operation. See proposal.md for motivation and the spec for required behavior.

## Goals / Non-Goals

**Goals:**
- Add the create defaults (F1, F2, F3, F4), edge-zone mapping (F7), name-or-ID expansion (F8, F9, F10), and IP tag mapping (F12) as handwritten customization on top of the create processor.
- Add list routing (F25) on top of the list processors.
- Add PowerShell remove confirmation and `-PassThru` (F28) in the PowerShell wrapper only.

**Non-Goals:**
- No change to generated models or atomic processors, no new endpoints, no api-version or URL in customization.
- No `$expand`, long-running-operation, or paging logic: the generated processors and processor base own them.
- No PublicIPPrefixes, DDoS, or resource-group requests: names are expanded locally.
- No compile in this change while the code lives in the output folder (the migration skill compiles at step 13).

## Decisions

- **Create customization inherits the create processor and only shapes the body.** A `Customization` class for PublicIPAddresses create inherits `CreateOrUpdatePublicIPAddressesProcessor` (its interface covers every parent input) and fills the body before the inherited PUT, using the smallest request-construction entry the private-preview guide allows. Order: apply the SKU default first (F1), then derive the allocation method from the effective SKU (F2), so the `Basic` check sees the final SKU. Alternative: a full `ProcessAsync()` override; rejected because one PUT is enough.
- **Edge zone is a customization input.** The customization adds an edge-zone input that sets `ExtendedLocationName` and `ExtendedLocationType = EdgeZone`; when no edge zone is given neither is set, so no `extendedLocation` is sent (F7). The surfaces bind `--edge-zone` / `-EdgeZone` to it at step 14.
- **Name-or-ID expansion reuses the existing ID inputs.** For `DdosProtectionPlanId`, `DdosCustomPolicyId`, and `PublicIpPrefixId`, a value that starts with `/` is kept; any other non-empty value is treated as a name and replaced with the `Microsoft.Network` resource ID built from the command's `SubscriptionId` and `ResourceGroupName` (`ddosProtectionPlans`, `ddosCustomPolicies`, `publicIPPrefixes`). One private helper does this for all three. The DDoS plan is sent whether or not a protection mode is given (F8). No GET is sent for any of them.
- **IP tags have one shared input shape.** The customization accepts IP tags as ordered type/value pairs and builds `IpTags = [{IpTagType, Tag}]` in order (F12). The CLI `TYPE=VALUE` strings are split into those pairs in the customization; the PowerShell `-IpTag` hashtable is turned into the same pairs by the PowerShell wrapper at step 14. With no IP tags, `IpTags` stays unset.
- **List customization routes between both list processors.** A `Customization` list class inherits `ListPublicIPAddressesProcessor` (the resource group becomes optional), keeps the inherited route when a resource group is given, and composes `ListAllPublicIPAddressesProcessor` with `CreateExtendedProcessor<T>` when it is not, following `src/Extensions/Compute/Customization/ListVMProcessor_Orchestrator.cs`. Paging stays in the processor base.
- **F28 lives in the PowerShell wrapper.** Confirmation (`ShouldProcess`/`-Force`) and `-PassThru` are PowerShell-only, so they go into the `Remove-AzPublicIpAddress` wrapper under `src/PS/Network/Cmdlets`, following existing wrappers such as `src/PS/Resources/Cmdlets/RemoveSubDeploymentCmdlet.cs`. Putting them in the shared customization would leak PowerShell behavior into the CLI.

## Risks / Trade-offs

- [The PowerShell module does not exist until migration step 14] → the F28 task and the PowerShell IP tag hashtable mapping are carried out when that module is created; they are marked so in tasks.md.
- [A name that contains `/` but does not start with it] → treated as a name, as the business logic states; the service rejects the malformed ID.
- [No compile before step 13] → each task is verified by review against the spec scenarios; compile errors surface at step 13.
