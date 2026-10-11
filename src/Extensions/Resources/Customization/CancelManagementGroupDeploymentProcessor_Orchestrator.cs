// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment mg cancel</c> / <c>Stop-AzManagementGroupDeployment</c>: generated cancel.
/// PS keeps the legacy pre-check and cancels only a deployment that is not Succeeded/Failed.
/// </summary>
public class CancelManagementGroupDeploymentProcessor_Orchestrator : CancelAtManagementGroupScopeDeploymentsProcessor
{
    internal override async Task<HttpResponseMessage> SendAsync()
    {
        if (PSDeployment.ForPowerShell)
        {
            await EnsureRunningAsync();
        }

        return await base.SendAsync();
    }

    private async Task EnsureRunningAsync()
    {
        using HttpResponseMessage response = await CreateExtendedProcessor<GetAtManagementGroupScopeDeploymentsProcessor>(p =>
        {
            p.GroupId = GroupId;
            p.DeploymentName = DeploymentName;
        }).SendAsync();
        DeploymentExtended? deployment = await response.Content.ReadFromJsonAsync<DeploymentExtended>(JsonOptions);
        string? state = deployment?.Properties?.ProvisioningState;
        if (string.Equals(state, "Succeeded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"There is no running deployment called '{DeploymentName}' at management group '{GroupId}'.");
        }
    }
}
