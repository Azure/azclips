# Design

## Context

The Network extension is being built in the codegen output folder before it moves to `src/Extensions/Network` (migration skill step 12). The generated atomic processors for VirtualNetworks are `NewVirtualNetworkProcessor`, `GetVirtualNetworkProcessor`, `ListVirtualNetworkProcessor`, `ListAllVirtualNetworkProcessor`, and `DeleteVirtualNetworkProcessor`. They are read-only. There is no `ExtensionIndex.cs` and no PowerShell module yet. See proposal.md for motivation and the spec for required behavior.

## Goals / Non-Goals

**Goals:**
- Add create request shaping (F1, F2, F3, F4, F5, F22) and list routing (F10) as handwritten customization on top of the generated processors.
- Add PowerShell remove confirmation and `-PassThru` (F18) in the PowerShell wrapper only.

**Non-Goals:**
- No change to generated models or atomic processors, no new endpoints, no api-version or URL in customization.
- No argument-parsing rules beyond the selected business logic; CLI/PS argument binding is shaped when the command table and PowerShell module are created.
- No compile in this change while the code lives in the output folder (the migration skill compiles at step 13).

## Decisions

- **Create customization inherits the create processor.** A `Customization` class for VirtualNetworks create inherits `NewVirtualNetworkProcessor` (its interface covers every parent input) and adjusts the body in the smallest request-construction entry that the private-preview guide allows (body construction, not `ProcessAsync`). Alternative: compose with `CreateExtendedProcessor<T>`; rejected because only one REST call is made.
  - F1/F2: when IPAM allocations are present, clear address prefixes; else when address prefixes are empty, set `["10.0.0.0/16"]`.
  - F3: when the edge zone name is present, set the extended location type to `EdgeZone`; else send no extended location.
  - F4/F5: subnet definitions build `Subnets`; a subnet name plus prefixes builds one subnet with `addressPrefix` (one prefix) or `addressPrefixes` (several).
  - F22: IP allocation IDs map to `SubResource { Id }` entries.
- **List customization routes between both list processors.** A `Customization` list class inherits `ListVirtualNetworkProcessor` (the resource group becomes optional) and keeps the inherited route when a resource group is given; without one it composes `ListAllVirtualNetworkProcessor` with `CreateExtendedProcessor<T>`, following the inheritance and composition style of `src/Extensions/Compute/Customization/ListVMProcessor_Orchestrator.cs`. Paging stays in the processor base. Alternative: a class not inheriting either processor; rejected because the customer-facing command must be a class inheriting or implementing a processor (migration skill step 10).
- **F18 lives in the PowerShell wrapper.** Confirmation (`ShouldProcess`/`-Force`) and `-PassThru` are PowerShell-only, so they go into the `Remove-AzVirtualNetwork` wrapper under `src/PS/Network/Cmdlets`, following existing wrappers such as `src/PS/Resources/Cmdlets/RemoveSubDeploymentCmdlet.cs`. Putting them in the shared customization would leak PowerShell behavior into the CLI.

## Risks / Trade-offs

- [The PowerShell module does not exist until migration step 14] → the F18 task is carried out when that module is created; it is marked so in tasks.md.
- [Detailed `--subnets` / `-Subnet` key parsing is not part of this change] → F4 only states the body outcome; parsing rules are owned by argument binding and may need a follow-up change.
- [No compile before step 13] → each task is verified by review against the spec scenarios; compile errors surface at step 13.
