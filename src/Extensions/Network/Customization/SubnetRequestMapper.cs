// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// The subnet inputs that subnet create and subnet update share. A null value means "not given".
/// </summary>
internal interface ISubnetInputs
{
    string SubscriptionId { get; }
    string ResourceGroupName { get; }
    IList<string>? AddressPrefixes { get; }
    string? NetworkSecurityGroup { get; }
    string? RouteTable { get; }
    string? NatGateway { get; }
    IList<string>? ServiceEndpoints { get; }
    IList<string>? Delegations { get; }
    string? PrivateEndpointNetworkPolicies { get; }
    string? PrivateLinkServiceNetworkPolicies { get; }
    bool? DefaultOutboundAccess { get; }
}

/// <summary>
/// Applies the given subnet inputs to a <see cref="Subnet"/> body, so create and update shape the
/// request in the same way. It changes only the properties whose inputs were given and applies no
/// defaults.
/// </summary>
internal static class SubnetRequestMapper
{
    internal static void ApplyGivenInputs(Subnet subnet, ISubnetInputs inputs)
    {
        SingularOrPluralMapper.Apply(
            inputs.AddressPrefixes,
            value => subnet.AddressPrefix = value,
            values => subnet.AddressPrefixes = values);

        if (inputs.NetworkSecurityGroup is not null)
        {
            string? id = ToReferenceId(inputs.NetworkSecurityGroup, inputs, NetworkResourceIdResolver.NetworkSecurityGroups);
            Properties(subnet).NetworkSecurityGroup = id is null ? null : new NetworkSecurityGroup { Id = id };
        }

        if (inputs.RouteTable is not null)
        {
            string? id = ToReferenceId(inputs.RouteTable, inputs, NetworkResourceIdResolver.RouteTables);
            Properties(subnet).RouteTable = id is null ? null : new RouteTable { Id = id };
        }

        if (inputs.NatGateway is not null)
        {
            string? id = ToReferenceId(inputs.NatGateway, inputs, NetworkResourceIdResolver.NatGateways);
            Properties(subnet).NatGateway = id is null ? null : new SubResource { Id = id };
        }

        if (inputs.ServiceEndpoints is not null)
        {
            subnet.ServiceEndpoints = inputs.ServiceEndpoints
                .Select(service => new ServiceEndpointPropertiesFormat { Service = service })
                .ToList();
        }

        if (inputs.Delegations is not null)
        {
            subnet.Delegations = inputs.Delegations
                .Select((serviceName, index) => new Delegation
                {
                    Name = index.ToString(CultureInfo.InvariantCulture),
                    ServiceName = serviceName,
                })
                .ToList();
        }

        if (inputs.PrivateEndpointNetworkPolicies is not null)
        {
            subnet.PrivateEndpointNetworkPolicies = inputs.PrivateEndpointNetworkPolicies;
        }

        if (inputs.PrivateLinkServiceNetworkPolicies is not null)
        {
            subnet.PrivateLinkServiceNetworkPolicies = inputs.PrivateLinkServiceNetworkPolicies;
        }

        if (inputs.DefaultOutboundAccess is not null)
        {
            subnet.DefaultOutboundAccess = inputs.DefaultOutboundAccess;
        }
    }

    /// <summary>
    /// An empty string detaches the reference (null). Any other value is a name or a full ARM ID.
    /// </summary>
    private static string? ToReferenceId(string nameOrId, ISubnetInputs inputs, string resourceType)
        => nameOrId.Length == 0
            ? null
            : NetworkResourceIdResolver.ToResourceId(nameOrId, inputs.SubscriptionId, inputs.ResourceGroupName, resourceType);

    // The references are set on the nested members, not through the generated *Id setters, so the
    // body carries only "id" and a detached reference is omitted instead of sent as "{}".
    private static SubnetPropertiesFormat Properties(Subnet subnet) => subnet.Properties ??= new SubnetPropertiesFormat();
}
