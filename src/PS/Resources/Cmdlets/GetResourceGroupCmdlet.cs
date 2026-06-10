using System.Collections;
using System.Diagnostics;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Core.Telemetry;
using Core.Telemetry.Constants;

namespace Microsoft.Azure.Cirrus.Cmdlets.Resources;

[Cmdlet(VerbsCommon.Get, "ResourceGroup", DefaultParameterSetName = "List")]
public class GetResourceGroupCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;
    private ClientType clientType;

    [Parameter(ParameterSetName = "Get", Mandatory = true)]
    [Parameter(ParameterSetName = "List", Mandatory = true)]
    public string SubscriptionId { get; set; } = "";

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
                    "ResourceGroupError",
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
        clientType = CirrusServiceProvider.GetRequiredService<ClientType>();
    }

    protected override void ProcessRecord()
    {
        using var activity = CirrusServiceProvider.GetRequiredService<ITelemetryService>().StartRootActivity();
        activity.TagClientContext(clientType);
        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))
        {
            parameters.Add("SubscriptionId", SubscriptionId);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Name)))
        {
            parameters.Add("Name", Name!);
        }

        var parameterSetName = ParameterSetName;
        var commandName = $"Get-ResourceGroup_{parameterSetName}";
        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Resources", commandName, parameters, false, moduleName, moduleVersion);
        activity.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();
}
