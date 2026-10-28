// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Lists public IP addresses. Scoped to a resource group when <see cref="ResourceGroupName"/> is
/// supplied, otherwise to the whole subscription — matching <c>az network public-ip list</c>. The ARM
/// list endpoint returns an envelope <c>{ "value": [...] }</c>; this processor unwraps it and
/// follows <c>nextLink</c> so PowerShell enumerates individual addresses and the CLI emits a JSON array.
/// </summary>
public class ListPublicIpAddressProcessor : NetworkProcessorBase<object, PublicIpAddressListResult>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri() =>
        string.IsNullOrWhiteSpace(ResourceGroupName)
            ? $"https://management.azure.com/subscriptions/{SubscriptionIdValidator.Require(SubscriptionId)}/providers/Microsoft.Network/publicIPAddresses?api-version={ApiVersion}"
            : $"https://management.azure.com/subscriptions/{SubscriptionIdValidator.Require(SubscriptionId)}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/publicIPAddresses?api-version={ApiVersion}";

    private string? nextLink(PublicIpAddressListResult body) => body.NextLink;
    private PublicIpAddress[]? pageItems(PublicIpAddressListResult body) => body.Value;

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(PublicIpAddressListResult? body)
    {
        await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
        return true;
    }
}
