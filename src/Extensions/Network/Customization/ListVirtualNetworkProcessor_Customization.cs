// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Virtual network list. Lists a resource group when <c>ResourceGroupName</c> is given, else the
/// subscription. Both routes return <c>VirtualNetworkListResult</c>, so the inherited
/// <c>BeforeWriteOutputAsyncAndReturn</c> follows every <c>nextLink</c> for either route.
/// </summary>
public class ListVirtualNetworkProcessor_Customization : ListVirtualNetworksProcessor
{
    internal override Task<HttpResponseMessage> SendAsync()
        => string.IsNullOrWhiteSpace(ResourceGroupName)
            ? CreateExtendedProcessor<ListAllVirtualNetworksProcessor>(p => p.SubscriptionId = SubscriptionId).SendAsync()
            : base.SendAsync();
}
