// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
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
    public string ResourceGroupName { get; set; } = string.Empty;

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines?api-version=2025-11-01";

    protected override object SelectOutput(VirtualMachineListResult body) => body.Value ?? [];
}