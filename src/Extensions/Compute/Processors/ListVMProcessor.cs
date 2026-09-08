// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

/// <summary>
/// Lists virtual machines in a resource group. The ARM list endpoint returns an envelope
/// <c>{ "value": [...] }</c>; this processor deserializes that envelope and writes its
/// <c>value</c> array so PowerShell enumerates individual VMs and the CLI serializes a JSON array.
/// Pagination (<c>nextLink</c>) is intentionally not followed yet.
/// </summary>
public class ListVMProcessor : ComputeProcessorBase<object, VirtualMachineListResult>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines?api-version=2025-11-01";

    private string? nextLink(VirtualMachineListResult body) => body.NextLink;
    private VirtualMachine[]? pageItems(VirtualMachineListResult body) => body.Value;

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(VirtualMachineListResult? body)
    {
        await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
        return true;
    }
}