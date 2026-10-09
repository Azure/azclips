// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment mg show</c> / <c>Get-AzManagementGroupDeployment -Name</c>: CLI outputs the deployment like legacy CLI;
/// PS outputs the legacy <see cref="PSDeployment"/>.
/// </summary>
public class GetAtManagementGroupScopeDeploymentsProcessor_Customization : GetAtManagementGroupScopeDeploymentsProcessor
{
    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentExtended? body)
    {
        if (!PSDeployment.ForPowerShell) return await base.BeforeWriteOutputAsyncAndReturn(body);
        if (body is not null)
        {
            await WriteOutput(PSDeployment.From(body, GroupId));
        }
        return true;
    }
}