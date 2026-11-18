// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Shapes the security rule inputs that create and update share: the address and port lists and
/// the application security group (ASG) lists.
/// </summary>
internal static class SecurityRuleRequestMapper
{
    /// <summary>
    /// Puts each given address or port list on its singular property (one item) or plural property
    /// (more items) and clears the other. A list that is not given keeps both properties.
    /// </summary>
    internal static void ApplyAddressAndPortLists(
        SecurityRule rule,
        IList<string>? sourceAddressPrefixes,
        IList<string>? sourcePortRanges,
        IList<string>? destinationAddressPrefixes,
        IList<string>? destinationPortRanges)
    {
        SingularOrPluralMapper.Apply(sourceAddressPrefixes, v => rule.SourceAddressPrefix = v, v => rule.SourceAddressPrefixes = v);
        SingularOrPluralMapper.Apply(sourcePortRanges, v => rule.SourcePortRange = v, v => rule.SourcePortRanges = v);
        SingularOrPluralMapper.Apply(destinationAddressPrefixes, v => rule.DestinationAddressPrefix = v, v => rule.DestinationAddressPrefixes = v);
        SingularOrPluralMapper.Apply(destinationPortRanges, v => rule.DestinationPortRange = v, v => rule.DestinationPortRanges = v);
    }

    /// <summary>
    /// Turns ASG names or IDs into entries that carry only <c>id</c>, in input order. A name becomes an
    /// ID in the given subscription and resource group, with no API call.
    /// </summary>
    internal static IList<ApplicationSecurityGroup> ToApplicationSecurityGroups(
        IList<string> namesOrIds,
        string subscriptionId,
        string resourceGroupName)
        => namesOrIds
            .Select(nameOrId => new ApplicationSecurityGroup
            {
                Id = NetworkResourceIdResolver.ToResourceId(
                    nameOrId,
                    subscriptionId,
                    resourceGroupName,
                    NetworkResourceIdResolver.ApplicationSecurityGroups),
            })
            .ToList();
}
