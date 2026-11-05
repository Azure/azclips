# `az vm get-instance-view` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the CLI command `az vm get-instance-view`, which returns a virtual machine with its full instance view, add PowerShell `Get-AzVM -Status` for one VM on the same orchestrator, and check that instance view at every power-state change in the VM lifecycle scenario.

**Architecture:** A new standalone `GetVMInstanceViewProcessor_Orchestrator` inherits the atomic `GetVMProcessor`. It always sends `$expand=instanceView` and writes the VM with `properties.instanceView` intact. The CLI command `vm get-instance-view` and two new PowerShell keys reference it. `Get-AzVM_GetStatus` and `Get-AzVM_IdStatus` are selected by new `GetStatus`/`IdStatus` parameter sets of `GetVMCmdlet`. The Compute instance-view models are hand-extended to every property of REST api-version `2025-11-01`.

**Tech Stack:** C# with nullable enabled (.NET SDK pinned in `global.json`), System.Text.Json, xUnit, the AzClips record/playback scenario harness in `tests/Scenario.Tests`, and the `Azclips.Help.Tool` help validators.

**Spec:** `docs/superpowers/specs/2026-11-05-vm-get-instance-view-design.md`

## Global Constraints

- REST api-version `2025-11-01`. `GetVMProcessor` owns the route, method and api-version. Customization code never declares `Method`, `CreateRequestUri()`, an api-version or a URL.
- Output is the full `VirtualMachine` with `properties.instanceView` intact. Neither `vm get-instance-view` nor `Get-AzVM -Status` for one VM sets the synthetic top-level fields `PowerState`, `PublicIps`, `Fqdns`, `PrivateIps`, `MacAddresses`, `OsName`, `OsVersion`, `HyperVGeneration` or `MaintenanceRedeployStatus`.
- CLI: register `vm get-instance-view` in `s_cliTable`. PowerShell: only Task 4 changes `s_psTable`, `src/PS/Compute` and PowerShell help. It adds `Get-AzVM_GetStatus` and `Get-AzVM_IdStatus`. `Get-AzVM_Get`, `Get-AzVM_Id` and `GetVMProcessor_Orchestrator` do not change.
- CLI parameters: `--subscription-id`, `--name`/`-n`, `--resource-group`/`-g` and `--ids`, plus `--profile`, which the existing static constructor adds. No `--include-user-data`, no `--show-details`.
- PowerShell `Get-AzVM -Status` for one VM:
  - It routes through new `GetStatus` (`-ResourceGroupName -Name -Status`) and `IdStatus` (`-Id -Status`) parameter sets to `Get-AzVM_GetStatus`/`Get-AzVM_IdStatus`.
  - The cmdlet forwards `-Status` only for the list sets.
  - `-Status -UserData` is one GET with `$expand=instanceView,userData`.
- Models cover every `VirtualMachineInstanceView` property of the `2025-11-01` spec, and not `capacityReservationType`. Enums and date-times are `string?`. New nullable members carry `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`. Moved members keep their existing types and attributes.
- Existing behavior does not change for `vm show`, `vm list`, `vm create`, or `Get-AzVM`, whether `-Status` is absent or used with the list forms.
- Recordings: re-record only after the user approves the scenario test code, and never hand-edit a recording. Never assert service-generated values such as timestamps, disk names, guest-agent values or etags.
- Files under `src/` and `tests/Extensions.Compute.Tests/` start with the two-line Microsoft MIT header. Files under `tests/Scenario.Tests/` have none. Follow the folder.
- Commit messages use the repo prefixes `[Compute]`, `{Docs}` and `{Tests}`. In PowerShell, wrap a message that contains backticks in single quotes.
- If `dotnet build azclips.slnx` fails to restore the GitHub Copilot npm package, use the `building-the-solution` skill.

## File Structure

| File | Action | Responsibility |
|---|---|---|
| `src/Extensions/Compute/Models/VirtualMachineInstanceView.cs` | Create | Every instance-view type, including the three moved from `VirtualMachine.cs`. Task 4 updates two comments |
| `src/Extensions/Compute/Models/VirtualMachine.cs` | Modify | Remove the moved types; update the `InstanceView` comment (Tasks 1 and 4) |
| `src/Extensions/Compute/Customization/GetVMInstanceViewProcessor_Orchestrator.cs` | Create | The shared instance-view orchestrator. Task 4 adds `IncludeUserData` |
| `src/Extensions/Compute/ExtensionIndex.cs` | Modify | `s_getVMInstanceView_CLI` and its `s_cliTable` entry. Task 4 adds `Get-AzVM_GetStatus` and `Get-AzVM_IdStatus` |
| `src/CLI/Help/Docs/vm/get-instance-view.md` | Create | CLI help |
| `tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json` | Regenerate | Snapshot of the exported `vm` command surface |
| `tests/Extensions.Compute.Tests/VirtualMachineInstanceViewModelTests.cs` | Create | Model fidelity against the REST spec |
| `tests/Scenario.Tests/Compute/VmGetInstanceViewOrchestrationTests.cs` | Create | Orchestrator behavior against a fake ARM. Task 4 adds the PowerShell cases |
| `tests/Scenario.Tests/Compute/VmLifecycleScenarioTests.cs` | Modify | Instance-view checks across the lifecycle: CLI in Task 3, PowerShell in Task 4 |
| `tests/Scenario.Tests/Compute/Records/VmLifecycleScenarioTests/CreateListShowStopDeallocateStartDeleteVirtualMachine.json` | Re-record | Written by record mode only |
| `src/PS/Compute/Cmdlets/GetVMCmdlet.cs` | Modify (Task 4) | `GetStatus`/`IdStatus` parameter sets; forward `-Status` only for list sets |
| `src/PS/Compute/help/Get-AzVM.md` | Modify (Task 4) | PowerShell help for the new sets and the single-VM `-Status` behavior |
| `tests/Scenario.Tests/Scenario.Tests.csproj` | Modify (Task 4) | Reference `Cmdlets.Compute` so tests can drive the real `GetVMCmdlet` |

---

### Task 1: Instance-view models

**Files:**
- Create: `src/Extensions/Compute/Models/VirtualMachineInstanceView.cs`
- Modify: `src/Extensions/Compute/Models/VirtualMachine.cs` (the `InstanceView` comment near line 124, and the three classes at the end of the file, from line 368)
- Test: `tests/Extensions.Compute.Tests/VirtualMachineInstanceViewModelTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: in namespace `Microsoft.Azure.Azclips.Extensions.Compute.Models`:
  - `VirtualMachineInstanceView`: `int? PlatformUpdateDomain`, `int? PlatformFaultDomain`, `string? ComputerName`, `string? OsName`, `string? OsVersion`, `string? HyperVGeneration`, `string? RdpThumbPrint`, `VirtualMachineAgentInstanceView? VmAgent`, `MaintenanceRedeployStatus? MaintenanceRedeployStatus`, `List<DiskInstanceView>? Disks`, `List<VirtualMachineExtensionInstanceView>? Extensions`, `VirtualMachineHealthStatus? VmHealth`, `BootDiagnosticsInstanceView? BootDiagnostics`, `string? AssignedHost`, `List<InstanceViewStatus> Statuses` (default `[]`), `VirtualMachinePatchStatus? PatchStatus`, `bool? IsVMInStandbyPool`
  - `InstanceViewStatus`: `Code`, `Level`, `DisplayStatus`, `Message`, `Time` (all `string?`)
  - `VirtualMachineAgentInstanceView`, `VirtualMachineExtensionHandlerInstanceView`, `DiskInstanceView`, `DiskEncryptionSettings`, `KeyVaultSecretReference`, `KeyVaultKeyReference`, `SubResource`, `VirtualMachineExtensionInstanceView`, `VirtualMachineHealthStatus`, `BootDiagnosticsInstanceView`, `VirtualMachinePatchStatus`, `AvailablePatchSummary`, `LastPatchInstallationSummary`, `ApiError`, `ApiErrorBase`, `InnerError`, with the members shown in Step 3
  - `MaintenanceRedeployStatus`: unchanged members

- [ ] **Step 1: Write the failing tests**

Create `tests/Extensions.Compute.Tests/VirtualMachineInstanceViewModelTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Extensions.Compute.Tests;

/// <summary>
/// Pins the hand-written instance-view models to api-version 2025-11-01: every property the spec
/// defines must deserialize, and must serialize back under ARM's own name.
/// </summary>
public class VirtualMachineInstanceViewModelTests
{
    // The options ComputeProcessorBase.Init builds for every Compute processor.
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Response body of the 2025-11-01 example VirtualMachine_Get_InstanceView.json,
    // Azure/azure-rest-api-specs@6a073b33fb652ffa7847af8e855a417d4f5b2b1a.
    private const string SpecExample = """
        {
          "platformUpdateDomain": 1,
          "platformFaultDomain": 1,
          "computerName": "myVM",
          "osName": "Windows Server 2016 Datacenter",
          "osVersion": "Microsoft Windows NT 10.0.14393.0",
          "vmAgent": {
            "vmAgentVersion": "2.7.41491.949",
            "statuses": [
              {
                "code": "ProvisioningState/succeeded",
                "level": "Info",
                "displayStatus": "Ready",
                "message": "GuestAgent is running and accepting new configurations.",
                "time": "2019-10-14T23:11:22+00:00"
              }
            ],
            "extensionHandlers": [
              {
                "type": "Microsoft.Azure.Security.IaaSAntimalware",
                "typeHandlerVersion": "1.5.5.9",
                "status": {
                  "code": "ProvisioningState/succeeded",
                  "level": "Info",
                  "displayStatus": "Ready"
                }
              }
            ]
          },
          "disks": [
            {
              "name": "myOsDisk",
              "statuses": [
                {
                  "code": "ProvisioningState/succeeded",
                  "level": "Info",
                  "displayStatus": "Provisioning succeeded",
                  "time": "2019-10-14T21:29:47.477089+00:00"
                }
              ]
            },
            {
              "name": "myDataDisk0",
              "statuses": [
                {
                  "code": "ProvisioningState/succeeded",
                  "level": "Info",
                  "displayStatus": "Provisioning succeeded",
                  "time": "2019-10-14T21:29:47.461517+00:00"
                }
              ]
            }
          ],
          "bootDiagnostics": {
            "consoleScreenshotBlobUri": "https://{myStorageAccount}.blob.core.windows.net/bootdiagnostics-myOsDisk/myOsDisk.screenshot.bmp",
            "serialConsoleLogBlobUri": "https://{myStorageAccount}.blob.core.windows.net/bootdiagnostics-myOsDisk/myOsDisk.serialconsole.log"
          },
          "extensions": [
            {
              "name": "IaaSAntiMalware-ext0",
              "type": "Microsoft.Azure.Security.IaaSAntimalware",
              "typeHandlerVersion": "1.5.5.9",
              "statuses": [
                {
                  "code": "ProvisioningState/succeeded",
                  "level": "Info",
                  "displayStatus": "Provisioning succeeded",
                  "message": "Microsoft Antimalware enabled"
                }
              ]
            }
          ],
          "hyperVGeneration": "V1",
          "patchStatus": {
            "availablePatchSummary": {
              "status": "Succeeded",
              "assessmentActivityId": "68f8b292-dfc2-4646-9781-33cc88631968",
              "rebootPending": true,
              "criticalAndSecurityPatchCount": 1,
              "otherPatchCount": 2,
              "startTime": "2020-04-24T21:02:04.2556154Z",
              "lastModifiedTime": "2020-04-24T21:02:04.2556154Z",
              "error": null
            },
            "lastPatchInstallationSummary": {
              "status": "Succeeded",
              "installationActivityId": "68f8b292-dfc2-4646-9981-33cc88631968",
              "maintenanceWindowExceeded": false,
              "notSelectedPatchCount": 1,
              "excludedPatchCount": 1,
              "pendingPatchCount": 1,
              "installedPatchCount": 1,
              "failedPatchCount": 1,
              "startTime": "2020-04-24T21:02:04.2556154Z",
              "lastModifiedTime": "2020-04-24T21:02:04.2556154Z",
              "error": null
            },
            "configurationStatuses": [
              {
                "code": "PatchModeConfigurationState/Ready",
                "level": "Info",
                "displayStatus": "Status_PatchModeConfigurationState_Ready",
                "time": "2020-04-24T21:02:04.2556154Z"
              },
              {
                "code": "AssessmentModeConfigurationState/Pending",
                "level": "Info",
                "displayStatus": "Status_AssessmentModeConfigurationState_Pending",
                "time": "2020-04-24T21:02:04.2556154Z"
              }
            ]
          },
          "isVMInStandbyPool": false,
          "statuses": [
            {
              "code": "ProvisioningState/succeeded",
              "level": "Info",
              "displayStatus": "Provisioning succeeded",
              "time": "2019-10-14T21:30:12.8051917+00:00"
            },
            {
              "code": "PowerState/running",
              "level": "Info",
              "displayStatus": "VM running"
            }
          ]
        }
        """;

