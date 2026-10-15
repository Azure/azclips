// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>Adds the legacy <c>-Id</c> input (a deployment resource id) to the delete-deployment processor.</summary>
public class DeleteTenantDeploymentProcessor_Orchestrator : DeleteAtTenantScopeDeploymentsProcessor
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
