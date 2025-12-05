using System.Management.Automation;
using Group.Business;
using Group.Business.Models;

namespace Group.Cmdlets;

/// <summary>
/// PowerShell adapter for creating resource groups.
/// Translates PowerShell parameters to business request and formats output for PowerShell.
/// </summary>
[Cmdlet(VerbsCommon.New, "AzV2ResourceGroup")]
[OutputType(typeof(GroupInfo))]
public class NewAzV2ResourceGroupCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
    public string Name { get; set; } = string.Empty;

    [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
    public string Location { get; set; } = string.Empty;

    [Parameter(Mandatory = false, ValueFromPipelineByPropertyName = true)]
    public Dictionary<string, string>? Tags { get; set; }

    protected override void ProcessRecord()
    {
        // Translate PowerShell parameters to business request
        var request = new CreateGroupRequest
        {
            Name = Name,
            Location = Location,
            Tags = Tags
        };

        try
        {
            WriteVerbose($"Creating resource group '{Name}' in location '{Location}'...");
            
            // Call business logic (same as CLI!)
            var result = GroupManager.CreateGroupAsync(request, CancellationToken.None).GetAwaiter().GetResult();
            
            // Output for PowerShell - return the object
            WriteObject(result);
            WriteVerbose($"Resource group '{result.Name}' created successfully.");
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "NewAzV2ResourceGroupError", ErrorCategory.OperationStopped, Name));
        }
    }
}
