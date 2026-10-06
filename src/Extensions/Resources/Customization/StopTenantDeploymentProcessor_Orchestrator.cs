// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class StopTenantDeploymentProcessor_Orchestrator : ResourcesProcessorBase<object, object>
{
    public string Name { get; set; } = string.Empty;

    // Forwards directly to ARM's cancel operation, matching `az deployment tenant cancel`
    // (cancel_deployment_at_tenant_scope) and `Stop-AzTenantDeployment`, which both delegate
    // straight to the service instead of pre-checking provisioningState locally. ARM itself
    // rejects cancellation of a deployment that is no longer running.
    internal override async Task<HttpResponseMessage> SendAsync()
    {
        CancelAtTenantScopeDeploymentsProcessor cancel =
            CreateExtendedProcessor<CancelAtTenantScopeDeploymentsProcessor>(p => p.DeploymentName = Name);
        return await cancel.SendAsync();
    }

    protected override Task<bool> BeforeWriteOutputAsyncAndReturn(object? body) =>
        Task.FromResult(true);
}
