// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment tenant create</c> / <c>New-AzTenantDeployment</c>: validate, then PUT and wait, like
/// <see cref="NewResourceGroupDeploymentProcessor_Orchestrator"/>. With <see cref="WhatIf"/> it runs only the tenant what-if.
/// </summary>
public class CreateTenantDeploymentProcessor_Orchestrator : TenantDeploymentOrchestratorBase<ScopedDeployment, DeploymentExtended>
{
    // Like New-AzTenantDeployment, a failure shows at most this many operation errors.
    private const int MaxErrorsShown = 3;

    public Dictionary<string, string>? Tags { get; set; }

    public string? DeploymentDebugLogLevel { get; set; }

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
        ScopedDeployment deployment = new() { Location = Location, Tags = Tags, Properties = BuildProperties() };
        if (DeploymentDebugSettings.Create(DeploymentDebugLogLevel) is { } debugSetting)
        {
            deployment.Properties.DebugSetting = debugSetting;
            await Publisher.Publish(Events.Warning, new EventData { Message = DeploymentDebugSettings.Warning });
        }
        using (await ValidateAsync(deployment))
        {
        }

        try
        {
            return await CreateExtendedProcessor<CreateOrUpdateAtTenantScopeDeploymentsProcessor>(p =>
            {
                p.DeploymentName = Name;
                p.Body = deployment;
            }).SendAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (await GetFailureMessageAsync(null) is { } message)
            {
                throw new InvalidOperationException(message, ex);
            }
            throw;
        }
    }

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentExtended? body)
    {
        if (IsFailed(body?.Properties?.ProvisioningState) && await GetFailureMessageAsync(body) is { } message)
        {
            throw new InvalidOperationException(message);
        }
        return false;
    }

    // Returns null unless the deployment reads back Failed, so the caller keeps the original outcome.
    private async Task<string?> GetFailureMessageAsync(DeploymentExtended? deployment)
    {
        try
        {
            if (deployment is null)
            {
                using HttpResponseMessage response = await CreateExtendedProcessor<GetAtTenantScopeDeploymentsProcessor>(p => p.DeploymentName = Name).SendAsync();
                deployment = JsonSerializer.Deserialize<DeploymentExtended>(await response.Content.ReadAsStringAsync(), JsonOptions);
            }
            if (!IsFailed(deployment?.Properties?.ProvisioningState))
            {
                return null;
            }

            List<ErrorResponse> errors = (await CreateExtendedProcessor<FailedOperationReader>(p => p.DeploymentName = Name).ReadAllAsync())
                .Where(o => IsFailed(o.Properties?.ProvisioningState) && o.Properties?.StatusMessage?.Error is not null)
                .Select(o => o.Properties!.StatusMessage!.Error!)
                .ToList();

            string correlation = $"Correlation ID: {deployment!.Properties!.CorrelationId}";
            if (errors.Count > 0)
            {
                int shown = Math.Min(errors.Count, MaxErrorsShown);
                IEnumerable<string> parts = errors.Take(shown).Select(DeploymentOperationStatusMessage.Format).Append(correlation);
                return $"Deployment '{Name}' failed with errors, showing {shown} of {errors.Count} errors.\n\n{string.Join("\n\n", parts)}";
            }

            ErrorResponse? error = deployment.Properties.Error is JsonElement element ? element.Deserialize<ErrorResponse>(JsonOptions) : null;
            string summary = error is null ? $"Deployment '{Name}' failed." : $"{error.Code}: {error.Message}";
            return $"{summary}\n{correlation}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static bool IsFailed(string? state) => string.Equals(state, "Failed", StringComparison.OrdinalIgnoreCase);

    // Drains every operation page without writing the list as command output.
    private sealed class FailedOperationReader : ListTenantDeploymentOperationProcessor
    {
        internal async Task<List<DeploymentOperation>> ReadAllAsync()
        {
            using HttpResponseMessage response = await SendAsync();
            DeploymentOperationsListResult? first = JsonSerializer.Deserialize<DeploymentOperationsListResult>(await response.Content.ReadAsStringAsync(), JsonOptions);
            return await first.CollectWithPaginationAsync(b => b.Value, b => b.NextLink, CreateRequestMessage, Publisher, HttpPipeline, JsonOptions);
        }
    }
}
