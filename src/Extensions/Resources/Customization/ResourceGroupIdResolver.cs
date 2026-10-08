// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

internal static class ResourceGroupIdResolver
{
    internal static (string SubscriptionId, string ResourceGroupName) Parse(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException("Resource group id cannot be empty.");
        }

        string[] segments = id.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 4
            || !segments[0].Equals("subscriptions", StringComparison.OrdinalIgnoreCase)
            || !segments[2].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Invalid resource group id '{id}'. Expected /subscriptions/{{subscriptionId}}/resourceGroups/{{resourceGroupName}}.");
        }

        return (segments[1], segments[3]);
    }
}
