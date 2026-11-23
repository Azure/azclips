# Tasks

These tasks change product code only. Scenario tests come later in a separate change. Generated models and atomic processors under `Processors/` and `Models/` are read-only. Before the extension moves to `src/Extensions/Network` (migration skill step 12), the extension root is the codegen output folder; afterwards it is `src/Extensions/Network`. Do not compile before migration step 13.

## 1. Create request shaping (shared customization)

- [x] 1.1 Add a NetworkSecurityGroups create customization class under `<extension root>/Customization` that inherits `NewNetworkSecurityGroupProcessor`, with no `Method`, `CreateRequestUri()`, api-version, or URL; verify by review that the file exists, inherits the processor, and no file under `Processors/` or `Models/` changed
- [x] 1.2 Implement F1: when `Location` is empty, compose the Resources `GetResourceGroupProcessor` with `CreateExtendedProcessor<T>` for the same subscription and resource group, set `Location` from its result, then run the inherited PUT; when the GET fails, surface its error and send no PUT; when `Location` is given, send no GET. Engage the Resources processor as migration skill step 11 decides; if that engagement is not possible, stop and report instead of hand-building the request. Verify by review against the scenarios of "Default location from the resource group"
- [x] 1.3 Implement F12: add a CLI tags input of `key[=value]` strings and parse it into `Tags` (bare key -> empty string), leaving the PowerShell `-Tag` mapping to `Tags` unchanged; verify by review against "CLI tags parsing on create"

## 2. List routing (shared customization)

- [x] 2.1 Add a NetworkSecurityGroups list customization class under `<extension root>/Customization` that inherits `ListNetworkSecurityGroupProcessor`, keeps its route when a resource group is given, and composes `ListAllNetworkSecurityGroupProcessor` with `CreateExtendedProcessor<T>` when it is not (F2), leaving paging to the processor base; verify by review against "List scope by resource group presence" and that no route, method, or api-version is declared in the class

## 3. PowerShell remove (PowerShell-only)

- [ ] 3.1 In the `Remove-AzNetworkSecurityGroup` wrapper under `src/PS/Network/Cmdlets`, add F8: confirm with ShouldProcess before the delete unless `-Force` is given, send no delete when declined, and output `true` with `-PassThru` after a completed delete (nothing otherwise), following `src/PS/Resources/Cmdlets/RemoveSubDeploymentCmdlet.cs`. The PowerShell module is created at migration step 14: if it does not exist when this change is applied, leave this task unchecked and carry it out at step 14; verify by review against "PowerShell remove confirmation and PassThru" and that the CLI delete path is unchanged

## Workflow follow-up

- Compile at migration step 13 and fix errors in the customization files only.
- Scenario tests for these requirements are proposed separately with `/integrate-scenario-test` and `/scenario-test-spec`.
- Archive the change after code and scenario tests are reviewed.
