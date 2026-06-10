using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Cmdlets.Compute;

[Cmdlet(VerbsCommon.Get, "VM", DefaultParameterSetName = "List")]
public class GetVMCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(ParameterSetName = "Get", Mandatory = true)]
    [Parameter(ParameterSetName = "List", Mandatory = true)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(ParameterSetName = "Get", Mandatory = true)]
    [Parameter(ParameterSetName = "List", Mandatory = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(ParameterSetName = "Get", Mandatory = true)]
    public string? Name { get; set; }

    protected override void BeginProcessing()
    {
        streams = new OutputStreams(
            WriteOutput: (value, ct) =>
            {
                if (value is IEnumerable and not string)
                    WriteObject(value, true);
                else
                    WriteObject(value);
                return Task.CompletedTask;
            },
            WriteVerbose: (value, ct) => { WriteVerbose(value?.ToString() ?? ""); return Task.CompletedTask; },
            WriteDebug: (value, ct) => { WriteDebug(value?.ToString() ?? ""); return Task.CompletedTask; },
            WriteWarning: (value, ct) => { WriteWarning(value?.ToString() ?? ""); return Task.CompletedTask; },
            WriteError: (value, ct) =>
            {
                WriteError(new ErrorRecord(
                    new Exception(value?.ToString() ?? ""),
                    "VMError",
                    ErrorCategory.NotSpecified,
                    null));
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
        var telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))
        {
            parameters.Add("SubscriptionId", SubscriptionId);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ResourceGroupName)))
        {
            parameters.Add("ResourceGroupName", ResourceGroupName);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Name)))
        {
            parameters.Add("Name", Name!);
        }

        var parameterSetName = ParameterSetName;
        var commandName = $"Get-VM_{parameterSetName}";
        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Compute", commandName, parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();
}
