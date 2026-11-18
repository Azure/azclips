// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Security rule create. Sets the body name, applies the create defaults, maps the address and port
/// lists, and builds the ASG lists. The user's lists bind to the inherited plural setters. The
/// request route and LRO polling stay in <see cref="CreateOrUpdateSecurityRulesProcessor"/>.
/// </summary>
public class CreateSecurityRuleProcessor_Customization : CreateOrUpdateSecurityRulesProcessor
{
    private const string DefaultAccess = "Allow";
    private const string DefaultDirection = "Inbound";
    private const string DefaultProtocol = "*";
    private const string DefaultSourcePortRange = "*";
    private const string DefaultDestinationPortRange = "80";
    private const string DefaultAddressPrefix = "*";

    /// <summary>Source application security group names or full resource IDs.</summary>
    public IList<string>? SourceAsgs { get; set; }

    /// <summary>Destination application security group names or full resource IDs.</summary>
    public IList<string>? DestinationAsgs { get; set; }

    protected override async Task BeforeProcessAsync()
    {
        await base.BeforeProcessAsync();

        SecurityRule rule = Body ??= new SecurityRule();
        rule.Name = SecurityRuleName;
        rule.Access ??= DefaultAccess;
        rule.Direction ??= DefaultDirection;
        rule.Protocol ??= DefaultProtocol;

        bool sourceAsgsGiven = SourceAsgs is { Count: > 0 };
        bool destinationAsgsGiven = DestinationAsgs is { Count: > 0 };
        bool sourcePrefixesGiven = rule.SourceAddressPrefixes is { Count: > 0 };
        bool destinationPrefixesGiven = rule.DestinationAddressPrefixes is { Count: > 0 };
        bool sourcePortsGiven = rule.SourcePortRanges is { Count: > 0 };
        bool destinationPortsGiven = rule.DestinationPortRanges is { Count: > 0 };

        SecurityRuleRequestMapper.ApplyAddressAndPortLists(
            rule,
            rule.SourceAddressPrefixes,
            rule.SourcePortRanges,
            rule.DestinationAddressPrefixes,
            rule.DestinationPortRanges);

        if (!sourcePortsGiven)
        {
            rule.SourcePortRanges = null;
            rule.SourcePortRange ??= DefaultSourcePortRange;
        }

        if (!destinationPortsGiven)
        {
            rule.DestinationPortRanges = null;
            rule.DestinationPortRange ??= DefaultDestinationPortRange;
        }

        if (sourceAsgsGiven)
        {
            rule.SourceApplicationSecurityGroups = SecurityRuleRequestMapper.ToApplicationSecurityGroups(SourceAsgs!, SubscriptionId, ResourceGroupName);
        }
        else if (!sourcePrefixesGiven)
        {
            rule.SourceAddressPrefixes = null;
            rule.SourceAddressPrefix ??= DefaultAddressPrefix;
        }

        if (destinationAsgsGiven)
        {
            rule.DestinationApplicationSecurityGroups = SecurityRuleRequestMapper.ToApplicationSecurityGroups(DestinationAsgs!, SubscriptionId, ResourceGroupName);
        }
        else if (!destinationPrefixesGiven)
        {
            rule.DestinationAddressPrefixes = null;
            rule.DestinationAddressPrefix ??= DefaultAddressPrefix;
        }
    }
}
