// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Adapts the legacy <c>az network nic create</c> / <c>New-AzNetworkInterface</c> surface onto the
/// generated <see cref="NewNetworkInterfaceProcessor"/>: <c>KEY=VALUE</c> tags, the name-or-id
/// subnet / public IP / network security group arguments, and the legacy IP configuration defaults.
/// The generated processor stays a straight spec-to-body mapping and takes already-resolved ids.
/// </summary>
public class NewNetworkInterfaceProcessor_Customization : NewNetworkInterfaceProcessor
{
    public List<string> TagSpecs { get; set; } = [];

    /// <summary>Virtual network that a bare subnet name is resolved against.</summary>
    public string VnetName { get; set; } = string.Empty;

    // Both legacy surfaces name the single IP configuration `ipconfig1`.
    private const string DefaultIpConfigurationName = "ipconfig1";

    protected override NetworkInterface? CreateRequestBody()
    {
        if (Subnet.Length > 0 && !Subnet.StartsWith('/') && VnetName.Length == 0)
        {
            throw new ArgumentException($"'{Subnet}' is not a subnet resource id; pass --vnet-name to resolve a subnet name.");
        }

        // The legacy CLI accepts a bare name for these and resolves it against the command's
        // resource group; a value that already looks like a resource id is passed through.
        Subnet = ResolveResourceId(Subnet, $"virtualNetworks/{VnetName}/subnets");
        PublicIpAddress = ResolveResourceId(PublicIpAddress, "publicIPAddresses");
        NetworkSecurityGroup = ResolveResourceId(NetworkSecurityGroup, "networkSecurityGroups");

        if (IpConfigurationName.Length == 0)
        {
            IpConfigurationName = DefaultIpConfigurationName;
        }
        if (PrivateIpAllocationMethod.Length == 0)
        {
            // Both legacy surfaces pin a supplied private IP and otherwise let ARM assign one.
            PrivateIpAllocationMethod = PrivateIpAddress.Length > 0 ? "Static" : "Dynamic";
        }
        if (TagSpecs.Count > 0)
        {
            Tags = KeyValueTokens.Parse(TagSpecs, "--tags", allowBareKey: true).ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        return base.CreateRequestBody();
    }

    private string ResolveResourceId(string nameOrId, string resourceType) =>
        nameOrId.Length == 0 || nameOrId.StartsWith('/')
            ? nameOrId
            : $"/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/{resourceType}/{nameOrId}";
}
