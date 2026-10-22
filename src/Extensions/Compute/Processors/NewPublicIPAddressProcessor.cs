// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

public class NewPublicIPAddressProcessor : ComputeProcessorBase<PublicIPAddress, PublicIPAddress>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string Sku { get; set; } = "Standard";
    public string AllocationMethod { get; set; } = "Static";
    public Dictionary<string, string>? Tags { get; set; }

    protected override HttpMethod Method => HttpMethod.Put;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Network/publicIPAddresses/{Name}?api-version=2025-05-01";

    protected override PublicIPAddress? CreateRequestBody() => new PublicIPAddress
    {
        Location = Location,
        Tags = Tags,
        Sku = new PublicIPAddressSku { Name = Sku },
        Properties = new PublicIPAddressProperties
        {
            PublicIPAllocationMethod = AllocationMethod,
        },
    };

    protected override Func<HttpResponseMessage, string?> GetFinalUri => response => null;
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
}
