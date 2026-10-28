// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

/// <summary>
/// Powers off (stops) a virtual machine while leaving its compute resources allocated.
/// The virtual machine keeps accruing charges; use <see cref="DeallocateVMProcessor"/> to release them.
/// </summary>
public class PowerOffVMProcessor : ComputeProcessorBase<object, object>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;

    /// <summary>Requests a non-graceful shutdown. The service default is false, so it is only sent when true.</summary>
    public bool SkipShutdown { get; set; }

    protected override HttpMethod Method => HttpMethod.Post;

    protected override string CreateRequestUri()
    {
        string skipShutdown = SkipShutdown ? "skipShutdown=true&" : string.Empty;
        return $"https://management.azure.com/subscriptions/{SubscriptionIdValidator.Require(SubscriptionId)}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{Name}/powerOff?{skipShutdown}api-version=2025-11-01";
    }

    protected override Func<HttpResponseMessage, string?> GetFinalUri => response => response.GetFirstHeader("Location");
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
}
