// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class NewManagementGroupDeploymentProcessor_Orchestrator
    : ManagementGroupDeploymentOrchestratorBase<ScopedDeployment, DeploymentExtended>
{
    public bool WhatIf { get; set; }

    public bool ConfirmWithWhatIf { get; set; }

    public bool ProceedIfNoChange { get; set; }

    public string? WhatIfResultFormat { get; set; }

    public string[]? WhatIfExcludeChangeType { get; set; }

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            PrepareAndValidate();
            if (WhatIf && ConfirmWithWhatIf)
            {
                throw new ArgumentException("WhatIf and ConfirmWithWhatIf cannot be used together.");
            }
            if (WhatIf || ConfirmWithWhatIf)
            {
                WhatIfOperationResult whatIfResult = await RunWhatIfAsync();
                await WriteOutput(whatIfResult);
                if (WhatIf)
                {
                    return;
                }
                if (!ProceedIfNoChange || HasChanges(whatIfResult))
                {
                    Console.Write("Are you sure you want to execute the deployment? (y/n): ");
                    string? answer = Console.ReadLine()?.Trim();
                    if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }
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
                p.GroupId = ManagementGroupId;
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
                p.GroupId = ManagementGroupId;
                p.DeploymentName = Name;
                p.Body = BuildWhatIf(WhatIfResultFormat, forceIncrementalMode: true);
            });

        using HttpResponseMessage response = await processor.SendAsync();
        string rawBody = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
        WhatIfOperationResult result = JsonSerializer.Deserialize<WhatIfOperationResult>(rawBody, JsonOptions)
            ?? throw new InvalidOperationException("Management group deployment what-if returned an empty result.");
        if (!string.IsNullOrWhiteSpace(result.Status)
            && !string.Equals(result.Status, "Succeeded", StringComparison.OrdinalIgnoreCase)
            && result.Error is null)
        {
            throw new InvalidOperationException(
                $"Management group deployment what-if {result.Status.ToLowerInvariant()}.");
        }
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
                p.GroupId = ManagementGroupId;
                p.DeploymentName = Name;
                p.Body = BuildDeployment();
            });
        return processor.SendAsync();
    }

    public static bool HasChanges(WhatIfOperationResult result) =>
        result.Properties?.Changes?.Any(change =>
            !string.Equals(change.ChangeType, "NoChange", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(change.ChangeType, "Ignore", StringComparison.OrdinalIgnoreCase)) == true;
}
