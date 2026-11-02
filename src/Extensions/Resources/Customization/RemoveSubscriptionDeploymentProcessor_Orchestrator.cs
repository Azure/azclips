// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>Adds an <c>Id</c> input (the resource id of a deployment piped to <c>-InputObject</c>) to the delete-deployment processor.</summary>
public class RemoveSubscriptionDeploymentProcessor_Orchestrator : RemoveSubscriptionDeploymentProcessor
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
