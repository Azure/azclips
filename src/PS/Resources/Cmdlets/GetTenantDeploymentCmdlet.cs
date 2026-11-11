// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsCommon.Get, "AzTenantDeployment", DefaultParameterSetName = "GetByDeploymentName")]
[OutputType(typeof(DeploymentExtended))]
public class GetTenantDeploymentCmdlet : TenantDeploymentCmdletBase
{
    [Parameter(Mandatory = false, Position = 0, ParameterSetName = "GetByDeploymentName", HelpMessage = "The name of deployment.")]
    [Alias("DeploymentName")]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = "ListByFilter", HelpMessage = "An OData filter, for example \"provisioningState eq 'Succeeded'\".")]
    public string? Filter { get; set; }

    protected override bool EnumerateOutput => true;

    protected override void ProcessRecord() =>
        Invoke(string.IsNullOrEmpty(Name) ? "Get-AzTenantDeployment_List" : "Get-AzTenantDeployment_Get", GetBoundParameters());
}
