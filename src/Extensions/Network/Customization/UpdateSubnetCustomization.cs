// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Subnet update: GET the subnet, apply only the given inputs, then PUT it back. The GET and PUT
/// routes come from <see cref="GetSubnetsProcessor"/> and <see cref="CreateOrUpdateSubnetsProcessor"/>.
/// An empty NetworkSecurityGroup, RouteTable or NatGateway detaches that reference.
/// </summary>
public class UpdateSubnetCustomization : NetworkProcessorBase<Subnet, Subnet>, ISubnetInputs
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string VirtualNetworkName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public IList<string>? AddressPrefixes { get; set; }

    /// <summary>A network security group name or full resource ID. An empty string detaches it.</summary>
    public string? NetworkSecurityGroup { get; set; }

    /// <summary>A route table name or full resource ID. An empty string detaches it.</summary>
    public string? RouteTable { get; set; }

    /// <summary>A NAT gateway name or full resource ID. An empty string detaches it.</summary>
    public string? NatGateway { get; set; }

    public IList<string>? ServiceEndpoints { get; set; }
    public IList<string>? Delegations { get; set; }
    public string? PrivateEndpointNetworkPolicies { get; set; }
    public string? PrivateLinkServiceNetworkPolicies { get; set; }
    public bool? DefaultOutboundAccess { get; set; }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        GetSubnetsProcessor get = CreateExtendedProcessor<GetSubnetsProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.ResourceGroupName = ResourceGroupName;
            p.VirtualNetworkName = VirtualNetworkName;
            p.SubnetName = Name;
        });
        Subnet subnet = await ChildResourceReader.ReadAsync<Subnet>(get, Publisher, JsonOptions);
        SubnetRequestMapper.ApplyGivenInputs(subnet, this);

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
