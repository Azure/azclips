using System.Management.Automation;

namespace Group;

[Cmdlet(VerbsCommon.Set, "AzV2ResourceGroup")]
[OutputType(typeof(string))]
public class SetAzV2ResourceGroupCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
    public string Name { get; set; } = string.Empty;

    [Parameter(Mandatory = false, ValueFromPipelineByPropertyName = true)]
    public Dictionary<string, string>? Tags { get; set; }

    protected override void ProcessRecord()
    {
        var options = new Dictionary<string, string>
        {
            ["name"] = Name
        };
        
        if (Tags != null && Tags.Count > 0)
        {
            options["tags"] = string.Join(",", Tags.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        }

        try
        {
            var command = new GroupShowCommand();
            command.ExecuteAsync(options, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "SetAzV2ResourceGroupError", ErrorCategory.OperationStopped, Name));
        }
    }
}
