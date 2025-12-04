using System.Management.Automation;

namespace Group;

[Cmdlet(VerbsCommon.Remove, "AzV2ResourceGroup")]
[OutputType(typeof(string))]
public class RemoveAzV2ResourceGroupCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
    public string Name { get; set; } = string.Empty;

    [Parameter(Mandatory = false)]
    public SwitchParameter Force { get; set; }

    protected override void ProcessRecord()
    {
        if (!Force && !ShouldContinue($"Remove resource group '{Name}'?", "Confirm"))
        {
            return;
        }

        var args = new List<string> { "group", "delete", "--name", Name };

        try
        {
            CliRunner.ExecuteCommand(args.ToArray());
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "RemoveAzV2ResourceGroupError", ErrorCategory.OperationStopped, Name));
        }
    }
}
