// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Network security group list. Sends the resource-group list when ResourceGroupName is given and
/// the subscription list otherwise. Both return the same list shape, so the inherited pager follows
/// every nextLink for either route.
/// </summary>
public class ListNetworkSecurityGroupProcessor_Customization : ListNetworkSecurityGroupsProcessor
{
    internal override async Task<HttpResponseMessage> SendAsync()
    {
        if (!string.IsNullOrWhiteSpace(ResourceGroupName))
        {
            return await base.SendAsync();
        }

        return await CreateExtendedProcessor<ListAllNetworkSecurityGroupsProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
        }).SendAsync();
    }
}
