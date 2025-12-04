using System.Management.Automation;

namespace Login;

[Cmdlet(VerbsCommunications.Disconnect, "AzV2Account")]
[OutputType(typeof(string))]
public class DisconnectAzV2AccountCmdlet : PSCmdlet
{
    protected override void ProcessRecord()
    {
        var args = new[] { "logout" };

        try
        {
            CliRunner.ExecuteCommand(args);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "DisconnectAzV2AccountError", ErrorCategory.AuthenticationError, null));
        }
    }
}
