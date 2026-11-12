// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

/// <summary>
/// New-/Test-AzTenantDeployment and Get-AzTenantDeploymentWhatIfResult inputs: the resource group deployment template
/// and parameter inputs plus Location. Tenant deployments are always Incremental, so there is no Mode.
/// </summary>
public abstract class TenantDeploymentTemplateCmdletBase : TenantDeploymentCmdletBase
{
    [Parameter(Mandatory = false, HelpMessage = "The name of the deployment. Defaults to a generated GUID when omitted.")]
    [Alias("DeploymentName")]
    public string? Name { get; set; }

    [Parameter(Mandatory = true)]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? TemplateUri { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The path to a local ARM JSON or Bicep template, resolved against the PowerShell session's current file-system location.")]
    public string? TemplateFile { get; set; }

    [Parameter(Mandatory = false)]
    public string? TemplateSpecId { get; set; }

    [Parameter(Mandatory = false)]
    public string? QueryString { get; set; }

    [Parameter(Mandatory = false)]
    public string? ParametersUri { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "The path to a local ARM JSON or .bicepparam parameters file, resolved against the PowerShell session's current file-system location.")]
    public string? ParametersFile { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? Parameter { get; set; }

    [Parameter(Mandatory = false)]
    public string? ValidationLevel { get; set; }
}
