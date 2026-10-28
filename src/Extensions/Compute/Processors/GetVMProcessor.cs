// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

public class GetVMProcessor : ComputeProcessorBase<object, VirtualMachine>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;

    /// <summary>
    /// ARM <c>$expand</c> value. Set by <c>GetVMProcessor_Orchestrator</c> from the user-data and
    /// show-details switches; deliberately not registered in <c>ExtensionIndex</c>, so no client
    /// can pass a raw expand string.
    /// </summary>
    public string? Expand { get; set; }

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri()
    {
        var uri = $"https://management.azure.com/subscriptions/{SubscriptionIdValidator.Require(SubscriptionId)}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{Name}?api-version=2025-11-01";
        return string.IsNullOrWhiteSpace(Expand) ? uri : $"{uri}&$expand={Expand}";
    }
}