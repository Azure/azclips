// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>Adds the legacy <c>-Id</c> input (a deployment resource id) to the get-deployment processor.</summary>
public class GetResourceGroupDeploymentProcessor_Orchestrator : GetResourceGroupDeploymentProcessor
{
    public string? Id { get; set; }

    protected override async Task BeforeProcessAsync()
    {
        if (!string.IsNullOrWhiteSpace(Id))
        {
            (SubscriptionId, ResourceGroupName, Name) = ParseDeploymentId(Id);
        }

        await base.BeforeProcessAsync();
    }

    private static (string SubscriptionId, string ResourceGroupName, string Name) ParseDeploymentId(string id)
    {
        string[] segments = id.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 8
            || !segments[0].Equals("subscriptions", StringComparison.OrdinalIgnoreCase)
            || !segments[2].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase)
            || !segments[4].Equals("providers", StringComparison.OrdinalIgnoreCase)
            || !segments[5].Equals("Microsoft.Resources", StringComparison.OrdinalIgnoreCase)
            || !segments[6].Equals("deployments", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Invalid deployment id '{id}'. Expected /subscriptions/{{subscriptionId}}/resourceGroups/{{resourceGroupName}}/providers/Microsoft.Resources/deployments/{{deploymentName}}.");
        }

        return (segments[1], segments[3], segments[7]);
    }
}