    // The four properties the spec example leaves out, plus disk encryption, storage alignment and a
    // patch error, built from the 2025-11-01 schema.
    private const string RemainingProperties = """
        {
          "rdpThumbPrint": "0123456789ABCDEF0123456789ABCDEF01234567",
          "assignedHost": "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.Compute/hostGroups/myHostGroup/hosts/myHost",
          "vmHealth": {
            "status": { "code": "HealthState/healthy", "level": "Info", "displayStatus": "The VM is healthy" }
          },
          "maintenanceRedeployStatus": {
            "isCustomerInitiatedMaintenanceAllowed": true,
            "lastOperationResultCode": "MaintenanceCompleted",
            "lastOperationMessage": "Maintenance completed."
          },
          "disks": [
            {
              "name": "myOsDisk",
              "storageAlignmentStatus": "Aligned",
              "encryptionSettings": [
                {
                  "enabled": true,
                  "diskEncryptionKey": {
                    "secretUrl": "https://myvault.vault.azure.net/secrets/mysecret",
                    "sourceVault": { "id": "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.KeyVault/vaults/myvault" }
                  },
                  "keyEncryptionKey": {
                    "keyUrl": "https://myvault.vault.azure.net/keys/mykey",
                    "sourceVault": { "id": "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.KeyVault/vaults/myvault" }
                  }
                }
              ]
            }
          ],
          "patchStatus": {
            "availablePatchSummary": {
              "status": "Failed",
              "error": {
                "code": "PatchAssessmentFailed",
                "target": "availablePatchSummary",
                "message": "Patch assessment failed.",
                "details": [
                  { "code": "AgentTimeout", "target": "vmAgent", "message": "The VM agent did not respond." }
                ],
                "innererror": { "exceptiontype": "System.TimeoutException", "errordetail": "Timed out after 30 seconds." }
              }
            }
          },
          "statuses": []
        }
        """;

    [Fact]
    public void Deserialize_SpecExample_PopulatesEveryReturnedProperty()
    {
        var view = Deserialize(SpecExample);

        Assert.Equal(1, view.PlatformUpdateDomain);
        Assert.Equal(1, view.PlatformFaultDomain);
        Assert.Equal("myVM", view.ComputerName);
        Assert.Equal("Windows Server 2016 Datacenter", view.OsName);
        Assert.Equal("Microsoft Windows NT 10.0.14393.0", view.OsVersion);
        Assert.Equal("V1", view.HyperVGeneration);
        Assert.False(view.IsVMInStandbyPool);

        Assert.NotNull(view.VmAgent);
        Assert.Equal("2.7.41491.949", view.VmAgent.VmAgentVersion);
        Assert.NotNull(view.VmAgent.Statuses);
        var agentStatus = Assert.Single(view.VmAgent.Statuses);
        Assert.Equal("Info", agentStatus.Level);
        Assert.Equal("GuestAgent is running and accepting new configurations.", agentStatus.Message);
        Assert.Equal("2019-10-14T23:11:22+00:00", agentStatus.Time);
        Assert.NotNull(view.VmAgent.ExtensionHandlers);
        var handler = Assert.Single(view.VmAgent.ExtensionHandlers);
        Assert.Equal("Microsoft.Azure.Security.IaaSAntimalware", handler.Type);
        Assert.Equal("1.5.5.9", handler.TypeHandlerVersion);
        Assert.NotNull(handler.Status);
        Assert.Equal("Ready", handler.Status.DisplayStatus);

        Assert.NotNull(view.Disks);
        Assert.Equal(new[] { "myOsDisk", "myDataDisk0" }, view.Disks.Select(disk => disk.Name));
        Assert.NotNull(view.Disks[0].Statuses);
        Assert.Equal("ProvisioningState/succeeded", Assert.Single(view.Disks[0].Statuses!).Code);

        Assert.NotNull(view.BootDiagnostics);
        Assert.EndsWith("myOsDisk.screenshot.bmp", view.BootDiagnostics.ConsoleScreenshotBlobUri);
        Assert.EndsWith("myOsDisk.serialconsole.log", view.BootDiagnostics.SerialConsoleLogBlobUri);

        Assert.NotNull(view.Extensions);
        var extension = Assert.Single(view.Extensions);
        Assert.Equal("IaaSAntiMalware-ext0", extension.Name);
        Assert.Equal("Microsoft.Azure.Security.IaaSAntimalware", extension.Type);
        Assert.Equal("1.5.5.9", extension.TypeHandlerVersion);
        Assert.NotNull(extension.Statuses);
        Assert.Equal("Microsoft Antimalware enabled", Assert.Single(extension.Statuses).Message);

        Assert.NotNull(view.PatchStatus);
        var available = view.PatchStatus.AvailablePatchSummary;
        Assert.NotNull(available);
        Assert.Equal("Succeeded", available.Status);
        Assert.Equal("68f8b292-dfc2-4646-9781-33cc88631968", available.AssessmentActivityId);
        Assert.True(available.RebootPending);
        Assert.Equal(1, available.CriticalAndSecurityPatchCount);
        Assert.Equal(2, available.OtherPatchCount);
        Assert.Equal("2020-04-24T21:02:04.2556154Z", available.StartTime);
        Assert.Equal("2020-04-24T21:02:04.2556154Z", available.LastModifiedTime);
        Assert.Null(available.Error);

        var installation = view.PatchStatus.LastPatchInstallationSummary;
        Assert.NotNull(installation);
        Assert.Equal("Succeeded", installation.Status);
        Assert.Equal("68f8b292-dfc2-4646-9981-33cc88631968", installation.InstallationActivityId);
        Assert.False(installation.MaintenanceWindowExceeded);
        Assert.Equal(1, installation.NotSelectedPatchCount);
        Assert.Equal(1, installation.ExcludedPatchCount);
        Assert.Equal(1, installation.PendingPatchCount);
        Assert.Equal(1, installation.InstalledPatchCount);
        Assert.Equal(1, installation.FailedPatchCount);
        Assert.Equal("2020-04-24T21:02:04.2556154Z", installation.StartTime);
        Assert.Equal("2020-04-24T21:02:04.2556154Z", installation.LastModifiedTime);

        Assert.NotNull(view.PatchStatus.ConfigurationStatuses);
        Assert.Equal(
            new[] { "PatchModeConfigurationState/Ready", "AssessmentModeConfigurationState/Pending" },
            view.PatchStatus.ConfigurationStatuses.Select(status => status.Code));

        Assert.Equal(new[] { "ProvisioningState/succeeded", "PowerState/running" }, view.Statuses.Select(status => status.Code));
        Assert.Equal("Info", view.Statuses[1].Level);
        Assert.Equal("VM running", view.Statuses[1].DisplayStatus);
        Assert.Equal("2019-10-14T21:30:12.8051917+00:00", view.Statuses[0].Time);
    }

    [Fact]
    public void Deserialize_PropertiesOutsideTheSpecExample_ArePopulated()
    {
        var view = Deserialize(RemainingProperties);

        Assert.Equal("0123456789ABCDEF0123456789ABCDEF01234567", view.RdpThumbPrint);
        Assert.EndsWith("/hostGroups/myHostGroup/hosts/myHost", view.AssignedHost);

        Assert.NotNull(view.VmHealth);
        Assert.NotNull(view.VmHealth.Status);
        Assert.Equal("HealthState/healthy", view.VmHealth.Status.Code);

        Assert.NotNull(view.MaintenanceRedeployStatus);
        Assert.True(view.MaintenanceRedeployStatus.IsCustomerInitiatedMaintenanceAllowed);
        Assert.Equal("MaintenanceCompleted", view.MaintenanceRedeployStatus.LastOperationResultCode);
        Assert.Equal("Maintenance completed.", view.MaintenanceRedeployStatus.LastOperationMessage);

        Assert.NotNull(view.Disks);
        var disk = Assert.Single(view.Disks);
        Assert.Equal("Aligned", disk.StorageAlignmentStatus);
        Assert.NotNull(disk.EncryptionSettings);
        var encryption = Assert.Single(disk.EncryptionSettings);
        Assert.True(encryption.Enabled);
        Assert.NotNull(encryption.DiskEncryptionKey);
        Assert.NotNull(encryption.DiskEncryptionKey.SourceVault);
        Assert.Equal("https://myvault.vault.azure.net/secrets/mysecret", encryption.DiskEncryptionKey.SecretUrl);
        Assert.EndsWith("/vaults/myvault", encryption.DiskEncryptionKey.SourceVault.Id);
        Assert.NotNull(encryption.KeyEncryptionKey);
        Assert.NotNull(encryption.KeyEncryptionKey.SourceVault);
        Assert.Equal("https://myvault.vault.azure.net/keys/mykey", encryption.KeyEncryptionKey.KeyUrl);
        Assert.EndsWith("/vaults/myvault", encryption.KeyEncryptionKey.SourceVault.Id);

        var error = view.PatchStatus?.AvailablePatchSummary?.Error;
        Assert.NotNull(error);
        Assert.Equal("PatchAssessmentFailed", error.Code);
        Assert.Equal("availablePatchSummary", error.Target);
        Assert.Equal("Patch assessment failed.", error.Message);
        Assert.NotNull(error.Details);
        var detail = Assert.Single(error.Details);
        Assert.Equal("AgentTimeout", detail.Code);
        Assert.Equal("vmAgent", detail.Target);
        Assert.Equal("The VM agent did not respond.", detail.Message);
        Assert.NotNull(error.Innererror);
        Assert.Equal("System.TimeoutException", error.Innererror.Exceptiontype);
        Assert.Equal("Timed out after 30 seconds.", error.Innererror.Errordetail);
    }

    [Fact]
    public void Serialize_UsesArmPropertyNamesAndOmitsNulls()
    {
        var view = new VirtualMachineInstanceView
        {
            RdpThumbPrint = "thumb",
            IsVMInStandbyPool = true,
            VmAgent = new VirtualMachineAgentInstanceView { VmAgentVersion = "2.7" },
            PatchStatus = new VirtualMachinePatchStatus
            {
                AvailablePatchSummary = new AvailablePatchSummary
                {
                    Error = new ApiError { Innererror = new InnerError { Exceptiontype = "System.TimeoutException" } },
                },
            },
        };

        string json = JsonSerializer.Serialize(view, s_options);

        Assert.Contains("\"rdpThumbPrint\":\"thumb\"", json);
        Assert.Contains("\"isVMInStandbyPool\":true", json);
        Assert.Contains("\"vmAgent\":{\"vmAgentVersion\":\"2.7\"}", json);
        Assert.Contains("\"innererror\":{\"exceptiontype\":\"System.TimeoutException\"}", json);
        Assert.DoesNotContain("assignedHost", json);
        Assert.DoesNotContain("vmHealth", json);
    }

