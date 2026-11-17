// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// PowerShell-only <see cref="GetResourceGroupDeploymentOperationProcessor_Orchestrator"/> that outputs every
    /// requested operation as a <see cref="PSDeploymentOperation"/>. Only the PowerShell command table routes here,
    /// so the CLI keeps printing the full operation.
    /// </summary>
    public class GetResourceGroupDeploymentOperationProcessor_PSOutput : GetResourceGroupDeploymentOperationProcessor_Orchestrator
    {
        protected override void AfterInit()
        {
            base.AfterInit();
            WriteOutput = PSDeploymentOperationOutput.Project(WriteOutput);
        }
    }
}
