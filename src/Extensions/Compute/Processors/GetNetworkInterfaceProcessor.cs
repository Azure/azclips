// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

public class GetNetworkInterfaceProcessor : ComputeProcessorBase<NetworkInterface>
{
    public string Name { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/networkInterfaces/{Name}?api-version=2025-05-01";
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }
}