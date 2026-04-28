using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Core.Telemetry;

namespace Microsoft.Azure.Cirrus.Cmdlets.Login;

[Cmdlet(VerbsCommon.Set, "CirrusProfile")]
public class SetCirrusProfileCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true, Position = 0)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? SubscriptionId { get; set; }

    [Parameter(Mandatory = false)]
    public string? TenantId { get; set; }

    [Parameter(Mandatory = false)]
    public string? Default { get; set; }

    [Parameter(Mandatory = false)]
    public string? RemoveDefault { get; set; }

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
        var parameters = new Dictionary<string, object> { ["Name"] = Name };
        if (SubscriptionId is not null) parameters["Subscription"] = SubscriptionId;
        if (TenantId is not null) parameters["TenantId"] = TenantId;
        if (Default is not null) parameters["Default"] = Default;
        if (RemoveDefault is not null) parameters["RemoveDefault"] = RemoveDefault;

        var invocation = new CommandInvocation("Login", "Set-CirrusProfile", parameters, false, MyInvocation.MyCommand.ModuleName, MyInvocation.MyCommand.Version?.ToString());
        using IInvocationSession session = engine.InvokeSession(invocation, streams, _cts.Token);
        session.Process();
    }

    protected override void EndProcessing() => _cts.Dispose();
    protected override void StopProcessing() => _cts.Cancel();
}
