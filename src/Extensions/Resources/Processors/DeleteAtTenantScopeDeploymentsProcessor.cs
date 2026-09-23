// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class DeleteAtTenantScopeDeploymentsProcessor : ResourcesProcessorBase<object, object>
{
    private const string ApiVersion = "2025-04-01";

    public string DeploymentName { get; set; } = string.Empty;

    public bool NoWait { get; set; }

    protected override HttpMethod Method => HttpMethod.Delete;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(DeploymentName)}?api-version={ApiVersion}";

    protected override Func<HttpResponseMessage, string?> GetLocationUri =>
        response => response.GetFirstHeader("Location");

    protected override Task<bool> BeforeWriteOutputAsyncAndReturn(object? body) =>
        Task.FromResult(true);

    internal override Task<HttpResponseMessage> SendAsync() =>
        NoWait ? base.SendAsync() : SendAsyncLRO();
}
