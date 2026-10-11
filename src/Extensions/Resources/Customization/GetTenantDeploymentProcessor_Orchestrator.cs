// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment tenant show</c> / <c>Get-AzTenantDeployment -Name/-Id</c>: generated GET with the legacy error text;
/// PS writes a legacy <see cref="PSDeployment"/>.
/// </summary>
public class GetTenantDeploymentProcessor_Orchestrator : GetAtTenantScopeDeploymentsProcessor
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
