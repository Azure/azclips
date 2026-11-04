// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsData.Save, "AzTenantDeploymentTemplate")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Processors.DeploymentTemplateSaveResult")]
public class SaveTenantDeploymentTemplateCmdlet : TenantDeploymentCmdletBase
{
    [Parameter(Mandatory = true, Position = 0, HelpMessage = "The name of the deployment whose template to save.")]
    [Alias("Name")]
    [ValidateNotNullOrEmpty]
    public string DeploymentName { get; set; } = "";

    [Parameter(Mandatory = false, HelpMessage = "The file or folder to write the template to. A folder receives '<DeploymentName>.json'. Defaults to the current location.")]
    [ValidateNotNullOrEmpty]
    public string? Path { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Overwrite the template file when it already exists.")]
    public SwitchParameter Force { get; set; }

    protected override void ProcessRecord()
    {
        // A relative path is relative to the PowerShell session's current file-system location, which can differ
        // from the process working directory the processor would otherwise see.
        var parameters = GetBoundParameters();
        parameters[nameof(Path)] = MyInvocation.BoundParameters.ContainsKey(nameof(Path))
            ? GetUnresolvedProviderPathFromPSPath(Path)
            : SessionState.Path.CurrentFileSystemLocation.Path;

        Invoke("Save-AzTenantDeploymentTemplate", parameters);
    }
}
