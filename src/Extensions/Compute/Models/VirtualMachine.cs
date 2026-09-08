// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Core.Abstractions.Output;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Models;

public class VirtualMachine : ITableOutputProvider
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Location { get; set; } = null!;
    public VirtualMachineProperties Properties { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();

    static string ITableOutputProvider.TableTypeName =>
        "Microsoft.Azure.Azclips.Extensions.Compute.Models.VirtualMachine";

    static IReadOnlyList<TableOutputColumn> ITableOutputProvider.GetTableColumns() =>
    [
        new("Name", PropertyName: "Name"),
        new("ResourceGroupName", ScriptBlock: "if ($null -ne $_.Id) { [regex]::Match($_.Id, '(?i)/resourceGroups/([^/]+)').Groups[1].Value }"),
        new("Location", PropertyName: "Location"),
        new("Size", ScriptBlock: "if ($null -ne $_.Properties -and $null -ne $_.Properties.HardwareProfile) { $_.Properties.HardwareProfile.VmSize }"),
        new("ProvisioningState", ScriptBlock: "if ($null -ne $_.Properties) { $_.Properties.ProvisioningState }"),
    ];
}

public class VirtualMachineProperties
{
    public HardwareProfile HardwareProfile { get; set; } = null!;
    public StorageProfile StorageProfile { get; set; } = null!;
    public OSProfile OSProfile { get; set; } = null!;
    public NetworkProfile NetworkProfile { get; set; } = null!;
    public SecurityProfile? SecurityProfile { get; set; }
    public DiagnosticsProfile? DiagnosticsProfile { get; set; }

    // Set by ARM on responses; null when this model is used as a create request body.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProvisioningState { get; set; }

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




