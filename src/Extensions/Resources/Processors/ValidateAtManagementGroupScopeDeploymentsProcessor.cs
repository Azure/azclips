// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class ValidateAtManagementGroupScopeDeploymentsProcessor
    : ResourcesProcessorBase<Deployment, DeploymentValidateResult>
{
    private const string ApiVersion = "2025-04-01";

    public string ManagementGroupId { get; set; } = string.Empty;

    public string DeploymentName { get; set; } = string.Empty;

    public Deployment Body { get; set; } = new();

    protected override HttpMethod Method => HttpMethod.Post;

    protected override string CreateRequestUri() =>
        $"{ManagementGroupDeploymentUri.Resource(ManagementGroupId, DeploymentName)}/validate?api-version={ApiVersion}";

    protected override Deployment? CreateRequestBody() => Body;

    protected override Func<HttpResponseMessage, string?> GetFinalUri =>
        response => response.GetFirstHeader("Location");

    protected override Func<HttpResponseMessage, string?> GetOperationLocationUri =>
        response => response.GetFirstHeader("Operation-Location");

    protected override Func<HttpResponseMessage, string?> GetAsyncOperationUri =>
        response => response.GetFirstHeader("Azure-AsyncOperation");

    protected override Func<HttpResponseMessage, string?> GetLocationUri =>
        response => response.GetFirstHeader("Location");

    internal override Task<HttpResponseMessage> SendAsync() =>
        SendAsyncLRO([HttpStatusCode.BadRequest]);
}
