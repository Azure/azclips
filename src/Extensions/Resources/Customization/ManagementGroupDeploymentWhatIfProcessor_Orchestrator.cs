// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class ManagementGroupDeploymentWhatIfProcessor_Orchestrator
    : ManagementGroupDeploymentOrchestratorBase<ScopedDeploymentWhatIf, WhatIfOperationResult>
{
    public string? ResultFormat { get; set; }

    public string[]? ExcludeChangeTypes { get; set; }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        PrepareAndValidate();
        WhatIfAtManagementGroupScopeDeploymentsProcessor processor =
            CreateExtendedProcessor<WhatIfAtManagementGroupScopeDeploymentsProcessor>(p =>
            {
                p.GroupId = ManagementGroupId;
                p.DeploymentName = Name;
                p.Body = BuildWhatIf(ResultFormat, forceIncrementalMode: true);
            });
        return await processor.SendAsync();
    }

    public override async Task ProcessAsync()
    {
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            using HttpResponseMessage response = await SendAsync();
            string rawBody = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return;
            }

            WhatIfOperationResult result = JsonSerializer.Deserialize<WhatIfOperationResult>(rawBody, JsonOptions)
                ?? throw new InvalidOperationException(
                    "Management group deployment what-if returned an empty result.");
            if (!string.IsNullOrWhiteSpace(result.Status)
                && !string.Equals(result.Status, "Succeeded", StringComparison.OrdinalIgnoreCase)
                && result.Error is null)
            {
                throw new InvalidOperationException(
                    $"Management group deployment what-if {result.Status.ToLowerInvariant()}.");
            }
            ThrowIfError(result.Error, "Management group deployment what-if");
            FilterChanges(result, ExcludeChangeTypes);
            await WriteOutput(result);
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }

    internal static void FilterChanges(WhatIfOperationResult result, string[]? excludeChangeTypes)
    {
        if (excludeChangeTypes is not { Length: > 0 })
        {
            return;
        }

        HashSet<string> excluded = new(excludeChangeTypes, StringComparer.OrdinalIgnoreCase);
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
