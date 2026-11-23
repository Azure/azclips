// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Network security group list. With a resource group it keeps the <see cref="ListNetworkSecurityGroupProcessor"/>
/// route; without one it runs <see cref="ListAllNetworkSecurityGroupProcessor"/> for the subscription.
/// Both processors follow every page themselves.
/// </summary>
public class ListNetworkSecurityGroupProcessor_Orchestrator : ListNetworkSecurityGroupProcessor
{
    public override async Task ProcessAsync()
    {
        if (!string.IsNullOrWhiteSpace(ResourceGroupName))
        {
            await base.ProcessAsync();
            return;
        }

        await CreateExtendedProcessor<ListAllNetworkSecurityGroupProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
        }).ProcessAsync();
    }
}
