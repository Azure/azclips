// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class TestManagementGroupDeploymentProcessor_Orchestrator
    : ValidateManagementGroupDeploymentProcessor_Orchestrator
{
    public TestManagementGroupDeploymentProcessor_Orchestrator()
    {
        SuppressSuccessOutput = true;
    }
}
