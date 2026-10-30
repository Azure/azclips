// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// PowerShell-only <see cref="ListSubscriptionDeploymentProcessor"/> that outputs every deployment of the
    /// subscription as a <see cref="PSDeployment"/>. Only the PowerShell command table routes here, so the CLI keeps
    /// printing the full deployment.
    /// </summary>
    public class ListSubscriptionDeploymentProcessor_PSOutput : ListSubscriptionDeploymentProcessor
    {
        protected override void AfterInit()
        {
            base.AfterInit();
            WriteOutput = PSDeploymentOutput.Project(WriteOutput);
        }
    }
}
