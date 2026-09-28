// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class StopManagementGroupDeploymentProcessor_Orchestrator
    : ResourcesProcessorBase<object, object>
{
    public string ManagementGroupId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        GetAtManagementGroupScopeDeploymentsProcessor get =
            CreateExtendedProcessor<GetAtManagementGroupScopeDeploymentsProcessor>(p =>
            {
                p.GroupId = ManagementGroupId;
                p.DeploymentName = Name;
            });
        using HttpResponseMessage response = await get.SendAsync();
        string rawBody = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
        DeploymentExtended? deployment = JsonSerializer.Deserialize<DeploymentExtended>(rawBody, JsonOptions);
        string? state = deployment?.Properties?.ProvisioningState;
        if (!string.Equals(state, "Accepted", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(state, "Running", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Management group deployment '{Name}' is not running and cannot be canceled.");
        }

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
}
