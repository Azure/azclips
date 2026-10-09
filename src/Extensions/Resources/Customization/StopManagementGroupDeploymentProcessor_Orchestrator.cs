// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>Stop-AzManagementGroupDeployment</c>: like legacy PS, GETs the deployment and refuses to cancel
/// one that is already Succeeded or Failed. CLI <c>cancel</c> uses the atomic processor directly.
/// </summary>
public class StopManagementGroupDeploymentProcessor_Orchestrator : CancelAtManagementGroupScopeDeploymentsProcessor
{
    internal override async Task<HttpResponseMessage> SendAsync()
    {
        using (HttpResponseMessage response = await CreateExtendedProcessor<GetAtManagementGroupScopeDeploymentsProcessor>(p =>
        {
            p.GroupId = GroupId;
            p.DeploymentName = DeploymentName;
        }).SendAsync())
        {
            DeploymentExtended? deployment = await response.Content.ReadFromJsonAsync<DeploymentExtended>(JsonOptions);
            string? state = deployment?.Properties?.ProvisioningState;
            if (string.Equals(state, "Succeeded", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"There is no running deployment called '{DeploymentName}' at management group '{GroupId}'.");
            }
        }

        return await base.SendAsync();
    }
}
