// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

[Cmdlet(VerbsData.Save, "AzTenantDeploymentTemplate", SupportsShouldProcess = true)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Resources.Processors.DeploymentTemplateSaveResult")]
public class SaveTenantDeploymentTemplateCmdlet : TenantDeploymentCmdletBase
{
    [Parameter(Mandatory = true, Position = 0, HelpMessage = "The name of the deployment.")]
    [Alias("DeploymentName")]
    [ValidateNotNullOrEmpty]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = false, HelpMessage = "The output path of the template file.")]
    [ValidateNotNullOrEmpty]
    public string? Path { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Overwrite the destination file when it already exists.")]
    public SwitchParameter Force { get; set; }

    protected override void ProcessRecord()
    {
        if (!ShouldProcess(Name, VerbsData.Save))
        {
            return;
        }

        Dictionary<string, object> parameters = GetBoundParameters();
        // A relative path is relative to the PowerShell location, not the process working directory.
        parameters["Path"] = Path is null ? SessionState.Path.CurrentFileSystemLocation.Path : GetUnresolvedProviderPathFromPSPath(Path);
        Invoke("Save-AzTenantDeploymentTemplate", parameters);
    }
}
