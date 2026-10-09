// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment mg list</c> / <c>Get-AzManagementGroupDeployment</c>: CLI outputs the deployments like legacy CLI;
/// PS outputs every deployment as a legacy <see cref="PSDeployment"/>.
/// </summary>
public class ListAtManagementGroupScopeDeploymentsProcessor_Customization : ListAtManagementGroupScopeDeploymentsProcessor
{
    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentListResult? body)
    {
        if (!PSDeployment.ForPowerShell) return await base.BeforeWriteOutputAsyncAndReturn(body);
        if (body is not null)
        {
            List<DeploymentExtended> deployments = await body.CollectWithPaginationAsync(
                b => b.Value, b => b.NextLink, CreateRequestMessage, Publisher, HttpPipeline, JsonOptions);
            await WriteOutput(deployments.Select(d => PSDeployment.From(d, GroupId)).ToList());
        }
        return true;
    }
}