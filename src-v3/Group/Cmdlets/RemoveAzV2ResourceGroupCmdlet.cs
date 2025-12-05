using System.Management.Automation;
using Group.Business;
using Group.Business.Models;

namespace Group.Cmdlets;

/// <summary>
/// PowerShell adapter for deleting resource groups.
/// Translates PowerShell parameters to business request and formats output for PowerShell.
/// </summary>
[Cmdlet(VerbsCommon.Remove, "AzV3ResourceGroup")]
[OutputType(typeof(bool))]
public class RemoveAzV3ResourceGroupCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
    public string Name { get; set; } = string.Empty;

    [Parameter(Mandatory = false)]
    public SwitchParameter Force { get; set; }

    protected override void ProcessRecord()
    {
        // PowerShell confirmation
        if (!Force && !ShouldContinue($"Remove resource group '{Name}'?", "Confirm"))
        {
            return;
        }

        // Translate PowerShell parameters to business request
        var request = new DeleteGroupRequest
        {
            Name = Name
        };

        try
        {
            WriteVerbose($"Deleting resource group '{Name}'...");
            
            // Call business logic (same as CLI!)
            var deleted = GroupManager.DeleteGroupAsync(request, CancellationToken.None).GetAwaiter().GetResult();
            
            if (!deleted)
            {
                WriteWarning($"Resource group '{Name}' not found.");
            }
            else
            {
                WriteVerbose($"Resource group '{Name}' deleted successfully.");
                WriteObject(true);
            }
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "RemoveAzV3ResourceGroupError", ErrorCategory.OperationStopped, Name));
        }
    }
}
