using System.Management.Automation;

namespace Group;

[Cmdlet(VerbsCommon.Get, "AzV2ResourceGroup")]
[OutputType(typeof(string))]
public class GetAzV2ResourceGroupCmdlet : PSCmdlet
{
    [Parameter(Mandatory = false, ValueFromPipelineByPropertyName = true)]
    public string? Name { get; set; }

    protected override void ProcessRecord()
    {
        var args = new List<string> { "group", "list" };
        if (!string.IsNullOrEmpty(Name))
        {
            args.AddRange(new[] { "--name", Name });
        }

        try
        {
            CliRunner.ExecuteCommand(args.ToArray());
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "GetAzV2ResourceGroupError", ErrorCategory.OperationStopped, Name));
        }
    }
}
