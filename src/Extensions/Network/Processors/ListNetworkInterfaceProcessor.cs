// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Lists network interfaces. Scoped to a resource group when <see cref="ResourceGroupName"/> is
/// supplied, otherwise to the whole subscription — matching <c>az network nic list</c>. The ARM
/// list endpoint returns an envelope <c>{ "value": [...] }</c>; this processor unwraps it and
/// follows <c>nextLink</c> so PowerShell enumerates individual interfaces and the CLI emits a JSON array.
/// </summary>
public class ListNetworkInterfaceProcessor : NetworkProcessorBase<object, NetworkInterfaceListResult>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri() =>
        string.IsNullOrWhiteSpace(ResourceGroupName)
            ? $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Network/networkInterfaces?api-version={ApiVersion}"
            : $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkInterfaces?api-version={ApiVersion}";

    private string? nextLink(NetworkInterfaceListResult body) => body.NextLink;
    private NetworkInterface[]? pageItems(NetworkInterfaceListResult body) => body.Value;

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(NetworkInterfaceListResult? body)
    {
        await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
        return true;
    }
}
