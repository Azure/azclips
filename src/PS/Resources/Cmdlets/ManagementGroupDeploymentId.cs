// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

internal static class ManagementGroupDeploymentId
{
    internal static (string ManagementGroupId, string Name) Parse(string id)
    {
        string[] segments = id.Trim('/').Split('/');
        if (segments.Length != 8
            || !segments[0].Equals("providers", StringComparison.OrdinalIgnoreCase)
            || !segments[1].Equals("Microsoft.Management", StringComparison.OrdinalIgnoreCase)
            || !segments[2].Equals("managementGroups", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(segments[3])
            || !segments[4].Equals("providers", StringComparison.OrdinalIgnoreCase)
            || !segments[5].Equals("Microsoft.Resources", StringComparison.OrdinalIgnoreCase)
            || !segments[6].Equals("deployments", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(segments[7]))
        {
            throw new ArgumentException("Id must be a management-group deployment resource ID.", nameof(id));
        }

        return (segments[3], segments[7]);
    }
}
