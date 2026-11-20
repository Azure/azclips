// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Virtual network create. Adds the location, address space and inline subnet defaults, the DDoS
/// plan name-or-ID, the edge zone and the encryption rule on top of the generated PUT. The request route and LRO polling stay in <see cref="CreateOrUpdateVirtualNetworksProcessor"/>.
/// </summary>
public class CreateVirtualNetworkProcessor_Customization : CreateOrUpdateVirtualNetworksProcessor
{
    private const string DefaultAddressPrefix = "10.0.0.0/16";
    private const int DefaultSubnetMask = 24;

    public IList<string>? AddressPrefixes { get; set; }
    public IList<string>? DnsServers { get; set; }
    public string? SubnetName { get; set; }
    public IList<string>? SubnetPrefixes { get; set; }

    /// <summary>A DDoS protection plan name in the virtual network's resource group, or a full resource ID.</summary>
    public string? DdosProtectionPlan { get; set; }

    public string? BgpCommunity { get; set; }
    public bool? EnableEncryption { get; set; }

    /// <summary>
    /// The encryption enforcement. It is held here, not in the generated <c>EncryptionEnforcement</c>
    /// setter, because that setter creates <c>encryption</c> with <c>enabled = false</c>. It is sent
    /// only together with <see cref="EnableEncryption"/>.
    /// </summary>
    public string? EncryptionEnforcementPolicy { get; set; }

    public string? EdgeZone { get; set; }

    /// <summary>CLI tags in KEY[=VALUE] form. They replace <c>Tags</c> when given.</summary>
    public string[]? TagsInput { get; set; }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        Tags = NetworkTagsParser.Parse(TagsInput) ?? Tags;

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

        return await base.SendAsync();
    }

    protected override VirtualNetwork? CreateRequestBody()
    {
        VirtualNetwork body = base.CreateRequestBody() ?? (Body = new VirtualNetwork());

        IList<string> addressPrefixes = AddressPrefixes is { Count: > 0 } ? AddressPrefixes.ToList() : [DefaultAddressPrefix];
        body.AddressSpaceAddressPrefixes = addressPrefixes;

        if (!string.IsNullOrWhiteSpace(SubnetName))
        {
            body.Subnets = [CreateSubnet(SubnetName, addressPrefixes[0])];
        }

        if (!string.IsNullOrWhiteSpace(DdosProtectionPlan))
        {
            body.DdosProtectionPlanId = NetworkResourceIdResolver.ToResourceId(
                DdosProtectionPlan, SubscriptionId, ResourceGroupName, NetworkResourceIdResolver.DdosProtectionPlans);
        }

        if (!string.IsNullOrWhiteSpace(EdgeZone))
        {
            body.ExtendedLocationName = EdgeZone;
            body.ExtendedLocationType = "EdgeZone";
        }

        if (EnableEncryption is bool enableEncryption)
        {
            body.EncryptionEnabled = enableEncryption;
            if (!string.IsNullOrWhiteSpace(EncryptionEnforcementPolicy))
            {
                body.EncryptionEnforcement = EncryptionEnforcementPolicy;
            }
        }

        if (DnsServers is not null)
        {
            body.DhcpOptionsDnsServers = DnsServers.ToList();
        }

        if (!string.IsNullOrWhiteSpace(BgpCommunity))
        {
            body.BgpCommunitiesVirtualNetworkCommunity = BgpCommunity;
        }

        return body;
    }

    private Subnet CreateSubnet(string subnetName, string firstVirtualNetworkPrefix)
    {
        Subnet subnet = new() { Name = subnetName };
        IList<string> subnetPrefixes = SubnetPrefixes is { Count: > 0 }
            ? SubnetPrefixes
            : [DefaultSubnetPrefix(firstVirtualNetworkPrefix)];
        SingularOrPluralMapper.Apply(
            subnetPrefixes,
            prefix => subnet.AddressPrefix = prefix,
            prefixes => subnet.AddressPrefixes = prefixes);
        return subnet;
    }

    /// <summary>
    /// The address part of the virtual network prefix with mask /24, or with the virtual network's
    /// own mask when it is 24 or longer. This is a string split, not IP math; a malformed prefix is
    /// sent unchanged and rejected by the service.
    /// </summary>
    private static string DefaultSubnetPrefix(string virtualNetworkPrefix)
    {
        string[] parts = virtualNetworkPrefix.Split('/');
        if (parts.Length != 2 || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int mask))
        {
            return virtualNetworkPrefix;
        }

        return $"{parts[0]}/{Math.Max(DefaultSubnetMask, mask).ToString(CultureInfo.InvariantCulture)}";
    }
}
