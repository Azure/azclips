// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class WhatIfTenantDeploymentProcessor_Orchestrator
    : TenantDeploymentOrchestratorBase<DeploymentWhatIf, WhatIfOperationResult>
{
    public string? ResultFormat { get; set; }

    public string[]? ExcludeChangeTypes { get; set; }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        PrepareAndValidate();
        WhatIfAtTenantScopeDeploymentsProcessor processor =
            CreateExtendedProcessor<WhatIfAtTenantScopeDeploymentsProcessor>(p =>
            {
                p.DeploymentName = Name;
                p.Body = BuildWhatIf(ResultFormat);
            });
        return await processor.SendAsync();
    }

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            using HttpResponseMessage response = await SendAsync();
            if (response.Content is null)
            {
                return;
            }

            string rawBody = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return;
            }

            WhatIfOperationResult result = JsonSerializer.Deserialize<WhatIfOperationResult>(rawBody, JsonOptions)
                ?? throw new InvalidOperationException("Tenant deployment what-if returned an empty result.");
            NewTenantDeploymentProcessor_Orchestrator.ThrowIfWhatIfFailed(result);
            FilterChanges(result);
            await WriteOutput(result);
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }

    internal void FilterChanges(WhatIfOperationResult result)
    {
        if (ExcludeChangeTypes is not { Length: > 0 })
        {
            return;
        }

        HashSet<string> excluded = new(ExcludeChangeTypes, StringComparer.OrdinalIgnoreCase);
        if (result.Properties?.Changes is not null)
        {
            result.Properties.Changes = result.Properties.Changes
                .Where(change => change.ChangeType is null || !excluded.Contains(change.ChangeType))
                .ToList();
        }
        if (result.Properties?.PotentialChanges is not null)
        {
            result.Properties.PotentialChanges = result.Properties.PotentialChanges
                .Where(change => change.ChangeType is null || !excluded.Contains(change.ChangeType))
                .ToList();
        }
    }
}
