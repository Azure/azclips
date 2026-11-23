// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Network interface list. With a resource group it keeps the <see cref="ListNetworkInterfaceProcessor"/>
/// route; without one it runs <see cref="ListAllNetworkInterfaceProcessor"/> for the subscription.
/// Both processors follow every page themselves.
/// </summary>
public class ListNetworkInterfaceProcessor_Orchestrator : ListNetworkInterfaceProcessor
{
    public override async Task ProcessAsync()
    {
        if (!string.IsNullOrWhiteSpace(ResourceGroupName))
        {
            await base.ProcessAsync();
            return;
        }

        await CreateExtendedProcessor<ListAllNetworkInterfaceProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
        }).ProcessAsync();
    }
}
