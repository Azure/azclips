using System.Management.Automation;
using Group.Business;
using Group.Business.Models;

namespace Group.Cmdlets;

/// <summary>
/// PowerShell adapter for updating resource group (currently shows details).
/// Translates PowerShell parameters to business request and formats output for PowerShell.
/// </summary>
[Cmdlet(VerbsCommon.Set, "AzV2ResourceGroup")]
[OutputType(typeof(GroupInfo))]
public class SetAzV2ResourceGroupCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
    public string Name { get; set; } = string.Empty;

    [Parameter(Mandatory = false, ValueFromPipelineByPropertyName = true)]
    public Dictionary<string, string>? Tags { get; set; }

    protected override void ProcessRecord()
    {
        // Note: Azure SDK doesn't have a direct update for tags on resource groups
        // This would typically require get + update. For now, just show the group.
        // TODO: Implement tag update logic if needed
        
        var request = new ShowGroupRequest
        {
            Name = Name
        };

        try
        {
            // Call business logic
            var result = GroupManager.ShowGroupAsync(request, CancellationToken.None).GetAwaiter().GetResult();
            
            if (result == null)
            {
                WriteWarning($"Resource group '{Name}' not found.");
                return;
            }

            // Output for PowerShell
            WriteObject(result);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "SetAzV2ResourceGroupError", ErrorCategory.OperationStopped, Name));
        }
    }
}
