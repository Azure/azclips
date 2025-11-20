using System.Management.Automation;

namespace Login;

[Cmdlet(VerbsCommunications.Connect, "Cirrus")]
public class LoginCmdlet : Cmdlet
{
    // todo: add parameters

    protected override void ProcessRecord()
    {
        var command = new LoginCommand(); // todo: pass parameters
        command.ExecuteAsync().GetAwaiter().GetResult();

        // todo: handle output (typed), errors, etc.
    }
}
