// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Virtual network list. With a resource group it keeps the <see cref="ListVirtualNetworksProcessor"/>
/// route; without one it runs <see cref="ListAllVirtualNetworksProcessor"/> for the subscription.
/// Both processors follow every page themselves.
/// </summary>
public class ListVirtualNetworksProcessor_Orchestrator : ListVirtualNetworksProcessor
{
    public override async Task ProcessAsync()
    {
        if (!string.IsNullOrWhiteSpace(ResourceGroupName))
        {
            await base.ProcessAsync();
            return;
        }

        await CreateExtendedProcessor<ListAllVirtualNetworksProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
        }).ProcessAsync();
    }
}
