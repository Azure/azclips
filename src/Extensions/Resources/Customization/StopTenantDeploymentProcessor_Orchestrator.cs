// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class StopTenantDeploymentProcessor_Orchestrator : ResourcesProcessorBase<object, object>
{
    public string Name { get; set; } = string.Empty;

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        GetAtTenantScopeDeploymentsProcessor get =
            CreateExtendedProcessor<GetAtTenantScopeDeploymentsProcessor>(p => p.DeploymentName = Name);
        using HttpResponseMessage response = await get.SendAsync();
        string rawBody = response.Content is null ? string.Empty : await response.Content.ReadAsStringAsync();
        DeploymentExtended? deployment = JsonSerializer.Deserialize<DeploymentExtended>(rawBody, JsonOptions);
        string? state = deployment?.Properties?.ProvisioningState;
        if (string.Equals(state, "Succeeded", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, "Failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(state, "Canceled", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Tenant deployment '{Name}' is not running and cannot be canceled.");
        }

        CancelAtTenantScopeDeploymentsProcessor cancel =
            CreateExtendedProcessor<CancelAtTenantScopeDeploymentsProcessor>(p => p.DeploymentName = Name);
        return await cancel.SendAsync();
    }
}
