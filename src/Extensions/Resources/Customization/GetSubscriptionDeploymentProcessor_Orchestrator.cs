// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>Adds the legacy <c>-Id</c> input (a subscription deployment resource id) to the get-deployment processor.</summary>
public class GetSubscriptionDeploymentProcessor_Orchestrator : GetSubscriptionDeploymentProcessor
{
    public string? Id { get; set; }

    protected override async Task BeforeProcessAsync()
    {
        if (!string.IsNullOrWhiteSpace(Id))
        {
            (SubscriptionId, Name) = ParseDeploymentId(Id);
        }

        await base.BeforeProcessAsync();
    }

    private static (string SubscriptionId, string Name) ParseDeploymentId(string id)
    {
        string[] segments = id.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 6
            || !segments[0].Equals("subscriptions", StringComparison.OrdinalIgnoreCase)
            || !segments[2].Equals("providers", StringComparison.OrdinalIgnoreCase)
            || !segments[3].Equals("Microsoft.Resources", StringComparison.OrdinalIgnoreCase)
            || !segments[4].Equals("deployments", StringComparison.OrdinalIgnoreCase))
        {
            throw AzclipsErrorException.InvalidInput(
                $"Invalid deployment id '{id}'. Expected /subscriptions/{{subscriptionId}}/providers/Microsoft.Resources/deployments/{{deploymentName}}.");
        }

        return (segments[1], segments[5]);
    }
}
