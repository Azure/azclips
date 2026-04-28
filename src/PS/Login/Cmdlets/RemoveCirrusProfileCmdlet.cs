using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Core.Telemetry;

namespace Microsoft.Azure.Cirrus.Cmdlets.Login;

[Cmdlet(VerbsCommon.Remove, "CirrusProfile", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public class RemoveCirrusProfileCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true, Position = 0)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = false)]
    public SwitchParameter Force { get; set; }

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
        if (!Force.IsPresent && !ShouldProcess(Name, "Remove-CirrusProfile"))
            return;

        using var activity = CirrusServiceProvider.GetRequiredService<ITelemetryService>().StartPowerShellActivity(MyInvocation.MyCommand.Name, MyInvocation.BoundParameters);
        var parameters = new Dictionary<string, object> { ["Name"] = Name };
        var invocation = new CommandInvocation("Login", "Remove-CirrusProfile", parameters, false, MyInvocation.MyCommand.ModuleName, MyInvocation.MyCommand.Version?.ToString());
        using IInvocationSession session = engine.InvokeSession(invocation, streams, _cts.Token);
        session.Process();
    }

    protected override void EndProcessing() => _cts.Dispose();
    protected override void StopProcessing() => _cts.Cancel();
}
