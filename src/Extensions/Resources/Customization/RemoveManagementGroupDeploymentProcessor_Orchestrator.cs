// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>Remove-AzManagementGroupDeployment</c>: like legacy PS, checks existence (HEAD) before deleting.
/// CLI <c>delete</c> uses the atomic processor directly.
/// </summary>
public class RemoveManagementGroupDeploymentProcessor_Orchestrator : DeleteAtManagementGroupScopeDeploymentsProcessor
{
    internal override async Task<HttpResponseMessage> SendAsync()
    {
        // Sent directly so an expected 404 is not published as a failure.
        using (HttpRequestMessage head = new(HttpMethod.Head, CreateRequestUri()))
        using (HttpResponseMessage response = await HttpPipeline.SendAsync(head, Publisher))
        {
            await Publisher.Publish(Events.TracingHttpResponseMessage, response);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ArgumentException(
                    $"Deployment '{DeploymentName}' doesn't exist at management group '{GroupId}'.");
            }
            await response.EnsureSuccessAsync(Publisher);
        }

        return await base.SendAsync();
    }
}
