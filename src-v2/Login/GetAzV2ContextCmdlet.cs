using System.Management.Automation;

namespace Login;

[Cmdlet(VerbsCommon.Get, "AzV2Context")]
[OutputType(typeof(string))]
public class GetAzV2ContextCmdlet : PSCmdlet
{
    protected override void ProcessRecord()
    {
        var options = new Dictionary<string, string>
        {
            ["action"] = "show"
        };

        try
        {
            var command = new LoginCommand();
            command.ExecuteAsync(options, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "GetAzV2ContextError", ErrorCategory.AuthenticationError, null));
        }
    }
}
