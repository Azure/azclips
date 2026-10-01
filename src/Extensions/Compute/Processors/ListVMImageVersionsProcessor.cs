// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

public class ListVMImageVersionsProcessor : ComputeProcessorBase<object, List<VirtualMachineImage>>
{
    private const string ApiVersion = "2025-04-01";

    public string SubscriptionId { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ImagePublisher { get; set; } = string.Empty;
    public string Offer { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Compute/locations/{Location}/publishers/{Uri.EscapeDataString(ImagePublisher)}/artifactTypes/vmImage/offers/{Uri.EscapeDataString(Offer)}/skus/{Uri.EscapeDataString(Sku)}/versions?api-version={ApiVersion}&$expand=properties";

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
}
