// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Subnet create. Shapes the subnet body from the approved inputs and sends it through
/// <see cref="CreateOrUpdateSubnetsProcessor"/>, which owns the request route and LRO.
/// </summary>
public class CreateSubnetCustomization : NetworkProcessorBase<Subnet, Subnet>, ISubnetInputs
{
    private const string DefaultPrivateEndpointNetworkPolicies = "Disabled";
    private const string DefaultPrivateLinkServiceNetworkPolicies = "Enabled";

    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string VirtualNetworkName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public IList<string>? AddressPrefixes { get; set; }

    /// <summary>A network security group name in the subnet's resource group, or a full resource ID.</summary>
    public string? NetworkSecurityGroup { get; set; }

    /// <summary>A route table name in the subnet's resource group, or a full resource ID.</summary>
    public string? RouteTable { get; set; }

    /// <summary>A NAT gateway name in the subnet's resource group, or a full resource ID.</summary>
    public string? NatGateway { get; set; }

    public IList<string>? ServiceEndpoints { get; set; }
    public IList<string>? Delegations { get; set; }
    public string? PrivateEndpointNetworkPolicies { get; set; }
    public string? PrivateLinkServiceNetworkPolicies { get; set; }
    public bool? DefaultOutboundAccess { get; set; }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        Subnet subnet = new() { Name = Name };
        SubnetRequestMapper.ApplyGivenInputs(subnet, this);
        subnet.PrivateEndpointNetworkPolicies ??= DefaultPrivateEndpointNetworkPolicies;
        subnet.PrivateLinkServiceNetworkPolicies ??= DefaultPrivateLinkServiceNetworkPolicies;

        CreateOrUpdateSubnetsProcessor put = CreateExtendedProcessor<CreateOrUpdateSubnetsProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.ResourceGroupName = ResourceGroupName;
            p.VirtualNetworkName = VirtualNetworkName;
            p.SubnetName = Name;
            p.Body = subnet;
        });

        return await put.SendAsync();
    }
}
