# `az vm get-instance-view` migration: design

- **Date:** 2026-11-05
- **Status:** Approved in brainstorming. Amended the same day to add PowerShell `Get-AzVM -Status` for one VM. The
  implementation plan follows separately.
- **Scope:** Compute extension: CLI `vm get-instance-view`, and PowerShell `Get-AzVM -Status` for one VM.

## Summary

Add `az vm get-instance-view` to AzClips. It returns the virtual machine with its full instance view at
`properties.instanceView`, from a single `GET .../virtualMachines/{vm}?$expand=instanceView`, matching legacy
Azure CLI. A new standalone `GetVMInstanceViewProcessor_Orchestrator` serves it, the Compute models are
hand-extended to every instance-view field in REST api-version `2025-11-01`, and the VM lifecycle scenario test
checks the instance view after each power-state change. PowerShell `Get-AzVM -Status` for one VM, addressed by name
and resource group or by `-Id`, routes to the same orchestrator through two new parameter sets and returns the same
shape.

## Legacy behavior

### Azure CLI

Evidence: `Azure/azure-cli@dev`, pinned to
[`17100e1`](https://github.com/Azure/azure-cli/tree/17100e145edf96640897d6515ad626402fde36a9/src/azure-cli/azure/cli/command_modules/vm).

- **Registration:** a custom command with a table transformer
  ([commands.py#L169](https://github.com/Azure/azure-cli/blob/17100e145edf96640897d6515ad626402fde36a9/src/azure-cli/azure/cli/command_modules/vm/commands.py#L169)).
- **Implementation:** `get_instance_view` calls the AAZ `VMShow` with `expand='instanceView'` and returns the
  whole VM
  ([custom.py#L1362-L1373](https://github.com/Azure/azure-cli/blob/17100e145edf96640897d6515ad626402fde36a9/src/azure-cli/azure/cli/command_modules/vm/custom.py#L1362-L1373)).
  `VMShow` only resolves a flatten-key conflict and does not reshape the output
  ([operations/vm.py#L75-L99](https://github.com/Azure/azure-cli/blob/17100e145edf96640897d6515ad626402fde36a9/src/azure-cli/azure/cli/command_modules/vm/operations/vm.py#L75-L99)).
- **User data:** `include_user_data` is ignored for this command, so the request is always `$expand=instanceView`
  ([_params.py#L565-L567](https://github.com/Azure/azure-cli/blob/17100e145edf96640897d6515ad626402fde36a9/src/azure-cli/azure/cli/command_modules/vm/_params.py#L565-L567)).
- **Addressing:** `--name/-n` with `--resource-group/-g`, or `--ids`. The help example passes every VM id in a
  resource group
  ([_help.py#L1424-L1434](https://github.com/Azure/azure-cli/blob/17100e145edf96640897d6515ad626402fde36a9/src/azure-cli/azure/cli/command_modules/vm/_help.py#L1424-L1434)).
- **Output:** the VM with its instance view. Legacy CLI client-flattens `properties`, so it prints `instanceView`
  at the top level; AzClips keeps the ARM shape, so it lives at `properties.instanceView`, as every other AzClips
  VM read does.

### Azure PowerShell

Evidence: `Azure/azure-powershell@main`, pinned to
[`2c214a0`](https://github.com/Azure/azure-powershell/tree/2c214a0e7b7e25cc81652ad3050b62c22030fc54/src/Compute/Compute).

- `-Status` is a switch on every parameter set
  ([GetAzureVMCommand.cs#L83-L86](https://github.com/Azure/azure-powershell/blob/2c214a0e7b7e25cc81652ad3050b62c22030fc54/src/Compute/Compute/VirtualMachine/Operation/GetAzureVMCommand.cs#L83-L86)).
- For a single VM it sends the same `$expand=instanceView` GET and writes a `PSVirtualMachineInstanceView`: the
  instance view plus `ResourceGroupName` and `Name`
  ([GetAzureVMCommand.cs#L147-L153](https://github.com/Azure/azure-powershell/blob/2c214a0e7b7e25cc81652ad3050b62c22030fc54/src/Compute/Compute/VirtualMachine/Operation/GetAzureVMCommand.cs#L147-L153),
  [PSVirtualMachineInstanceView.cs#L20-L86](https://github.com/Azure/azure-powershell/blob/2c214a0e7b7e25cc81652ad3050b62c22030fc54/src/Compute/Compute/Models/PSVirtualMachineInstanceView.cs#L20-L86)).
- `-Status` takes precedence over `-UserData`.

### REST API

Evidence: `Azure/azure-rest-api-specs@main`, pinned to `6a073b3`.

- `VirtualMachineInstanceView` at `2025-11-01` defines 17 properties
  ([ComputeRP.json](https://github.com/Azure/azure-rest-api-specs/blob/6a073b33fb652ffa7847af8e855a417d4f5b2b1a/specification/compute/resource-manager/Microsoft.Compute/Compute/stable/2025-11-01/ComputeRP.json#L19979)).
- ARM also returns `capacityReservationType` at this api-version (it appears in the committed lifecycle
  recording), but the `2025-11-01` spec does not define it.

## Current AzClips state

- `vm show`, `Get-AzVM_Get` and `Get-AzVM_Id` route to `GetVMProcessor_Orchestrator`. `vm list` and
  `Get-AzVM_List*` route to `ListVMProcessor_Orchestrator`. Both use `VirtualMachineDetailEnricher`.
- The detail switches (`vm show -d`, `vm list -d`, and `Get-AzVM -Status` on the list parameter sets) flatten the
  power state, and for `-Status` the OS, hypervisor and maintenance fields, onto `VirtualMachine`, then drop
  `properties.instanceView`.
- `VirtualMachineInstanceView` models only `statuses` (`code`, `displayStatus`), `osName`, `osVersion`,
  `hyperVGeneration` and `maintenanceRedeployStatus`. Everything else ARM returns is dropped on deserialization.
- There is no `vm get-instance-view`, and PowerShell `-Status` is not available on the `Get` and `Id` parameter
  sets.

## Decisions

| # | Decision | Rationale |
|---|---|---|
| D1 | The output is the full `VirtualMachine`, with `properties.instanceView` kept and fully modeled. | Matches legacy CLI, and gives PowerShell one contract to adopt later. |
| D2 | Hand-extend the typed Compute models to every instance-view field in the `2025-11-01` spec. | Compute models are hand-maintained (Compute has no codegen config), and typed models read well from PowerShell later. |
| D3 | A standalone `GetVMInstanceViewProcessor_Orchestrator : GetVMProcessor`, routed from CLI `vm get-instance-view` and from PowerShell `Get-AzVM_GetStatus`/`Get-AzVM_IdStatus`. | Keeps instance-view behavior in one class both surfaces share, without changing the existing show orchestrator. |
| D4 | PowerShell `Get-AzVM -Status` for one VM routes on the switch's presence. New `GetStatus`/`IdStatus` parameter sets select `Get-AzVM_GetStatus`/`Get-AzVM_IdStatus`, which run `GetVMInstanceViewProcessor_Orchestrator`. Without `-Status`, or with `-Status:$false`, `Get-AzVM_Get`/`Get-AzVM_Id` keep `GetVMProcessor_Orchestrator`. | Amended; this was deferred. Routing in PowerShell's own parameter binder leaves the show orchestrator and the CLI untouched. The binder also picks `GetStatus`/`IdStatus` for `-Status:$false`, so the cmdlet routes a false switch back to `Get-AzVM_Get`/`Get-AzVM_Id`, as legacy treats it as absent. |
| D5 | The design and plan live under `docs/superpowers/`. | Chosen during brainstorming. |
| D6 | `-Status -UserData` combine into one GET with `$expand=instanceView,userData`. | The output is the full VM, so both fit. `Get-AzVM -Status -UserData` on a list and `vm show -d -u` already combine the same way. |

## Design

### Command surface: `src/Extensions/Compute/ExtensionIndex.cs`

Add `s_getVMInstanceView_CLI` and register it in `s_cliTable` as `["vm get-instance-view"]`. The PowerShell entries
are under "PowerShell surface" below.

| CLI parameter | Processor property | Type | Aliases | Help text |
|---|---|---|---|---|
| `subscription-id` | `SubscriptionId` | `string` | | Azure Subscription Id. Defaults to the active profile's subscription. |
| `name` | `Name` | `string` | `n` | The name of the virtual machine. Provide either --ids or --name and --resource-group. |
| `resource-group` | `ResourceGroupName` | `string` | `g` | The resource group name. Provide either --ids or --name and --resource-group. |
| `ids` | `Ids` | `string[]` | | One or more virtual machine resource IDs. Provide either --ids or --name and --resource-group. |

- Every parameter is `IsRequired: false`. The orchestrator enforces "name and resource group, or ids", as
  `vm show` does, because `ParameterMetadata` cannot express that rule.
- `ParameterAliases` maps `n` to `name` and `g` to `resource-group`, following `vm delete`, `vm stop` and
  `vm deallocate`.
- `profile` is added by the existing static constructor, like every other command.
- `--include-user-data` and `--show-details` are not exposed. Legacy ignores user data for this command.

### PowerShell surface: `src/PS/Compute/Cmdlets/GetVMCmdlet.cs` and `ExtensionIndex.cs`

`GetVMCmdlet` gains two parameter sets:

| Parameter set | Mandatory | Optional |
|---|---|---|
| `GetStatus` | `-ResourceGroupName`, `-Name`, `-Status` | `-SubscriptionId`, `-UserData`, `-Profile` |
| `IdStatus` | `-Id` (position 0, from the pipeline by property name), `-Status` | `-SubscriptionId`, `-UserData`, `-Profile` |

- The existing `Get` and `Id` sets do not take `-Status`. So PowerShell's binder picks `GetStatus` or `IdStatus`
  exactly when `-Status` is given with a name and resource group, or with an id. The list sets keep their optional
  `-Status`.
- Routing does not change for `-Status` itself: the cmdlet sends `Get-AzVM_{ParameterSetName}`, which now resolves
  to `Get-AzVM_GetStatus` or `Get-AzVM_IdStatus`. `-Status:$false` also binds `GetStatus` or `IdStatus`, but the
  cmdlet sends `Get-AzVM_Get` or `Get-AzVM_Id` instead, as legacy treats a false switch as absent.
- The cmdlet forwards `-Status` only for the list sets (`List`, `ListByName`, `ListLocation`). For `GetStatus` and
  `IdStatus`, the command key itself says "with status". Forwarding a parameter that the target command does not
  declare fails binding with "The property 'Status' does not exist on processor".

`ExtensionIndex` adds two PowerShell entries, both with `ProcessorType` `GetVMInstanceViewProcessor_Orchestrator`:

| Key | Parameters |
|---|---|
| `Get-AzVM_GetStatus` | `SubscriptionId`; `Name` (required); `ResourceGroupName` (required); `UserData` → `IncludeUserData` |
| `Get-AzVM_IdStatus` | `SubscriptionId`; `Id` → `Ids` (`string[]`, required); `UserData` → `IncludeUserData` |

`Get-AzVM_Get`, `Get-AzVM_Id` and `GetVMProcessor_Orchestrator` do not change.

| Call | Key | Request |
|---|---|---|
| `Get-AzVM -ResourceGroupName rg -Name vm` | `Get-AzVM_Get` | Plain GET, unchanged |
| `Get-AzVM -ResourceGroupName rg -Name vm -Status` | `Get-AzVM_GetStatus` | `$expand=instanceView` |
| `Get-AzVM -ResourceGroupName rg -Name vm -Status -UserData` | `Get-AzVM_GetStatus` | `$expand=instanceView,userData` |
| `Get-AzVM -ResourceGroupName rg -Name vm -Status:$false` | `Get-AzVM_Get` | Plain GET, as without `-Status` |
| `Get-AzVM -Id <id> -Status` | `Get-AzVM_IdStatus` | One GET per id, written as one array |
| `Get-AzVM -Id <id> -Status:$false` | `Get-AzVM_Id` | Plain GET, as without `-Status` |
| `Get-AzVM -Status`, `-Name vm -Status`, `-Location l -Status` | List keys, unchanged | List, then a per-VM fan-out |

The output is the VM with `Properties.InstanceView` intact, the same shape as `vm get-instance-view`.
`-UserData` adds `Properties.UserData`.

### Orchestrator: `src/Extensions/Compute/Customization/GetVMInstanceViewProcessor_Orchestrator.cs`

```csharp
public class GetVMInstanceViewProcessor_Orchestrator : GetVMProcessor
{
    public string[]? Ids { get; set; }
    public bool IncludeUserData { get; set; }

    public override async Task ProcessAsync()
    {
        Expand = VirtualMachineDetailEnricher.ResolveExpand(instanceView: true, userData: IncludeUserData);

        if (Ids is null || Ids.Length == 0)
        {
            EnsureAddressable();
            await base.ProcessAsync(); // one GET; the base pipeline writes the VM as returned
            return;
        }

        // Parse every id with VirtualMachineIdResolver.ParseVirtualMachineResourceId, publish
        // ProcessorProcessAsyncStart, send one CreateExtendedProcessor<GetVMProcessor> GET per id, then a
        // single WriteOutput(results.ToArray()), and publish ProcessorProcessAsyncEnd in a finally block.
    }
}
```

Behavior:

- Every request uses `$expand=instanceView`, or `$expand=instanceView,userData` when PowerShell passes
  `-UserData`. The CLI exposes no user-data switch. The value still comes from `ResolveExpand`, the one place detail
  switches become expand strings.
- Without `--ids`, a missing name or resource group fails before any request with
  `AzclipsErrorException.InvalidInput("Specify the virtual machine by both name and resource group, or address it by resource id.")`.
- The name and resource group path writes one `VirtualMachine`. The `--ids` path writes one `VirtualMachine[]`,
  one entry per id in input order, even for a single id. Both match `vm show`.
- `properties.instanceView` is never cleared, and none of the synthetic top-level fields (`powerState`,
  `publicIps`, `fqdns`, `privateIps`, `macAddresses`, `osName`, `osVersion`, `hyperVGeneration`,
  `maintenanceRedeployStatus`) are set.
- Every id is parsed before the first request, so a malformed id fails with `VirtualMachineIdResolver`'s
  existing `InvalidInput` without reading the others. ARM errors, such as a 404, propagate unchanged through
  `HttpRuntime.SendAsync`. With `--ids`, the first failing id fails the command, as it does for `vm show --ids`.
- The customization owns no endpoint, method or api-version. `GetVMProcessor` keeps the route and
  `api-version=2025-11-01`.
- Existing commands do not change: `vm show -d`, `vm list -d`, `vm create` and `Get-AzVM -Status` (list) still
  flatten the power state and drop the instance view.

### Output model: `src/Extensions/Compute/Models`

Create `Models/VirtualMachineInstanceView.cs` for every instance-view type. Move the three existing types,
`VirtualMachineInstanceView`, `InstanceViewStatus` and `MaintenanceRedeployStatus`, there from `VirtualMachine.cs`
(about 420 lines today), keeping their names and namespace.

| Type | Properties (JSON name is the camelCase of the C# name) |
|---|---|
| `VirtualMachineInstanceView` | `PlatformUpdateDomain` (`int?`), `PlatformFaultDomain` (`int?`), `ComputerName`, `OsName`, `OsVersion`, `HyperVGeneration`, `RdpThumbPrint`, `VmAgent`, `MaintenanceRedeployStatus`, `Disks`, `Extensions`, `VmHealth`, `BootDiagnostics` (`BootDiagnosticsInstanceView?`), `AssignedHost`, `Statuses`, `PatchStatus`, `IsVMInStandbyPool` (`bool?`) |
| `InstanceViewStatus` | `Code`, `Level`, `DisplayStatus`, `Message`, `Time` |
| `VirtualMachineAgentInstanceView` | `VmAgentVersion`, `ExtensionHandlers`, `Statuses` |
| `VirtualMachineExtensionHandlerInstanceView` | `Type`, `TypeHandlerVersion`, `Status` |
| `DiskInstanceView` | `Name`, `EncryptionSettings`, `Statuses`, `StorageAlignmentStatus` |
| `DiskEncryptionSettings` | `DiskEncryptionKey` (`KeyVaultSecretReference?`), `KeyEncryptionKey` (`KeyVaultKeyReference?`), `Enabled` (`bool?`) |
| `KeyVaultSecretReference` | `SecretUrl`, `SourceVault` (`SubResource?`) |
| `KeyVaultKeyReference` | `KeyUrl`, `SourceVault` (`SubResource?`) |
| `SubResource` | `Id` |
| `VirtualMachineExtensionInstanceView` | `Name`, `Type`, `TypeHandlerVersion`, `Substatuses`, `Statuses` |
| `VirtualMachineHealthStatus` | `Status` |
| `BootDiagnosticsInstanceView` | `ConsoleScreenshotBlobUri`, `SerialConsoleLogBlobUri`, `Status` |
| `VirtualMachinePatchStatus` | `AvailablePatchSummary`, `LastPatchInstallationSummary`, `ConfigurationStatuses` |
| `AvailablePatchSummary` | `Status`, `AssessmentActivityId`, `RebootPending` (`bool?`), `CriticalAndSecurityPatchCount` (`int?`), `OtherPatchCount` (`int?`), `StartTime`, `LastModifiedTime`, `Error` (`ApiError?`) |
| `LastPatchInstallationSummary` | `Status`, `InstallationActivityId`, `MaintenanceWindowExceeded` (`bool?`), `NotSelectedPatchCount`, `ExcludedPatchCount`, `PendingPatchCount`, `InstalledPatchCount`, `FailedPatchCount` (all `int?`), `StartTime`, `LastModifiedTime`, `Error` (`ApiError?`) |
| `ApiError` | `Details` (`List<ApiErrorBase>?`), `Innererror` (`InnerError?`), `Code`, `Target`, `Message` |
| `ApiErrorBase` | `Code`, `Target`, `Message` |
| `InnerError` | `Exceptiontype`, `Errordetail` |
| `MaintenanceRedeployStatus` | Unchanged |

Conventions, matching the existing Compute models:

- Properties not marked otherwise are `string?`. Enums (`Level`, `HyperVGeneration`, `StorageAlignmentStatus`,
  patch `Status`, `LastOperationResultCode`) and date-times (`Time`, `StartTime`, `LastModifiedTime`) stay
  strings, as `MaintenanceRedeployStatus` already does, so a new enum value or time format cannot break
  deserialization.
- Lists are nullable, except `VirtualMachineInstanceView.Statuses`, which keeps its existing `[]` default.
- Every new nullable property carries `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`, and every
  type overrides `ToString()` with `JsonNode.FromObject(this).ToString()`. The moved types keep their existing
  members exactly as they are, so output that already includes them does not change.
- C# names camelCase back to ARM's exact names under the camelCase policy that both `ComputeProcessorBase` and
  `CliOutputFormatter` use, for example `IsVMInStandbyPool` to `isVMInStandbyPool` and `Innererror` to
  `innererror`.
- The existing `BootDiagnostics` (the `diagnosticsProfile` setting) is unrelated and unchanged.
- The comments on `VirtualMachineProperties.InstanceView` and `VirtualMachineInstanceView` change to say that
  `vm get-instance-view`, and `Get-AzVM -Status` for one VM, keep the instance view, while the detail switches
  still flatten and drop it.
- `capacityReservationType` is not modeled, because the `2025-11-01` spec does not define it.
- Existing readers (`VirtualMachineDetailEnricher`, `vm create`, and the list and show detail switches) only read
  fields that already exist, so they are unaffected.

### Help: `src/CLI/Help/Docs/vm/get-instance-view.md`

- Write the file directly, or scaffold it with `dotnet run --project tools/Help/Azclips.Help.Tool -- --scaffold vm`
  and replace every `TODO`.
- **Summary:** "Get instance information about a virtual machine."
- **Description:** the command returns the VM with its run-time instance view at `properties.instanceView`
  (power and provisioning statuses, VM agent, disks, extensions, boot diagnostics, patch status) from one request
  per virtual machine.
- **Examples:**
  - `az vm get-instance-view --name myvm --resource-group myrg`
  - the short form, `az vm get-instance-view -n myvm -g myrg`
  - `--ids`, noting that it always returns an array with one entry per id
  - reading the power state with
    `--query "properties.instanceView.statuses[?starts_with(code, 'PowerState/')].displayStatus"`
- `vm/index.md` needs no change: its description already covers inspecting VMs.
- Validate with `.\build\build.ps1 -GenerateHelpArtifacts`.
- Refresh `tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json` with
  `pwsh -File build/Update-CliExportFixtures.ps1`. `CliExportFixturesTests` snapshots the exported `vm` command
  surface, help text included, so a new command fails it until the fixture is refreshed.

### Help: `src/PS/Compute/help/Get-AzVM.md`

- Add `GetStatus` and `IdStatus` syntax blocks.
- Add the new sets to the parameter-set lists of `-ResourceGroupName`, `-Name`, `-Id`, `-UserData`,
  `-SubscriptionId` and `-Status`. `-Status` is required in `GetStatus` and `IdStatus`.
- Rewrite the `-Status` description to cover both behaviors:
  - The list forms add the power state, OS name, OS version, hypervisor generation and maintenance status to each
    VM, for at most 100 VMs.
  - For one VM, `-Status` returns the full `Properties.InstanceView`.

  Remove "Cannot be combined with `-ResourceGroupName` and `-Name` together".
- Add an example that gets one VM's instance view, and update OUTPUTS.
- Validate with `.\build\build.ps1 -GenerateHelpArtifacts`.

## Testing

### Scenario test: extend `VmLifecycleScenarioTests`

Extend `CreateListShowStopDeallocateStartDeleteVirtualMachine` in
`tests/Scenario.Tests/Compute/VmLifecycleScenarioTests.cs`. The method keeps its name, so the same recording,
`Compute/Records/VmLifecycleScenarioTests/CreateListShowStopDeallocateStartDeleteVirtualMachine.json`, is
re-recorded rather than replaced by a new file.

The CLI runs `vm get-instance-view` four times, in both the record branch and the CLI playback branch, mirroring
upstream `_check_vm_power_state` in
[`test_vm_create_state_modifications`](https://github.com/Azure/azure-cli/blob/17100e145edf96640897d6515ad626402fde36a9/src/azure-cli/azure/cli/command_modules/vm/tests/latest/test_vm_commands.py#L1852-L1957):

| Inserted after | Addressing | Expected power state | Detailed checks |
|---|---|---|---|
| `vm show` | `-g` / `-n` | `PowerState/running` | Yes |
| `vm stop` | `--resource-group` / `--name` | `PowerState/stopped` | No |
| `vm deallocate` | `--ids` | `PowerState/deallocated` | No |
| `vm start` | `--resource-group` / `--name` | `PowerState/running` | No |

A shared helper, `AssertInstanceView(outputs, expectedPowerState, detailed)`, checks:

- The output is one `VirtualMachine`, or a one-element `VirtualMachine[]` for `--ids`, named `lifecycle-vm`. In
  playback, its `Id` also matches the placeholder-subscription resource id.
- `properties.instanceView.statuses` has exactly two entries: `ProvisioningState/succeeded` with level `Info`,
  then the expected `PowerState/*` code.
- The synthetic top-level fields stay null, which guards that `get-instance-view` neither flattens nor clears the
  instance view.
- With `detailed`, it also checks that `hyperVGeneration` is non-empty, `vmAgent` is present, and `disks` holds
  exactly one OS disk whose status is `ProvisioningState/succeeded`, which proves the new models deserialize
  live data. Guest-agent values, disk names and timestamps are not asserted, because they change on every
  re-record.

In record mode, the `--ids` step builds the id from the live subscription and the run-scoped resource group, so
the resource-id helper takes the group name as a parameter.

The PowerShell playback branch mirrors the CLI's four checks with the same `AssertInstanceView` helper:

| Inserted after | Command | Expected power state | Detailed checks |
|---|---|---|---|
| `Get-AzVM_Get` | `Get-AzVM_GetStatus` | `PowerState/running` | Yes |
| `Stop-AzVM -StayProvisioned` | `Get-AzVM_GetStatus` | `PowerState/stopped` | No |
| `Stop-AzVM` (deallocates) | `Get-AzVM_IdStatus` | `PowerState/deallocated` | No |
| `Start-AzVM -Id` | `Get-AzVM_GetStatus` | `PowerState/running` | No |

They send the same four requests the CLI records, in the same order, so one recording holds both surfaces to one
contract. Like every other PowerShell step, they call the command keys directly, with the parameters the cmdlet
would forward. For these keys that means no `Status`. The first instance-view read is recorded after `vm list -d`,
so `New-AzVM` and `Get-AzVM_List -Status` still consume the same `$expand=instanceView` entries, in the same
order, as today.

Re-recording performs real Azure operations in the developer's subscription. It runs only after the test code is
approved, while signed in with the azclips CLI, and the rewritten recording is reviewed before commit.

### Orchestration test: `tests/Scenario.Tests/Compute/VmGetInstanceViewOrchestrationTests.cs`

Drives `vm get-instance-view` through the real dispatcher, binder and request pipeline against an in-memory fake
ARM, the pattern `VmCreateOrchestrationTests` uses, so the orchestrator is test-driven without a live run. The fake
answers a VM GET with an instance view only when the request carries `$expand=instanceView`, as ARM does.

- Name and resource group, spelled `-g`/`-n`, write one VM with the instance view intact, and send exactly one
  `$expand=instanceView` GET.
- `--ids` with two ids writes a two-element array, in input order.
- A missing resource group and a malformed id each fail with `InvalidInputException` before any request.
- An unknown VM fails with ARM's 404.
- PowerShell cases drive the real `GetVMCmdlet` through `ScenarioHost.RunPowerShellCmdlet<GetVMCmdlet>`, so
  PowerShell's own parameter-set selection is exercised. This needs a `ProjectReference` from
  `tests/Scenario.Tests/Scenario.Tests.csproj` to `src/PS/Compute/Cmdlets.Compute.csproj`, alongside the other
  `Cmdlets.*` references already there.
  - `-ResourceGroupName -Name -Status` sends one `$expand=instanceView` GET and writes one VM with its instance view
    intact.
  - Adding `-UserData` sends `$expand=instanceView,userData`.
  - `-Id -Status` sends one `$expand=instanceView` GET. The orchestrator writes a one-element array, which the
    cmdlet unrolls, so PowerShell emits one VM.
  - Without `-Status`, the same name and resource group send a plain GET with no `$expand`, and the VM has no
    instance view. That is `GetVMProcessor_Orchestrator`, unchanged.
  - `-ResourceGroupName -Name -Status:$false` binds `GetStatus`, but the cmdlet routes the false switch back to
    `Get-AzVM_Get`: one plain GET with no `$expand`, and one VM with no instance view.
  - `-Id -Status:$false` binds `IdStatus`, but goes to `Get-AzVM_Id` the same way: one plain GET, and one VM with
    no instance view.
  - `-ResourceGroupName -Status`, a list form, still lists and then flattens the power state, leaving no instance
    view.

### Unit test: `tests/Extensions.Compute.Tests/VirtualMachineInstanceViewModelTests.cs`

Deserialize with the options `ComputeProcessorBase.Init` builds:
`PropertyNameCaseInsensitive = true` and `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`.

1. **Spec example:** the response body of the `2025-11-01` example
   [`VirtualMachine_Get_InstanceView.json`](https://github.com/Azure/azure-rest-api-specs/blob/6a073b33fb652ffa7847af8e855a417d4f5b2b1a/specification/compute/resource-manager/Microsoft.Compute/Compute/stable/2025-11-01/examples/virtualMachineExamples/VirtualMachine_Get_InstanceView.json),
   embedded as a raw string literal. It covers 13 of the 17 fields, including `vmAgent.extensionHandlers`,
   `extensions`, `patchStatus` and `bootDiagnostics`, which a new VM does not return live. Assert that each one
   lands on the model, along with representative nested values.
2. **Remaining fields:** a small JSON built from the spec schema for the other four fields (`assignedHost`,
   `maintenanceRedeployStatus`, `rdpThumbPrint`, `vmHealth`), plus `disks[].encryptionSettings`,
   `disks[].storageAlignmentStatus`, and a patch-summary `error` with `details` and `innererror`.
3. **Serialization:** round-trip a populated model and assert the output uses ARM's exact names, for example
   `rdpThumbPrint`, `isVMInStandbyPool`, `vmAgent` and `innererror`.

### Upstream live-test mapping

| Upstream case | Decision | Notes |
|---|---|---|
| CLI [`test_vm_create_state_modifications`](https://github.com/Azure/azure-cli/blob/17100e145edf96640897d6515ad626402fde36a9/src/azure-cli/azure/cli/command_modules/vm/tests/latest/test_vm_commands.py#L1867) | Convert partially, by extending `VmLifecycleScenarioTests` | `vm restart`, `vm resize`, `vm user` and unmanaged disks are absent. AzClips `VirtualMachine` has no top-level `resourceGroup`. |
| CLI [`test_vm_simulate_eviction`](https://github.com/Azure/azure-cli/blob/17100e145edf96640897d6515ad626402fde36a9/src/azure-cli/azure/cli/command_modules/vm/tests/latest/test_vm_commands.py#L2081) | Prerequisite missing | Needs `vm simulate-eviction` and Spot priority. |
| CLI [`test_vm_create_no_wait`](https://github.com/Azure/azure-cli/blob/17100e145edf96640897d6515ad626402fde36a9/src/azure-cli/azure/cli/command_modules/vm/tests/latest/test_vm_commands.py#L2128) | Prerequisite missing | Needs `--no-wait` and `vm wait`. |
| CLI extension cases ([#L2431](https://github.com/Azure/azure-cli/blob/17100e145edf96640897d6515ad626402fde36a9/src/azure-cli/azure/cli/command_modules/vm/tests/latest/test_vm_commands.py#L2431), [#L2500](https://github.com/Azure/azure-cli/blob/17100e145edf96640897d6515ad626402fde36a9/src/azure-cli/azure/cli/command_modules/vm/tests/latest/test_vm_commands.py#L2500)) | Prerequisite missing | Needs `vm extension set`. The unit test covers the extension instance view. |
| CLI storage-alignment case ([#L12375](https://github.com/Azure/azure-cli/blob/17100e145edf96640897d6515ad626402fde36a9/src/azure-cli/azure/cli/command_modules/vm/tests/latest/test_vm_commands.py#L12375)) | Prerequisite missing | Needs `--vmss` and data disks. The unit test covers `storageAlignmentStatus`. |
| CLI capacity-reservation case ([#L14036](https://github.com/Azure/azure-cli/blob/17100e145edf96640897d6515ad626402fde36a9/src/azure-cli/azure/cli/command_modules/vm/tests/latest/test_vm_commands.py#L14036)) | Out of scope | `capacityReservationType` is not in the `2025-11-01` spec, and capacity reservation commands are absent. |
| PowerShell single-VM `-Status` cases ([VirtualMachineTests.ps1#L4255-L4262](https://github.com/Azure/azure-powershell/blob/2c214a0e7b7e25cc81652ad3050b62c22030fc54/src/Compute/Compute.Test/ScenarioTests/VirtualMachineTests.ps1#L4255-L4262)) | Convert, in the lifecycle PowerShell branch | The same power-state codes, read from `Properties.InstanceView.Statuses` instead of legacy's top-level `Statuses`. |

## Non-goals

- Legacy's command-specific table columns. AzClips formats `-o table` generically.
- `vm wait`, whose legacy getter is `get_instance_view`.
- `capacityReservationType`, and a top-level `resourceGroup` field (`vm show` has neither).
- A live smoke script under `tests/Scripts`.

## Validation

| Check | Command |
|---|---|
| Build | `dotnet build azclips.slnx` |
| Unit tests | `dotnet test tests\Extensions.Compute.Tests\Extensions.Compute.Tests.csproj` |
| Orchestration tests | `dotnet test tests\Scenario.Tests --filter FullyQualifiedName~VmGetInstanceViewOrchestrationTests` |
| Scenario playback | `dotnet test tests\Scenario.Tests --filter FullyQualifiedName~VmLifecycleScenarioTests` |
| Help validators | `.\build\build.ps1 -GenerateHelpArtifacts` |
| CLI export fixture | `dotnet test tools\Help\Azclips.Help.Tool.Tests --filter FullyQualifiedName~CliExportFixturesTests` |
| Re-record, after approval | `$env:AZURE_TEST_MODE = 'Record'; dotnet test tests\Scenario.Tests --filter FullyQualifiedName~VmLifecycleScenarioTests` |

Playback of the extended scenario fails until it is re-recorded. That failure is expected, and is the signal to
record after review.
