// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsCommon.Get, "AzTenantDeployment")]
[OutputType(typeof(DeploymentExtended))]
public class GetTenantDeploymentCmdlet : TenantDeploymentCmdletBase
{
    [Parameter(Mandatory = false, Position = 0, HelpMessage = "The name of deployment.")]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    protected override bool EnumerateOutput => true;

    protected override void ProcessRecord() =>
        Invoke(string.IsNullOrEmpty(Name) ? "Get-AzTenantDeployment_List" : "Get-AzTenantDeployment_Get", GetBoundParameters());
}
