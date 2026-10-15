// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>Adds the legacy <c>-Id</c> input (a deployment resource id) to the cancel-deployment processor.</summary>
public class CancelManagementGroupDeploymentProcessor_Orchestrator : CancelAtManagementGroupScopeDeploymentsProcessor
{
    public string? Id { get; set; }

    protected override async Task BeforeProcessAsync()
    {
        if (Id is not null)
        {
            (GroupId, DeploymentName) = DeploymentIdResolver.ParseManagementGroup(Id);
        }

        await base.BeforeProcessAsync();
    }
}
