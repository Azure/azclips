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
    // Off for the CLI, which only waits on the deployment like `az deployment mg create`. The PowerShell-only _PSOutput
    // subclass turns it on to write -Verbose resource progress, like New-AzManagementGroupDeployment.
    protected virtual bool ReportsProgress => false;

    // Waits between progress checks; tests replace it so they run without real delays.
    internal Func<TimeSpan, CancellationToken, Task> ProgressDelay { get; set; } = Task.Delay;

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        ScopedDeployment deployment = new() { Location = Location, Properties = BuildProperties() };
        using (await ValidateAsync(deployment))
        {
        }

        CreateOrUpdateAtManagementGroupScopeDeploymentsProcessor create = CreateExtendedProcessor<CreateOrUpdateAtManagementGroupScopeDeploymentsProcessor>(p =>
        {
            p.GroupId = ManagementGroupId;
            p.DeploymentName = Name;
            p.Body = deployment;
        });

        if (!ReportsProgress)
        {
            return await create.SendAsync();
        }

        DeploymentProgressReporter progress = new(
            () => CreateExtendedProcessor<OperationReader>(p =>
            {
                p.ManagementGroupId = ManagementGroupId;
                p.DeploymentName = Name;
            }).ReadAllAsync(),
            ProgressDelay);
        return await progress.RunAsync(create.SendAsync, Publisher.Token);
    }

    // Reads the deployment's operations for progress without writing them as command output.
    private sealed class OperationReader : ListManagementGroupDeploymentOperationProcessor
    {
        internal Task<List<DeploymentOperation>> ReadAllAsync() =>
            DeploymentProgressReporter.ReadAllPagesAsync(CreateRequestMessage, Publisher, HttpPipeline, JsonOptions);
    }
}
