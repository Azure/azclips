// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>Adds the legacy <c>-Id</c> input (a deployment resource id) to the get-deployment processor.</summary>
public class GetTenantDeploymentProcessor_Orchestrator : GetAtTenantScopeDeploymentsProcessor
{
    public string? Id { get; set; }

    protected override async Task BeforeProcessAsync()
    {
        if (Id is not null)
        {
            DeploymentName = DeploymentIdResolver.ParseTenant(Id);
        }

        await base.BeforeProcessAsync();
    }
}
