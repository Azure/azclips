// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Adapts the legacy <c>az network nic create</c> / <c>New-AzNetworkInterface</c> surface onto the
/// generated <see cref="CreateOrUpdateNetworkInterfacesProcessor"/>: <c>KEY=VALUE</c> tags, the
/// name-or-id subnet / public IP / network security group arguments, and the single legacy IP
/// configuration with its defaults.
/// </summary>
public class CreateOrUpdateNetworkInterfacesProcessor_Customization : CreateOrUpdateNetworkInterfacesProcessor
{
    public List<string> TagSpecs { get; set; } = [];

    /// <summary>Virtual network that a bare subnet name is resolved against.</summary>
    public string VnetName { get; set; } = string.Empty;

    public string Subnet { get; set; } = string.Empty;
    public string PublicIpAddress { get; set; } = string.Empty;
    public string NetworkSecurityGroup { get; set; } = string.Empty;
    public string IpConfigurationName { get; set; } = string.Empty;
    public string PrivateIpAddress { get; set; } = string.Empty;
    public string PrivateIpAddressVersion { get; set; } = string.Empty;

    // Both legacy surfaces name the single IP configuration `ipconfig1`.
    private const string DefaultIpConfigurationName = "ipconfig1";

    protected override NetworkInterface? CreateRequestBody()
    {
        if (Subnet.Length > 0 && !Subnet.StartsWith('/') && VnetName.Length == 0)
        {
            throw AzclipsErrorException.InvalidInput($"'{Subnet}' is not a subnet resource id; pass --vnet-name to resolve a subnet name.");
        }

        // The legacy CLI accepts a bare name for these and resolves it against the command's
        // resource group; a value that already looks like a resource id is passed through.
        var ipConfiguration = new NetworkInterfaceIPConfiguration
        {
            Name = IpConfigurationName.Length > 0 ? IpConfigurationName : DefaultIpConfigurationName,
            PrivateIPAddress = PrivateIpAddress.Length > 0 ? PrivateIpAddress : null,
            // Both legacy surfaces pin a supplied private IP and otherwise let ARM assign one.
            PrivateIPAllocationMethod = PrivateIpAddress.Length > 0 ? "Static" : "Dynamic",
            PrivateIPAddressVersion = PrivateIpAddressVersion.Length > 0 ? PrivateIpAddressVersion : null
        };
        // The generated reference setters create the referenced object even for null.
        if (Subnet.Length > 0)
        {
            ipConfiguration.SubnetId = ResolveResourceId(Subnet, $"virtualNetworks/{VnetName}/subnets");
        }
        if (PublicIpAddress.Length > 0)
        {
            ipConfiguration.PublicIpAddressId = ResolveResourceId(PublicIpAddress, "publicIPAddresses");
        }
        IpConfigurations = [ipConfiguration];

        if (NetworkSecurityGroup.Length > 0)
        {
            NetworkSecurityGroupId = ResolveResourceId(NetworkSecurityGroup, "networkSecurityGroups");
        }
        if (TagSpecs.Count > 0)
        {
            Tags = KeyValueTokens.Parse(TagSpecs, "--tags", allowBareKey: true).ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        // The legacy surfaces take only the edge zone name; EdgeZone is the only extended location type.
        if (!string.IsNullOrEmpty(ExtendedLocationName))
        {
            ExtendedLocationType ??= "EdgeZone";
        }

        return base.CreateRequestBody();
    }

    private string? ResolveResourceId(string nameOrId, string resourceType) =>
        nameOrId.Length == 0
            ? null
            : nameOrId.StartsWith('/')
                ? nameOrId
                : $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/{resourceType}/{nameOrId}";
}
