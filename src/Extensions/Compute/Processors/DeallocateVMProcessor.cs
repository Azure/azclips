// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

/// <summary>
/// Shuts down a virtual machine and releases its compute resources, so they are no longer billed.
/// Use <see cref="PowerOffVMProcessor"/> to stop the machine while keeping it provisioned.
/// </summary>
public class DeallocateVMProcessor : ComputeProcessorBase<object, object>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;

    /// <summary>Hibernates the virtual machine instead of a plain deallocate. Only sent when true.</summary>
    public bool Hibernate { get; set; }

    /// <summary>Forces the deallocation. The service default is false, so it is only sent when true.</summary>
    public bool ForceDeallocate { get; set; }

    protected override HttpMethod Method => HttpMethod.Post;

    protected override string CreateRequestUri()
    {
        string hibernate = Hibernate ? "hibernate=true&" : string.Empty;
        string forceDeallocate = ForceDeallocate ? "forceDeallocate=true&" : string.Empty;
        return $"https://management.azure.com/subscriptions/{SubscriptionIdValidator.Require(SubscriptionId)}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{Name}/deallocate?{hibernate}{forceDeallocate}api-version=2025-11-01";
    }

    protected override Func<HttpResponseMessage, string?> GetFinalUri => response => response.GetFirstHeader("Location");
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
}
