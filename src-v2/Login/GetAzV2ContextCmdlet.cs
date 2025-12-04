using System.Management.Automation;

namespace Login;

[Cmdlet(VerbsCommon.Get, "AzV2Context")]
[OutputType(typeof(string))]
public class GetAzV2ContextCmdlet : PSCmdlet
{
    protected override void ProcessRecord()
    {
        var args = new[] { "account", "show" };

        try
        {
            CliRunner.ExecuteCommand(args);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "GetAzV2ContextError", ErrorCategory.AuthenticationError, null));
        }
    }
}
