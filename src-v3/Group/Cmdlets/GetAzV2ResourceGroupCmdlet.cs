using System.Management.Automation;
using Group.Business;
using Group.Business.Models;

namespace Group.Cmdlets;

/// <summary>
/// PowerShell adapter for listing/getting resource groups.
/// Translates PowerShell parameters to business request and formats output for PowerShell.
/// </summary>
[Cmdlet(VerbsCommon.Get, "AzV3ResourceGroup")]
[OutputType(typeof(GroupInfo[]))]
public class GetAzV3ResourceGroupCmdlet : PSCmdlet
{
    [Parameter(Mandatory = false, ValueFromPipelineByPropertyName = true)]
    public string? Name { get; set; }

    protected override void ProcessRecord()
    {
        // Translate PowerShell parameters to business request
        var request = new ListGroupsRequest
        {
            Name = Name
        };

        try
        {
            // Call business logic (same as CLI!)
            var results = GroupManager.ListGroupsAsync(request, CancellationToken.None).GetAwaiter().GetResult();
            
            // Output for PowerShell - return the objects
            if (results.Count == 0)
            {
                if (!string.IsNullOrEmpty(Name))
                {
                    WriteWarning($"Resource group '{Name}' not found.");
                }
                return;
            }

            foreach (var group in results)
            {
                WriteObject(group);
            }
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "GetAzV3ResourceGroupError", ErrorCategory.OperationStopped, Name));
        }
    }
}
