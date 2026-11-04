// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsCommon.New, "AzTenantDeployment")]
[OutputType(typeof(DeploymentExtended))]
public class NewTenantDeploymentCmdlet : TenantDeploymentTemplateCmdletBase
{
    [Parameter(Mandatory = false, HelpMessage = "Return once the deployment is submitted and output its initial state, instead of waiting for it to finish. Validation still runs first. Does not create a PowerShell job.")]
    public SwitchParameter NoWait { get; set; }

    protected override void ProcessRecord() => Invoke("New-AzTenantDeployment", GetBoundParameters());
}
