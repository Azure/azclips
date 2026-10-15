// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment mg list</c> / <c>Get-AzManagementGroupDeployment</c> (list): sends <c>$filter</c>/<c>$top</c> only when set.
/// </summary>
public class ListManagementGroupDeploymentProcessor_Orchestrator : ListAtManagementGroupScopeDeploymentsProcessor
{
    protected override string CreateRequestUri()
    {
        var uri = $"https://management.azure.com/providers/Microsoft.Management/managementGroups/{Uri.EscapeDataString(GroupId)}/providers/Microsoft.Resources/deployments?api-version=2025-04-01";
        if (!string.IsNullOrEmpty(_filter)) uri += $"&$filter={Uri.EscapeDataString(_filter)}";
        if (_top is not null) uri += $"&$top={_top}";
        return uri;
    }
}