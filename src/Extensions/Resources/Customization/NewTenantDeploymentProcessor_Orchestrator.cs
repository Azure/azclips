// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class NewTenantDeploymentProcessor_Orchestrator
    : TenantDeploymentOrchestratorBase<Deployment, DeploymentExtended>
{
    public bool NoWait { get; set; }

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
            if (WhatIf || ConfirmWithWhatIf)
            {
                WhatIfOperationResult result = await RunWhatIfAsync();
                if (WhatIf)
                {
                    await WriteOutput(result);
                    return;
                }

                bool hasChanges = HasEffectiveChanges(result.Properties?.Changes)
                    || HasEffectiveChanges(result.Properties?.PotentialChanges);

                if (!ProceedIfNoChange || hasChanges)
                {
                    if (Console.IsInputRedirected)
                    {
                        throw new InvalidOperationException(
                            "Confirmation requires an interactive terminal. Review `az deployment tenant what-if`, then rerun create without --confirm-with-what-if.");
                    }

                    Console.Write("Execute the tenant deployment? [y/N]: ");
                    string? answer = Console.ReadLine();
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
            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return;
            }

            DeploymentExtended? deployment = JsonSerializer.Deserialize<DeploymentExtended>(rawBody, JsonOptions);
            if (deployment is not null)
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
        ValidateAtTenantScopeDeploymentsProcessor processor =
            CreateExtendedProcessor<ValidateAtTenantScopeDeploymentsProcessor>(p =>
            {
                p.DeploymentName = Name;
                p.Body = BuildDeployment();
            });

        using HttpResponseMessage response = await processor.SendAsync();
        if (response.Content is null)
        {
            return;
        }

        string rawBody = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return;
        }

        DeploymentValidateResult? result = JsonSerializer.Deserialize<DeploymentValidateResult>(rawBody, JsonOptions);
        ThrowIfError(result?.Error, "Tenant deployment validation");
    }

    private async Task<WhatIfOperationResult> RunWhatIfAsync()
    {
        WhatIfAtTenantScopeDeploymentsProcessor processor =
            CreateExtendedProcessor<WhatIfAtTenantScopeDeploymentsProcessor>(p =>
            {
                p.DeploymentName = Name;
                p.Body = BuildWhatIf(WhatIfResultFormat);
            });

        using HttpResponseMessage response = await processor.SendAsync();
        string rawBody = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
        WhatIfOperationResult result = JsonSerializer.Deserialize<WhatIfOperationResult>(rawBody, JsonOptions)
            ?? throw new InvalidOperationException("Tenant deployment what-if returned an empty result.");
        ThrowIfWhatIfFailed(result);

        WhatIfTenantDeploymentProcessor_Orchestrator filter = new()
        {
            ExcludeChangeTypes = WhatIfExcludeChangeType,
        };
        filter.FilterChanges(result);
        return result;
    }

    internal static bool HasEffectiveChanges(IEnumerable<WhatIfChange>? changes) =>
        changes?.Any(change =>
            !string.Equals(change.ChangeType, "NoChange", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(change.ChangeType, "Ignore", StringComparison.OrdinalIgnoreCase)) == true;

    internal static void ThrowIfWhatIfFailed(WhatIfOperationResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.Status)
            && !string.Equals(result.Status, "Succeeded", StringComparison.OrdinalIgnoreCase)
            && result.Error is null)
        {
            throw new InvalidOperationException(
                $"Tenant deployment what-if {result.Status.ToLowerInvariant()}.");
        }
        ThrowIfError(result.Error, "Tenant deployment what-if");
    }

    private Task<HttpResponseMessage> CreateAsync()
    {
        CreateOrUpdateAtTenantScopeDeploymentsProcessor processor =
            CreateExtendedProcessor<CreateOrUpdateAtTenantScopeDeploymentsProcessor>(p =>
            {
                p.DeploymentName = Name;
                p.Body = BuildDeployment();
                p.NoWait = NoWait;
            });
        return processor.SendAsync();
    }
}
