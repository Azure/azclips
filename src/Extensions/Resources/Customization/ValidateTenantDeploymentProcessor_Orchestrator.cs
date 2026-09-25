// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class ValidateTenantDeploymentProcessor_Orchestrator
    : TenantDeploymentOrchestratorBase<ScopedDeployment, DeploymentValidateResult>
{
    public bool ThrowOnError { get; set; } = true;

    public bool SuppressSuccessOutput { get; set; }

    internal override async Task<HttpResponseMessage> SendAsync()
    {
        PrepareAndValidate();
        ValidateAtTenantScopeDeploymentsProcessor processor =
            CreateExtendedProcessor<ValidateAtTenantScopeDeploymentsProcessor>(p =>
            {
                p.DeploymentName = Name;
                p.Body = BuildDeployment();
            });
        return await processor.SendAsync();
    }

    protected override Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentValidateResult? body)
    {
        if (ThrowOnError)
        {
            ThrowIfError(body?.Error, "Tenant deployment validation");
        }
        return Task.FromResult(SuppressSuccessOutput);
    }
}
