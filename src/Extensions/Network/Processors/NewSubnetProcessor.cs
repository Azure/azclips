// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

public class NewSubnetProcessor : NetworkProcessorBase<Subnet, Subnet>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string VirtualNetworkName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public List<string> AddressPrefixes { get; set; } = [];
    public string NetworkSecurityGroup { get; set; } = string.Empty;
    public string RouteTable { get; set; } = string.Empty;
    public string NatGateway { get; set; } = string.Empty;
    public List<string> ServiceEndpoints { get; set; } = [];
    public List<string> ServiceEndpointPolicies { get; set; } = [];
    public List<string> DelegatedServices { get; set; } = [];
    public List<IpamPoolPrefixAllocation> IpamPoolPrefixAllocations { get; set; } = [];
    public string PrivateEndpointNetworkPolicies { get; set; } = "Disabled";
    public string PrivateLinkServiceNetworkPolicies { get; set; } = "Enabled";
    public string SharingScope { get; set; } = string.Empty;
    public bool? DefaultOutboundAccess { get; set; }

    protected override HttpMethod Method => HttpMethod.Put;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/virtualNetworks/{VirtualNetworkName}/subnets/{Name}?api-version={ApiVersion}";

    protected override Subnet? CreateRequestBody() => new()
    {
        Name = Name,
        Properties = new SubnetProperties
        {
            AddressPrefixes = AddressPrefixes.Count > 0 ? AddressPrefixes : null,
            NetworkSecurityGroup = NetworkSecurityGroup.Length > 0 ? new SubResource { Id = NetworkSecurityGroup } : null,
            RouteTable = RouteTable.Length > 0 ? new SubResource { Id = RouteTable } : null,
            NatGateway = NatGateway.Length > 0 ? new SubResource { Id = NatGateway } : null,
            ServiceEndpoints = ServiceEndpoints.Select(service => new ServiceEndpoint { Service = service }).ToList(),
            ServiceEndpointPolicies = ServiceEndpointPolicies.Select(id => new SubResource { Id = id }).ToList(),
            Delegations = DelegatedServices.Select(service => new Delegation
            {
                Name = service,
                Properties = new DelegationProperties { ServiceName = service }
            }).ToList(),
            IpamPoolPrefixAllocations = IpamPoolPrefixAllocations,
            PrivateEndpointNetworkPolicies = PrivateEndpointNetworkPolicies,
            PrivateLinkServiceNetworkPolicies = PrivateLinkServiceNetworkPolicies,
            SharingScope = SharingScope.Length > 0 ? SharingScope : null,
            DefaultOutboundAccess = DefaultOutboundAccess
        }
    };

    protected override Func<HttpResponseMessage, string?> GetFinalUri => _ => null;
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
}
