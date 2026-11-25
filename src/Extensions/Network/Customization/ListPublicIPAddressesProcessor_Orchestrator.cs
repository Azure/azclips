// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Public IP address list. With a resource group it keeps the <see cref="ListPublicIPAddressesProcessor"/>
/// route; without one it runs <see cref="ListAllPublicIPAddressesProcessor"/> for the subscription.
/// Both processors follow every page themselves.
/// </summary>
public class ListPublicIPAddressesProcessor_Orchestrator : ListPublicIPAddressesProcessor
{
    public override async Task ProcessAsync()
    {
        if (!string.IsNullOrWhiteSpace(ResourceGroupName))
        {
            await base.ProcessAsync();
            return;
        }

        await CreateExtendedProcessor<ListAllPublicIPAddressesProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
        }).ProcessAsync();
    }
}
