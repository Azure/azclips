// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Shapes the <see cref="CreateOrUpdateSubnetsProcessor"/> request body behind subnet create:
/// single or plural address prefix, IPAM allocations, service endpoints and policies,
/// delegations, referenced resource IDs, and network policy defaults.
/// The route, method, and api-version stay in the atomic processor.
/// </summary>
public class CreateOrUpdateSubnetsProcessor_Orchestrator : CreateOrUpdateSubnetsProcessor
{
    internal const string Enabled = "Enabled";
    internal const string Disabled = "Disabled";

    private const string IpamIdKey = "id";
    private const string IpamNumberOfIpAddressesKey = "number-of-ip-addresses";

    /// <summary>IPAM allocations as <c>id=&lt;pool-id&gt; [number-of-ip-addresses=&lt;n&gt;]</c> or a bare pool ID.</summary>
    public IList<string>? IpamAllocations { get; set; }

    /// <summary>Service names for service endpoints, for example <c>Microsoft.Storage</c>.</summary>
    public IList<string>? ServiceEndpointNames { get; set; }

    /// <summary>Names or resource IDs of service endpoint policies.</summary>
    public IList<string>? ServiceEndpointPolicyIds { get; set; }

    /// <summary>Services to delegate the subnet to, for example <c>Microsoft.Web/serverFarms</c>.</summary>
    public IList<string>? DelegationValues { get; set; }

    /// <summary>Deprecated. True sends private endpoint network policies <c>Disabled</c>; false sends <c>Enabled</c>.</summary>
    public bool? DisablePrivateEndpointNetworkPolicies { get; set; }

    /// <summary>Deprecated. True sends private link service network policies <c>Disabled</c>; false sends <c>Enabled</c>.</summary>
    public bool? DisablePrivateLinkServiceNetworkPolicies { get; set; }

    protected override async Task BeforeProcessAsync()
    {
        await base.BeforeProcessAsync();

        if (DisablePrivateEndpointNetworkPolicies.HasValue)
        {
            await PublishDeprecationWarningAsync("--disable-private-endpoint-network-policies", "--private-endpoint-network-policies");
        }

        if (DisablePrivateLinkServiceNetworkPolicies.HasValue)
        {
            await PublishDeprecationWarningAsync("--disable-private-link-service-network-policies", "--private-link-service-network-policies");
        }
    }

    /// <summary>
    /// Shaping runs here, not in BeforeProcessAsync, so it also applies when this processor runs
    /// as a child of another processor. Every step is idempotent because the body can be built more than once.
    /// </summary>
    protected override Subnet? CreateRequestBody()
    {
        ApplyAddressPrefixes();
        ApplyIpamAllocations();
        ApplyServiceEndpoints();
        ApplyServiceEndpointPolicies();
        ApplyDelegations();
        ApplyReferencedResources();
        ApplyNetworkPolicies();
        return base.CreateRequestBody();
    }

    private void ApplyAddressPrefixes()
    {
        if (AddressPrefixes is { Count: 1 })
        {
            AddressPrefix = AddressPrefixes[0];
            AddressPrefixes = null;
        }
        else if (AddressPrefixes is { Count: > 1 })
        {
            AddressPrefix = null;
        }
        else if (AddressPrefixes is { Count: 0 })
        {
            AddressPrefixes = null;
        }
    }

