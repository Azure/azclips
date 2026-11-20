// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Public IP address list. Sends the resource-group list when ResourceGroupName is given and the
/// subscription list otherwise. Both return the same list shape, so the inherited pager follows every
/// nextLink for either scope.
/// </summary>
public class ListPublicIPAddressesProcessor_Customization : ListAllPublicIPAddressesProcessor
{
    public string? ResourceGroupName { get; set; }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        if (string.IsNullOrWhiteSpace(ResourceGroupName))
        {
            return await base.SendAsync();
        }

        return await CreateExtendedProcessor<ListPublicIPAddressesProcessor>(p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.ResourceGroupName = ResourceGroupName;
        }).SendAsync();
    }
}
