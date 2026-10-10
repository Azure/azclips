// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment tenant list</c> / <c>Get-AzTenantDeployment</c> (list): sends <c>$filter</c>/<c>$top</c> only when set;
/// PS writes every deployment as a legacy <see cref="PSDeployment"/>.
/// </summary>
public class ListAtTenantScopeDeploymentsProcessor_Customization : ListAtTenantScopeDeploymentsProcessor
{
    protected override void AfterInit() => Publisher = TenantDeploymentErrorPublisher.Wrap(Publisher, powerShell: PSDeployment.ForPowerShell);

    protected override string CreateRequestUri()
    {
        var uri = "https://management.azure.com/providers/Microsoft.Resources/deployments?api-version=2025-04-01";
        if (!string.IsNullOrEmpty(_filter)) uri += $"&$filter={Uri.EscapeDataString(_filter)}";
        if (_top is not null) uri += $"&$top={_top}";
        return uri;
    }

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentListResult? body)
    {
        if (!PSDeployment.ForPowerShell) return await base.BeforeWriteOutputAsyncAndReturn(body);
        var deployments = await body.CollectWithPaginationAsync(b => b.Value, b => b.NextLink, CreateRequestMessage, Publisher, HttpPipeline, JsonOptions);
        await WriteOutput(deployments.Select(PSDeployment.From).ToList());
        return true;
    }
}

/// <summary>
/// <c>az deployment tenant show</c> / <c>Get-AzTenantDeployment -Name/-Id</c>: generated GET with the legacy error text;
/// PS writes a legacy <see cref="PSDeployment"/>.
/// </summary>
public class GetAtTenantScopeDeploymentsProcessor_Customization : GetAtTenantScopeDeploymentsProcessor
{
    protected override void AfterInit() => Publisher = TenantDeploymentErrorPublisher.Wrap(Publisher, powerShell: PSDeployment.ForPowerShell);

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentExtended? body)
    {
        if (!PSDeployment.ForPowerShell) return await base.BeforeWriteOutputAsyncAndReturn(body);
        if (body is null) return false;
        await WriteOutput(PSDeployment.From(body));
        return true;
    }
}