// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// <c>az deployment tenant cancel</c> / <c>Stop-AzTenantDeployment</c>: generated cancel with the legacy error text.
/// PS keeps the legacy pre-check and cancels only a deployment that is not Succeeded/Failed.
/// </summary>
public class CancelAtTenantScopeDeploymentsProcessor_Customization : CancelAtTenantScopeDeploymentsProcessor
{
    protected override void AfterInit() => Publisher = TenantDeploymentErrorPublisher.Wrap(Publisher, powerShell: PSDeployment.ForPowerShell);

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        if (PSDeployment.ForPowerShell)
        {
            await EnsureRunningAsync();
        }

        return await base.SendAsync();
    }

    private async Task EnsureRunningAsync()
    {
        if (string.IsNullOrEmpty(DeploymentName))
        {
            throw new ArgumentException($"There is no deployment called '{DeploymentName}' to cancel");
        }

        GetAtTenantScopeDeploymentsProcessor get = CreateExtendedProcessor<GetAtTenantScopeDeploymentsProcessor>(p => p.DeploymentName = DeploymentName);
        using HttpResponseMessage response = await get.SendAsync();
        var state = JsonSerializer.Deserialize<DeploymentExtended>(await response.Content.ReadAsStringAsync(), JsonOptions)?.Properties?.ProvisioningState;
        if (string.Equals(state, "Succeeded", StringComparison.OrdinalIgnoreCase) || string.Equals(state, "Failed", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"There is no running deployment called '{DeploymentName}' at the current tenant scope.");
        }
    }
}