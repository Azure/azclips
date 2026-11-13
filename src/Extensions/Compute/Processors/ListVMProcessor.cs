// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

/// <summary>
/// Lists virtual machines from one of three ARM scopes, chosen by which properties are set:
/// location, then resource group, then subscription-wide. All three return the envelope
/// <c>{ "value": [...] }</c>; this processor deserializes it and writes the <c>value</c> array so
/// PowerShell enumerates individual VMs and the CLI serializes a JSON array.
/// <para>
/// This processor owns only the HTTP shape. Every client-facing switch — name filtering, VMSS
/// filtering, and the detail fan-out — lives on <c>ListVMProcessor_Orchestrator</c>.
/// </para>
/// </summary>
public class ListVMProcessor : ComputeProcessorBase<object, VirtualMachineListResult>
{
    private const string ApiVersion = "2025-11-01";

    public string SubscriptionId { get; set; } = string.Empty;

    /// <summary>Empty selects the subscription-wide endpoint rather than the resource-group one.</summary>
    public string ResourceGroupName { get; set; } = string.Empty;

    /// <summary>When set, wins over <see cref="ResourceGroupName"/> and selects the by-location endpoint.</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// ARM <c>$filter</c> value. Set by <c>ListVMProcessor_Orchestrator</c> from the VMSS switch;
    /// deliberately not registered in <c>ExtensionIndex</c>, so no client can pass a raw filter.
    /// </summary>
    public string? Filter { get; set; }

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri()
    {
        string uri = !string.IsNullOrWhiteSpace(Location)
            ? $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Compute/locations/{Location}/virtualMachines?api-version={ApiVersion}"
            : !string.IsNullOrWhiteSpace(ResourceGroupName)
                ? $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines?api-version={ApiVersion}"
                : $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Compute/virtualMachines?api-version={ApiVersion}";

        return string.IsNullOrWhiteSpace(Filter) ? uri : $"{uri}&$filter={Uri.EscapeDataString(Filter)}";
    }

    private string? nextLink(VirtualMachineListResult body) => body.NextLink;
    private VirtualMachine[]? pageItems(VirtualMachineListResult body) => body.Value;

    protected override IAsyncEnumerable<VirtualMachineListResult> ReadPagesAsync() =>
        Pagination.ReadWithNextLinkAsync(ReadResponseAsync, nextLink, CreateRequestMessage, JsonOptions, Publisher, HttpPipeline);

    protected override Task ExecuteAsync() => ExecutePagesAsync(pageItems);
}