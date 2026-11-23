# Tasks

These tasks change product code only. Scenario tests come later in a separate change. Generated models and atomic processors under `Processors/` and `Models/` are read-only. Before the extension moves to `src/Extensions/Network` (migration skill step 12), the extension root is the codegen output folder; afterwards it is `src/Extensions/Network`. Do not compile before migration step 13.

## 1. Create request shaping (shared customization)

- [x] 1.1 Add a VirtualNetworks create customization class under `<extension root>/Customization` that inherits `NewVirtualNetworkProcessor` and adjusts the request body through the smallest request-construction entry in `eng-docs/howto/Extension-Development-For-Private-Preview.md`, with no `Method`, `CreateRequestUri()`, api-version, or URL; verify by review that the file exists, inherits the processor, and no file under `Processors/` or `Models/` changed
- [x] 1.2 Implement F1 and F2 (default `10.0.0.0/16` only when neither address prefixes nor IPAM allocations are given; IPAM allocations sent one entry per allocation and clear address prefixes); verify by review against the scenarios of "Default virtual network address prefix" and "IPAM pool allocations replace address prefixes"
- [x] 1.3 Implement F3 (edge zone name -> extended location `{ name, type: "EdgeZone" }`, none when absent); verify by review against "Edge zone maps to extended location"
- [x] 1.4 Implement F4 and F5 (subnet definitions -> `properties.subnets`, one entry each; a subnet name with one prefix -> `addressPrefix`, with several -> `addressPrefixes`; no `subnets` when none given); verify by review against "Inline subnet definitions" and "Subnet from a name plus prefixes"
- [x] 1.5 Implement F22 (IP allocation IDs -> `[{ id }]`); verify by review against "IP allocation references"

## 2. List routing (shared customization)

- [x] 2.1 Add a VirtualNetworks list customization class under `<extension root>/Customization` that inherits `ListVirtualNetworkProcessor`, keeps its route when a resource group is given, and composes `ListAllVirtualNetworkProcessor` with `CreateExtendedProcessor<T>` when it is not (F10), leaving paging to the processor base; verify by review against "List scope by resource group presence" and that no route, method, or api-version is declared in the class

## 3. PowerShell remove (PowerShell-only)

- [ ] 3.1 In the `Remove-AzVirtualNetwork` wrapper under `src/PS/Network/Cmdlets`, add F18: confirm with ShouldProcess before the delete unless `-Force` is given, send no delete when declined, and write `$true` with `-PassThru` after a successful delete, following `src/PS/Resources/Cmdlets/RemoveSubDeploymentCmdlet.cs`. The PowerShell module is created at migration step 14: if it does not exist when this change is applied, leave this task unchecked and carry it out at step 14; verify by review against "PowerShell remove confirmation and PassThru" and that the CLI delete path is unchanged

## Workflow follow-up

- Compile at migration step 13 and fix errors in the customization files only.
- Scenario tests for these requirements are proposed separately with `/integrate-scenario-test` and `/scenario-test-spec`.
- Archive the change after code and scenario tests are reviewed.