    private static VirtualMachineInstanceView Deserialize(string json)
        => JsonSerializer.Deserialize<VirtualMachineInstanceView>(json, s_options)
            ?? throw new InvalidOperationException("The instance view deserialized to null.");
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests\Extensions.Compute.Tests\Extensions.Compute.Tests.csproj --filter FullyQualifiedName~VirtualMachineInstanceViewModelTests`

Expected: the build fails with `CS0246` (for example, `The type or namespace name 'VirtualMachineAgentInstanceView' could not be found`) and `CS1061` (for example, `'VirtualMachineInstanceView' does not contain a definition for 'PlatformUpdateDomain'`).

- [ ] **Step 3: Create the instance-view model file**

Create `src/Extensions/Compute/Models/VirtualMachineInstanceView.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Models;

/// <summary>
/// A virtual machine's run-time state, returned inside <c>$expand=instanceView</c> and modeled on
/// every property of api-version 2025-11-01. <c>az vm get-instance-view</c> returns it in place at
/// <c>properties.instanceView</c>; the detail switches (<c>--show-details</c>, <c>-Status</c>)
/// flatten what they need onto <see cref="VirtualMachine"/> and then drop it.
/// </summary>
public class VirtualMachineInstanceView
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PlatformUpdateDomain { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PlatformFaultDomain { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ComputerName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OsName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OsVersion { get; set; }

    /// <summary>V1 or V2.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HyperVGeneration { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RdpThumbPrint { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public VirtualMachineAgentInstanceView? VmAgent { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MaintenanceRedeployStatus? MaintenanceRedeployStatus { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DiskInstanceView>? Disks { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<VirtualMachineExtensionInstanceView>? Extensions { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public VirtualMachineHealthStatus? VmHealth { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BootDiagnosticsInstanceView? BootDiagnostics { get; set; }

    /// <summary>Resource id of the dedicated host the VM was placed on.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AssignedHost { get; set; }

    public List<InstanceViewStatus> Statuses { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public VirtualMachinePatchStatus? PatchStatus { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsVMInStandbyPool { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary>One status entry: provisioning, power, agent, disk, extension or patch state.</summary>
public class InstanceViewStatus
{
    public string? Code { get; set; }

    /// <summary>Info, Warning or Error.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Level { get; set; }

    public string? DisplayStatus { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Time { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary>
/// ARM's maintenance and redeploy status block. <c>Get-AzVM -Status</c> flattens it onto
/// <see cref="VirtualMachine"/>; <c>az vm get-instance-view</c> returns it in place.
/// </summary>
public class MaintenanceRedeployStatus
{
    public bool? IsCustomerInitiatedMaintenanceAllowed { get; set; }
    public string? PreMaintenanceWindowStartTime { get; set; }
    public string? PreMaintenanceWindowEndTime { get; set; }
    public string? MaintenanceWindowStartTime { get; set; }
    public string? MaintenanceWindowEndTime { get; set; }
    public string? LastOperationResultCode { get; set; }
    public string? LastOperationMessage { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class VirtualMachineAgentInstanceView
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VmAgentVersion { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<VirtualMachineExtensionHandlerInstanceView>? ExtensionHandlers { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<InstanceViewStatus>? Statuses { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class VirtualMachineExtensionHandlerInstanceView
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TypeHandlerVersion { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InstanceViewStatus? Status { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class DiskInstanceView
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DiskEncryptionSettings>? EncryptionSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<InstanceViewStatus>? Statuses { get; set; }

    /// <summary>Aligned or Unaligned.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StorageAlignmentStatus { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class DiskEncryptionSettings
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public KeyVaultSecretReference? DiskEncryptionKey { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public KeyVaultKeyReference? KeyEncryptionKey { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Enabled { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class KeyVaultSecretReference
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SecretUrl { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubResource? SourceVault { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class KeyVaultKeyReference
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? KeyUrl { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubResource? SourceVault { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary>A reference to another ARM resource by id.</summary>
public class SubResource
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class VirtualMachineExtensionInstanceView
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TypeHandlerVersion { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<InstanceViewStatus>? Substatuses { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<InstanceViewStatus>? Statuses { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class VirtualMachineHealthStatus
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InstanceViewStatus? Status { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class BootDiagnosticsInstanceView
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConsoleScreenshotBlobUri { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SerialConsoleLogBlobUri { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InstanceViewStatus? Status { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class VirtualMachinePatchStatus
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AvailablePatchSummary? AvailablePatchSummary { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LastPatchInstallationSummary? LastPatchInstallationSummary { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<InstanceViewStatus>? ConfigurationStatuses { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary>The latest patch assessment. <see cref="Status"/> is Unknown, InProgress, Failed, Succeeded or CompletedWithWarnings.</summary>
public class AvailablePatchSummary
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Status { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AssessmentActivityId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RebootPending { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CriticalAndSecurityPatchCount { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? OtherPatchCount { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StartTime { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastModifiedTime { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApiError? Error { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary>The latest patch installation. <see cref="Status"/> is Unknown, InProgress, Failed, Succeeded or CompletedWithWarnings.</summary>
public class LastPatchInstallationSummary
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Status { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InstallationActivityId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? MaintenanceWindowExceeded { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NotSelectedPatchCount { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ExcludedPatchCount { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PendingPatchCount { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? InstalledPatchCount { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FailedPatchCount { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StartTime { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastModifiedTime { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApiError? Error { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary>ARM's common <c>ApiError</c>. <see cref="Innererror"/> keeps ARM's own spelling, <c>innererror</c>.</summary>
public class ApiError
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ApiErrorBase>? Details { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InnerError? Innererror { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Target { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class ApiErrorBase
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Target { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary>ARM's inner error. The property names keep ARM's lowercase spelling, <c>exceptiontype</c> and <c>errordetail</c>.</summary>
public class InnerError
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Exceptiontype { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Errordetail { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}
```

- [ ] **Step 4: Remove the moved types from `VirtualMachine.cs` and update its comment**

In `src/Extensions/Compute/Models/VirtualMachine.cs`, delete everything from `public class VirtualMachineInstanceView` to the end of the file. That is these three classes:

```csharp
public class VirtualMachineInstanceView
{
    public List<InstanceViewStatus> Statuses { get; set; } = [];

    // Returned inside $expand=instanceView. Flattened onto VirtualMachine by -Status, then dropped.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OsName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OsVersion { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HyperVGeneration { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MaintenanceRedeployStatus? MaintenanceRedeployStatus { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary>
/// ARM's maintenance and redeploy status block, returned inside <c>$expand=instanceView</c>.
/// Surfaced by <c>Get-AzVM -Status</c> only.
/// </summary>
public class MaintenanceRedeployStatus
{
    public bool? IsCustomerInitiatedMaintenanceAllowed { get; set; }
    public string? PreMaintenanceWindowStartTime { get; set; }
    public string? PreMaintenanceWindowEndTime { get; set; }
    public string? MaintenanceWindowStartTime { get; set; }
    public string? MaintenanceWindowEndTime { get; set; }
    public string? LastOperationResultCode { get; set; }
    public string? LastOperationMessage { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class InstanceViewStatus
{
    public string? Code { get; set; }
    public string? DisplayStatus { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}
```

The file should now end with the closing brace of `BootDiagnostics`.

In the same file, in `VirtualMachineProperties`, replace:

```csharp
    // Returned only with $expand=instanceView; cleared once --show-details has read the power state.
```

with:

```csharp
    // Returned only with $expand=instanceView. vm get-instance-view returns it intact; the detail
    // switches (--show-details, -Status) flatten what they need onto VirtualMachine, then clear it.
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests\Extensions.Compute.Tests\Extensions.Compute.Tests.csproj --filter FullyQualifiedName~VirtualMachineInstanceViewModelTests`

Expected: PASS, 3 tests.

- [ ] **Step 6: Check nothing else regressed**

Run: `dotnet test tests\Extensions.Compute.Tests\Extensions.Compute.Tests.csproj`

Expected: PASS for every test in the project.

Run: `dotnet test tests\Scenario.Tests --filter FullyQualifiedName~VmLifecycleScenarioTests`

Expected: PASS. The committed recording still replays, because the models only gained members.

- [ ] **Step 7: Commit**

```powershell
git add src/Extensions/Compute/Models/VirtualMachineInstanceView.cs src/Extensions/Compute/Models/VirtualMachine.cs tests/Extensions.Compute.Tests/VirtualMachineInstanceViewModelTests.cs
git commit -m '[Compute] Model the full VM instance view for api-version 2025-11-01'
```

---

### Task 2: The `vm get-instance-view` command

**Files:**
- Create: `src/Extensions/Compute/Customization/GetVMInstanceViewProcessor_Orchestrator.cs`
- Modify: `src/Extensions/Compute/ExtensionIndex.cs` (insert before `s_listVM_CLI`; add an entry to `s_cliTable`)
- Create: `src/CLI/Help/Docs/vm/get-instance-view.md`
- Regenerate: `tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json`
- Test: `tests/Scenario.Tests/Compute/VmGetInstanceViewOrchestrationTests.cs`

**Interfaces:**
- Consumes (from Task 1): `VirtualMachineInstanceView` with `Statuses`, `HyperVGeneration`, `VmAgent`, `Disks`; `InstanceViewStatus.Level` and `Time`; `VirtualMachineAgentInstanceView.VmAgentVersion`; `DiskInstanceView.Name`.
- Consumes (existing): `GetVMProcessor` (`SubscriptionId`, `ResourceGroupName`, `Name`, `string? Expand`), `VirtualMachineDetailEnricher.ResolveExpand(bool instanceView, bool userData)`, `VirtualMachineIdResolver.ParseVirtualMachineResourceId(string id)`, which returns `(string SubscriptionId, string ResourceGroupName, string Name)`, and `AzclipsErrorException.InvalidInput(string message)`, which returns `InvalidInputException`.
- Produces: `public class GetVMInstanceViewProcessor_Orchestrator : GetVMProcessor` with `public string[]? Ids { get; set; }`, in namespace `Microsoft.Azure.Azclips.Extensions.Compute.Processors`. The CLI command key is `vm get-instance-view`.

- [ ] **Step 1: Write the failing orchestration tests**

Create `tests/Scenario.Tests/Compute/VmGetInstanceViewOrchestrationTests.cs`:

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Drives <c>vm get-instance-view</c> through the real dispatcher, binder and ARM request pipeline
/// against a fake ARM, so the orchestration is pinned without a live recording: the expand it
/// sends, the shape it writes, and where it fails.
/// </summary>
[Collection("Scenario Tests")]
public class VmGetInstanceViewOrchestrationTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-vm-rg";
    private const string VmName = "scenario-vm";
    private const string SecondVmName = "scenario-vm-2";

    [Fact]
    public void ByNameAndResourceGroup_WritesTheVmWithItsInstanceViewIntact()
    {
        var (outputs, record) = Run("vm", "get-instance-view", "--subscription-id", SubscriptionId, "-g", ResourceGroupName, "-n", VmName);

        AssertInstanceView(Assert.IsType<VirtualMachine>(Assert.Single(outputs)), VmName);
        var entry = Assert.Single(record.Entries);
        Assert.Equal("GET", entry.Request.Method);
        Assert.EndsWith(
            $"/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{VmName}?api-version=2025-11-01&$expand=instanceView",
            entry.Request.Uri);
    }

    [Fact]
    public void Ids_WritesOneVmPerIdInInputOrder()
    {
        var (outputs, record) = Run("vm", "get-instance-view", "--ids", VmId(VmName), VmId(SecondVmName));

        var vms = Assert.IsType<VirtualMachine[]>(Assert.Single(outputs));
        Assert.Equal(new[] { VmName, SecondVmName }, vms.Select(vm => vm.Name));
        Assert.All(vms, vm => AssertInstanceView(vm, vm.Name));
        Assert.Equal(2, record.Entries.Count);
        Assert.All(record.Entries, entry => Assert.EndsWith("?api-version=2025-11-01&$expand=instanceView", entry.Request.Uri));
    }

    [Fact]
    public void MissingResourceGroup_FailsBeforeAnyRequest()
    {
        var record = new SessionRecord();
        using var host = Host(record);

        var error = Assert.Throws<InvalidInputException>(
            () => host.RunCli("vm", "get-instance-view", "--subscription-id", SubscriptionId, "-n", VmName));

        Assert.Equal("Specify the virtual machine by both name and resource group, or address it by resource id.", error.Message);
        Assert.Empty(record.Entries);
    }

    [Fact]
    public void MalformedId_FailsBeforeAnyRequest()
    {
        var record = new SessionRecord();
        using var host = Host(record);
        var nicId = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkInterfaces/scenario-vm-nic";

        var error = Assert.Throws<InvalidInputException>(
            () => host.RunCli("vm", "get-instance-view", "--ids", VmId(VmName), nicId));

        Assert.StartsWith($"Invalid virtual machine resource id '{nicId}'.", error.Message);
        Assert.Empty(record.Entries);
    }

    [Fact]
    public void UnknownVm_FailsWithArmNotFound()
    {
        var record = new SessionRecord();
        using var host = Host(record);

        var error = Assert.Throws<HttpRequestException>(
            () => host.RunCli("vm", "get-instance-view", "--subscription-id", SubscriptionId, "-g", ResourceGroupName, "-n", "missing-vm"));

        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
    }

    /// <summary>
    /// The fake's instance view arrived whole, and get-instance-view set none of the fields the
    /// detail switches flatten onto the VM.
    /// </summary>
    private static void AssertInstanceView(VirtualMachine vm, string expectedName)
    {
        Assert.Equal(expectedName, vm.Name);
        Assert.Equal(VmId(expectedName), vm.Id);

        var view = Assert.IsType<VirtualMachineInstanceView>(vm.Properties.InstanceView);
        Assert.Equal(new[] { "ProvisioningState/succeeded", "PowerState/running" }, view.Statuses.Select(status => status.Code));
        Assert.Equal("Info", view.Statuses[0].Level);
        Assert.Equal("2026-11-05T00:00:00+00:00", view.Statuses[0].Time);
        Assert.Equal("V2", view.HyperVGeneration);
        Assert.NotNull(view.VmAgent);
        Assert.Equal("2.7.41491.1102", view.VmAgent.VmAgentVersion);
        Assert.NotNull(view.Disks);
        Assert.Equal($"{expectedName}_OsDisk", Assert.Single(view.Disks).Name);

        Assert.Null(vm.PowerState);
        Assert.Null(vm.PublicIps);
        Assert.Null(vm.Fqdns);
        Assert.Null(vm.PrivateIps);
        Assert.Null(vm.MacAddresses);
        Assert.Null(vm.OsName);
        Assert.Null(vm.OsVersion);
        Assert.Null(vm.HyperVGeneration);
        Assert.Null(vm.MaintenanceRedeployStatus);
    }

    private static (IReadOnlyList<object> Outputs, SessionRecord Record) Run(params string[] args)
    {
        var record = new SessionRecord();
        using var host = Host(record);
        return (host.RunCli(args), record);
    }

    private static ScenarioHost Host(SessionRecord record) =>
        new(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Record, record), new InstanceViewArm(), extensions: [ExtensionUnderTest.Compute]);

    private static string VmId(string name) =>
        $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{name}";

    /// <summary>
    /// Stands in for ARM: a GET of either known VM answers with the VM, carrying its instance view only
    /// when the request asks for <c>$expand=instanceView</c>, as ARM does. Any other VM answers 404.
    /// </summary>
    private sealed class InstanceViewArm : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method != HttpMethod.Get
                || !path.Contains("/providers/Microsoft.Compute/virtualMachines/", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Fake ARM got an unexpected request: {request.Method} {request.RequestUri}");
            }

            var name = path.TrimEnd('/').Split('/')[^1];
            if (name != VmName && name != SecondVmName)
            {
                return Task.FromResult(Json(new
                {
                    error = new
                    {
                        code = "ResourceNotFound",
                        message = $"The Resource 'Microsoft.Compute/virtualMachines/{name}' under resource group '{ResourceGroupName}' was not found.",
                    },
                }, HttpStatusCode.NotFound));
            }

            var expanded = Uri.UnescapeDataString(request.RequestUri.Query).Contains("$expand=instanceView", StringComparison.OrdinalIgnoreCase);
            return Task.FromResult(Json(new
            {
                id = VmId(name),
                name,
                location = "westus2",
                properties = new
                {
                    provisioningState = "Succeeded",
                    hardwareProfile = new { vmSize = "Standard_D2s_v3" },
                    instanceView = expanded ? InstanceView(name) : null,
                },
            }, HttpStatusCode.OK));
        }

        private static object InstanceView(string name) => new
        {
            computerName = name,
            hyperVGeneration = "V2",
            vmAgent = new
            {
                vmAgentVersion = "2.7.41491.1102",
                statuses = new[] { new { code = "ProvisioningState/succeeded", level = "Info", displayStatus = "Ready" } },
            },
            disks = new[]
            {
                new
                {
                    name = $"{name}_OsDisk",
                    statuses = new[] { new { code = "ProvisioningState/succeeded", level = "Info", displayStatus = "Provisioning succeeded" } },
                },
            },
            statuses = new object[]
            {
                new { code = "ProvisioningState/succeeded", level = "Info", displayStatus = "Provisioning succeeded", time = "2026-11-05T00:00:00+00:00" },
                new { code = "PowerState/running", level = "Info", displayStatus = "VM running" },
            },
        };

        private static HttpResponseMessage Json(object body, HttpStatusCode status) => new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests\Scenario.Tests --filter FullyQualifiedName~VmGetInstanceViewOrchestrationTests`

Expected: all 5 tests FAIL. `vm get-instance-view` is not in the Compute command table yet, so every `RunCli` call throws before reaching its assertions. The three tests that expect `InvalidInputException` or `HttpRequestException` report a different exception type.

- [ ] **Step 3: Create the orchestrator**

Create `src/Extensions/Compute/Customization/GetVMInstanceViewProcessor_Orchestrator.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors
{
    /// <summary>
    /// Read orchestrator behind <c>az vm get-instance-view</c>. Every request asks ARM for the
    /// instance view, and the VM is written with <c>properties.instanceView</c> intact: nothing is
    /// flattened onto the model and nothing is cleared, unlike the detail switches of
    /// <c>vm show</c> and <c>vm list</c>. Addressed by name and resource group, or by one or more
    /// resource ids, one GET per id, written as one array.
    /// </summary>
    public class GetVMInstanceViewProcessor_Orchestrator : GetVMProcessor
    {
        public string[]? Ids { get; set; }

        public override async Task ProcessAsync()
        {
            Expand = VirtualMachineDetailEnricher.ResolveExpand(instanceView: true, userData: false);

            if (Ids is null || Ids.Length == 0)
            {
                EnsureAddressable();
                await base.ProcessAsync();
                return;
            }

            // Parse every id before the first request, so one malformed id fails the command
            // without reading the others.
            var vmIds = Ids.Select(VirtualMachineIdResolver.ParseVirtualMachineResourceId).ToList();

            try
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);

                var results = new List<VirtualMachine>();
                foreach (var vmId in vmIds)
                {
                    using HttpResponseMessage response = await CreateExtendedProcessor<GetVMProcessor>(p =>
                    {
                        p.SubscriptionId = vmId.SubscriptionId;
                        p.ResourceGroupName = vmId.ResourceGroupName;
                        p.Name = vmId.Name;
                        p.Expand = Expand;
                    }).SendAsync();

                    if (await response.Content.ReadFromJsonAsync<VirtualMachine>(JsonOptions) is { } vm)
                    {
                        results.Add(vm);
                    }
                }

                await WriteOutput(results.ToArray());
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        /// <summary>
        /// ParameterMetadata cannot express "required unless --ids", so without this guard the
        /// binder accepts an empty name and ARM answers a confusing 404 for a malformed URL.
        /// </summary>
        private void EnsureAddressable()
        {
            if (string.IsNullOrWhiteSpace(ResourceGroupName) || string.IsNullOrWhiteSpace(Name))
            {
                throw AzclipsErrorException.InvalidInput(
                    "Specify the virtual machine by both name and resource group, or address it by resource id.");
            }
        }
    }
}
```

- [ ] **Step 4: Register the CLI command**

In `src/Extensions/Compute/ExtensionIndex.cs`, insert this block immediately before the line `        private static readonly CommandMetadata s_listVM_CLI = new(`:

```csharp
        private static readonly CommandMetadata s_getVMInstanceView_CLI = new(
            Name: "GetVMInstanceViewProcessor_Orchestrator",
            InvocationName: "vm get-instance-view",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.GetVMInstanceViewProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.GetVMInstanceViewProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["subscription-id"] = new("SubscriptionId", "subscription-id", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["name"]            = new("Name", "name", typeof(string), "The name of the virtual machine. Provide either --ids or --name and --resource-group.", IsRequired: false, Aliases: ["n"], AllowedValues: []),
                ["resource-group"]  = new("ResourceGroupName", "resource-group", typeof(string), "The resource group name. Provide either --ids or --name and --resource-group.", IsRequired: false, Aliases: ["g"], AllowedValues: []),
                ["ids"]             = new("Ids", "ids", typeof(string[]), "One or more virtual machine resource IDs. Provide either --ids or --name and --resource-group.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>
            {
                ["n"] = "name",
                ["g"] = "resource-group",
            }
        );
```

In the same file, in `s_cliTable`, replace:

```csharp
            ["vm show"]     = s_getVM_CLI,
```

with:

```csharp
            ["vm show"]     = s_getVM_CLI,
            ["vm get-instance-view"] = s_getVMInstanceView_CLI,
```

Do not touch `s_psTable`.

- [ ] **Step 5: Run the orchestration tests to verify they pass**

Run: `dotnet test tests\Scenario.Tests --filter FullyQualifiedName~VmGetInstanceViewOrchestrationTests`

Expected: PASS, 5 tests.

- [ ] **Step 6: Confirm the CLI export snapshot now drifts**

Run: `dotnet build azclips.slnx`

Expected: build succeeds.

Run: `dotnet test tools\Help\Azclips.Help.Tool.Tests --filter FullyQualifiedName~CliExportFixturesTests`

Expected: `ExporterOutput_MatchesFixture(verb: "vm")` FAILS with `CLI export JSON drift on 'vm.json'`, because the exported `vm` surface now includes `vm get-instance-view`. The `_root_` and `login` cases pass.

- [ ] **Step 7: Write the CLI help**

Create `src/CLI/Help/Docs/vm/get-instance-view.md`:

````markdown
---
summary: Get instance information about a virtual machine.
status: ga
schema: 1.0.0
online version:
---

Description: Retrieve a virtual machine together with its run-time instance view, returned at `properties.instanceView`: the provisioning and power states, the VM agent, disks, extensions, boot diagnostics, and patch status. The command makes one request, so it is a quick way to check whether a VM is running, stopped, or deallocated.

### Get the instance view of a virtual machine

```bash
az vm get-instance-view --name myvm --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000
```

### Use the short option names

```bash
az vm get-instance-view -n myvm -g myrg
```

### Get the instance views of several virtual machines by resource ID

```bash
az vm get-instance-view --ids /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.Compute/virtualMachines/myvm /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.Compute/virtualMachines/myvm2
```

`--ids` replaces `--name`, `--resource-group` and `--subscription-id`, and accepts more than one id. It always returns an array, one entry per id.

### Show only the power state

```bash
az vm get-instance-view -n myvm -g myrg --query "properties.instanceView.statuses[?starts_with(code, 'PowerState/')].displayStatus"
```
````

- [ ] **Step 8: Run the help validators**

Run: `.\build\build.ps1 -GenerateHelpArtifacts`

Expected: the solution builds into `artifacts\Debug`, and the help tool reports no errors for `vm/get-instance-view.md` (no `MD_TodoPlaceholder`, `CLI_ExamplePlaceholder`, `CLI_L2_UnknownFlag` or `CLI_L3_InvalidValue`). The script exits with code 0.

- [ ] **Step 9: Refresh the CLI export fixture**

Run: `pwsh -File build/Update-CliExportFixtures.ps1`

Then run: `git --no-pager diff --stat -- tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export`

Expected: only `vm.json` changes. Open the diff and confirm it only adds a `vm get-instance-view` command entry, with the summary, description and examples from Step 7 and the parameters `--ids`, `--name`, `--profile`, `--resource-group` and `--subscription-id`, plus the global flags. If any other command's entry changed, stop and investigate before continuing.

- [ ] **Step 10: Verify the snapshot and that nothing regressed**

Run: `dotnet test tools\Help\Azclips.Help.Tool.Tests --filter FullyQualifiedName~CliExportFixturesTests`

Expected: PASS, 3 cases.

Run: `dotnet test tests\Scenario.Tests --filter "FullyQualifiedName~VmGetInstanceViewOrchestrationTests|FullyQualifiedName~VmCreateOrchestrationTests|FullyQualifiedName~VmCreateScenarioTests|FullyQualifiedName~VmLifecycleScenarioTests"`

Expected: PASS for every test.

- [ ] **Step 11: Commit**

```powershell
git add src/Extensions/Compute/Customization/GetVMInstanceViewProcessor_Orchestrator.cs src/Extensions/Compute/ExtensionIndex.cs src/CLI/Help/Docs/vm/get-instance-view.md tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/vm.json tests/Scenario.Tests/Compute/VmGetInstanceViewOrchestrationTests.cs
git commit -m '[Compute] `az vm get-instance-view`: Add command'
```

---

### Task 3: Instance-view checks across the VM lifecycle

**Files:**
- Modify: `tests/Scenario.Tests/Compute/VmLifecycleScenarioTests.cs`
- Re-record: `tests/Scenario.Tests/Compute/Records/VmLifecycleScenarioTests/CreateListShowStopDeallocateStartDeleteVirtualMachine.json`

**Interfaces:**
- Consumes (from Task 2): the CLI command `vm get-instance-view` with `--subscription-id`, `--name`/`-n`, `--resource-group`/`-g` and `--ids`. It writes one `VirtualMachine`, or a `VirtualMachine[]` for `--ids`.
- Consumes (from Task 1): `VirtualMachineInstanceView.Statuses`, `HyperVGeneration`, `VmAgent` and `Disks`; `InstanceViewStatus.Code` and `Level`.
- Produces: helpers inside the test class: `RunGetInstanceView(ScenarioHost host, string subscriptionId, string vmName, bool shortAliases = false, string? groupName = null)`, `RunGetInstanceViewByIds(ScenarioHost host, string vmId)`, `AssertInstanceView(IReadOnlyList<object> outputs, string expectedPowerState, string? expectedSubscriptionId = null, bool detailed = false)`, and `VmId(string subscriptionId, string vmName, string? groupName = null)`.

- [ ] **Step 1: Describe the new checks in the class summary**

In `tests/Scenario.Tests/Compute/VmLifecycleScenarioTests.cs`, replace:

```csharp
/// recording; playback replays the committed recording on both surfaces without credentials.
/// </summary>
```

with:

```csharp
/// recording; playback replays the committed recording on both surfaces without credentials.
/// On the CLI, <c>vm get-instance-view</c> follows show, stop, deallocate and start, and must report
/// the power state each one left behind.
/// </summary>
```

- [ ] **Step 2: Add the record-mode steps**

Replace:

```csharp
                AssertVm(RunShow(host, subscriptionId, VmName, groupName), VmName);
                AssertActionCompleted(RunStop(host, subscriptionId, VmName, groupName));
                AssertActionCompleted(RunDeallocate(host, subscriptionId, VmName, groupName));
                AssertActionCompleted(RunStart(host, subscriptionId, VmName, groupName));
```

with:

```csharp
                AssertVm(RunShow(host, subscriptionId, VmName, groupName), VmName);
                AssertInstanceView(RunGetInstanceView(host, subscriptionId, VmName, shortAliases: true, groupName: groupName), "PowerState/running", detailed: true);
                AssertActionCompleted(RunStop(host, subscriptionId, VmName, groupName));
                AssertInstanceView(RunGetInstanceView(host, subscriptionId, VmName, groupName: groupName), "PowerState/stopped");
                AssertActionCompleted(RunDeallocate(host, subscriptionId, VmName, groupName));
                AssertInstanceView(RunGetInstanceViewByIds(host, VmId(subscriptionId, VmName, groupName)), "PowerState/deallocated");
                AssertActionCompleted(RunStart(host, subscriptionId, VmName, groupName));
                AssertInstanceView(RunGetInstanceView(host, subscriptionId, VmName, groupName: groupName), "PowerState/running");
```

- [ ] **Step 3: Add the same steps to CLI playback**

Replace:

```csharp
            AssertVm(RunShow(cli, SubscriptionId, VmName), VmName, SubscriptionId);
            AssertActionCompleted(RunStop(cli, SubscriptionId, VmName));
            AssertActionCompleted(RunDeallocate(cli, SubscriptionId, VmName));
            AssertActionCompleted(RunStart(cli, SubscriptionId, VmName));
```

with:

```csharp
            AssertVm(RunShow(cli, SubscriptionId, VmName), VmName, SubscriptionId);
            AssertInstanceView(RunGetInstanceView(cli, SubscriptionId, VmName, shortAliases: true), "PowerState/running", SubscriptionId, detailed: true);
            AssertActionCompleted(RunStop(cli, SubscriptionId, VmName));
            AssertInstanceView(RunGetInstanceView(cli, SubscriptionId, VmName), "PowerState/stopped", SubscriptionId);
            AssertActionCompleted(RunDeallocate(cli, SubscriptionId, VmName));
            AssertInstanceView(RunGetInstanceViewByIds(cli, VmId(SubscriptionId, VmName)), "PowerState/deallocated", SubscriptionId);
            AssertActionCompleted(RunStart(cli, SubscriptionId, VmName));
            AssertInstanceView(RunGetInstanceView(cli, SubscriptionId, VmName), "PowerState/running", SubscriptionId);
```

- [ ] **Step 4: Explain why the PowerShell branch is unchanged**

Replace:

```csharp
        using (var ps = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor(), extensions: s_extensions))
        {
            ps.RunPowerShell("New-AzResourceGroup", PsGroupParameters(SubscriptionId), extension: "Resources");
```

with:

```csharp
        using (var ps = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor(), extensions: s_extensions))
        {
            // vm get-instance-view has no PowerShell counterpart yet. Playback fails only on a request it
            // cannot match, so the CLI's four instance-view reads go unused here. The first of them is
            // recorded after vm list -d, so New-AzVM and Get-AzVM_List -Status below still consume the
            // same $expand=instanceView entries, in the same order, as before.
            ps.RunPowerShell("New-AzResourceGroup", PsGroupParameters(SubscriptionId), extension: "Resources");
```

- [ ] **Step 5: Add the run helpers**

Insert this between the `RunShow` helper and the line `    private static IReadOnlyList<object> RunStart(ScenarioHost host, string subscriptionId, string vmName, string? groupName = null) =>`:

```csharp

    /// <summary>
    /// <c>vm get-instance-view</c> by name. <paramref name="shortAliases"/> spells the addressing as
    /// <c>-n</c>/<c>-g</c>, so one step also exercises the alias wiring.
    /// </summary>
    private static IReadOnlyList<object> RunGetInstanceView(ScenarioHost host, string subscriptionId, string vmName, bool shortAliases = false, string? groupName = null) =>
        host.RunCli("vm", "get-instance-view",
            "--subscription-id", subscriptionId,
            shortAliases ? "-n" : "--name", vmName,
            shortAliases ? "-g" : "--resource-group", groupName ?? ResourceGroupName);

    /// <summary><c>vm get-instance-view --ids</c>. The subscription comes from the id, so none is passed.</summary>
    private static IReadOnlyList<object> RunGetInstanceViewByIds(ScenarioHost host, string vmId) =>
        host.RunCli("vm", "get-instance-view", "--ids", vmId);
```

- [ ] **Step 6: Let `VmId` take the run-scoped group**

Replace:

```csharp
    private static string VmId(string subscriptionId, string vmName) =>
        $"/subscriptions/{subscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}";
```

with:

```csharp
    private static string VmId(string subscriptionId, string vmName, string? groupName = null) =>
        $"/subscriptions/{subscriptionId}/resourceGroups/{groupName ?? ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}";
```

- [ ] **Step 7: Add the assertion helper**

Insert this method and its comment immediately before the `/// <summary>` line that opens the `AssertActionCompleted` doc comment. The line after it reads `/// stop, deallocate, start, and delete are body-less actions. The long-running operation's final`.

```csharp
    /// <summary>
    /// <c>vm get-instance-view</c>: one virtual machine, or a one-element array for <c>--ids</c>, whose
    /// instance view holds exactly the provisioning status and the power state the previous step left.
    /// The flattened detail fields stay null, because get-instance-view returns the instance view in
    /// place. <paramref name="detailed"/> also checks the platform-reported hypervisor generation, the
    /// VM agent, and the single OS disk. Guest-agent values, disk names and timestamps change on every
    /// re-record, so they are never asserted.
    /// </summary>
    private static void AssertInstanceView(IReadOnlyList<object> outputs, string expectedPowerState, string? expectedSubscriptionId = null, bool detailed = false)
    {
        var output = Assert.Single(outputs);
        var vm = output is VirtualMachine[] byIds ? Assert.Single(byIds) : Assert.IsType<VirtualMachine>(output);
        Assert.Equal(VmName, vm.Name);
        if (expectedSubscriptionId is not null)
        {
            Assert.Equal(VmId(expectedSubscriptionId, VmName), vm.Id);
        }

        var view = Assert.IsType<VirtualMachineInstanceView>(vm.Properties.InstanceView);
        Assert.Equal(2, view.Statuses.Count);
        Assert.Equal("ProvisioningState/succeeded", view.Statuses[0].Code);
        Assert.Equal("Info", view.Statuses[0].Level);
        Assert.Equal(expectedPowerState, view.Statuses[1].Code);

        Assert.Null(vm.PowerState);
        Assert.Null(vm.PublicIps);
        Assert.Null(vm.Fqdns);
        Assert.Null(vm.PrivateIps);
        Assert.Null(vm.MacAddresses);
        Assert.Null(vm.OsName);
        Assert.Null(vm.OsVersion);
        Assert.Null(vm.HyperVGeneration);
        Assert.Null(vm.MaintenanceRedeployStatus);

        if (detailed)
        {
            Assert.False(string.IsNullOrEmpty(view.HyperVGeneration));
            Assert.NotNull(view.VmAgent);
            Assert.NotNull(view.Disks);
            var osDisk = Assert.Single(view.Disks);
            Assert.NotNull(osDisk.Statuses);
            Assert.Contains(osDisk.Statuses, status => status.Code == "ProvisioningState/succeeded");
        }
    }

```

- [ ] **Step 8: Run playback to confirm it needs a recording**

Run: `dotnet test tests\Scenario.Tests --filter FullyQualifiedName~VmLifecycleScenarioTests`

Expected: FAIL in the CLI playback branch at the first `vm get-instance-view`, with `InvalidOperationException: Playback found no recorded response (matching on ...) for:` followed by `GET https://management.azure.com/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/scenario-vm-lifecycle-rg/providers/Microsoft.Compute/virtualMachines/lifecycle-vm?api-version=2025-11-01&$expand=instanceView`. This is the expected recording-required state. A compile error or any other failure means Steps 1-7 need fixing.

- [ ] **Step 9: Stop for user review of the test code**

Show the user `git --no-pager diff -- tests/Scenario.Tests/Compute/VmLifecycleScenarioTests.cs`, along with the Step 8 result. Apply any changes they ask for and show the diff again, repeating until they explicitly approve. Do not record before that approval.

- [ ] **Step 10: Ask before recording**

Ask the user whether to record now, and state the live effects. Record mode runs against their signed-in subscription. It creates a run-scoped resource group `scenario-vm-lifecycle-rg-t<8 hex>` in `westus2`, with two `Standard_D2s_v3` VMs and their NICs, virtual networks, NSGs and Standard public IPs. It deletes the group when the run ends.

Prerequisites:
- Signed in with the azclips CLI, not classic Azure CLI.
- `Microsoft.Compute` and `Microsoft.Network` registered on the subscription.
- Quota for two small VMs and two Standard public IPs.

Only continue once the user says yes.

- [ ] **Step 11: Record**

```powershell
$env:AZURE_TEST_MODE = "Record"
dotnet test tests\Scenario.Tests --filter FullyQualifiedName~VmLifecycleScenarioTests
$env:AZURE_TEST_MODE = $null
```

Expected: PASS. `CreateListShowStopDeallocateStartDeleteVirtualMachine.json` is rewritten.

- [ ] **Step 12: Verify the recording and playback**

Run:

```powershell
$record = Get-Content tests\Scenario.Tests\Compute\Records\VmLifecycleScenarioTests\CreateListShowStopDeallocateStartDeleteVirtualMachine.json -Raw | ConvertFrom-Json
($record.Entries | Where-Object { $_.Request.Uri -like '*/virtualMachines/lifecycle-vm?api-version=2025-11-01&$expand=instanceView' }).Count
```

Expected: `6`. That is the read after `vm create`, the `vm list -d` fan-out, and the four `vm get-instance-view` calls.

Search the recording for the live subscription id and tenant id the user signed in with. Expect no matches: the sanitisers replace them with `00000000-0000-0000-0000-000000000000`.

Run: `dotnet test tests\Scenario.Tests --filter FullyQualifiedName~VmLifecycleScenarioTests`

Expected: PASS. Both the CLI and the PowerShell branches replay.

Run: `az group list --query "[?starts_with(name, 'scenario-vm-lifecycle-rg')].name"`

Expected: `[]`. If a group is left behind, delete it with `az group delete --name` followed by the listed name.

- [ ] **Step 13: Ask the user to review the recording, then commit**

Show `git --no-pager diff --stat` and point the user at the rewritten recording. Once they approve it:

```powershell
git add tests/Scenario.Tests/Compute/VmLifecycleScenarioTests.cs tests/Scenario.Tests/Compute/Records/VmLifecycleScenarioTests/CreateListShowStopDeallocateStartDeleteVirtualMachine.json
git commit -m '{Tests} `az vm get-instance-view`: Check the instance view across the VM lifecycle'
```

---

### Task 4: PowerShell `Get-AzVM -Status` for one VM

**Files:**
- Modify: `tests/Scenario.Tests/Scenario.Tests.csproj` (one `ProjectReference`)
- Modify: `tests/Scenario.Tests/Compute/VmGetInstanceViewOrchestrationTests.cs` (replace the whole file)
- Modify: `src/Extensions/Compute/Customization/GetVMInstanceViewProcessor_Orchestrator.cs` (summary, `IncludeUserData`, `Expand`)
- Modify: `src/Extensions/Compute/ExtensionIndex.cs` (two PowerShell entries, and their `s_psTable` lines)
- Modify: `src/PS/Compute/Cmdlets/GetVMCmdlet.cs` (parameter sets, `-Status` forwarding)
- Modify: `tests/Scenario.Tests/Compute/VmLifecycleScenarioTests.cs` (PowerShell branch and three comments)
- Modify: `src/Extensions/Compute/Models/VirtualMachineInstanceView.cs` and `src/Extensions/Compute/Models/VirtualMachine.cs` (comments only)
- Modify: `src/PS/Compute/help/Get-AzVM.md`

**Interfaces:**
- **Consumes, from Tasks 1-3:**
  - `GetVMInstanceViewProcessor_Orchestrator`, with `string[]? Ids`, plus `SubscriptionId`, `ResourceGroupName` and `Name` from `GetVMProcessor`.
  - `VirtualMachineDetailEnricher.ResolveExpand(bool instanceView, bool userData)`, which returns `"instanceView,userData"` when both are true.
  - The lifecycle helpers:
    - `AssertInstanceView(IReadOnlyList<object> outputs, string expectedPowerState, string? expectedSubscriptionId = null, bool detailed = false)`
    - `PsVmParameters(string subscriptionId, string vmName)`
    - `VmId(string subscriptionId, string vmName, string? groupName = null)`
  - `ScenarioHost.RunPowerShellCmdlet<TCmdlet>(IDictionary<string, object> parameters)`. It runs the real cmdlet in a runspace. `GetVMCmdlet` enumerates collections, so an orchestrator's `VirtualMachine[]` reaches PowerShell as one `VirtualMachine` per id.
- **Produces:**
  - `GetVMInstanceViewProcessor_Orchestrator.IncludeUserData` (`bool`)
  - the PowerShell command keys `Get-AzVM_GetStatus` and `Get-AzVM_IdStatus`
  - the `GetVMCmdlet` parameter sets `GetStatus` and `IdStatus`

- [ ] **Step 1: Let the scenario tests drive the real `GetVMCmdlet`**

In `tests/Scenario.Tests/Scenario.Tests.csproj`, replace:

```xml
    <ProjectReference Include="../../src/PS/Accounts/Cmdlets.Accounts.csproj" />
```

with:

```xml
    <ProjectReference Include="../../src/PS/Accounts/Cmdlets.Accounts.csproj" />
    <ProjectReference Include="../../src/PS/Compute/Cmdlets.Compute.csproj" />
```

- [ ] **Step 2: Write the failing PowerShell tests**

Replace the whole content of `tests/Scenario.Tests/Compute/VmGetInstanceViewOrchestrationTests.cs` with the code below. The five CLI tests and `AssertInstanceView` don't change. The new code adds:
- five PowerShell tests
- the `RunGetAzVM` helper
- a `UserDataValue` constant
- a fake that also answers the resource group's VM list, and echoes user data when it is expanded

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Cmdlets.Compute;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Drives <c>vm get-instance-view</c>, and <c>Get-AzVM</c> through the real cmdlet, through the real
/// dispatcher, binder and ARM request pipeline against a fake ARM. That pins the orchestration without a
/// live recording: the expand it sends, the shape it writes, where it fails, and which PowerShell parameter
/// set reaches it.
/// </summary>
[Collection("Scenario Tests")]
public class VmGetInstanceViewOrchestrationTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ResourceGroupName = "scenario-vm-rg";
    private const string VmName = "scenario-vm";
    private const string SecondVmName = "scenario-vm-2";
    private const string UserDataValue = "dXNlci1kYXRh";

    [Fact]
    public void ByNameAndResourceGroup_WritesTheVmWithItsInstanceViewIntact()
    {
        var (outputs, record) = Run("vm", "get-instance-view", "--subscription-id", SubscriptionId, "-g", ResourceGroupName, "-n", VmName);

        AssertInstanceView(Assert.IsType<VirtualMachine>(Assert.Single(outputs)), VmName);
        var entry = Assert.Single(record.Entries);
        Assert.Equal("GET", entry.Request.Method);
        Assert.EndsWith(
            $"/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{VmName}?api-version=2025-11-01&$expand=instanceView",
            entry.Request.Uri);
    }

    [Fact]
    public void Ids_WritesOneVmPerIdInInputOrder()
    {
        var (outputs, record) = Run("vm", "get-instance-view", "--ids", VmId(VmName), VmId(SecondVmName));

        var vms = Assert.IsType<VirtualMachine[]>(Assert.Single(outputs));
        Assert.Equal(new[] { VmName, SecondVmName }, vms.Select(vm => vm.Name));
        Assert.All(vms, vm => AssertInstanceView(vm, vm.Name));
        Assert.Equal(2, record.Entries.Count);
        Assert.All(record.Entries, entry => Assert.EndsWith("?api-version=2025-11-01&$expand=instanceView", entry.Request.Uri));
    }

    [Fact]
    public void MissingResourceGroup_FailsBeforeAnyRequest()
    {
        var record = new SessionRecord();
        using var host = Host(record);

        var error = Assert.Throws<InvalidInputException>(
            () => host.RunCli("vm", "get-instance-view", "--subscription-id", SubscriptionId, "-n", VmName));

        Assert.Equal("Specify the virtual machine by both name and resource group, or address it by resource id.", error.Message);
        Assert.Empty(record.Entries);
    }

    [Fact]
    public void MalformedId_FailsBeforeAnyRequest()
    {
        var record = new SessionRecord();
        using var host = Host(record);
        var nicId = $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkInterfaces/scenario-vm-nic";

        var error = Assert.Throws<InvalidInputException>(
            () => host.RunCli("vm", "get-instance-view", "--ids", VmId(VmName), nicId));

        Assert.StartsWith($"Invalid virtual machine resource id '{nicId}'.", error.Message);
        Assert.Empty(record.Entries);
    }

    [Fact]
    public void UnknownVm_FailsWithArmNotFound()
    {
        var record = new SessionRecord();
        using var host = Host(record);

        var error = Assert.Throws<HttpRequestException>(
            () => host.RunCli("vm", "get-instance-view", "--subscription-id", SubscriptionId, "-g", ResourceGroupName, "-n", "missing-vm"));

        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
    }

    [Fact]
    public void PowerShellStatus_ByNameAndResourceGroup_WritesTheVmWithItsInstanceViewIntact()
    {
        var (outputs, record) = RunGetAzVM(new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
            ["Name"] = VmName,
            ["Status"] = true,
        });

        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        AssertInstanceView(vm, VmName);
        Assert.Null(vm.Properties.UserData);
        Assert.EndsWith(
            $"/virtualMachines/{VmName}?api-version=2025-11-01&$expand=instanceView",
            Assert.Single(record.Entries).Request.Uri);
    }

    [Fact]
    public void PowerShellStatusWithUserData_ExpandsBothInOneRequest()
    {
        var (outputs, record) = RunGetAzVM(new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
            ["Name"] = VmName,
            ["Status"] = true,
            ["UserData"] = true,
        });

        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        AssertInstanceView(vm, VmName);
        Assert.Equal(UserDataValue, vm.Properties.UserData);
        Assert.EndsWith(
            "?api-version=2025-11-01&$expand=instanceView,userData",
            Uri.UnescapeDataString(Assert.Single(record.Entries).Request.Uri));
    }

    [Fact]
    public void PowerShellStatus_ById_WritesTheVmWithItsInstanceView()
    {
        var (outputs, record) = RunGetAzVM(new Dictionary<string, object>
        {
            ["Id"] = VmId(VmName),
            ["Status"] = true,
        });

        // The cmdlet enumerates the orchestrator's array, so PowerShell sees one VM per id.
        AssertInstanceView(Assert.IsType<VirtualMachine>(Assert.Single(outputs)), VmName);
        Assert.EndsWith(
            $"/virtualMachines/{VmName}?api-version=2025-11-01&$expand=instanceView",
            Assert.Single(record.Entries).Request.Uri);
    }

    [Fact]
    public void PowerShellWithoutStatus_ReadsThePlainVm()
    {
        var (outputs, record) = RunGetAzVM(new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
            ["Name"] = VmName,
        });

        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal(VmName, vm.Name);
        Assert.Null(vm.Properties.InstanceView);
        Assert.EndsWith($"/virtualMachines/{VmName}?api-version=2025-11-01", Assert.Single(record.Entries).Request.Uri);
    }

    [Fact]
    public void PowerShellListStatus_StillFlattensThePowerState()
    {
        var (outputs, record) = RunGetAzVM(new Dictionary<string, object>
        {
            ["SubscriptionId"] = SubscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
            ["Status"] = true,
        });

        var vm = Assert.IsType<VirtualMachine>(Assert.Single(outputs));
        Assert.Equal("VM running", vm.PowerState);
        Assert.Equal("V2", vm.HyperVGeneration);
        Assert.Null(vm.Properties.InstanceView);
        Assert.Equal(2, record.Entries.Count);
        Assert.EndsWith("/providers/Microsoft.Compute/virtualMachines?api-version=2025-11-01", record.Entries[0].Request.Uri);
        Assert.EndsWith($"/virtualMachines/{VmName}?api-version=2025-11-01&$expand=instanceView", record.Entries[1].Request.Uri);
    }

    /// <summary>
    /// The fake's instance view arrived whole, and get-instance-view set none of the fields the
    /// detail switches flatten onto the VM.
    /// </summary>
    private static void AssertInstanceView(VirtualMachine vm, string expectedName)
    {
        Assert.Equal(expectedName, vm.Name);
        Assert.Equal(VmId(expectedName), vm.Id);

        var view = Assert.IsType<VirtualMachineInstanceView>(vm.Properties.InstanceView);
        Assert.Equal(new[] { "ProvisioningState/succeeded", "PowerState/running" }, view.Statuses.Select(status => status.Code));
        Assert.Equal("Info", view.Statuses[0].Level);
        Assert.Equal("2026-11-05T00:00:00+00:00", view.Statuses[0].Time);
        Assert.Equal("V2", view.HyperVGeneration);
        Assert.NotNull(view.VmAgent);
        Assert.Equal("2.7.41491.1102", view.VmAgent.VmAgentVersion);
        Assert.NotNull(view.Disks);
        Assert.Equal($"{expectedName}_OsDisk", Assert.Single(view.Disks).Name);

        Assert.Null(vm.PowerState);
        Assert.Null(vm.PublicIps);
        Assert.Null(vm.Fqdns);
        Assert.Null(vm.PrivateIps);
        Assert.Null(vm.MacAddresses);
        Assert.Null(vm.OsName);
        Assert.Null(vm.OsVersion);
        Assert.Null(vm.HyperVGeneration);
        Assert.Null(vm.MaintenanceRedeployStatus);
    }

    private static (IReadOnlyList<object> Outputs, SessionRecord Record) Run(params string[] args)
    {
        var record = new SessionRecord();
        using var host = Host(record);
        return (host.RunCli(args), record);
    }

    private static ScenarioHost Host(SessionRecord record) =>
        new(ClientType.CLI, new RecordPlaybackInterceptor(RecorderMode.Record, record), new InstanceViewArm(), extensions: [ExtensionUnderTest.Compute]);

    /// <summary>
    /// Runs the real <c>Get-AzVM</c> cmdlet, so PowerShell's own parameter binding picks the parameter
    /// set, and through it the command key.
    /// </summary>
    private static (IReadOnlyList<object> Outputs, SessionRecord Record) RunGetAzVM(Dictionary<string, object> parameters)
    {
        var record = new SessionRecord();
        using var host = new ScenarioHost(ClientType.PowerShell, new RecordPlaybackInterceptor(RecorderMode.Record, record), new InstanceViewArm(), extensions: [ExtensionUnderTest.Compute]);
        return (host.RunPowerShellCmdlet<GetVMCmdlet>(parameters), record);
    }

    private static string VmId(string name) =>
        $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{name}";

    /// <summary>
    /// Stands in for ARM. The resource group's VM list holds the first VM. A GET of either known VM answers
    /// with the VM, carrying its instance view and user data only when the request expands them, as ARM
    /// does. Any other VM answers 404.
    /// </summary>
    private sealed class InstanceViewArm : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath.TrimEnd('/');
            var query = Uri.UnescapeDataString(request.RequestUri.Query);
            if (request.Method != HttpMethod.Get
                || !path.Contains("/providers/Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Fake ARM got an unexpected request: {request.Method} {request.RequestUri}");
            }

            if (path.EndsWith("/providers/Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Json(new { value = new[] { Vm(VmName, query) } }, HttpStatusCode.OK));
            }

            var name = path.Split('/')[^1];
            if (name != VmName && name != SecondVmName)
            {
                return Task.FromResult(Json(new
                {
                    error = new
                    {
                        code = "ResourceNotFound",
                        message = $"The Resource 'Microsoft.Compute/virtualMachines/{name}' under resource group '{ResourceGroupName}' was not found.",
                    },
                }, HttpStatusCode.NotFound));
            }

            return Task.FromResult(Json(Vm(name, query), HttpStatusCode.OK));
        }

        private static object Vm(string name, string query) => new
        {
            id = VmId(name),
            name,
            location = "westus2",
            properties = new
            {
                provisioningState = "Succeeded",
                hardwareProfile = new { vmSize = "Standard_D2s_v3" },
                userData = query.Contains("userData", StringComparison.OrdinalIgnoreCase) ? UserDataValue : null,
                instanceView = query.Contains("$expand=instanceView", StringComparison.OrdinalIgnoreCase) ? InstanceView(name) : null,
            },
        };

        private static object InstanceView(string name) => new
        {
            computerName = name,
            hyperVGeneration = "V2",
            vmAgent = new
            {
                vmAgentVersion = "2.7.41491.1102",
                statuses = new[] { new { code = "ProvisioningState/succeeded", level = "Info", displayStatus = "Ready" } },
            },
            disks = new[]
            {
                new
                {
                    name = $"{name}_OsDisk",
                    statuses = new[] { new { code = "ProvisioningState/succeeded", level = "Info", displayStatus = "Provisioning succeeded" } },
                },
            },
            statuses = new object[]
            {
                new { code = "ProvisioningState/succeeded", level = "Info", displayStatus = "Provisioning succeeded", time = "2026-11-05T00:00:00+00:00" },
                new { code = "PowerState/running", level = "Info", displayStatus = "VM running" },
            },
        };

        private static HttpResponseMessage Json(object body, HttpStatusCode status) => new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
    }
}
```

- [ ] **Step 3: Run the tests to verify the new behavior fails**

Run: `dotnet test tests\Scenario.Tests --filter FullyQualifiedName~VmGetInstanceViewOrchestrationTests`

Expected: the project builds. Three tests FAIL:
- `PowerShellStatus_ByNameAndResourceGroup_WritesTheVmWithItsInstanceViewIntact`
- `PowerShellStatusWithUserData_ExpandsBothInOneRequest`
- `PowerShellStatus_ById_WritesTheVmWithItsInstanceView`

Each fails because PowerShell cannot resolve a parameter set: no existing set takes `-Status` together with `-ResourceGroupName -Name`, or with `-Id`.

The other seven PASS. That is the five CLI tests, plus `PowerShellWithoutStatus_ReadsThePlainVm` and `PowerShellListStatus_StillFlattensThePowerState`, which guard behavior this task must keep.

- [ ] **Step 4: Let the orchestrator include user data**

In `src/Extensions/Compute/Customization/GetVMInstanceViewProcessor_Orchestrator.cs`, replace:

```csharp
    /// <summary>
    /// Read orchestrator behind <c>az vm get-instance-view</c>. Every request asks ARM for the
    /// instance view, and the VM is written with <c>properties.instanceView</c> intact: nothing is
    /// flattened onto the model and nothing is cleared, unlike the detail switches of
    /// <c>vm show</c> and <c>vm list</c>. Addressed by name and resource group, or by one or more
    /// resource ids, one GET per id, written as one array.
    /// </summary>
    public class GetVMInstanceViewProcessor_Orchestrator : GetVMProcessor
    {
        public string[]? Ids { get; set; }

        public override async Task ProcessAsync()
        {
            Expand = VirtualMachineDetailEnricher.ResolveExpand(instanceView: true, userData: false);
```

with:

```csharp
    /// <summary>
    /// Read orchestrator behind <c>az vm get-instance-view</c>, and behind <c>Get-AzVM -Status</c> for one
    /// VM (<c>Get-AzVM_GetStatus</c>, <c>Get-AzVM_IdStatus</c>). Every request asks ARM for the instance
    /// view, plus the user data when PowerShell passes <c>-UserData</c>. The VM is written with
    /// <c>properties.instanceView</c> intact: nothing is flattened onto the model and nothing is cleared,
    /// unlike the detail switches of <c>vm show</c> and <c>vm list</c>. Addressed by name and resource
    /// group, or by one or more resource ids, one GET per id, written as one array.
    /// </summary>
    public class GetVMInstanceViewProcessor_Orchestrator : GetVMProcessor
    {
        public string[]? Ids { get; set; }
        public bool IncludeUserData { get; set; }

        public override async Task ProcessAsync()
        {
            Expand = VirtualMachineDetailEnricher.ResolveExpand(instanceView: true, userData: IncludeUserData);
```

- [ ] **Step 5: Register the two PowerShell commands**

In `src/Extensions/Compute/ExtensionIndex.cs`, insert this block immediately before the line `        private static readonly CommandMetadata s_listVM_PS = new(`:

```csharp
        private static readonly CommandMetadata s_getVMStatus_PS = new(
            Name: "GetVMInstanceViewProcessor_Orchestrator",
            InvocationName: "Get-AzVM_GetStatus",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.GetVMInstanceViewProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.GetVMInstanceViewProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"]    = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Name"]              = new("Name", "Name", typeof(string), "The name of the virtual machine.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["ResourceGroupName"] = new("ResourceGroupName", "ResourceGroupName", typeof(string), "The resource group name.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["UserData"]          = new("IncludeUserData", "UserData", typeof(bool), "Include the user data properties in the query result.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
        private static readonly CommandMetadata s_getVMByIdStatus_PS = new(
            Name: "GetVMInstanceViewProcessor_Orchestrator",
            InvocationName: "Get-AzVM_IdStatus",
            Aliases: [],
            FullName: "Microsoft.Azure.Azclips.Extensions.Compute.Processors.GetVMInstanceViewProcessor_Orchestrator",
            AssemblyName: "Microsoft.Azure.Azclips.Extensions.Compute",
            ProcessorType: typeof(Processors.GetVMInstanceViewProcessor_Orchestrator),
            Parameters: new Dictionary<string, ParameterMetadata>
            {
                ["SubscriptionId"] = new("SubscriptionId", "SubscriptionId", typeof(string), "Azure Subscription Id. Defaults to the active profile's subscription.", IsRequired: false, Aliases: [], AllowedValues: []),
                ["Id"]             = new("Ids", "Id", typeof(string[]), "The resource ID of the virtual machine. Provide either -Id or -Name and -ResourceGroupName.", IsRequired: true, Aliases: [], AllowedValues: []),
                ["UserData"]       = new("IncludeUserData", "UserData", typeof(bool), "Include the user data properties in the query result.", IsRequired: false, Aliases: [], AllowedValues: []),
            },
            ParameterAliases: new Dictionary<string, string>()
        );
```

In the same file, in `s_psTable`, replace:

```csharp
            ["Get-AzVM_Id"]                = s_getVMById_PS,
```

with:

```csharp
            ["Get-AzVM_Id"]                = s_getVMById_PS,
            ["Get-AzVM_GetStatus"]         = s_getVMStatus_PS,
            ["Get-AzVM_IdStatus"]          = s_getVMByIdStatus_PS,
```

Do not change `s_getVM_PS`, `s_getVMById_PS`, any CLI entry, or `s_cliTable`.

- [ ] **Step 6: Add the `GetStatus` and `IdStatus` parameter sets to `GetVMCmdlet`**

In `src/PS/Compute/Cmdlets/GetVMCmdlet.cs`, make these seven replacements.

1. Replace:

```csharp
    [Parameter(ParameterSetName = "Get", Mandatory = false)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    [Parameter(ParameterSetName = "ListByName", Mandatory = false)]
    [Parameter(ParameterSetName = "ListLocation", Mandatory = false)]
    [Parameter(ParameterSetName = "Id", Mandatory = false)]
    public string SubscriptionId { get; set; } = "";
```

with:

```csharp
    [Parameter(ParameterSetName = "Get", Mandatory = false)]
    [Parameter(ParameterSetName = "GetStatus", Mandatory = false)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    [Parameter(ParameterSetName = "ListByName", Mandatory = false)]
    [Parameter(ParameterSetName = "ListLocation", Mandatory = false)]
    [Parameter(ParameterSetName = "Id", Mandatory = false)]
    [Parameter(ParameterSetName = "IdStatus", Mandatory = false)]
    public string SubscriptionId { get; set; } = "";
```

2. Replace:

```csharp
    [Parameter(ParameterSetName = "Get", Mandatory = true)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    public string ResourceGroupName { get; set; } = "";
```

with:

```csharp
    [Parameter(ParameterSetName = "Get", Mandatory = true)]
    [Parameter(ParameterSetName = "GetStatus", Mandatory = true)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    public string ResourceGroupName { get; set; } = "";
```

3. Replace:

```csharp
    [Parameter(ParameterSetName = "Get", Mandatory = true)]
    [Parameter(ParameterSetName = "ListByName", Mandatory = true)]
    public string? Name { get; set; }
```

with:

```csharp
    [Parameter(ParameterSetName = "Get", Mandatory = true)]
    [Parameter(ParameterSetName = "GetStatus", Mandatory = true)]
    [Parameter(ParameterSetName = "ListByName", Mandatory = true)]
    public string? Name { get; set; }
```

4. Replace:

```csharp
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    [Parameter(ParameterSetName = "ListByName", Mandatory = false)]
    [Parameter(ParameterSetName = "ListLocation", Mandatory = false)]
    public SwitchParameter Status { get; set; }
```

with:

```csharp
    // Mandatory in GetStatus and IdStatus, so PowerShell picks those sets exactly when -Status is given
    // for one VM.
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    [Parameter(ParameterSetName = "ListByName", Mandatory = false)]
    [Parameter(ParameterSetName = "ListLocation", Mandatory = false)]
    [Parameter(ParameterSetName = "GetStatus", Mandatory = true)]
    [Parameter(ParameterSetName = "IdStatus", Mandatory = true)]
    public SwitchParameter Status { get; set; }
```

5. Replace:

```csharp
    [Parameter(ParameterSetName = "Id", Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
    public string Id { get; set; } = "";
```

with:

```csharp
    [Parameter(ParameterSetName = "Id", Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
    [Parameter(ParameterSetName = "IdStatus", Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
    public string Id { get; set; } = "";
```

6. Replace:

```csharp
    [Parameter(ParameterSetName = "Get", Mandatory = false)]
    [Parameter(ParameterSetName = "Id", Mandatory = false)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    [Parameter(ParameterSetName = "ListByName", Mandatory = false)]
    [Parameter(ParameterSetName = "ListLocation", Mandatory = false)]
    public SwitchParameter UserData { get; set; }
```

with:

```csharp
    [Parameter(ParameterSetName = "Get", Mandatory = false)]
    [Parameter(ParameterSetName = "GetStatus", Mandatory = false)]
    [Parameter(ParameterSetName = "Id", Mandatory = false)]
    [Parameter(ParameterSetName = "IdStatus", Mandatory = false)]
    [Parameter(ParameterSetName = "List", Mandatory = false)]
    [Parameter(ParameterSetName = "ListByName", Mandatory = false)]
    [Parameter(ParameterSetName = "ListLocation", Mandatory = false)]
    public SwitchParameter UserData { get; set; }
```

7. Replace:

```csharp
        if (Status.IsPresent)
        {
            parameters.Add(nameof(Status), true);
        }
```

with:

```csharp
        // For one VM, -Status selects the GetStatus or IdStatus key, whose command always returns the
        // instance view, so only the list forms take it as a parameter.
        if (Status.IsPresent && ParameterSetName is "List" or "ListByName" or "ListLocation")
        {
            parameters.Add(nameof(Status), true);
        }
```

The routing line `var commandName = $"Get-AzVM_{parameterSetName}";` does not change.

- [ ] **Step 7: Run the orchestration tests to verify they pass**

Run: `dotnet test tests\Scenario.Tests --filter FullyQualifiedName~VmGetInstanceViewOrchestrationTests`

Expected: PASS, 10 tests.

- [ ] **Step 8: Mirror the CLI's checks in the lifecycle PowerShell branch**

In `tests/Scenario.Tests/Compute/VmLifecycleScenarioTests.cs`, make these six replacements.

1. In the class summary, replace:

```csharp
/// On the CLI, <c>vm get-instance-view</c> follows show, stop, deallocate and start, and must report
/// the power state each one left behind.
```

with:

```csharp
/// After show, stop, deallocate and start, <c>vm get-instance-view</c> on the CLI, and <c>Get-AzVM -Status</c>
/// in PowerShell, must report the power state each one left behind.
```

2. Replace:

```csharp
            // vm get-instance-view has no PowerShell counterpart yet. Playback fails only on a request it
            // cannot match, so the CLI's four instance-view reads go unused here. The first of them is
            // recorded after vm list -d, so New-AzVM and Get-AzVM_List -Status below still consume the
            // same $expand=instanceView entries, in the same order, as before.
```

with:

```csharp
            // Get-AzVM_GetStatus and Get-AzVM_IdStatus send the same $expand=instanceView reads the CLI's
            // vm get-instance-view records, in the same order, so they replay those entries. The first is
            // recorded after vm list -d, so New-AzVM and Get-AzVM_List -Status below still consume the
            // $expand=instanceView entries they did before.
```

3. Replace:

```csharp
            AssertVm(ps.RunPowerShell("Get-AzVM_Get", PsVmParameters(SubscriptionId, VmName), extension: "Compute"), VmName, SubscriptionId);
```

with:

```csharp
            AssertVm(ps.RunPowerShell("Get-AzVM_Get", PsVmParameters(SubscriptionId, VmName), extension: "Compute"), VmName, SubscriptionId);
            AssertInstanceView(ps.RunPowerShell("Get-AzVM_GetStatus", PsVmParameters(SubscriptionId, VmName), extension: "Compute"), "PowerState/running", SubscriptionId, detailed: true);
```

4. Replace:

```csharp
            AssertActionCompleted(ps.RunPowerShell("Stop-AzVM", PsStopParameters(SubscriptionId, VmName, stayProvisioned: true), extension: "Compute"));
            AssertActionCompleted(ps.RunPowerShell("Stop-AzVM", PsStopParameters(SubscriptionId, VmName, stayProvisioned: false), extension: "Compute"));
```

with:

```csharp
            AssertActionCompleted(ps.RunPowerShell("Stop-AzVM", PsStopParameters(SubscriptionId, VmName, stayProvisioned: true), extension: "Compute"));
            AssertInstanceView(ps.RunPowerShell("Get-AzVM_GetStatus", PsVmParameters(SubscriptionId, VmName), extension: "Compute"), "PowerState/stopped", SubscriptionId);
            AssertActionCompleted(ps.RunPowerShell("Stop-AzVM", PsStopParameters(SubscriptionId, VmName, stayProvisioned: false), extension: "Compute"));
            AssertInstanceView(ps.RunPowerShell("Get-AzVM_IdStatus", new Dictionary<string, object> { ["Id"] = new[] { VmId(SubscriptionId, VmName) } }, extension: "Compute"), "PowerState/deallocated", SubscriptionId);
```

5. Replace:

```csharp
            AssertActionCompleted(ps.RunPowerShell("Start-AzVM", new Dictionary<string, object> { ["Id"] = new[] { VmId(SubscriptionId, VmName) } }, extension: "Compute"));
```

with:

```csharp
            AssertActionCompleted(ps.RunPowerShell("Start-AzVM", new Dictionary<string, object> { ["Id"] = new[] { VmId(SubscriptionId, VmName) } }, extension: "Compute"));
            AssertInstanceView(ps.RunPowerShell("Get-AzVM_GetStatus", PsVmParameters(SubscriptionId, VmName), extension: "Compute"), "PowerState/running", SubscriptionId);
```

6. In the `AssertInstanceView` summary, replace:

```csharp
    /// <c>vm get-instance-view</c>: one virtual machine, or a one-element array for <c>--ids</c>, whose
    /// instance view holds exactly the provisioning status and the power state the previous step left.
```

with:

```csharp
    /// <c>vm get-instance-view</c> and <c>Get-AzVM -Status</c>: one virtual machine, or a one-element array
    /// for <c>--ids</c> and <c>-Id</c>, whose instance view holds exactly the provisioning status and the
    /// power state the previous step left.
```

The PowerShell steps call the command keys directly, with the parameters the cmdlet forwards. For `Get-AzVM_GetStatus` and `Get-AzVM_IdStatus`, the cmdlet forwards no `Status`.

- [ ] **Step 9: Confirm the lifecycle test still only needs its recording**

Run: `dotnet test tests\Scenario.Tests --filter FullyQualifiedName~VmLifecycleScenarioTests`

Expected: the same FAIL as Task 3 Step 8. The CLI playback branch stops at its first `vm get-instance-view` with `InvalidOperationException: Playback found no recorded response (matching on ...) for:` followed by `GET https://management.azure.com/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/scenario-vm-lifecycle-rg/providers/Microsoft.Compute/virtualMachines/lifecycle-vm?api-version=2025-11-01&$expand=instanceView`.

The CLI branch runs first, so the PowerShell steps can only be checked after the re-record. A compile error, or a failure at any other step, means Step 8 needs fixing.

- [ ] **Step 10: Update the model comments**

In `src/Extensions/Compute/Models/VirtualMachineInstanceView.cs`, replace:

```csharp
/// A virtual machine's run-time state, returned inside <c>$expand=instanceView</c> and modeled on
/// every property of api-version 2025-11-01. <c>az vm get-instance-view</c> returns it in place at
/// <c>properties.instanceView</c>; the detail switches (<c>--show-details</c>, <c>-Status</c>)
/// flatten what they need onto <see cref="VirtualMachine"/> and then drop it.
```

with:

```csharp
/// A virtual machine's run-time state, returned inside <c>$expand=instanceView</c> and modeled on
/// every property of api-version 2025-11-01. <c>az vm get-instance-view</c>, and <c>Get-AzVM -Status</c>
/// for one VM, return it in place at <c>properties.instanceView</c>. The detail switches
/// (<c>--show-details</c>, and <c>-Status</c> on the list forms) flatten what they need onto
/// <see cref="VirtualMachine"/> and then drop it.
```

In the same file, replace:

```csharp
/// ARM's maintenance and redeploy status block. <c>Get-AzVM -Status</c> flattens it onto
/// <see cref="VirtualMachine"/>; <c>az vm get-instance-view</c> returns it in place.
```

with:

```csharp
/// ARM's maintenance and redeploy status block. The list forms of <c>Get-AzVM -Status</c> flatten it
/// onto <see cref="VirtualMachine"/>; <c>az vm get-instance-view</c>, and <c>Get-AzVM -Status</c> for one
/// VM, return it in place.
```

In `src/Extensions/Compute/Models/VirtualMachine.cs`, replace:

```csharp
    // Returned only with $expand=instanceView. vm get-instance-view returns it intact; the detail
    // switches (--show-details, -Status) flatten what they need onto VirtualMachine, then clear it.
```

with:

```csharp
    // Returned only with $expand=instanceView. vm get-instance-view, and Get-AzVM -Status for one VM,
    // return it intact; the detail switches (--show-details, list -Status) flatten what they need onto
    // VirtualMachine, then clear it.
```

- [ ] **Step 11: Update the PowerShell help**

1. Build and stage the modules: `.\build\build.ps1`
2. Re-sync the help's syntax and parameter sets from the built cmdlet: `pwsh -File build/New-PSHelpMarkdown.ps1 -Module Compute -Refresh`
3. Run `git --no-pager diff --stat -- src/PS/Compute/help`. Keep only the changes to `Get-AzVM.md`, and restore any other file the refresh touched with `git checkout -- <that file>`. In `Get-AzVM.md`, check that the refresh added:
   - `GetStatus` and `IdStatus` syntax blocks
   - the new sets in the parameter-set lists of `-ResourceGroupName`, `-Name`, `-Id`, `-UserData`, `-SubscriptionId` and `-Status`, with `-Status` required in `GetStatus` and `IdStatus`
4. Make the following prose edits in `src/PS/Compute/help/Get-AzVM.md`.

   **4a.** In DESCRIPTION, replace:

   ```markdown
   Alternatively, address a single virtual machine directly by its ARM resource ID with `-Id`.
   ```

   with:

   ```markdown
   Alternatively, address a single virtual machine directly by its ARM resource ID with `-Id`.
   Add `-Status` to `-ResourceGroupName` and `-Name`, or to `-Id`, to return the virtual machine with its instance view.
   ```

   **4b.** Under EXAMPLES, insert this example immediately before `## PARAMETERS`:

   ````markdown
   ### Get the instance view of a VM

   Return the virtual machine with its instance view in `Properties.InstanceView`: provisioning and power statuses, VM agent, disks, extensions, boot diagnostics, and patch status.

   ```powershell
   Get-AzVM -SubscriptionId 00000000-0000-0000-0000-000000000000 -ResourceGroupName myrg -Name myvm -Status
   ```

   ````

   **4c.** In the `-Status` parameter, replace the description paragraph:

   ```markdown
   Retrieve the power state, OS name, OS version, hypervisor generation and maintenance status of each virtual machine. Command will run slow. Details are retrieved for at most 100 virtual machines. Cannot be combined with `-ResourceGroupName` and `-Name` together; use `-Name` alone to get the status of one virtual machine.
   ```

   with:

   ```markdown
   With `-ResourceGroupName` and `-Name`, or with `-Id`, return the virtual machine with its full instance view in `Properties.InstanceView`: provisioning and power statuses, VM agent, disks, extensions, boot diagnostics, and patch status. With the list forms, retrieve the power state, OS name, OS version, hypervisor generation and maintenance status of each virtual machine. The list forms run one extra request per virtual machine, so they are slow, and retrieve details for at most 100 virtual machines.
   ```

   **4d.** In OUTPUTS, replace:

   ```markdown
   Each object includes the ARM resource ID, provisioning state, VM size, and network configuration; `-Status` adds the power state, OS name, OS version, hypervisor generation and maintenance status.
   ```

   with:

   ```markdown
   Each object includes the ARM resource ID, provisioning state, VM size, and network configuration. With the list forms, `-Status` adds the power state, OS name, OS version, hypervisor generation and maintenance status. With `-ResourceGroupName` and `-Name`, or `-Id`, `-Status` returns the full instance view in `Properties.InstanceView`.
   ```
5. Check for drift: `pwsh -File build/Test-PSHelpMarkdown.ps1 -Module Compute`

   Expected: no PSL13 or PSL14 records for `Get-AzVM.md`.
6. Run the help validators: `.\build\build.ps1 -GenerateHelpArtifacts`

   Expected: exit code 0, with no errors for `Get-AzVM.md`. For example, there should be no `PS_ExamplePlaceholder`, PSL2 or PSL5.

- [ ] **Step 12: Check nothing else regressed**

Run: `dotnet test tests\Extensions.Compute.Tests\Extensions.Compute.Tests.csproj`

Expected: PASS.

Run: `dotnet test tests\Scenario.Tests --filter "FullyQualifiedName~VmGetInstanceViewOrchestrationTests|FullyQualifiedName~VmCreateOrchestrationTests|FullyQualifiedName~VmCreateScenarioTests"`

Expected: PASS.

Run: `dotnet test tools\Help\Azclips.Help.Tool.Tests --filter FullyQualifiedName~CliExportFixturesTests`

Expected: PASS, 3 cases. The CLI surface is unchanged.

- [ ] **Step 13: Commit**

```powershell
git add tests/Scenario.Tests/Scenario.Tests.csproj tests/Scenario.Tests/Compute/VmGetInstanceViewOrchestrationTests.cs src/Extensions/Compute/Customization/GetVMInstanceViewProcessor_Orchestrator.cs src/Extensions/Compute/ExtensionIndex.cs src/PS/Compute/Cmdlets/GetVMCmdlet.cs tests/Scenario.Tests/Compute/VmLifecycleScenarioTests.cs src/Extensions/Compute/Models/VirtualMachineInstanceView.cs src/Extensions/Compute/Models/VirtualMachine.cs src/PS/Compute/help/Get-AzVM.md
git commit -m '[Compute] `Get-AzVM -Status`: Return the instance view for one VM'
```

After this task, run Task 3's recording gate, Steps 9-13, with these changes:
- Tasks 3 and 4 have committed the lifecycle test, so Step 9 shows `git --no-pager diff d93dd1b32 HEAD -- tests/Scenario.Tests/Compute/VmLifecycleScenarioTests.cs`. The user has approved the CLI steps, and now reviews the PowerShell steps.
- In Step 12, playback passing means the PowerShell branch replayed the four instance-view entries too.
- Step 13 commits only the recording: `git commit -m '{Tests} Re-record the VM lifecycle scenario with instance-view checks'`.

---

## Final verification

Run each check, and confirm it passes before reporting the work complete:

| Check | Command | Expected |
|---|---|---|
| Build | `dotnet build azclips.slnx` | Succeeds |
| Model unit tests | `dotnet test tests\Extensions.Compute.Tests\Extensions.Compute.Tests.csproj` | PASS |
| VM scenario and orchestration tests | `dotnet test tests\Scenario.Tests --filter "FullyQualifiedName~VmGetInstanceViewOrchestrationTests\|FullyQualifiedName~VmCreateOrchestrationTests\|FullyQualifiedName~VmCreateScenarioTests\|FullyQualifiedName~VmLifecycleScenarioTests"` | PASS |
| Help validators | `.\build\build.ps1 -GenerateHelpArtifacts` | Exit code 0 |
| PowerShell help drift | `pwsh -File build/Test-PSHelpMarkdown.ps1 -Module Compute` | No PSL13 or PSL14 records |
| CLI export snapshot | `dotnet test tools\Help\Azclips.Help.Tool.Tests --filter FullyQualifiedName~CliExportFixturesTests` | PASS |

(In the scenario filter, `\|` is only Markdown table escaping. Type a plain `|`.)
