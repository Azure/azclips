// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

internal static class DeploymentIdResolver
{
    internal static (string SubscriptionId, string ResourceGroupName, string Name) Parse(string id)
    {
        // Example id (before trim):
        //   " /subscriptions/0000-1111/resourceGroups/myRg/providers/Microsoft.Resources/deployments/myDeployment/ "
        // After trim + split, segments is:
        //   [0] subscriptions  [1] 0000-1111  [2] resourceGroups  [3] myRg
        //   [4] providers      [5] Microsoft.Resources  [6] deployments  [7] myDeployment
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
