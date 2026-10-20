// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Network.Models;

public class NetworkInterface
{
    // Set by ARM on responses; null when this model is used as a create request body.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Etag { get; set; }

    public NetworkInterfaceProperties Properties { get; set; } = null!;
    public string Location { get; set; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Tags { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExtendedLocation? ExtendedLocation { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class NetworkInterfaceProperties
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<NetworkInterfaceIpConfiguration>? IpConfigurations { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExpandableSubResource? NetworkSecurityGroup { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NetworkInterfaceDnsSettings? DnsSettings { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? EnableAcceleratedNetworking { get; set; }

    // The wire name keeps the legacy `IP` acronym casing, which the camelCase naming policy would not produce.
    [JsonPropertyName("enableIPForwarding")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? EnableIpForwarding { get; set; }

    // The members below are not set by `create`; ARM returns them on responses.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExpandableSubResource? VirtualMachine { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MacAddress { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Primary { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? VnetEncryptionSupported { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? DefaultOutboundConnectivityEnabled { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? DisableTcpStateTracking { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? HostedWorkloads { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExpandableSubResource? DscpConfiguration { get; set; }

    // Full resource envelopes, kept as opaque nodes so `show` echoes them verbatim. Type one when a
    // command writes into it.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<object>? TapConfigurations { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? PrivateEndpoint { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? PrivateLinkService { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NicType { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AuxiliaryMode { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AuxiliarySku { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkloadType { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MigrationPhase { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResourceGuid { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProvisioningState { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary>
/// DNS settings of the network interface. <c>AppliedDnsServers</c>, <c>InternalFqdn</c> and
/// <c>InternalDomainNameSuffix</c> are read-only.
/// </summary>
public class NetworkInterfaceDnsSettings
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? DnsServers { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? AppliedDnsServers { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InternalDnsNameLabel { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InternalFqdn { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InternalDomainNameSuffix { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class NetworkInterfaceIpConfiguration
{
    public string Name { get; set; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Etag { get; set; }

    public NetworkInterfaceIpConfigurationProperties Properties { get; set; } = null!;

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class NetworkInterfaceIpConfigurationProperties
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExpandableSubResource? Subnet { get; set; }

    // The wire names keep the legacy `IP` acronym casing, which the camelCase naming policy would
    // not produce from these property names, so they are pinned explicitly.
    [JsonPropertyName("publicIPAddress")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExpandableSubResource? PublicIpAddress { get; set; }

    [JsonPropertyName("privateIPAddress")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PrivateIpAddress { get; set; }

    [JsonPropertyName("privateIPAllocationMethod")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PrivateIpAllocationMethod { get; set; }

    [JsonPropertyName("privateIPAddressVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PrivateIpAddressVersion { get; set; }

    // The members below are not set by `create`; ARM returns them on responses.
    [JsonPropertyName("privateIPAddressPrefixLength")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PrivateIpAddressPrefixLength { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Primary { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExpandableSubResource? GatewayLoadBalancer { get; set; }

    // Full resource envelopes, kept as opaque nodes so `show` echoes them verbatim. Type one when a
    // command writes into it.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<object>? ApplicationSecurityGroups { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<object>? LoadBalancerBackendAddressPools { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<object>? LoadBalancerInboundNatRules { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<object>? ApplicationGatewayBackendAddressPools { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<object>? VirtualNetworkTaps { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? PrivateLinkConnectionProperties { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProvisioningState { get; set; }

    public override string ToString() => JsonNode.FromObject(this).ToString();
}

/// <summary>
/// A reference ARM returns as just an id unless <c>$expand</c> names it, in which case the whole
/// referenced resource comes back. The extra members are kept verbatim so <c>show --expand</c>
/// echoes them instead of flattening the reference to its id, while <c>Id</c> stays typed.
/// </summary>
public class ExpandableSubResource : SubResource
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
