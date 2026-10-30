// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment mg what-if</c>: POST whatIf, throw when it failed, otherwise write the raw result, like
/// <see cref="NewResourceGroupDeploymentWhatIfProcessor_Orchestrator"/>.
/// </summary>
public class WhatIfManagementGroupDeploymentProcessor_Orchestrator : ManagementGroupDeploymentOrchestratorBase<ScopedDeploymentWhatIf, WhatIfOperationResult>
{
    public string? ResultFormat { get; set; }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        if (!string.IsNullOrWhiteSpace(ResultFormat) && NewResourceGroupDeploymentWhatIfProcessor_Orchestrator.NormalizeResultFormatOrNull(ResultFormat) is null)
        {
            throw AzclipsErrorException.InvalidInput("ResultFormat must be one of 'ResourceIdOnly' or 'FullResourcePayloads'.");
        }

        DeploymentProperties properties = await BuildPropertiesAsync();
        ScopedDeploymentWhatIf body = new()
        {
            Location = Location,
            Properties = new DeploymentWhatIfProperties
            {
                Mode = DeploymentMode.Incremental,
                Template = properties.Template,
                TemplateLink = properties.TemplateLink,
                Parameters = properties.Parameters,
                ParametersLink = properties.ParametersLink,
                ValidationLevel = properties.ValidationLevel,
                WhatIfSettings = new DeploymentWhatIfSettings
                {
                    ResultFormat = NewResourceGroupDeploymentWhatIfProcessor_Orchestrator.NormalizeResultFormat(ResultFormat),
                },
            },
        };

        return await CreateExtendedProcessor<WhatIfAtManagementGroupScopeDeploymentsProcessor>(p =>
        {
            p.GroupId = ManagementGroupId;
            p.DeploymentName = Name;
            p.Body = body;
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
