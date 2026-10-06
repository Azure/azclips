// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Parses the scope segments and deployment name out of a fully qualified deployment resource id,
/// for the PowerShell-only <c>-Id</c>/<c>-DeploymentId</c>/<c>-ResourceId</c> parameter sets.
/// </summary>
internal static class DeploymentIdResolver
{
    /// <summary>Last segment of the id, which is always the deployment name.</summary>
    internal static string ParseDeploymentName(string id) =>
        Segments(id)[^1];

    /// <summary>Resource group segment plus the deployment name.</summary>
    internal static (string ResourceGroupName, string DeploymentName) ParseResourceGroupScope(string id)
    {
        string[] segments = Segments(id);
        return (SegmentAfter(segments, "resourceGroups", id), segments[^1]);
    }

    /// <summary>Management group segment plus the deployment name.</summary>
    internal static (string ManagementGroupId, string DeploymentName) ParseManagementGroupScope(string id)
    {
        string[] segments = Segments(id);
        return (SegmentAfter(segments, "managementGroups", id), segments[^1]);
    }

    private static string[] Segments(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException("Deployment id cannot be empty.");
        }

        string[] segments = id.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2
            || !segments[^2].Equals("deployments", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Invalid deployment id '{id}'. Expected an id ending in /providers/Microsoft.Resources/deployments/{{deploymentName}}.");
        }

        return segments;
    }

    private static string SegmentAfter(string[] segments, string key, string id)
    {
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return segments[i + 1];
            }
        }

        throw new InvalidOperationException($"Invalid deployment id '{id}'. Expected a '{key}' segment.");
    }
}
