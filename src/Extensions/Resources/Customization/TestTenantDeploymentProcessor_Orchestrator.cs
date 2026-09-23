// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class TestTenantDeploymentProcessor_Orchestrator : ValidateTenantDeploymentProcessor_Orchestrator
{
    public TestTenantDeploymentProcessor_Orchestrator()
    {
        ThrowOnError = false;
    }
}
