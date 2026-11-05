// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

// Validate is read-only, so like Test-AzResourceGroupDeployment it does not support ShouldProcess.
[Cmdlet(VerbsDiagnostic.Test, "AzTenantDeployment")]
[OutputType(typeof(DeploymentValidateResult))]
public class TestTenantDeploymentCmdlet : TenantDeploymentTemplateCmdletBase
{
    [Parameter(Mandatory = false)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? ParametersUri { get; set; }

    [Parameter(Mandatory = false)]
    public string? ParametersFile { get; set; }

    protected override void ProcessRecord() => Invoke("Test-AzTenantDeployment", GetBoundParameters());
}
