// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Security rule update: GET the rule, apply only the given inputs, then PUT it back. The inherited
/// setters collect the given inputs (null means "not given"). The GET route comes from
/// <see cref="GetSecurityRulesProcessor"/>; the PUT route and LRO stay in
/// <see cref="CreateOrUpdateSecurityRulesProcessor"/>. No create defaults are applied.
/// </summary>
public class UpdateSecurityRuleProcessor_Customization : CreateOrUpdateSecurityRulesProcessor
{
    /// <summary>
    /// The rule priority, or null when it is not given. Bound instead of the inherited
    /// <c>Priority</c>, because the model stores priority as a non-nullable int and so turns
    /// "not given" into 0.
    /// </summary>
    public int? GivenPriority { get; set; }

    /// <summary>Source application security group names or full resource IDs. Replaces the source list.</summary>
    public IList<string>? SourceAsgs { get; set; }

    /// <summary>Destination application security group names or full resource IDs. Replaces the destination list.</summary>
    public IList<string>? DestinationAsgs { get; set; }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        SecurityRule? given = Body;

        GetSecurityRulesProcessor get = CreateExtendedProcessor<GetSecurityRulesProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.ResourceGroupName = ResourceGroupName;
            p.NetworkSecurityGroupName = NetworkSecurityGroupName;
            p.SecurityRuleName = SecurityRuleName;
        });
        SecurityRule rule = await ChildResourceReader.ReadAsync<SecurityRule>(get, Publisher, JsonOptions);

        if (given is not null)
        {
            ApplyGivenScalars(rule, given);
            SecurityRuleRequestMapper.ApplyAddressAndPortLists(
                rule,
                given.SourceAddressPrefixes,
                given.SourcePortRanges,
                given.DestinationAddressPrefixes,
                given.DestinationPortRanges);
        }

        if (GivenPriority is not null)
        {
            rule.Priority = GivenPriority;
        }

        if (SourceAsgs is not null)
        {
            rule.SourceApplicationSecurityGroups = SecurityRuleRequestMapper.ToApplicationSecurityGroups(SourceAsgs, SubscriptionId, ResourceGroupName);
        }

        if (DestinationAsgs is not null)
        {
            rule.DestinationApplicationSecurityGroups = SecurityRuleRequestMapper.ToApplicationSecurityGroups(DestinationAsgs, SubscriptionId, ResourceGroupName);
        }

        Body = rule;
        return await base.SendAsync();
    }

    private static void ApplyGivenScalars(SecurityRule rule, SecurityRule given)
    {
        if (given.Access is not null)
        {
            rule.Access = given.Access;
        }

        if (given.Direction is not null)
        {
            rule.Direction = given.Direction;
        }

        if (given.Protocol is not null)
        {
            rule.Protocol = given.Protocol;
        }

        if (given.Description is not null)
        {
            rule.Description = given.Description;
        }
    }
}
