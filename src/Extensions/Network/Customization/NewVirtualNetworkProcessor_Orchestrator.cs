// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Shapes the <see cref="NewVirtualNetworkProcessor"/> request body behind virtual network create:
/// default address prefix, IPAM allocations, edge zone, inline subnets, and IP allocation IDs.
/// The route, method, and api-version stay in the atomic processor.
/// </summary>
public class NewVirtualNetworkProcessor_Orchestrator : NewVirtualNetworkProcessor
{
    internal const string DefaultAddressPrefix = "10.0.0.0/16";
    internal const string EdgeZoneType = "EdgeZone";

    /// <summary>Name of one subnet to create with the virtual network.</summary>
    public string? SubnetName { get; set; }

    /// <summary>Address prefixes of the subnet named by <see cref="SubnetName"/>.</summary>
    public IList<string>? SubnetPrefixes { get; set; }

    /// <summary>Resource IDs of IP allocations that reference this virtual network.</summary>
    public IList<string>? IpAllocationIds { get; set; }

    /// <summary>
    /// Shaping runs here, not in BeforeProcessAsync, so it also applies when this processor runs
    /// as a child of another processor. Every step is idempotent because the body can be built more than once.
    /// </summary>
    protected override VirtualNetwork? CreateRequestBody()
    {
        ApplyAddressSpace();
        ApplyEdgeZone();
        ApplySubnets();
        ApplyIpAllocations();
        return base.CreateRequestBody();
    }

    private void ApplyAddressSpace()
    {
        if (AddressSpaceIpamPoolPrefixAllocations is { Count: > 0 })
        {
            AddressSpaceAddressPrefixes = null;
        }
        else if (AddressSpaceAddressPrefixes is not { Count: > 0 })
        {
            AddressSpaceAddressPrefixes = new List<string> { DefaultAddressPrefix };
        }
    }

    private void ApplyEdgeZone()
    {
        if (!string.IsNullOrWhiteSpace(ExtendedLocationName))
        {
            ExtendedLocationType = EdgeZoneType;
        }
        else if (Body is not null)
        {
            Body.ExtendedLocation = null;
        }
    }

    private void ApplySubnets()
    {
        if (!string.IsNullOrWhiteSpace(SubnetName)
            && Subnets?.Any(subnet => string.Equals(subnet.Name, SubnetName, StringComparison.OrdinalIgnoreCase)) != true)
        {
            var subnet = new Subnet { Name = SubnetName };
            if (SubnetPrefixes is { Count: 1 })
            {
                subnet.AddressPrefix = SubnetPrefixes[0];
            }
            else if (SubnetPrefixes is { Count: > 1 })
            {
                subnet.AddressPrefixes = SubnetPrefixes.ToList();
            }

            var subnets = Subnets is null ? new List<Subnet>() : Subnets.ToList();
            subnets.Add(subnet);
            Subnets = subnets;
        }

        if (Subnets is { Count: 0 })
        {
            Subnets = null;
        }
    }

    private void ApplyIpAllocations()
    {
        if (IpAllocationIds is { Count: > 0 })
        {
            IpAllocations = IpAllocationIds.Select(id => new SubResource { Id = id }).ToList();
        }
    }
}
