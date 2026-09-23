// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

internal static class ManagementGroupDeploymentUri
{
    public static string Collection(string managementGroupId) =>
        $"https://management.azure.com/providers/Microsoft.Management/managementGroups/{Uri.EscapeDataString(managementGroupId)}/providers/Microsoft.Resources/deployments";

    public static string Resource(string managementGroupId, string deploymentName) =>
        $"{Collection(managementGroupId)}/{Uri.EscapeDataString(deploymentName)}";
}
