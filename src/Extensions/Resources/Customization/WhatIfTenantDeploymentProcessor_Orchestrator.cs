// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment tenant what-if</c>: POST whatIf, throw when it failed, otherwise write the change report, or the raw
/// result with <see cref="NoPrettyPrint"/>.
/// </summary>
public class WhatIfTenantDeploymentProcessor_Orchestrator : TenantDeploymentOrchestratorBase<ScopedDeploymentWhatIf, WhatIfOperationResult>
{
    public string? ResultFormat { get; set; }

    public string[]? ExcludeChangeTypes { get; set; }

    public bool NoPrettyPrint { get; set; }

    internal override Task<HttpResponseMessage> SendAsync()
    {
        if (!string.IsNullOrWhiteSpace(ResultFormat) && NewResourceGroupDeploymentWhatIfProcessor_Orchestrator.NormalizeResultFormatOrNull(ResultFormat) is null)
        {
            throw AzclipsErrorException.InvalidInput("ResultFormat must be one of 'ResourceIdOnly' or 'FullResourcePayloads'.");
        }

        DeploymentProperties properties = BuildProperties();
        ScopedDeploymentWhatIf body = new()
        {
            Location = Location,
            Properties = new DeploymentWhatIfProperties
            {
                Mode = properties.Mode,
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

        return CreateExtendedProcessor<WhatIfAtTenantScopeDeploymentsProcessor>(p =>
        {
            p.DeploymentName = Name;
            p.Body = body;
        }).SendAsyncPollLocation();
    }

    // ARM returns a template error with HTTP 200, so check the result before writing it.
    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(WhatIfOperationResult? body)
    {
        if (body is null)
        {
            return false;
        }
        if (string.Equals(body.Status, "Failed", StringComparison.OrdinalIgnoreCase) || body.Error is not null)
        {
            throw new InvalidOperationException(NewResourceGroupDeploymentWhatIfProcessor_Orchestrator.BuildErrorMessage(body.Error));
        }
        if (ExcludeChangeTypes is { Length: > 0 } && body.Properties?.Changes is { } changes)
        {
            HashSet<string> excluded = new(ExcludeChangeTypes, StringComparer.OrdinalIgnoreCase);
            changes.RemoveAll(c => c.ChangeType is not null && excluded.Contains(c.ChangeType));
        }
        await WriteOutput(WhatIfReportFormatter.ToOutput(body, NoPrettyPrint));
        return true;
    }
}
