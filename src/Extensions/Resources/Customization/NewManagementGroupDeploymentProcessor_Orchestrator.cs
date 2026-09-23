// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class NewManagementGroupDeploymentProcessor_Orchestrator
    : ManagementGroupDeploymentOrchestratorBase<Deployment, DeploymentExtended>
{
    public bool NoWait { get; set; }

    public bool WhatIf { get; set; }

    public string? WhatIfResultFormat { get; set; }

    public string[]? WhatIfExcludeChangeType { get; set; }

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            PrepareAndValidate();
            if (WhatIf)
            {
                await WriteOutput(await RunWhatIfAsync());
                return;
            }

            await ValidateAsync();
            using HttpResponseMessage response = await CreateAsync();
            if (response.Content is null)
            {
                return;
            }

            string rawBody = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(rawBody)
                && JsonSerializer.Deserialize<DeploymentExtended>(rawBody, JsonOptions) is { } deployment)
            {
                await WriteOutput(deployment);
            }
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }

    private async Task ValidateAsync()
    {
        ValidateAtManagementGroupScopeDeploymentsProcessor processor =
            CreateExtendedProcessor<ValidateAtManagementGroupScopeDeploymentsProcessor>(p =>
            {
                p.ManagementGroupId = ManagementGroupId;
                p.DeploymentName = Name;
                p.Body = BuildDeployment();
            });

        using HttpResponseMessage response = await processor.SendAsync();
        string rawBody = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
        if (!string.IsNullOrWhiteSpace(rawBody))
        {
            DeploymentValidateResult? result =
                JsonSerializer.Deserialize<DeploymentValidateResult>(rawBody, JsonOptions);
            ThrowIfError(result?.Error, "Management group deployment validation");
        }
    }

    private async Task<WhatIfOperationResult> RunWhatIfAsync()
    {
        WhatIfAtManagementGroupScopeDeploymentsProcessor processor =
            CreateExtendedProcessor<WhatIfAtManagementGroupScopeDeploymentsProcessor>(p =>
            {
                p.ManagementGroupId = ManagementGroupId;
                p.DeploymentName = Name;
                p.Body = BuildWhatIf(WhatIfResultFormat);
            });

        using HttpResponseMessage response = await processor.SendAsync();
        string rawBody = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
        WhatIfOperationResult result = JsonSerializer.Deserialize<WhatIfOperationResult>(rawBody, JsonOptions)
            ?? throw new InvalidOperationException("Management group deployment what-if returned an empty result.");
        ThrowIfError(result.Error, "Management group deployment what-if");
        ManagementGroupDeploymentWhatIfProcessor_Orchestrator.FilterChanges(
            result,
            WhatIfExcludeChangeType);
        return result;
    }

    private Task<HttpResponseMessage> CreateAsync()
    {
        CreateOrUpdateAtManagementGroupScopeDeploymentsProcessor processor =
            CreateExtendedProcessor<CreateOrUpdateAtManagementGroupScopeDeploymentsProcessor>(p =>
            {
                p.ManagementGroupId = ManagementGroupId;
                p.DeploymentName = Name;
                p.Body = BuildDeployment();
                p.NoWait = NoWait;
            });
        return processor.SendAsync();
    }
}
