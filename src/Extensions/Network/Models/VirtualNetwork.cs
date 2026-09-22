// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Network.Models;

public class VirtualNetwork
{
    // Set by ARM on responses; null when this model is used as a create request body.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    public VirtualNetworkProperties Properties { get; set; } = null!;
    public string Location { get; set; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Tags { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExtendedLocation? ExtendedLocation { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class VirtualNetworkProperties
{
    public AddressSpace AddressSpace { get; set; } = null!;
    public List<Subnet> Subnets { get; set; } = null!;
    public List<object> VirtualNetworkPeerings { get; set; } = [];
    public bool EnableDdosProtection { get; set; } = false;
    public List<SubResource> IpAllocations { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DhcpOptions? DhcpOptions { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? FlowTimeoutInMinutes { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? EnableVmProtection { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubResource? DdosProtectionPlan { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public VirtualNetworkBgpCommunities? BgpCommunities { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public VirtualNetworkEncryption? Encryption { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PrivateEndpointVNetPolicies { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AddressSpace? SummarizedGatewayPrefixes { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProvisioningState { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary>An ARM resource referenced only by its id.</summary>
public class SubResource
{
    public string Id { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class ExtendedLocation
{
    public string Name { get; set; } = null!;
    public string Type { get; set; } = "EdgeZone";
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class DhcpOptions
{
    public List<string> DnsServers { get; set; } = [];
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class VirtualNetworkBgpCommunities
{
    public string VirtualNetworkCommunity { get; set; } = null!;

    // Assigned by ARM; never sent on a create request.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RegionalCommunity { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class VirtualNetworkEncryption
{
    public bool Enabled { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Enforcement { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class Subnet
{
    public string Name { get; set; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    public SubnetProperties Properties { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class SubnetProperties
{
    public string AddressPrefix { get; set; } = string.Empty;
    public List<string> AddressPrefixes { get; set; } = [];
    public List<ServiceEndpoint> ServiceEndpoints { get; set; } = [];
    public List<SubResource> ServiceEndpointPolicies { get; set; } = [];
    public List<SubResource> IpAllocations { get; set; } = [];
    public List<Delegation> Delegations { get; set; } = [];
    public string PrivateEndpointNetworkPolicies { get; set; } = "Disabled";
    public string PrivateLinkServiceNetworkPolicies { get; set; } = "Enabled";
    public List<ApplicationGatewayIPConfiguration> ApplicationGatewayIPConfigurations { get; set; } = [];
    public List<IpamPoolPrefixAllocation> IpamPoolPrefixAllocations { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubResource? NetworkSecurityGroup { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubResource? RouteTable { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubResource? NatGateway { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubResource? ServiceGateway { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SharingScope { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? DefaultOutboundAccess { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class ServiceEndpoint
{
    public string Service { get; set; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Locations { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class Delegation
{
    public string Name { get; set; } = null!;
    public DelegationProperties Properties { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class DelegationProperties
{
    public string ServiceName { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class ApplicationGatewayIPConfiguration
{
    public string Name { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class IpamPoolPrefixAllocation
{
    public SubResource Pool { get; set; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NumberOfIpAddresses { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class AddressSpace
{
    public List<string> AddressPrefixes { get; set; } = null!;
    public List<IpamPoolPrefixAllocation> IpamPoolPrefixAllocations { get; set; } = [];
    public override string ToString() => JsonNode.FromObject(this).ToString();
}
