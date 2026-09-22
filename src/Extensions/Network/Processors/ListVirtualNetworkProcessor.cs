// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Lists virtual networks. Scoped to a resource group when <see cref="ResourceGroupName"/> is
/// supplied, otherwise to the whole subscription — matching <c>az network vnet list</c>. The ARM
/// list endpoint returns an envelope <c>{ "value": [...] }</c>; this processor unwraps it and
/// follows <c>nextLink</c> so PowerShell enumerates individual VNets and the CLI emits a JSON array.
/// </summary>
public class ListVirtualNetworkProcessor : NetworkProcessorBase<object, VirtualNetworkListResult>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri() =>
        string.IsNullOrWhiteSpace(ResourceGroupName)
            ? $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Network/virtualNetworks?api-version={ApiVersion}"
            : $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/virtualNetworks?api-version={ApiVersion}";

    private string? nextLink(VirtualNetworkListResult body) => body.NextLink;
    private VirtualNetwork[]? pageItems(VirtualNetworkListResult body) => body.Value;

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(VirtualNetworkListResult? body)
    {
        await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
        return true;
    }
}
