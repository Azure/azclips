// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

public class ListNetworkSecurityGroupProcessor : NetworkProcessorBase<object, NetworkSecurityGroupListResult>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkSecurityGroups?api-version={ApiVersion}";

    private string? nextLink(NetworkSecurityGroupListResult body) => body.NextLink;
    private NetworkSecurityGroup[]? pageItems(NetworkSecurityGroupListResult body) => body.Value;

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(NetworkSecurityGroupListResult? body)
    {
        await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
        return true;
    }
}
