// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Models;

public class VirtualMachine
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Location { get; set; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImagePlan? Plan { get; set; }

    public VirtualMachineProperties Properties { get; set; } = null!;

    // Synthetic fields computed by the detail switches rather than returned by ARM. They sit
    // outside Properties so the ARM-sourced payload stays untouched, and every one is null unless
    // the command named in its comment asked for it.

    /// <summary>
    /// Power state, flattened out of the instance view.
    /// CLI: populated by <c>az vm create</c>, <c>az vm show -d</c> and <c>az vm list -d</c>.
    /// PowerShell: populated by <c>New-AzVM</c>, and by <c>Get-AzVM -Status</c> (parameter sets <c>List</c>, <c>ListByName</c>, <c>ListLocation</c>).
    /// Set to "Info Not Available" when a per-VM lookup failed during a list fan-out.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PowerState { get; set; }

    /// <summary>
    /// Comma-joined public IP addresses across every NIC.
    /// CLI: populated by <c>az vm create</c>, <c>az vm show -d</c> and <c>az vm list -d</c>.
    /// PowerShell: populated by <c>New-AzVM</c> only — <c>Get-AzVM</c> has no spelling of --show-details.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PublicIps { get; set; }

    /// <summary>
    /// Comma-joined fully-qualified domain names across every public IP.
    /// CLI: populated by <c>az vm create</c>, <c>az vm show -d</c> and <c>az vm list -d</c>.
    /// PowerShell: populated by <c>New-AzVM</c> only — <c>Get-AzVM</c> has no spelling of --show-details.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Fqdns { get; set; }

    /// <summary>
    /// Comma-joined private IP addresses across every NIC IP configuration.
    /// CLI: populated by <c>az vm create</c>, <c>az vm show -d</c> and <c>az vm list -d</c>.
    /// PowerShell: populated by <c>New-AzVM</c> only — <c>Get-AzVM</c> has no spelling of --show-details.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PrivateIps { get; set; }

    /// <summary>
    /// Comma-joined MAC addresses across every NIC.
    /// CLI: populated by <c>az vm create</c>, <c>az vm show -d</c> and <c>az vm list -d</c>.
    /// PowerShell: populated by <c>New-AzVM</c> only — <c>Get-AzVM</c> has no spelling of --show-details.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MacAddresses { get; set; }

    /// <summary>
    /// Operating system name, flattened out of the instance view.
    /// PowerShell: populated by <c>Get-AzVM -Status</c> (parameter sets <c>List</c>, <c>ListByName</c>, <c>ListLocation</c>).
    /// CLI: never populated — --show-details does not surface this field.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OsName { get; set; }

    /// <summary>
    /// Operating system version, flattened out of the instance view.
    /// PowerShell: populated by <c>Get-AzVM -Status</c> (parameter sets <c>List</c>, <c>ListByName</c>, <c>ListLocation</c>).
    /// CLI: never populated — --show-details does not surface this field.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OsVersion { get; set; }

    /// <summary>
    /// Hypervisor generation, flattened out of the instance view.
    /// PowerShell: populated by <c>Get-AzVM -Status</c> (parameter sets <c>List</c>, <c>ListByName</c>, <c>ListLocation</c>).
    /// CLI: never populated — --show-details does not surface this field.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HyperVGeneration { get; set; }

    /// <summary>
    /// Maintenance and redeploy status, flattened out of the instance view.
    /// PowerShell: populated by <c>Get-AzVM -Status</c> (parameter sets <c>List</c>, <c>ListByName</c>, <c>ListLocation</c>).
    /// CLI: never populated — --show-details does not surface this field.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MaintenanceRedeployStatus? MaintenanceRedeployStatus { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class VirtualMachineProperties
{
    public HardwareProfile HardwareProfile { get; set; } = null!;
    public StorageProfile StorageProfile { get; set; } = null!;
    public OSProfile OSProfile { get; set; } = null!;
    public NetworkProfile NetworkProfile { get; set; } = null!;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SecurityProfile? SecurityProfile { get; set; }
    public DiagnosticsProfile? DiagnosticsProfile { get; set; }

    // Set by ARM on responses; null when this model is used as a create request body.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProvisioningState { get; set; }

    // Returned only when the request asks for $expand=userData.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UserData { get; set; }

    // Returned only with $expand=instanceView; cleared once --show-details has read the power state.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public VirtualMachineInstanceView? InstanceView { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class HardwareProfile
{
    public string VmSize { get; set; }

    public HardwareProfile(string vmSize)
    {
        VmSize = vmSize;
    }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class VMSizeProperties
{
    public int? VCPUsAvailable { get; set; }
    public int? VCPUsPerCore { get; set; }
    public VMSizeProperties(int? vCPUsAvailable, int? vCPUsPerCore)
    {
        VCPUsAvailable = vCPUsAvailable;
        VCPUsPerCore = vCPUsPerCore;
    }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class OSProfile
{
    public string ComputerName { get; set; } = null!;
    public string AdminUsername { get; set; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AdminPassword { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WindowsConfiguration? WindowsConfiguration { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LinuxConfiguration? LinuxConfiguration { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class WindowsConfiguration
{
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class LinuxConfiguration
{
    public bool DisablePasswordAuthentication { get; set; }
    public SshConfiguration? Ssh { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class SshConfiguration
{
    public IList<SshPublicKey> PublicKeys { get; set; } = [];
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class SshPublicKey
{
    public string Path { get; set; } = null!;
    public string KeyData { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class StorageProfile
{
    public OSDisk? OSDisk { get; set; }
    public ImageReference ImageReference { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class ImageReference
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Publisher { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Offer { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Sku { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Version { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SharedGalleryImageId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CommunityGalleryImageId { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class VirtualMachineImage
{
    public string Name { get; set; } = null!;
    public VirtualMachineImageProperties? Properties { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class VirtualMachineImageProperties
{
    public OsDiskImage? OsDiskImage { get; set; }
    public ImagePlan? Plan { get; set; }
    public string? HyperVGeneration { get; set; }
    public List<ImageFeature> Features { get; set; } = [];
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class ImageFeature
{
    public string? Name { get; set; }
    public string? Value { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class OsDiskImage
{
    public string OperatingSystem { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class ImagePlan
{
    public string? Name { get; set; }
    public string? Product { get; set; }
    public string? Publisher { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class OSDisk
{
    public string CreateOption { get; set; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ManagedDiskParameters? ManagedDisk { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class ManagedDiskParameters
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public VMDiskSecurityProfile? SecurityProfile { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class VMDiskSecurityProfile
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SecurityEncryptionType { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DiskEncryptionSetParameters? DiskEncryptionSet { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class DiskEncryptionSetParameters
{
    public string Id { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class NetworkProfile
{
    public IList<NetworkInterfaceReference> NetworkInterfaces { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class NetworkInterfaceReference
{
    public string Id { get; set; } = null!;
    public bool? Primary { get; set; } = null!;
    public string? DeleteOption { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class SecurityProfile
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UefiSettings? UefiSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SecurityType { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class UefiSettings
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? SecureBootEnabled { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? VTpmEnabled { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class DiagnosticsProfile
{
    public BootDiagnostics? BootDiagnostics { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class BootDiagnostics
{
    public bool Enabled { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}
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