    private void ApplyIpamAllocations()
    {
        if (IpamAllocations is not { Count: > 0 })
        {
            return;
        }

        var allocations = new List<IpamPoolPrefixAllocation>();
        IpamPoolPrefixAllocation? current = null;
        var tokens = IpamAllocations.SelectMany(value => value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        foreach (string token in tokens)
        {
            int separator = token.IndexOf('=');
            if (separator < 0)
            {
                current = new IpamPoolPrefixAllocation { PoolId = token };
                allocations.Add(current);
                continue;
            }

            string key = token[..separator].Trim();
            string value = token[(separator + 1)..].Trim();
            if (string.Equals(key, IpamIdKey, StringComparison.OrdinalIgnoreCase))
            {
                current = new IpamPoolPrefixAllocation { PoolId = value };
                allocations.Add(current);
            }
            else if (string.Equals(key, IpamNumberOfIpAddressesKey, StringComparison.OrdinalIgnoreCase))
            {
                if (current is null)
                {
                    throw AzclipsErrorException.UsageError(
                        $"IPAM allocation value '{token}' must follow '{IpamIdKey}=<pool-id>'.");
                }

                current.NumberOfIpAddresses = value;
            }
            else
            {
                throw AzclipsErrorException.UsageError(
                    $"Unknown IPAM allocation key '{key}'. Valid keys: {IpamIdKey}, {IpamNumberOfIpAddressesKey}.");
            }
        }

        IpamPoolPrefixAllocations = allocations;
    }

    private void ApplyServiceEndpoints()
    {
        if (ServiceEndpointNames is { Count: > 0 })
        {
            ServiceEndpoints = ServiceEndpointNames
                .Select(service => new ServiceEndpointPropertiesFormat { Service = service })
                .ToList();
        }
    }

    private void ApplyServiceEndpointPolicies()
    {
        if (ServiceEndpointPolicyIds is { Count: > 0 })
        {
            ServiceEndpointPolicies = ServiceEndpointPolicyIds
                .Select(value => new ServiceEndpointPolicy { Id = ToResourceId(value, "serviceEndpointPolicies") })
                .ToList();
        }
    }

    private void ApplyDelegations()
    {
        if (DelegationValues is { Count: > 0 })
        {
            Delegations = DelegationValues
                .Select(value => new Delegation { Name = value, ServiceName = NormalizeServiceName(value) })
                .ToList();
        }
    }

    private void ApplyReferencedResources()
    {
        NetworkSecurityGroupId = ToResourceId(NetworkSecurityGroupId, "networkSecurityGroups");
        RouteTableId = ToResourceId(RouteTableId, "routeTables");
        NatGatewayId = ToResourceId(NatGatewayId, "natGateways");
    }

    private void ApplyNetworkPolicies()
    {
        if (DisablePrivateEndpointNetworkPolicies.HasValue)
        {
            PrivateEndpointNetworkPolicies = DisablePrivateEndpointNetworkPolicies.Value ? Disabled : Enabled;
        }

        if (DisablePrivateLinkServiceNetworkPolicies.HasValue)
        {
            PrivateLinkServiceNetworkPolicies = DisablePrivateLinkServiceNetworkPolicies.Value ? Disabled : Enabled;
        }

        PrivateEndpointNetworkPolicies ??= Disabled;
        PrivateLinkServiceNetworkPolicies ??= Enabled;
    }

    /// <summary>
    /// Returns a value that starts with <c>/</c> unchanged; otherwise builds the Microsoft.Network
    /// resource ID from the command subscription and resource group.
    /// </summary>
    private string? ToResourceId(string? nameOrId, string resourceType)
    {
        if (string.IsNullOrWhiteSpace(nameOrId) || nameOrId.StartsWith('/'))
        {
            return nameOrId;
        }

        return $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/{resourceType}/{nameOrId}";
    }

    /// <summary>Turns <c>Microsoft.Sql.servers</c> into <c>Microsoft.Sql/servers</c>; other values stay unchanged.</summary>
    private static string NormalizeServiceName(string value)
    {
        if (value.Contains('/'))
        {
            return value;
        }

        string[] parts = value.Split('.');
        return parts.Length == 3 && parts.All(part => part.Length > 0)
            ? $"Microsoft.{parts[1]}/{parts[2]}"
            : value;
    }

    private Task PublishDeprecationWarningAsync(string deprecatedOption, string replacementOption) =>
        Publisher.Publish(Events.Warning, new EventData
        {
            Message = $"Option '{deprecatedOption}' has been deprecated and will be removed in a future release. Use '{replacementOption}' instead."
        });
}
