using System.Management.Automation;
using Core.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Login;

[Cmdlet(VerbsCommunications.Connect, "Cirrus")]
public class LoginCmdlet : Cmdlet
{
    // todo: [Wrapper/Execution layer] add parameters

    protected override void ProcessRecord()
    {
        var command = new LoginCommand(); // todo: [Wrapper/Execution layer] pass parameters

        // todo: [Wrapper/Execution layer] handle output (typed), errors, etc.
        var serviceProvider = Bootstrapper.StartAsync().GetAwaiter().GetResult();
        using var scope = serviceProvider.CreateScope(); // is scope necessary or is root provider sufficient?
        command.ExecuteAsync(scope.ServiceProvider).GetAwaiter().GetResult(); // todo: [Execution layer] Using GetAwaiter().GetResult() in a PowerShell cmdlet can cause deadlocks in certain synchronization contexts. Consider using async/await properly or handling the task differently. PowerShell cmdlets should either use synchronous methods or properly implement async patterns with ProcessRecordAsync in PowerShell 7+.
    }
}
