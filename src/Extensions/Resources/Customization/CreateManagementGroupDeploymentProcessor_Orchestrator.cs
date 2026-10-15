// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment mg create</c> / <c>New-AzManagementGroupDeployment</c>: validate, then PUT and wait, like
/// <see cref="NewResourceGroupDeploymentProcessor_Orchestrator"/>.
/// </summary>
public class CreateManagementGroupDeploymentProcessor_Orchestrator : ManagementGroupDeploymentOrchestratorBase<ScopedDeployment, DeploymentExtended>
{
    internal override async Task<HttpResponseMessage> SendAsync()
    {
        ScopedDeployment deployment = new() { Location = Location, Properties = BuildProperties() };
        using (await ValidateAsync(deployment))
        {
        }

        return await CreateExtendedProcessor<CreateOrUpdateAtManagementGroupScopeDeploymentsProcessor>(p =>
        {
            p.GroupId = ManagementGroupId;
            p.DeploymentName = Name;
            p.Body = deployment;
        }).SendAsync();
    }
}
