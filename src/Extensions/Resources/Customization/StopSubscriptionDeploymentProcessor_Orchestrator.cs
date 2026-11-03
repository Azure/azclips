// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>Adds the legacy <c>-Id</c> input (a subscription deployment resource id) to the cancel-deployment processor.</summary>
public class StopSubscriptionDeploymentProcessor_Orchestrator : StopSubscriptionDeploymentProcessor
{
    public string? Id { get; set; }

    protected override async Task BeforeProcessAsync()
    {
        if (!string.IsNullOrWhiteSpace(Id))
        {
            (SubscriptionId, Name) = DeploymentIdResolver.ParseSubscription(Id);
        }

        await base.BeforeProcessAsync();
    }
}
