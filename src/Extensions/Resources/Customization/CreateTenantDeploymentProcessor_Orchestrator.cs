// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment tenant create</c> / <c>New-AzTenantDeployment</c>: validate, then PUT and wait, like
/// <see cref="NewResourceGroupDeploymentProcessor_Orchestrator"/>. With <see cref="WhatIf"/> it runs only the tenant what-if.
/// </summary>
public class CreateTenantDeploymentProcessor_Orchestrator : TenantDeploymentOrchestratorBase<ScopedDeployment, DeploymentExtended>
{
    public bool WhatIf { get; set; }

    public string? WhatIfResultFormat { get; set; }

    public string[]? WhatIfExcludeChangeTypes { get; set; }

    public override async Task ProcessAsync()
    {
        if (!WhatIf)
        {
            await base.ProcessAsync();
            return;
        }

        WhatIfTenantDeploymentProcessor_Orchestrator whatIf = CreateExtendedProcessor<WhatIfTenantDeploymentProcessor_Orchestrator>(p =>
        {
            p.ResultFormat = WhatIfResultFormat;
            p.ExcludeChangeTypes = WhatIfExcludeChangeTypes;
        });
        CopyTemplateInputsTo(whatIf);
        await whatIf.ProcessAsync();
    }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        ScopedDeployment deployment = new() { Location = Location, Properties = BuildProperties() };
        using (await ValidateAsync(deployment))
        {
        }

        return await CreateExtendedProcessor<CreateOrUpdateAtTenantScopeDeploymentsProcessor>(p =>
        {
            p.DeploymentName = Name;
            p.Body = deployment;
        }).SendAsync();
    }
}