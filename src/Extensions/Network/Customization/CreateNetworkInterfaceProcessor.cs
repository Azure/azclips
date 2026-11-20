// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Network interface create. Resolves the subnet, public IP and NSG references, defaults the
/// location, and builds one primary IP configuration. The request route and LRO polling
/// stay in <see cref="CreateOrUpdateNetworkInterfacesProcessor"/>.
/// </summary>
public class CreateNetworkInterfaceProcessor : CreateOrUpdateNetworkInterfacesProcessor
{
    private const string DefaultIpConfigurationName = "ipconfig1";
    private const string DefaultPrivateIpAddressVersion = "IPv4";
    private const string StaticAllocation = "Static";
    private const string DynamicAllocation = "Dynamic";

    /// <summary>A subnet name (needs <see cref="VirtualNetworkName"/>) or a full subnet resource ID.</summary>
    public string? Subnet { get; set; }

    /// <summary>The virtual network of the subnet when <see cref="Subnet"/> is a name.</summary>
    public string? VirtualNetworkName { get; set; }

    public string? IpConfigurationName { get; set; }
    public string? PrivateIpAddress { get; set; }
    public string? PrivateIpAddressVersion { get; set; }

    /// <summary>A public IP address name in the NIC's resource group, or a full resource ID.</summary>
    public string? PublicIpAddress { get; set; }

    /// <summary>A network security group name in the NIC's resource group, or a full resource ID.</summary>
    public string? NetworkSecurityGroup { get; set; }

    public string? EdgeZone { get; set; }

    /// <summary>CLI tags in KEY[=VALUE] form. They replace <c>Tags</c> when given.</summary>
    public string[]? TagsInput { get; set; }

    protected override async Task BeforeProcessAsync()
    {
        await base.BeforeProcessAsync();

        Tags = NetworkTagsParser.Parse(TagsInput) ?? Tags;

        string? subnetId = ResolveSubnetId();
        string? publicIpAddressId = ResolveId(PublicIpAddress, NetworkResourceIdResolver.PublicIPAddresses);
        string? networkSecurityGroupId = ResolveId(NetworkSecurityGroup, NetworkResourceIdResolver.NetworkSecurityGroups);

        if (string.IsNullOrWhiteSpace(Location))
        {
            Location = await ResourceGroupLocationResolver.GetLocationAsync(
                CreateExtendedProcessor<GetResourceGroupsProcessor>(p =>
                {
                    p.SubscriptionId = SubscriptionId;
                    p.ResourceGroupName = ResourceGroupName;
                }),
                Publisher,
                JsonOptions);
        }

        bool privateIpGiven = !string.IsNullOrWhiteSpace(PrivateIpAddress);
        NetworkInterfaceIPConfiguration ipConfiguration = new()
        {
            Name = string.IsNullOrWhiteSpace(IpConfigurationName) ? DefaultIpConfigurationName : IpConfigurationName,
            Primary = true,
            SubnetId = subnetId,
            PrivateIPAddress = privateIpGiven ? PrivateIpAddress : null,
            PrivateIPAllocationMethod = privateIpGiven ? StaticAllocation : DynamicAllocation,
            PrivateIPAddressVersion = string.IsNullOrWhiteSpace(PrivateIpAddressVersion) ? DefaultPrivateIpAddressVersion : PrivateIpAddressVersion,
        };

        if (publicIpAddressId is not null)
        {
            ipConfiguration.PublicIPAddress = new PublicIPAddress { Id = publicIpAddressId };
        }

        IpConfigurations = [ipConfiguration];

        if (networkSecurityGroupId is not null)
        {
            NetworkSecurityGroupId = networkSecurityGroupId;
        }

        if (!string.IsNullOrWhiteSpace(EdgeZone))
        {
            ExtendedLocationName = EdgeZone;
            ExtendedLocationType = "EdgeZone";
        }
    }

    private string? ResolveSubnetId()
    {
        if (string.IsNullOrWhiteSpace(Subnet))
        {
            return null;
        }

        if (NetworkResourceIdResolver.IsResourceId(Subnet))
        {
            return Subnet;
        }

        if (string.IsNullOrWhiteSpace(VirtualNetworkName))
        {
            throw AzclipsErrorException.InvalidInput(
                $"Subnet '{Subnet}' is a name, so VirtualNetworkName is required. Give VirtualNetworkName or a full subnet resource ID.");
        }

        return NetworkResourceIdResolver.ToSubnetId(SubscriptionId, ResourceGroupName, VirtualNetworkName, Subnet);
    }

    private string? ResolveId(string? nameOrId, string resourceType)
        => string.IsNullOrWhiteSpace(nameOrId)
            ? null
            : NetworkResourceIdResolver.ToResourceId(nameOrId, SubscriptionId, ResourceGroupName, resourceType);
}
