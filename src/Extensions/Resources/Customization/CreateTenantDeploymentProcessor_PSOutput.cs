// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// PowerShell-only <see cref="CreateTenantDeploymentProcessor_Orchestrator"/> that outputs the deployment as a
    /// <see cref="PSDeployment"/> and, like New-AzTenantDeployment, lists the deployment operations while it runs for -Verbose
    /// progress. Only the PowerShell command table routes here, so the CLI keeps printing the full deployment.
    /// </summary>
    public class CreateTenantDeploymentProcessor_PSOutput : CreateTenantDeploymentProcessor_Orchestrator
    {
        protected override bool ReportsProgress => true;

        protected override void AfterInit()
        {
            base.AfterInit();
            WriteOutput = PSDeploymentOutput.Project(WriteOutput);
        }
    }
}