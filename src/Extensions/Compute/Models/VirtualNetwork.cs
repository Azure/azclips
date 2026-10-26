// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Models;

public class VirtualNetwork
{
    public VirtualNetworkProperties Properties { get; set; } = null!;
    public string Location { get; set; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Tags { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class VirtualNetworkProperties
{
    public AddressSpace AddressSpace { get; set; } = null!;
    public List<Subnet> Subnets { get; set; } = null!;
    public List<object> VirtualNetworkPeerings { get; set; } = [];
    public bool EnableDdosProtection { get; set; } = false;
    public List<object> IpAllocations { get; set; } = [];
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class Subnet
{
    public string Name { get; set; } = null!;
    public string Id { get; set; } = null!;
    public SubnetProperties Properties { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class SubnetProperties
{
    public string AddressPrefix { get; set; } = string.Empty;
    public List<string> AddressPrefixes { get; set; } = [];
    public List<object> ServiceEndpoints { get; set; } = [];
    public List<object> ServiceEndpointPolicies { get; set; } = [];
    public List<object> IpAllocations { get; set; } = [];
    public List<object> Delegations { get; set; } = [];
    public string PrivateEndpointNetworkPolicies { get; set; } = "Disabled";
    public string PrivateLinkServiceNetworkPolicies { get; set; } = "Enabled";
    public List<object> ApplicationGatewayIPConfigurations { get; set; } = [];
    public List<object> IpamPoolPrefixAllocations { get; set; } = [];
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class AddressSpace
{
    public List<string> AddressPrefixes { get; set; } = null!;
    public List<object> IpamPoolPrefixAllocations { get; set; } = [];
    public override string ToString() => JsonNode.FromObject(this).ToString();
}




