using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Core.Telemetry;

namespace Microsoft.Azure.Cirrus.Cmdlets.Login;

[Cmdlet(VerbsCommon.Get, "CirrusProfile", DefaultParameterSetName = "Show")]
public class GetCirrusProfileCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false, Position = 0, ParameterSetName = "Show")]
    public string? Name { get; set; }

    [Parameter(Mandatory = false, ParameterSetName = "List")]
    public SwitchParameter List { get; set; }

    protected override void BeginProcessing()
    {
        streams = new OutputStreams(
            WriteOutput: (value, ct) => { WriteObject(value); return Task.CompletedTask; },
            WriteVerbose: (value, ct) => { WriteVerbose(value?.ToString() ?? ""); return Task.CompletedTask; },
            WriteDebug: (value, ct) => { WriteDebug(value?.ToString() ?? ""); return Task.CompletedTask; },
            WriteWarning: (value, ct) => { WriteWarning(value?.ToString() ?? ""); return Task.CompletedTask; },
            WriteError: (value, ct) =>
            {
                WriteError(new ErrorRecord(new Exception(value?.ToString() ?? ""), "ProfileError", ErrorCategory.InvalidOperation, null));
                return Task.CompletedTask;
            },
            WriteInformation: (value, ct) =>
            {
                WriteInformation(new InformationRecord(value, null));
                return Task.CompletedTask;
            });
        engine = CirrusServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        using var activity = CirrusServiceProvider.GetRequiredService<ITelemetryService>().StartPowerShellActivity(MyInvocation.MyCommand.Name, MyInvocation.BoundParameters);

        if (List.IsPresent)
        {
            var invocation = new CommandInvocation("Login", "Get-CirrusProfile", new Dictionary<string, object>(), false, MyInvocation.MyCommand.ModuleName, MyInvocation.MyCommand.Version?.ToString());
            using IInvocationSession session = engine.InvokeSession(invocation, streams, _cts.Token);
            session.Process();
        }
        else
        {
            var parameters = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(Name))
                parameters["Name"] = Name;
            var invocation = new CommandInvocation("Login", "Get-CirrusProfile", parameters, false, MyInvocation.MyCommand.ModuleName, MyInvocation.MyCommand.Version?.ToString());
            using IInvocationSession session = engine.InvokeSession(invocation, streams, _cts.Token);
            session.Process();
        }
    }

    protected override void EndProcessing() => _cts.Dispose();
    protected override void StopProcessing() => _cts.Cancel();
}
