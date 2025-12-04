using System.Management.Automation;

namespace Group;

[Cmdlet(VerbsCommon.New, "AzV2ResourceGroup")]
[OutputType(typeof(string))]
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
        var args = new List<string> { "group", "create", "--name", Name, "--location", Location };
        
        if (Tags != null && Tags.Count > 0)
        {
            args.Add("--tags");
            args.Add(string.Join(",", Tags.Select(kvp => $"{kvp.Key}={kvp.Value}")));
        }

        try
        {
            CliRunner.ExecuteCommand(args.ToArray());
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "NewAzV2ResourceGroupError", ErrorCategory.OperationStopped, Name));
        }
    }
}
