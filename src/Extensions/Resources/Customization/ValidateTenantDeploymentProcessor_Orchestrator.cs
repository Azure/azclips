// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class ValidateTenantDeploymentProcessor_Orchestrator
    : TenantDeploymentOrchestratorBase<Deployment, DeploymentValidateResult>
{
    public bool ThrowOnError { get; set; } = true;

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

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentValidateResult? body)
    {
        if (ThrowOnError)
        {
            ThrowIfError(body?.Error, "Tenant deployment validation");
        }
        return false;
    }
}
