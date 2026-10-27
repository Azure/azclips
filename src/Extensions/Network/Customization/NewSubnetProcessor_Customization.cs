// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

public class NewSubnetProcessor_Customization : NewSubnetProcessor
{
    private bool? _disablePrivateEndpointNetworkPolicies;
    private bool? _disablePrivateLinkServiceNetworkPolicies;

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
        get => base.DefaultOutboundAccess ?? false;
        set => base.DefaultOutboundAccess = value;
    }

    protected override Subnet? CreateRequestBody()
    {
        NetworkSecurityGroup = ResolveResourceId(NetworkSecurityGroup, "networkSecurityGroups");
        RouteTable = ResolveResourceId(RouteTable, "routeTables");
        NatGateway = ResolveResourceId(NatGateway, "natGateways");
        ServiceEndpointPolicies = ServiceEndpointPolicies
            .Select(value => ResolveResourceId(value, "serviceEndpointPolicies"))
            .ToList();
        DelegatedServices = DelegatedServices.Select(NormalizeDelegation).ToList();
        IpamPoolPrefixAllocations = IpamPoolPrefixAllocationSpecs.Select(ParseIpamAllocation).ToList();
        if (IpamPoolPrefixAllocations.Count > 0)
        {
            AddressPrefixes = [];
        }
        if (_disablePrivateEndpointNetworkPolicies.HasValue)
        {
            PrivateEndpointNetworkPolicies = _disablePrivateEndpointNetworkPolicies.Value ? "Disabled" : "Enabled";
        }
        if (_disablePrivateLinkServiceNetworkPolicies.HasValue)
        {
            PrivateLinkServiceNetworkPolicies = _disablePrivateLinkServiceNetworkPolicies.Value ? "Disabled" : "Enabled";
        }

        return base.CreateRequestBody();
    }

    // az CLI accepts the dotted form (Microsoft.Web.serverFarms); ARM requires Microsoft.Web/serverFarms.
    internal static string NormalizeDelegation(string service)
    {
        if (service.Contains('/'))
        {
            return service;
        }
        var lastDot = service.LastIndexOf('.');
        return lastDot > 0 && service.IndexOf('.') < lastDot
            ? $"{service[..lastDot]}/{service[(lastDot + 1)..]}"
            : service;
    }

    // A bare token is the pool id (legacy positional form); repeated keys follow last-wins.
    private static IpamPoolPrefixAllocation ParseIpamAllocation(List<string> tokens)
    {
        string? id = null;
        string? numberOfIpAddresses = null;
        var normalized = tokens.Select(token => token.Contains('=') || string.IsNullOrWhiteSpace(token) ? token : $"id={token}");
        foreach (var (key, value) in KeyValueTokens.Parse(normalized, "--ipam-allocations"))
        {
            switch (key)
            {
                case "id": id = value; break;
                case "number-of-ip-addresses": numberOfIpAddresses = value; break;
                default: throw AzclipsErrorException.InvalidInput($"Unknown --ipam-allocations key '{key}'. Supported keys: id, number-of-ip-addresses.");
            }
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            throw AzclipsErrorException.InvalidInput("Each --ipam-allocations value requires an 'id=<resource-id>' key.");
        }

        return new IpamPoolPrefixAllocation
        {
            Pool = new SubResource { Id = id },
            NumberOfIpAddresses = numberOfIpAddresses
        };
    }

    private string ResolveResourceId(string nameOrId, string resourceType) =>
        nameOrId.Length == 0 || nameOrId.StartsWith('/')
            ? nameOrId
            : $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/{resourceType}/{nameOrId}";
}
