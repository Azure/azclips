// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment tenant what-if</c> / <c>Get-AzTenantDeploymentWhatIfResult</c>: POST whatIf, throw when it failed,
/// otherwise write the raw result, like <see cref="WhatIfManagementGroupDeploymentProcessor_Orchestrator"/>.
/// </summary>
public class WhatIfTenantDeploymentProcessor_Orchestrator : TenantDeploymentOrchestratorBase<ScopedDeploymentWhatIf, WhatIfOperationResult>
{
    public string? ResultFormat { get; set; }

    internal override Task<HttpResponseMessage> SendAsync()
    {
        ResolvedDeployment resolved = ResolveInputs();
        return CreateExtendedProcessor<WhatIfAtTenantScopeDeploymentsProcessor>(p =>
        {
            p.DeploymentName = Name;
            p.Body = new ScopedDeploymentWhatIf
            {
                Location = Location,
                Properties = new DeploymentWhatIfProperties
                {
                    Mode = "Incremental",
                    Template = resolved.Template,
                    TemplateLink = resolved.TemplateLink,
                    Parameters = resolved.Parameters,
                    ParametersLink = resolved.ParametersLink,
                    ValidationLevel = resolved.ValidationLevel,
                    WhatIfSettings = new DeploymentWhatIfSettings
                    {
                        ResultFormat = NewResourceGroupDeploymentWhatIfProcessor_Orchestrator.NormalizeResultFormat(ResultFormat),
                    },
                },
            };
        }).SendAsyncPollLocation();
    }

    // ARM returns a template error with HTTP 200, so check the result before writing it.
    protected override Task<bool> BeforeWriteOutputAsyncAndReturn(WhatIfOperationResult? body)
    {
        if (body is not null && (string.Equals(body.Status, "Failed", StringComparison.OrdinalIgnoreCase) || body.Error is not null))
        {
            throw new InvalidOperationException(NewResourceGroupDeploymentWhatIfProcessor_Orchestrator.BuildErrorMessage(body.Error));
        }
        return Task.FromResult(false);
    }
}
