// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// PowerShell-only <see cref="ListTenantDeploymentOperationProcessor"/> that outputs every operation of
    /// the deployment as a <see cref="PSDeploymentOperation"/>. Only the PowerShell command table routes here,
    /// so the CLI keeps printing the full operation.
    /// </summary>
    public class ListTenantDeploymentOperationProcessor_PSOutput : ListTenantDeploymentOperationProcessor
    {
        protected override void AfterInit()
        {
            base.AfterInit();
            WriteOutput = PSDeploymentOperationOutput.Project(WriteOutput);
        }
    }
}
