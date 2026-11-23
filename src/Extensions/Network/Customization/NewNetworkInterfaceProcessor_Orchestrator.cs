// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Shapes the <see cref="NewNetworkInterfaceProcessor"/> request body behind network interface create:
/// one IP configuration from flat inputs, subnet / public IP address / network security group name-or-ID expansion,
/// and edge zone. The route, method, and api-version stay in the atomic processor.
/// </summary>
public class NewNetworkInterfaceProcessor_Orchestrator : NewNetworkInterfaceProcessor
{
    internal const string DefaultIpConfigurationName = "ipconfig1";
    internal const string StaticAllocation = "Static";
    internal const string DynamicAllocation = "Dynamic";
    internal const string EdgeZoneType = "EdgeZone";

    /// <summary>Subnet ID, or subnet name together with <see cref="VirtualNetworkName"/>.</summary>
    public string? SubnetId { get; set; }

    /// <summary>Virtual network name used to expand a subnet name into a subnet ID.</summary>
    public string? VirtualNetworkName { get; set; }

    /// <summary>Public IP address ID or name.</summary>
    public string? PublicIpAddressId { get; set; }

    /// <summary>Static private IP address. Without it the allocation is dynamic.</summary>
    public string? PrivateIpAddress { get; set; }

    /// <summary>Private IP address version (IPv4 or IPv6). Sent only when given.</summary>
    public string? PrivateIpAddressVersion { get; set; }

    /// <summary>Name of the single IP configuration.</summary>
    public string IpConfigurationName { get; set; } = DefaultIpConfigurationName;

    /// <summary>Edge zone to create the network interface in.</summary>
    public string? EdgeZone { get; set; }

    /// <summary>
    /// Shaping runs here, not in BeforeProcessAsync, so it also applies when this processor runs
    /// as a child of another processor. Every step is idempotent because the body can be built more than once.
    /// </summary>
    protected override NetworkInterface? CreateRequestBody()
    {
        NetworkSecurityGroupId = ToNetworkResourceId(NetworkSecurityGroupId, "networkSecurityGroups");
        IpConfigurations = [BuildIpConfiguration()];
        ApplyEdgeZone();
        return base.CreateRequestBody();
    }

    private NetworkInterfaceIPConfiguration BuildIpConfiguration()
    {
        var ipConfiguration = new NetworkInterfaceIPConfiguration
        {
            Name = string.IsNullOrWhiteSpace(IpConfigurationName) ? DefaultIpConfigurationName : IpConfigurationName,
        };

        string? subnetId = ToSubnetId(SubnetId);
        if (!string.IsNullOrWhiteSpace(subnetId))
        {
            ipConfiguration.SubnetId = subnetId;
        }

        string? publicIpAddressId = ToNetworkResourceId(PublicIpAddressId, "publicIPAddresses");
        if (!string.IsNullOrWhiteSpace(publicIpAddressId))
        {
            ipConfiguration.PublicIPAddress = new PublicIPAddress { Id = publicIpAddressId };
        }

        if (!string.IsNullOrWhiteSpace(PrivateIpAddress))
        {
            ipConfiguration.PrivateIPAddress = PrivateIpAddress;
            ipConfiguration.PrivateIPAllocationMethod = StaticAllocation;
        }
        else
        {
            ipConfiguration.PrivateIPAllocationMethod = DynamicAllocation;
        }

        if (!string.IsNullOrWhiteSpace(PrivateIpAddressVersion))
        {
            ipConfiguration.PrivateIPAddressVersion = PrivateIpAddressVersion;
        }

        return ipConfiguration;
    }

    private void ApplyEdgeZone()
    {
        if (!string.IsNullOrWhiteSpace(EdgeZone))
        {
            ExtendedLocationName = EdgeZone;
            ExtendedLocationType = EdgeZoneType;
        }
        else if (Body is not null)
        {
            Body.ExtendedLocation = null;
        }
    }

    // A subnet name without a virtual network name is passed through unchanged; the spec states no behavior for it.
    private string? ToSubnetId(string? nameOrId)
    {
        if (string.IsNullOrWhiteSpace(nameOrId) || nameOrId.StartsWith('/') || string.IsNullOrWhiteSpace(VirtualNetworkName))
        {
            return nameOrId;
        }

        return ToNetworkResourceId($"{VirtualNetworkName}/subnets/{nameOrId}", "virtualNetworks");
    }

    private string? ToNetworkResourceId(string? nameOrId, string resourceType)
    {
        if (string.IsNullOrWhiteSpace(nameOrId) || nameOrId.StartsWith('/'))
        {
            return nameOrId;
        }

        return $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/{resourceType}/{nameOrId}";
    }
}
