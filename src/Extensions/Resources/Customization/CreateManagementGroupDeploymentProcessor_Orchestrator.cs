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
    // Waits between progress checks; tests replace it so they run without real delays.
    internal Func<TimeSpan, CancellationToken, Task> ProgressDelay { get; set; } = Task.Delay;

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        ScopedDeployment deployment = new() { Location = Location, Properties = BuildProperties() };
        using (await ValidateAsync(deployment))
        {
        }

        DeploymentProgressReporter progress = new(
            Publisher,
            token => CreateExtendedProcessor<OperationReader>(p =>
            {
                p.ManagementGroupId = ManagementGroupId;
                p.DeploymentName = Name;
            }).ReadAllAsync(token),
            ProgressDelay);

        ProgressWatchedDeployment create = CreateExtendedProcessor<ProgressWatchedDeployment>(p =>
        {
            p.GroupId = ManagementGroupId;
            p.DeploymentName = Name;
            p.Body = deployment;
            p.Progress = progress;
        });

        return await progress.RunAsync(create.SendAsync, Publisher.Token);
    }

    // The deployment PUT, telling the progress reporter once ARM has accepted it.
    private sealed class ProgressWatchedDeployment : CreateOrUpdateAtManagementGroupScopeDeploymentsProcessor
    {
        internal DeploymentProgressReporter Progress { get; set; } = null!;

        internal override Task<HttpResponseMessage> SendAsync()
        {
            Publisher = Progress.WatchForSubmission(Publisher);
            return base.SendAsync();
        }
    }

    // Reads the deployment's operations for progress without writing them as command output. The token stops the read,
    // including its retries, when the deployment finishes.
    private sealed class OperationReader : ListManagementGroupDeploymentOperationProcessor
    {
        internal Task<List<DeploymentOperation>> ReadAllAsync(CancellationToken token)
        {
            Publisher = new DeploymentProgressPublisher(Publisher, token);
            return DeploymentProgressReporter.ReadAllPagesAsync(CreateRequestMessage, Publisher, HttpPipeline, JsonOptions);
        }
    }
}
