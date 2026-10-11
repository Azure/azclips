// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment mg delete</c> / <c>Remove-AzManagementGroupDeployment</c>: generated delete.
/// PS keeps the legacy existence check (HEAD) before deleting.
/// </summary>
public class DeleteManagementGroupDeploymentProcessor_Orchestrator : DeleteAtManagementGroupScopeDeploymentsProcessor
{
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
        // Sent directly so an expected 404 is not published as a failure.
        using HttpRequestMessage head = new(HttpMethod.Head, CreateRequestUri());
        using HttpResponseMessage response = await HttpPipeline.SendAsync(head, Publisher);
        await Publisher.Publish(Events.TracingHttpResponseMessage, response);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ArgumentException(
                $"Deployment '{DeploymentName}' doesn't exist at management group '{GroupId}'.");
        }
        await response.EnsureSuccessAsync(Publisher);
    }
}
