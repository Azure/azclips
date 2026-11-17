// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Adapts the legacy <c>az network vnet subnet create</c> surface onto the generated
/// <see cref="CreateOrUpdateSubnetsProcessor"/>: name-or-id references, string lists for service
/// endpoints / policies / delegations, <c>--ipam-allocations</c> specs, the <c>--disable-*</c>
/// switches, and the legacy network-policy defaults.
/// </summary>
public class CreateOrUpdateSubnetsProcessor_Customization : CreateOrUpdateSubnetsProcessor
{
    private bool? _disablePrivateEndpointNetworkPolicies;
    private bool? _disablePrivateLinkServiceNetworkPolicies;

    public string NetworkSecurityGroup { get; set; } = string.Empty;
    public string RouteTable { get; set; } = string.Empty;
    public string NatGateway { get; set; } = string.Empty;
    public List<string> ServiceEndpointServices { get; set; } = [];
    public List<string> ServiceEndpointPolicyNames { get; set; } = [];
    public List<string> DelegatedServices { get; set; } = [];
    public List<List<string>> IpamPoolPrefixAllocationSpecs { get; set; } = [];

    public bool DisablePrivateEndpointNetworkPolicies
    {
        get => _disablePrivateEndpointNetworkPolicies ?? false;
        set => _disablePrivateEndpointNetworkPolicies = value;
    }

    public bool DisablePrivateLinkServiceNetworkPolicies
    {
        get => _disablePrivateLinkServiceNetworkPolicies ?? false;
        set => _disablePrivateLinkServiceNetworkPolicies = value;
    }

    public bool DefaultOutboundAccessValue
    {
        get => DefaultOutboundAccess ?? false;
        set => DefaultOutboundAccess = value;
    }

    protected override Subnet? CreateRequestBody()
    {
        if (NetworkSecurityGroup.Length > 0)
        {
            NetworkSecurityGroupId = ResolveResourceId(NetworkSecurityGroup, "networkSecurityGroups");
        }
        if (RouteTable.Length > 0)
        {
            RouteTableId = ResolveResourceId(RouteTable, "routeTables");
        }
        if (NatGateway.Length > 0)
        {
            NatGatewayId = ResolveResourceId(NatGateway, "natGateways");
        }
        if (ServiceEndpointServices.Count > 0)
        {
            ServiceEndpoints = ServiceEndpointServices.Select(service => new ServiceEndpointPropertiesFormat { Service = service }).ToList();
        }
        if (ServiceEndpointPolicyNames.Count > 0)
        {
            ServiceEndpointPolicies = ServiceEndpointPolicyNames
                .Select(value => new ServiceEndpointPolicy { Id = ResolveResourceId(value, "serviceEndpointPolicies") })
                .ToList();
        }
        if (DelegatedServices.Count > 0)
        {
            Delegations = DelegatedServices.Select(service => new Delegation { Name = service, ServiceName = service }).ToList();
        }
        if (IpamPoolPrefixAllocationSpecs.Count > 0)
        {
            IpamPoolPrefixAllocations = IpamPoolPrefixAllocationSpecs.Select(ParseIpamAllocation).ToList();
            AddressPrefixes = null;
        }
        if (_disablePrivateEndpointNetworkPolicies.HasValue)
        {
            PrivateEndpointNetworkPolicies = _disablePrivateEndpointNetworkPolicies.Value ? "Disabled" : "Enabled";
        }
        if (_disablePrivateLinkServiceNetworkPolicies.HasValue)
        {
            PrivateLinkServiceNetworkPolicies = _disablePrivateLinkServiceNetworkPolicies.Value ? "Disabled" : "Enabled";
        }
        // legacy `az network vnet subnet create` defaults.
        PrivateEndpointNetworkPolicies ??= "Disabled";
        PrivateLinkServiceNetworkPolicies ??= "Enabled";

        return base.CreateRequestBody();
    }

    private static IpamPoolPrefixAllocation ParseIpamAllocation(List<string> tokens)
    {
        string? id = null;
        string? numberOfIpAddresses = null;
        foreach (var token in tokens)
        {
            var separator = token.IndexOf('=');
            if (separator < 0)
            {
                id = token;
                continue;
            }

            var key = token[..separator];
            var value = token[(separator + 1)..];
            switch (key)
            {
                case "id": id = value; break;
                case "number-of-ip-addresses": numberOfIpAddresses = value; break;
                default: throw new ArgumentException($"Unknown --ipam-allocations key '{key}'. Supported keys: id, number-of-ip-addresses.");
            }
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Each --ipam-allocations value requires an 'id=<resource-id>' key.");
        }

        return new IpamPoolPrefixAllocation { PoolId = id, NumberOfIpAddresses = numberOfIpAddresses };
    }

    private string ResolveResourceId(string nameOrId, string resourceType) =>
        nameOrId.StartsWith('/')
            ? nameOrId
            : $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/{resourceType}/{nameOrId}";
}
