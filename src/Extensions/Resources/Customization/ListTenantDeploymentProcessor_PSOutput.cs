// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// PowerShell-only <see cref="ListTenantDeploymentProcessor_Orchestrator"/> that outputs every deployment as a
    /// <see cref="PSDeployment"/>. Only the PowerShell command table routes here, so the CLI keeps printing the full deployments.
    /// </summary>
    public class ListTenantDeploymentProcessor_PSOutput : ListTenantDeploymentProcessor_Orchestrator
    {
        protected override void AfterInit()
        {
            base.AfterInit();
            WriteOutput = PSDeploymentOutput.Project(WriteOutput);
        }
    }
}