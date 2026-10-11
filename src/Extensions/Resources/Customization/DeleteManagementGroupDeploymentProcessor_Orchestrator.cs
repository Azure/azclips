// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment mg delete</c> / <c>Remove-AzManagementGroupDeployment</c>: generated delete with the legacy error text.
/// PS keeps the legacy existence check (HEAD) before deleting.
/// </summary>
public class DeleteManagementGroupDeploymentProcessor_Orchestrator : DeleteAtManagementGroupScopeDeploymentsProcessor
{
    protected override void AfterInit() => Publisher = DeploymentErrorPublisher.Wrap(Publisher, powerShell: PSDeployment.ForPowerShell);

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        if (PSDeployment.ForPowerShell)
        {
            await EnsureExistsAsync();
        }

        return await base.SendAsync();
    }

    private async Task EnsureExistsAsync()
    {
        using HttpRequestMessage head = new(HttpMethod.Head, CreateRequestUri());
        CreateRequestHeaders(head);
        using HttpResponseMessage response = await HttpPipeline.SendAsync(head, Publisher);
        await Publisher.Publish(Events.TracingHttpResponseMessage, response);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ArgumentException($"Deployment '{DeploymentName}' doesn't exist at management group '{GroupId}'.");
        }
        // Legacy CheckExistence throws on any status other than 204/404.
        await response.EnsureSuccessAsync(Publisher);
    }
}