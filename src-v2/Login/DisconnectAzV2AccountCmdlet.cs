using System.Management.Automation;

namespace Login;

[Cmdlet(VerbsCommunications.Disconnect, "AzV2Account")]
[OutputType(typeof(string))]
public class DisconnectAzV2AccountCmdlet : PSCmdlet
{
    protected override void ProcessRecord()
    {
        var options = new Dictionary<string, string>
        {
            ["action"] = "logout"
        };

        try
        {
            var command = new LoginCommand();
            command.ExecuteAsync(options, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "DisconnectAzV2AccountError", ErrorCategory.AuthenticationError, null));
        }
    }
}
