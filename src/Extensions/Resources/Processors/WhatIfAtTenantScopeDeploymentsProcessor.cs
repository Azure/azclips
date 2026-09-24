// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class WhatIfAtTenantScopeDeploymentsProcessor : ResourcesProcessorBase<DeploymentWhatIf, WhatIfOperationResult>
{
    private const string ApiVersion = "2025-04-01";

    public string DeploymentName { get; set; } = string.Empty;

    public DeploymentWhatIf Body { get; set; } = new();

    protected override HttpMethod Method => HttpMethod.Post;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(DeploymentName)}/whatIf?api-version={ApiVersion}";

    protected override DeploymentWhatIf? CreateRequestBody() => Body;

    protected override Func<HttpResponseMessage, string?> GetLocationUri =>
        response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() => SendAsyncLRO(fetchFinalState: false);
}
