# Design

## Context

The Network extension is being built in the codegen output folder before it moves to `src/Extensions/Network` (migration skill step 12). The generated atomic processors for NetworkSecurityGroups are `CreateOrUpdateNetworkSecurityGroupsProcessor`, `GetNetworkSecurityGroupsProcessor`, `ListNetworkSecurityGroupsProcessor`, `ListAllNetworkSecurityGroupsProcessor`, and `DeleteNetworkSecurityGroupsProcessor`. They are read-only. There is no `ExtensionIndex.cs` and no PowerShell module yet. The location default needs ResourceGroups_Get, which is not a Network operation; the Resources extension already has `GetResourceGroupsProcessor` (`src/Extensions/Resources/Processors/GetResourceGroupsProcessor.cs`). See proposal.md for motivation and the spec for required behavior.

## Goals / Non-Goals

**Goals:**
- Add the create location default (F1) and CLI tag parsing (F12) as handwritten customization on top of the create processor.
- Add list routing (F2) on top of the list processors.
- Add PowerShell remove confirmation and `-PassThru` (F8) in the PowerShell wrapper only.

**Non-Goals:**
- No change to generated models or atomic processors, no new endpoints, no api-version or URL in customization.
- No `$expand`, long-running-operation, or paging logic: the generated processors and processor base own them.
- No compile in this change while the code lives in the output folder (the migration skill compiles at step 13).

## Decisions

- **Create customization inherits the create processor and composes the resource-group read.** A `Customization` class for NetworkSecurityGroups create inherits `CreateOrUpdateNetworkSecurityGroupsProcessor` (its interface covers every parent input). When `Location` is empty it composes the Resources `GetResourceGroupsProcessor` with `CreateExtendedProcessor<T>`, following `src/Extensions/Resources/Customization/UpdateResourceGroupProcessor_Orchestrator.cs` and the Compute customizations, takes the returned location, then runs the inherited PUT. A failed GET surfaces its error and the PUT is not sent. When `Location` is given, no GET is made. How the Network extension references the Resources processor (project reference or another engagement) is decided by migration skill step 11 (external APIs); this change only requires that the route comes from that processor and is never copied. Alternative: a hand-built resource-group request; rejected because customization never owns an endpoint.
- **F12 parses a CLI-only tags input.** The create customization adds a CLI tags input (`key[=value]` strings) and parses it into `Tags` (bare key -> empty string), following the tags-parsing pattern of existing customizations such as `src/Extensions/Compute/Customization/VmTagsParser.cs`. The PowerShell `-Tag` hashtable keeps mapping to `Tags` directly, so the parsing does not change PowerShell behavior.
- **List customization routes between both list processors.** A `Customization` list class inherits `ListNetworkSecurityGroupsProcessor` (the resource group becomes optional), keeps the inherited route when a resource group is given, and composes `ListAllNetworkSecurityGroupsProcessor` with `CreateExtendedProcessor<T>` when it is not, following `src/Extensions/Compute/Customization/ListVMProcessor_Orchestrator.cs`. Paging stays in the processor base.
- **F8 lives in the PowerShell wrapper.** Confirmation (`ShouldProcess`/`-Force`) and `-PassThru` are PowerShell-only, so they go into the `Remove-AzNetworkSecurityGroup` wrapper under `src/PS/Network/Cmdlets`, following existing wrappers such as `src/PS/Resources/Cmdlets/RemoveSubDeploymentCmdlet.cs`. Putting them in the shared customization would leak PowerShell behavior into the CLI.

## Risks / Trade-offs

- [The Network extension depends on a Resources processor] → step 11 decides the engagement; if a cross-extension reference is not allowed, the F1 task stops and asks for direction rather than hand-building the request.
- [The PowerShell module does not exist until migration step 14] → the F8 task is carried out when that module is created; it is marked so in tasks.md.
- [Repeated tag keys] → the selected business logic does not state which repeat wins; the parser keeps the last value, matching a dictionary overwrite, and the spec does not assert it.
- [No compile before step 13] → each task is verified by review against the spec scenarios; compile errors surface at step 13.
