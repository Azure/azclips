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
