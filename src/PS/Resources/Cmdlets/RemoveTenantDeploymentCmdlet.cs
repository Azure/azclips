// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsCommon.Remove, "AzTenantDeployment", SupportsShouldProcess = true)]
[OutputType(typeof(void))]
public class RemoveTenantDeploymentCmdlet : TenantDeploymentCmdletBase
{
    [Parameter(Mandatory = true, Position = 0, HelpMessage = "The name of the deployment.")]
    [Alias("DeploymentName")]
    [ValidateNotNullOrEmpty]
    public string Name { get; set; } = "";

    protected override void ProcessRecord()
    {
        if (ShouldProcess(Name, "Deleting the deployment ..."))
        {
            Invoke("Remove-AzTenantDeployment", GetBoundParameters());
        }
    }
}
