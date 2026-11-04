// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// PowerShell-only <see cref="NewResourceGroupDeploymentProcessor_Orchestrator"/> that, like New-AzResourceGroupDeployment,
    /// lists the deployment's operations while it runs and writes each resource's provisioning state for <c>-Verbose</c>.
    /// Only the PowerShell command table routes here, so the CLI keeps waiting on the deployment alone.
    /// </summary>
    public class NewResourceGroupDeploymentProcessor_PSOutput : NewResourceGroupDeploymentProcessor_Orchestrator
    {
        protected override bool ReportsProgress => true;
    }
}
