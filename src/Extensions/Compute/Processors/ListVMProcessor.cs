// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

/// <summary>
/// Lists virtual machines in a resource group. ARM list endpoints return an envelope
/// <c>{ "value": [...], "nextLink": "..." }</c>; this processor delegates the unwrap +
/// <c>nextLink</c> pagination to <see cref="ArmListPaginator"/>.
/// </summary>
public class ListVMProcessor : ComputeProcessorBase<VirtualMachineListResult>
{
    public string ResourceGroupName { get; set; } = string.Empty;

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines?api-version=2025-11-01";
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }

    public override Task ProcessAsync() =>
        ArmListPaginator.CollectAndWriteAsync<VirtualMachineListResult, VirtualMachine>(
            HttpPipeline, Publisher, JsonOptions, WriteOutput, SendAsync);
}