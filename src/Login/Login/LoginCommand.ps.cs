using System.Management.Automation;
using Core.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Login;

[Cmdlet(VerbsCommunications.Connect, "Cirrus")]
public class LoginCmdlet : Cmdlet
{
    private IServiceScope? _scope;

    protected override void BeginProcessing()
    {
        base.BeginProcessing();
        var serviceProvider = Bootstrapper.ServiceProviderAsync.GetAwaiter().GetResult();
        _scope = serviceProvider.CreateScope(); // is scope necessary or is root provider sufficient?
    }

    protected override void ProcessRecord()
    {
        var command = new LoginCommand(); // todo: [Wrapper/Execution layer] pass parameters
        // Yeming: instead of calling ICommandEngine here, directly execute the command for simplicity
        command.ExecuteAsync(_scope!.ServiceProvider).GetAwaiter().GetResult();
    }

    protected override void EndProcessing()
    {
        base.EndProcessing();
        _scope?.Dispose();
    }
}
