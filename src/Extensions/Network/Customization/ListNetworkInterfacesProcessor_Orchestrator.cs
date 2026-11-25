// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Network interface list. With a resource group it keeps the <see cref="ListNetworkInterfacesProcessor"/>
/// route; without one it runs <see cref="ListAllNetworkInterfacesProcessor"/> for the subscription.
/// Both processors follow every page themselves.
/// </summary>
public class ListNetworkInterfacesProcessor_Orchestrator : ListNetworkInterfacesProcessor
{
    public override async Task ProcessAsync()
    {
        if (!string.IsNullOrWhiteSpace(ResourceGroupName))
        {
            await base.ProcessAsync();
            return;
        }

        await CreateExtendedProcessor<ListAllNetworkInterfacesProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
        }).ProcessAsync();
    }
}
