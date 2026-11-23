// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsLifecycle.Stop, "AzTenantDeployment")]
[OutputType(typeof(void))]
public class StopTenantDeploymentCmdlet : TenantDeploymentCmdletBase
{
    [Parameter(Mandatory = true, Position = 0, HelpMessage = "The name of the deployment.")]
    [Alias("DeploymentName")]
    [ValidateNotNullOrEmpty]
    public string Name { get; set; } = "";

    protected override void ProcessRecord() => Invoke("Stop-AzTenantDeployment", GetBoundParameters());
}
