// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Models;

/// <summary>
/// A virtual machine's run-time state, returned inside <c>$expand=instanceView</c> and modeled on
/// every property of api-version 2025-11-01. <c>az vm get-instance-view</c>, and <c>Get-AzVM -Status</c>
/// for one VM, return it in place at <c>properties.instanceView</c>. The detail switches
/// (<c>--show-details</c>, and <c>-Status</c> on the list forms) flatten what they need onto
/// <see cref="VirtualMachine"/> and then drop it.
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
/// ARM's maintenance and redeploy status block. The list forms of <c>Get-AzVM -Status</c> flatten it
/// onto <see cref="VirtualMachine"/>; <c>az vm get-instance-view</c>, and <c>Get-AzVM -Status</c> for one
/// VM, return it in place.
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
