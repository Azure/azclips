// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// PowerShell-only <see cref="GetSubscriptionDeploymentProcessor_Orchestrator"/> that outputs the deployment as a
    /// <see cref="PSDeployment"/>. Only the PowerShell command table routes here, so the CLI keeps printing the full
    /// deployment.
    /// </summary>
    public class GetSubscriptionDeploymentProcessor_PSOutput : GetSubscriptionDeploymentProcessor_Orchestrator
    {
        protected override void AfterInit()
        {
            base.AfterInit();
            WriteOutput = PSDeploymentOutput.Project(WriteOutput);
        }
    }
}
