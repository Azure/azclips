// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

// What-if is read-only, so like Test-AzTenantDeployment it does not support ShouldProcess.
[Cmdlet(VerbsCommon.Get, "AzTenantDeploymentWhatIfResult")]
[OutputType(typeof(string))]
public class GetTenantDeploymentWhatIfResultCmdlet : TenantDeploymentTemplateCmdletBase
{
    [Parameter(Mandatory = false)]
    [Alias("DeploymentName")]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? ParametersUri { get; set; }

    [Parameter(Mandatory = false)]
    public string? ParametersFile { get; set; }

    [Parameter(Mandatory = false)]
    public string? ResultFormat { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? ExcludeChangeType { get; set; }

    protected override void ProcessRecord() => Invoke("Get-AzTenantDeploymentWhatIfResult", GetBoundParameters());
}
