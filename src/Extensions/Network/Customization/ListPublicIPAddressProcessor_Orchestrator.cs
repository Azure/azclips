// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Public IP address list. With a resource group it keeps the <see cref="ListPublicIPAddressProcessor"/>
/// route; without one it runs <see cref="ListAllPublicIPAddressProcessor"/> for the subscription.
/// Both processors follow every page themselves.
/// </summary>
public class ListPublicIPAddressProcessor_Orchestrator : ListPublicIPAddressProcessor
{
    public override async Task ProcessAsync()
    {
        if (!string.IsNullOrWhiteSpace(ResourceGroupName))
        {
            await base.ProcessAsync();
            return;
        }

        await CreateExtendedProcessor<ListAllPublicIPAddressProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
        }).ProcessAsync();
    }
}
