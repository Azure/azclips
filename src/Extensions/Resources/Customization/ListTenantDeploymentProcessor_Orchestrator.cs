// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment tenant list</c> / <c>Get-AzTenantDeployment</c> (list): sends <c>$filter</c>/<c>$top</c> only when set.
/// </summary>
public class ListTenantDeploymentProcessor_Orchestrator : ListAtTenantScopeDeploymentsProcessor
{
    protected override string CreateRequestUri()
    {
        var uri = "https://management.azure.com/providers/Microsoft.Resources/deployments?api-version=2025-04-01";
        if (!string.IsNullOrEmpty(_filter)) uri += $"&$filter={Uri.EscapeDataString(_filter)}";
        if (_top is not null) uri += $"&$top={_top}";
        return uri;
    }
}
