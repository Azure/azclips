// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// PowerShell-only <see cref="ListManagementGroupDeploymentOperationProcessor"/> that outputs every operation of
    /// the deployment as a <see cref="PSDeploymentOperation"/>. Only the PowerShell command table routes here,
    /// so the CLI keeps printing the full operation.
    /// </summary>
    public class ListManagementGroupDeploymentOperationProcessor_PSOutput : ListManagementGroupDeploymentOperationProcessor
    {
        protected override void AfterInit()
        {
            base.AfterInit();
            WriteOutput = PSDeploymentOperationOutput.Project(WriteOutput);
        }
    }
}
