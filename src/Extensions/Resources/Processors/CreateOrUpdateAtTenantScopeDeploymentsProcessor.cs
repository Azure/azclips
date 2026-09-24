// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class CreateOrUpdateAtTenantScopeDeploymentsProcessor : ResourcesProcessorBase<Deployment, DeploymentExtended>
{
    private const string ApiVersion = "2025-04-01";

    public string DeploymentName { get; set; } = string.Empty;

    public Deployment Body { get; set; } = new();

    public bool NoWait { get; set; }

    protected override HttpMethod Method => HttpMethod.Put;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(DeploymentName)}?api-version={ApiVersion}";

    protected override Deployment? CreateRequestBody() => Body;

    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri =>
        response => response.GetFirstHeader("Azure-AsyncOperation");

    protected override Func<HttpResponseMessage, string?> GetLocationUri =>
        response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() =>
        NoWait ? base.SendAsync() : SendAsyncLRO();
}
