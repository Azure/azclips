// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime.Json;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Models;

public class NetworkInterface
{
    public string Id { get; set; } = null!;
    public string Location { get; set; } = null!;
    public NetworkInterfaceProperties Properties { get; set; } = null!;
    public override string ToString() => JsonNode.FromObject(this).ToString();
}

public class NetworkInterfaceProperties
{
    public NetworkSecurityGroup? NetworkSecurityGroup { get; set; }
    public List<IPConfiguration> IPConfigurations { get; set; } = [];
    public bool EnableAcceleratedNetworking { get; set; }
    public bool EnableIPForwarding { get; set; }
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
    public override string ToString() => JsonNode.FromObject(this).ToString();
}




