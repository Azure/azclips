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
    public VirtualMachineProperties Properties { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class VirtualMachineProperties
{
    public HardwareProfile HardwareProfile { get; set; } = null!;
    public StorageProfile StorageProfile { get; set; } = null!;
    public OSProfile OSProfile { get; set; } = null!;
    public NetworkProfile NetworkProfile { get; set; } = null!;
    public ScheduledEventsPolicy? ScheduledEventsPolicy { get; set; }
    public AdditionalCapabilities? AdditionalCapabilities { get; set; }
    public SecurityProfile? SecurityProfile { get; set; }
    public DiagnosticsProfile? DiagnosticsProfile { get; set; }
    public SubResource? AvailabilitySet { get; set; }
    public SubResource? VirtualMachineScaleSet { get; set; }
    public SubResource? ProximityPlacementGroup { get; set; }
    public string? Priority { get; set; }
    public string? EvictionPolicy { get; set; }
    public BillingProfile? BillingProfile { get; set; }
    public SubResource? Host { get; set; }
    public SubResource? HostGroup { get; set; }

    // Set by ARM on responses; null when this model is used as a create request body.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProvisioningState { get; set; }

    // Read-only from ARM.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? InstanceView { get; set; }

    public string? LicenseType { get; set; }

    // Read-only from ARM.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VmId { get; set; }

    public string? ExtensionsTimeBudget { get; set; }
    public int? PlatformFaultDomain { get; set; }
    public ScheduledEventsProfile? ScheduledEventsProfile { get; set; }
    public string? UserData { get; set; }
    public CapacityReservationProfile? CapacityReservation { get; set; }
    public InterconnectBlockProfile? InterconnectBlockProfile { get; set; }
    public ApplicationProfile? ApplicationProfile { get; set; }

    // Read-only from ARM.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TimeCreated { get; set; }

    public ResiliencyProfile? ResiliencyProfile { get; set; }

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
    public string AdminPassword { get; set; } = null!;
    public WindowsConfiguration? WindowsConfiguration { get; set; }
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class WindowsConfiguration
{
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
    public string Publisher { get; set; } = null!;
    public string Offer { get; set; } = null!;
    public string Sku { get; set; } = null!;
    public string Version { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class OSDisk
{
    public string CreateOption { get; set; } = null!;

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
    public UefiSettings? UefiSettings { get; set; }
    public string SecurityType { get; set; } = null!;
    public ProxyAgentSettings? ProxyAgentSettings { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class ProxyAgentSettings
{
    public bool? AddProxyAgentExtension { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class UefiSettings
{
    public bool SecureBootEnabled { get; set; }
    public bool VTpmEnabled { get; set; }
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

public class ScheduledEventsPolicy
{
    public object? UserInitiatedRedeploy { get; set; }
    public object? UserInitiatedReboot { get; set; }
    public ScheduledEventsAdditionalPublishingTargets? ScheduledEventsAdditionalPublishingTargets { get; set; }
    public object? AllInstancesDown { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class ScheduledEventsAdditionalPublishingTargets
{
    public EventGridAndResourceGraph? EventGridAndResourceGraph { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class EventGridAndResourceGraph
{
    public bool? Enable { get; set; }
    public string? ScheduledEventsApiVersion { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class AdditionalCapabilities
{
    public bool? UltraSSDEnabled { get; set; }
    public bool? HibernationEnabled { get; set; }
    public bool? EnableFips1403Encryption { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class BillingProfile
{
    public double? MaxPrice { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class SubResource
{
    public string Id { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class ScheduledEventsProfile
{
    public TerminateNotificationProfile? TerminateNotificationProfile { get; set; }
    public OSImageNotificationProfile? OSImageNotificationProfile { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class TerminateNotificationProfile
{
    public string? NotBeforeTimeout { get; set; }
    public bool? Enable { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class OSImageNotificationProfile
{
    public string? NotBeforeTimeout { get; set; }
    public bool? Enable { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class CapacityReservationProfile
{
    public SubResource? CapacityReservationGroup { get; set; }
    public bool? DisableCapacityReservationAssignment { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class InterconnectBlockProfile
{
    public ApiEntityReference? InterconnectBlock { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class ApiEntityReference
{
    public string? Id { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class ApplicationProfile
{
    public IList<VMGalleryApplication>? GalleryApplications { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class VMGalleryApplication
{
    public string? Tags { get; set; }
    public int? Order { get; set; }
    public string PackageReferenceId { get; set; } = null!;
    public string? ConfigurationReference { get; set; }
    public bool? TreatFailureAsDeploymentFailure { get; set; }
    public bool? EnableAutomaticUpgrade { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class ResiliencyProfile
{
    public ZoneMovement? ZoneMovement { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class ZoneMovement
{
    public bool? IsEnabled { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}
