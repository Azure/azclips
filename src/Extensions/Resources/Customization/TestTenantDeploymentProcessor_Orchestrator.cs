// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class TestTenantDeploymentProcessor_Orchestrator : ValidateTenantDeploymentProcessor_Orchestrator
{
    public TestTenantDeploymentProcessor_Orchestrator()
    {
        SuppressSuccessOutput = true;
        ThrowOnError = false;
    }

    protected override Task<bool> BeforeWriteOutputAsyncAndReturn(
        Models.DeploymentValidateResult? body) =>
        Task.FromResult(body?.Error is null);
}
