using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Cmdlets.Config;

public abstract class ConfigCmdletBase : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    protected OutputStreams Streams = null!;
    protected IEngine Engine = null!;

    protected override void BeginProcessing()
    {
        Streams = new OutputStreams(
            WriteOutput: (value, ct) => { WriteObject(value); return Task.CompletedTask; },
            WriteVerbose: (value, ct) => { WriteVerbose(value?.ToString() ?? string.Empty); return Task.CompletedTask; },
            WriteDebug: (value, ct) => { WriteDebug(value?.ToString() ?? string.Empty); return Task.CompletedTask; },
            WriteWarning: (value, ct) => { WriteWarning(value?.ToString() ?? string.Empty); return Task.CompletedTask; },
            WriteError: (value, ct) =>
            {
                WriteError(new ErrorRecord(
                    new Exception(value?.ToString() ?? string.Empty),
                    "ConfigError",
                    ErrorCategory.NotSpecified,
                    null));
                return Task.CompletedTask;
            },
            WriteInformation: (value, ct) =>
            {
                WriteInformation(new InformationRecord(value, null));
                return Task.CompletedTask;
            });

        Engine = CirrusServiceProvider.GetRequiredService<IEngine>();
    }

    protected void ExecuteInvocation(Dictionary<string, object> parameters)
    {
        var commandName = MyInvocation.MyCommand.Name;

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Config", commandName, parameters, false, moduleName, moduleVersion);

        using IInvocationSession session = Engine.InvokeSession(invocation, Streams, _cts.Token);
        session.Process();
    }

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();
}
