# VM lifecycle scenario: add the list steps — design

**Date:** 2026-09-25
**Status:** Approved, ready for planning
**Scope:** `tests/Scenario.Tests/VmLifecycleScenarioTests.cs` and its recording under
`tests/Scenario.Tests/SessionRecords/VmLifecycleScenarioTests/`

## Problem

`VmLifecycleScenarioTests` runs create, show, stop, deallocate, start and delete through the real
dispatcher, binder and ARM request pipeline, replaying a recorded session on every PR. It has no
list step, so the list work on this branch — the three list scopes, the list orchestrator, and the
per-VM detail enrichment behind `--show-details` and `-Status` — has no automated end-to-end
coverage. The only other coverage is the `VMList` smoke scripts, which need a live subscription and
are run by hand.

## Goals

- Extend the lifecycle to create, list, show, stop, deallocate, start, delete.
- Cover the plain resource-group list and the detail paths on both surfaces: `az vm list` and
  `az vm list --show-details` on the CLI; `Get-AzVM_List` and `Get-AzVM_List` with `Status` on
  PowerShell.
- Assert the exact list output shape and the enrichment each detail switch performs.

## Non-goals

- **Subscription-wide listing** (`az vm list` without `--resource-group`, `Get-AzVM`, or
  `Get-AzVM -Name`). The recording would capture every VM in the recording subscription,
  committing unrelated resource names that change on every re-record.
- **List-mode `-UserData`.** It needs a GET with `$expand=userData`, which only a PowerShell call
  makes. Both surfaces replay one CLI-recorded session, so nothing would record that request and
  its playback would fail.
- **PowerShell parameter-set binding** (`Get`, `List`, `ListByName`, `ListLocation`).
  `ScenarioHost.RunPowerShell` sends the engine command key directly and bypasses the cmdlet, so
  binding stays covered only by the binding probe.
- **Resource-group cleanup inside the test.** The test deletes the VM but not the resource group,
  as today; record-mode cleanup stays manual, per the scenario README.
- **A new `-g` alias on `az vm list`.** See "Finding outside scope".

## Scenario flow

The test method is renamed `CreateShowStopDeallocateStartDeleteVirtualMachine` →
`CreateListShowStopDeallocateStartDeleteVirtualMachine`, following
`IdentityLifecycleScenarioTests.CreateListShowUpdateDeleteIdentity`. The two list steps run
straight after create, while the VM is running.

| # | CLI (records live, then replays) | PowerShell (replays) |
|---|---|---|
| 1 | `group create` | `New-AzResourceGroup` |
| 2 | `vm create` | `New-AzVM` |
| 3 | **`vm list --subscription-id <sub> --resource-group scenario-vm-lifecycle-rg`** | **`Get-AzVM_List` with `SubscriptionId`, `ResourceGroupName`** |
| 4 | **the same, plus `-d`** | **the same, plus `Status = true`** |
| 5 | `vm show` | `Get-AzVM_Get` |
| 6–9 | stop, deallocate, start, delete | unchanged |

Step 4 on the CLI uses the short alias `-d`, so the scenario also exercises the alias wiring
(`ScenarioHost.RunCli` parses through the real `ExtensionDispatcher`).

The record block runs the same two CLI list steps against live Azure, calling the same assertions
without an expected subscription ID, exactly as the existing record-block steps do. Playback passes
the placeholder subscription ID on both surfaces.

The class `<summary>` changes its step list to "create, list, show, stop, deallocate, start,
delete".

## Recorded requests and playback sharing

Steps 3 and 4 add four entries to the recording, all scoped to the scenario's own resource group
`scenario-vm-lifecycle-rg`:

| Entry | Recorded by | Replayed by |
|---|---|---|
| `GET …/resourceGroups/scenario-vm-lifecycle-rg/providers/Microsoft.Compute/virtualMachines` (step 3) | CLI | CLI, PowerShell |
| the same list GET again (step 4) | CLI | CLI, PowerShell |
| `GET …/virtualMachines/lifecycle-vm?…&$expand=instanceView` (step 4) | CLI | CLI, PowerShell |
| `GET …/networkInterfaces/scenario-lifecycle-nic` (step 4) | CLI | CLI only |

Playback matches on method plus full URI, uses each entry once, and consumes duplicates in recorded
order; each surface replays the recording independently. Consequences:

- Both surfaces send the identical `$expand=instanceView` GET, because both go through
  `VirtualMachineDetailEnricher.ResolveExpand`, so PowerShell replays what the CLI recorded.
- The NIC GET serves `-d` only. PowerShell never sends it and leaves that entry unused, which is
  expected: playback fails only on a request with no recorded response, never on a leftover entry.
- `vm create` allocates no public IP, so `-d` sends no public-IP GET.

## Assertions

### List shape — every list step, both surfaces

```csharp
private static VirtualMachine AssertListed(IReadOnlyList<object> outputs, string? expectedSubscriptionId = null)
{
    var vms = Assert.IsType<List<VirtualMachine>>(Assert.Single(outputs));
    AssertVm(vms, expectedSubscriptionId);
    return vms[0];
}
```

- `Assert.IsType` is an exact-type match, so the output must be the `List<VirtualMachine>` the list
  orchestrator writes: an array, another collection, or VMs written one at a time all fail.
- The outer `Assert.Single` checks the command wrote exactly one object, the list; the existing
  `AssertVm` then checks the list holds exactly one VM, with the right name, location,
  `Succeeded` provisioning state, and on playback the full resource ID. `AssertVm` takes the list
  directly because `List<VirtualMachine>` converts to `IReadOnlyList<object>`.
