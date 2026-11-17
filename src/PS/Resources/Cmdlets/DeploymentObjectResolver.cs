// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

/// <summary>
/// Reads the deployment a <c>-DeploymentObject</c> or <c>-InputObject</c> names. Values that cannot be resolved are returned as null;
/// the cmdlet then writes the matching error and sends no request, because the PowerShell argument-parsing
/// provider performs no required-parameter check.
/// </summary>
internal static class DeploymentObjectResolver
{
    internal static ErrorRecord MissingDeploymentNameError(PSObject deployment) => new(
        new PSArgumentException("The deployment object has no DeploymentName or Name property."),
        "DeploymentObjectMissingDeploymentName",
        ErrorCategory.InvalidArgument,
        deployment);

    internal static ErrorRecord MissingManagementGroupIdError(PSObject deployment) => new(
        new PSArgumentException("The deployment object has no ManagementGroupId property, and its Id is not a management group deployment id."),
        "DeploymentObjectMissingManagementGroupId",
        ErrorCategory.InvalidArgument,
        deployment);

    internal static ErrorRecord InvalidSubscriptionDeploymentIdError(DeploymentExtended deployment) => new(
        new PSArgumentException($"Invalid deployment id '{deployment.Id}'. Expected /subscriptions/{{subscriptionId}}/providers/Microsoft.Resources/deployments/{{deploymentName}}."),
        "InvalidSubscriptionDeploymentId",
        ErrorCategory.InvalidArgument,
        deployment);

    internal static string? GetDeploymentName(PSObject deployment) =>
        GetString(deployment, "DeploymentName") ?? GetString(deployment, "Name");

    internal static string? GetManagementGroupId(PSObject deployment) =>
        GetString(deployment, "ManagementGroupId") ?? ParseManagementGroupId(GetString(deployment, "Id"));

    private static string? GetString(PSObject deployment, string propertyName) =>
        deployment.Properties[propertyName]?.Value?.ToString() is { Length: > 0 } value ? value : null;

    // Expects /subscriptions/{subscriptionId}/providers/Microsoft.Resources/deployments/{deploymentName}.
    internal static (string SubscriptionId, string Name)? ParseSubscriptionDeploymentId(string? id)
    {
        if (id is null)
        {
            return null;
        }

        string[] segments = id.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 6
            && segments[0].Equals("subscriptions", StringComparison.OrdinalIgnoreCase)
            && segments[2].Equals("providers", StringComparison.OrdinalIgnoreCase)
            && segments[3].Equals("Microsoft.Resources", StringComparison.OrdinalIgnoreCase)
            && segments[4].Equals("deployments", StringComparison.OrdinalIgnoreCase)
                ? (segments[1], segments[5])
                : null;
    }

    // Expects /providers/Microsoft.Management/managementGroups/{managementGroupId}/providers/Microsoft.Resources/deployments/{deploymentName}.
    private static string? ParseManagementGroupId(string? id)
    {
        if (id is null)
        {
            return null;
        }

        string[] segments = id.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 8
            && segments[0].Equals("providers", StringComparison.OrdinalIgnoreCase)
            && segments[1].Equals("Microsoft.Management", StringComparison.OrdinalIgnoreCase)
            && segments[2].Equals("managementGroups", StringComparison.OrdinalIgnoreCase)
            && segments[4].Equals("providers", StringComparison.OrdinalIgnoreCase)
            && segments[5].Equals("Microsoft.Resources", StringComparison.OrdinalIgnoreCase)
            && segments[6].Equals("deployments", StringComparison.OrdinalIgnoreCase)
                ? segments[3]
                : null;
    }
}
