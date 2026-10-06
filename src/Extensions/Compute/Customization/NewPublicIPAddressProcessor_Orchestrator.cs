// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

internal sealed class NewPublicIPAddressProcessor_Orchestrator : ComputeProcessorBase<object, ArmResourceReference>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string DomainNameLabel { get; set; } = string.Empty;
    public string Sku { get; set; } = "Standard";
    public string AllocationMethod { get; set; } = "Static";

    protected override HttpMethod Method => HttpMethod.Put;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/publicIPAddresses/{Name}?api-version=2025-05-01";

    protected override object? CreateRequestBody()
    {
        Dictionary<string, object?> properties = new(StringComparer.OrdinalIgnoreCase)
        {
            ["publicIPAllocationMethod"] = string.IsNullOrWhiteSpace(AllocationMethod) ? "Static" : AllocationMethod,
        };

        if (!string.IsNullOrWhiteSpace(DomainNameLabel))
        {
            properties["dnsSettings"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["domainNameLabel"] = DomainNameLabel,
            };
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["location"] = Location,
            ["sku"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = string.IsNullOrWhiteSpace(Sku) ? "Standard" : Sku,
            },
            ["properties"] = properties,
        };
    }

    protected override Func<HttpResponseMessage, string?> GetFinalUri => response => null;
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
}