// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class StopManagementGroupDeploymentProcessor_Orchestrator
    : ResourcesProcessorBase<object, object>
{
    public string ManagementGroupId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        await EnsureTargetIsCancelableAsync();

        CancelAtManagementGroupScopeDeploymentsProcessor cancel =
            CreateExtendedProcessor<CancelAtManagementGroupScopeDeploymentsProcessor>(p =>
            {
                p.GroupId = ManagementGroupId;
                p.DeploymentName = Name;
            });
        return await cancel.SendAsync();
    }

    protected override Task<bool> BeforeWriteOutputAsyncAndReturn(object? body) =>
        Task.FromResult(true);

    private async Task EnsureTargetIsCancelableAsync()
    {
        GetAtManagementGroupScopeDeploymentsProcessor get =
            CreateExtendedProcessor<GetAtManagementGroupScopeDeploymentsProcessor>(p =>
            {
                p.GroupId = ManagementGroupId;
                p.DeploymentName = Name;
            });

        try
        {
            using HttpResponseMessage response = await get.SendAsync();
            string rawBody = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
            DeploymentExtended? deployment = string.IsNullOrWhiteSpace(rawBody)
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<DeploymentExtended>(rawBody, JsonOptions);

            string? provisioningState = deployment?.Properties?.ProvisioningState;
            if (string.Equals(provisioningState, "Failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(provisioningState, "Succeeded", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"No running deployments with name '{Name}' were found at management group '{ManagementGroupId}'.");
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ArgumentException(
                $"No running deployments with name '{Name}' were found at management group '{ManagementGroupId}'.",
                ex);
        }
    }
}
