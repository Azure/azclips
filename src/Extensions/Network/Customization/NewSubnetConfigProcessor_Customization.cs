// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

public class NewSubnetConfigProcessor_Customization : NetworkProcessorBase<object, Subnet>
{
    private bool? _defaultOutboundAccess;

    public string Name { get; set; } = string.Empty;
    public List<string> AddressPrefixes { get; set; } = [];
    public string NetworkSecurityGroupId { get; set; } = string.Empty;
    public string RouteTableId { get; set; } = string.Empty;
    public string NatGatewayId { get; set; } = string.Empty;
    public List<string> ServiceEndpoints { get; set; } = [];
    public List<string> ServiceEndpointPolicies { get; set; } = [];
    public List<string> DelegatedServices { get; set; } = [];
    public List<string> IpamPoolPrefixAllocations { get; set; } = [];
    public string PrivateEndpointNetworkPolicies { get; set; } = "Disabled";
    public string PrivateLinkServiceNetworkPolicies { get; set; } = "Enabled";

    public bool DefaultOutboundAccess
    {
        get => _defaultOutboundAccess ?? false;
        set => _defaultOutboundAccess = value;
    }

    public override Task ProcessAsync() => WriteOutput(new Subnet
    {
        Name = Name,
        Properties = new SubnetProperties
        {
            AddressPrefixes = AddressPrefixes.Count > 0 ? AddressPrefixes : null,
            NetworkSecurityGroup = NetworkSecurityGroupId.Length > 0 ? new SubResource { Id = NetworkSecurityGroupId } : null,
            RouteTable = RouteTableId.Length > 0 ? new SubResource { Id = RouteTableId } : null,
            NatGateway = NatGatewayId.Length > 0 ? new SubResource { Id = NatGatewayId } : null,
            ServiceEndpoints = ServiceEndpoints.Select(service => new ServiceEndpoint { Service = service }).ToList(),
            ServiceEndpointPolicies = ServiceEndpointPolicies.Select(id => new SubResource { Id = id }).ToList(),
            Delegations = DelegatedServices.Select(NewSubnetProcessor_Customization.NormalizeDelegation).Select(service => new Delegation
            {
                Name = service,
                Properties = new DelegationProperties { ServiceName = service }
            }).ToList(),
            IpamPoolPrefixAllocations = IpamPoolPrefixAllocations.Select(id => new IpamPoolPrefixAllocation
            {
                Pool = new SubResource { Id = id }
            }).ToList(),
            PrivateEndpointNetworkPolicies = PrivateEndpointNetworkPolicies,
            PrivateLinkServiceNetworkPolicies = PrivateLinkServiceNetworkPolicies,
            DefaultOutboundAccess = _defaultOutboundAccess
        }
    });
}
