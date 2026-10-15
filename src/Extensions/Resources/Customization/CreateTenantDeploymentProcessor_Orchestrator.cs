// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment tenant create</c> / <c>New-AzTenantDeployment</c>: validate, then PUT and wait, like
/// <see cref="NewResourceGroupDeploymentProcessor_Orchestrator"/>.
/// </summary>
public class CreateTenantDeploymentProcessor_Orchestrator : TenantDeploymentOrchestratorBase<ScopedDeployment, DeploymentExtended>
{
    internal override async Task<HttpResponseMessage> SendAsync()
    {
        ScopedDeployment deployment = new() { Location = Location, Properties = BuildProperties() };
        using (await ValidateAsync(deployment))
        {
        }

        return await CreateExtendedProcessor<CreateOrUpdateAtTenantScopeDeploymentsProcessor>(p =>
        {
            p.DeploymentName = Name;
            p.Body = deployment;
        }).SendAsync();
    }
}
