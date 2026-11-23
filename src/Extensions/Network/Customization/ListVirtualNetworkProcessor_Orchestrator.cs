// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Virtual network list. With a resource group it keeps the <see cref="ListVirtualNetworkProcessor"/>
/// route; without one it runs <see cref="ListAllVirtualNetworkProcessor"/> for the subscription.
/// Both processors follow every page themselves.
/// </summary>
public class ListVirtualNetworkProcessor_Orchestrator : ListVirtualNetworkProcessor
{
    public override async Task ProcessAsync()
    {
        if (!string.IsNullOrWhiteSpace(ResourceGroupName))
        {
            await base.ProcessAsync();
            return;
        }

        await CreateExtendedProcessor<ListAllVirtualNetworkProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
        }).ProcessAsync();
    }
}
