// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using System.Net;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class ValidateAtTenantScopeDeploymentsProcessor : ResourcesProcessorBase<Deployment, DeploymentValidateResult>
{
    private const string ApiVersion = "2025-04-01";

    public string DeploymentName { get; set; } = string.Empty;

    public Deployment Body { get; set; } = new();

    protected override HttpMethod Method => HttpMethod.Post;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/providers/Microsoft.Resources/deployments/{Uri.EscapeDataString(DeploymentName)}/validate?api-version={ApiVersion}";

    protected override Deployment? CreateRequestBody() => Body;

    protected override Func<HttpResponseMessage, string?> GetLocationUri =>
        response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() =>
        SendAsyncLRO([HttpStatusCode.BadRequest]);
}
