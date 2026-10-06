// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

internal sealed class NewVMGuestAttestationExtensionProcessor_Orchestrator : ComputeProcessorBase<object, ArmResourceReference>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string VMName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ExtensionPublisher { get; set; } = "Microsoft.Azure.Security.WindowsAttestation";
    public string TypeHandlerVersion { get; set; } = "1.0";
    public bool EnableAutomaticUpgrade { get; set; } = true;

    protected override HttpMethod Method => HttpMethod.Put;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/virtualMachines/{VMName}/extensions/GuestAttestation?api-version=2025-11-01";

    protected override object? CreateRequestBody() => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
    {
        ["location"] = Location,
        ["properties"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["publisher"] = string.IsNullOrWhiteSpace(ExtensionPublisher) ? "Microsoft.Azure.Security.WindowsAttestation" : ExtensionPublisher,
            ["type"] = "GuestAttestation",
            ["typeHandlerVersion"] = string.IsNullOrWhiteSpace(TypeHandlerVersion) ? "1.0" : TypeHandlerVersion,
            ["autoUpgradeMinorVersion"] = true,
            ["enableAutomaticUpgrade"] = EnableAutomaticUpgrade,
        },
    };

    protected override Func<HttpResponseMessage, string?> GetFinalUri => response => response.GetFirstHeader("Location");
    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri => response => response.GetFirstHeader("Operation-Location");
    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri => response => response.GetFirstHeader("Azure-AsyncOperation");
    protected override Func<HttpResponseMessage, string?> GetLocationUri => response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO();
}