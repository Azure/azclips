// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Network interface list. Sends the resource-group list when ResourceGroupName is given and the
/// subscription list otherwise. Both return the same list shape, so the inherited pager follows every
/// nextLink for either route.
/// </summary>
public class ListNetworkInterfaceProcessor_Orchestrator : ListNetworkInterfacesProcessor
{
    internal override async Task<HttpResponseMessage> SendAsync()
    {
        if (!string.IsNullOrWhiteSpace(ResourceGroupName))
        {
            return await base.SendAsync();
        }

        return await CreateExtendedProcessor<ListAllNetworkInterfacesProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
        }).SendAsync();
    }
}