- The VM is returned so the detail checks build on it.

This mirrors `VNetScenarioTests.AssertListed`. It holds on both surfaces here because
`RunPowerShell` bypasses the cmdlet; the real cmdlet unrolls the list into separate objects.

### CLI `--show-details` (step 4)

`AssertListedWithDetails` calls `AssertListed`, then asserts:

| Check | Why |
|---|---|
| `PowerState` is `VM running` | flattened from the instance view |
| `PrivateIps` starts with `10.0.0.` | the scenario's subnet is `10.0.0.0/24` |
| `MacAddresses` is not empty | the NIC lookup ran |
| `PublicIps` and `Fqdns` are `""` | empty string, not null: the NIC lookup ran and found no public IP |
| `Properties.InstanceView` is null | flattened, then dropped |
| `OsName`, `OsVersion`, `HyperVGeneration`, `MaintenanceRedeployStatus` are null | **regression guard** |

### PowerShell `-Status` (step 4)

`AssertListedWithStatus` calls `AssertListed`, then asserts:

| Check | Why |
|---|---|
| `PowerState` is `VM running` | flattened from the instance view |
| `HyperVGeneration` is not null or empty | the `-Status`-only fields were copied |
| `Properties.InstanceView` is null | flattened, then dropped |
| `PublicIps`, `Fqdns`, `PrivateIps`, `MacAddresses` are null | **regression guard** |

### Deliberately not asserted

- `OsName` and `OsVersion`: the guest agent reports them, and it can lag on a VM that was just
  created, so their values could change between recordings.
- `MaintenanceRedeployStatus` under `-Status`: normally absent unless maintenance is scheduled.
- A specific `HyperVGeneration` value: `V1` and `V2` both pass, so the image `vm create` picks
  doesn't matter.

## Comments the test must carry

1. **Regression guards.** Each of the two guard blocks is preceded by a comment saying it can't
   fail with today's wiring and exists to catch a change to a gate:

   ```csharp
   // Regression guard: can't fail with today's wiring. The CLI has no -Status, so vm list -d passes
   // includeStatusFields: false and must never surface the -Status-only fields.
   ```

   ```csharp
   // Regression guard: can't fail with today's wiring. PowerShell has no -ShowDetails, so -Status
   // must never run the NIC lookup that fills the network fields.
   ```
2. **List-mode `-UserData`**, immediately above the PowerShell `-Status` list call:

   ```csharp
   // List-mode -UserData isn't covered: it needs a GET with $expand=userData, which only a
   // PowerShell call makes. Both surfaces replay this one CLI-recorded session, so nothing
   // records that request and its playback would fail.
   ```

## Recording workflow

The renamed method resolves to a new recording file, and the old one lacks the list requests. So
the test change and the new recording land in **one commit**; committing either alone breaks CI
playback.

1. **Change the test** and build it. Playback is expected to fail until step 2, with no
   recording found.
2. **Record live.** A human runs this, because it needs a live sign-in and creates real resources:
   - Sign in with the azclips CLI, not the classic Azure CLI, as the README requires.
   - The subscription needs `Microsoft.Compute` and `Microsoft.Network` registered, and quota for
     one `Standard_D2s_v3` in `westus2`.
   - **Start from a clean resource group.** If `scenario-vm-lifecycle-rg` survives from an earlier
     run, delete it first: a leftover VM would make the list return two and fail the single-VM
     check.
   - Run:
     ```powershell
     $env:AZURE_TEST_MODE = "Record"
     dotnet test tests/Scenario.Tests --filter FullyQualifiedName~VmLifecycleScenarioTests
     $env:AZURE_TEST_MODE = $null
     ```
   - Record mode runs the CLI assertions live, so the plain list and `-d` checks are verified
     against real ARM during the run.
   - Afterwards, delete `scenario-vm-lifecycle-rg`; the test removes the VM but not its network
     resources or the group.
3. **Verify and commit.**
   - Run the scenario in playback (`AZURE_TEST_MODE` unset). This is the first run of the
     PowerShell surface, including its `-Status` checks.
   - Confirm the new recording contains the four new entries and no live subscription IDs,
     tokens or passwords, then run `python3 devops/scripts/ci/scan_recordings.py --all`.
   - Delete the old `CreateShowStopDeallocateStartDeleteVirtualMachine.json`.
   - Commit the test change, the new recording and the deletion together.

## Risks

- **`HyperVGeneration` is first checked on playback.** Record mode runs only the CLI, and
  `vm list -d` fetches the instance view but never copies `hyperVGeneration`, so the live run
  can't confirm it. The CLI does save the full instance view to the recording, and PowerShell reads
  the field from there on playback. If Azure's reply omits it, the first playback fails. The
  recording is still correct in that case — it is what Azure returned — so the fix is to delete
  that one assertion and keep the recording; re-recording would get the same reply. The risk is
  low: the REST spec's `VirtualMachine_Get_InstanceView.json` example includes
  `"hyperVGeneration": "V1"`. Once the first playback passes, it keeps passing, because the
  recording never changes.
- **A leftover resource group** makes the record run fail at the list step; see step 2.

## Finding outside scope

AzClips `az vm list` has no `-g` alias for `--resource-group`, although legacy `az vm list -g`
does. Across the Compute CLI commands, only `vm stop` and `vm deallocate` accept `-g`; `vm create`,
`vm show`, `vm list`, `vm start` and `vm delete` don't. The scenario uses `--resource-group`, and
this gap is left for a separate change.
