// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>Deletes a resource group and all resources it contains.</summary>
public class DeleteResourceGroupProcessor : ResourcesProcessorBase<object, object>
{
    public string SubscriptionId { get; set; } = string.Empty;

    public string ResourceGroupName { get; set; } = string.Empty;

    public string? ForceDeletionTypes { get; set; }

    protected override HttpMethod Method => HttpMethod.Delete;

    protected override string CreateRequestUri()
    {
        string uri =
            $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourcegroups/{Uri.EscapeDataString(ResourceGroupName)}?api-version=2025-04-01";

        return string.IsNullOrWhiteSpace(ForceDeletionTypes)
            ? uri
            : $"{uri}&forceDeletionTypes={Uri.EscapeDataString(ForceDeletionTypes)}";
    }

    protected override Func<HttpResponseMessage, string?> GetFinalUri =>
        response => response.GetFirstHeader("Location");

    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri =>
        response => response.GetFirstHeader("Operation-Location");

    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri =>
        response => response.GetFirstHeader("Azure-AsyncOperation");

    protected override Func<HttpResponseMessage, string?> GetLocationUri =>
        response => response.GetFirstHeader("Location");

    protected override Task<bool> BeforeWriteOutputAsyncAndReturn(object? body) => Task.FromResult(true);

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
}
