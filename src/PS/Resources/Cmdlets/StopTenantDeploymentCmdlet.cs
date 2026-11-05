// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsLifecycle.Stop, "AzTenantDeployment", SupportsShouldProcess = true)]
public class StopTenantDeploymentCmdlet : TenantDeploymentCmdletBase
{
    [Parameter(Mandatory = true, Position = 0, HelpMessage = "The name of the deployment.")]
    [ValidateNotNullOrEmpty]
    public string Name { get; set; } = "";

    protected override void ProcessRecord()
    {
        if (ShouldProcess(Name, "Cancelling active deployment ..."))
        {
            Invoke("Stop-AzTenantDeployment", GetBoundParameters());
        }
    }
}
