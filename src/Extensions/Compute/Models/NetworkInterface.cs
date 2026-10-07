// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Models;

public class NetworkInterface
{
    public string Id { get; set; } = null!;
    public string Location { get; set; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Tags { get; set; }

    public NetworkInterfaceProperties Properties { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class NetworkInterfaceProperties
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NetworkSecurityGroup? NetworkSecurityGroup { get; set; }
    public List<IPConfiguration> IPConfigurations { get; set; } = [];
    public bool EnableAcceleratedNetworking { get; set; }
    public bool EnableIPForwarding { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MacAddress { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class IPConfiguration
{
    public string Name { get; set; } = null!;
    public IPConfigurationProperties Properties { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class IPConfigurationProperties
{
    public Subnet Subnet { get; set; } = null!;
    public List<object> VirtualNetworkTaps { get; set; } = [];
    public List<object> ApplicationGatewayBackendAddressPools { get; set; } = [];
    public List<object> LoadBalancerBackendAddressPools { get; set; } = [];
    public List<object> LoadBalancerInboundNatRules { get; set; } = [];
    public string PrivateIPAllocationMethod { get; set; } = "Dynamic";
    public bool Primary { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PrivateIPAddress { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResourceReference? PublicIPAddress { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary>An ARM sub-resource reference: the service returns only the id.</summary>
public class ResourceReference
{
    public string Id { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}



