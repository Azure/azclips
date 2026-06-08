using System.Diagnostics;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Core.Telemetry;
using Core.Telemetry.Constants;

namespace Microsoft.Azure.Cirrus.Cmdlets.Compute;

[Cmdlet(VerbsCommon.New, "NetworkSecurityGroup", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
public class NewNetworkSecurityGroupCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;
    private ClientType clientType;

    [Parameter(Mandatory = true)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Location { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string[]? RuleNames { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleDirections { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleProtocols { get; set; }

    [Parameter(Mandatory = false)]
    public int[]? RulePriorities { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleSourceAddressPrefixes { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleSourcePortRanges { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleDestinationPortRanges { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleDestinationAddressPrefixes { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? RuleAccesses { get; set; }

    protected override void BeginProcessing()
    {
        streams = new OutputStreams(
            WriteOutput: (value, ct) => { WriteObject(value); return Task.CompletedTask; },
            WriteVerbose: (value, ct) => { WriteVerbose(value?.ToString() ?? ""); return Task.CompletedTask; },
            WriteDebug: (value, ct) => { WriteDebug(value?.ToString() ?? ""); return Task.CompletedTask; },
            WriteWarning: (value, ct) => { WriteWarning(value?.ToString() ?? ""); return Task.CompletedTask; },
            WriteError: (value, ct) =>
            {
                WriteError(new ErrorRecord(
                    new Exception(value?.ToString() ?? ""),
                    "NetworkSecurityGroupError",
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
        using var activity = CirrusServiceProvider.GetRequiredService<ITelemetryService>().StartActivity(TelemetryConstants.PSRootActivityName, ActivityKind.Server);
        activity.TagClientContext(clientType);
        var parameters = new Dictionary<string, object>
        {
            { "SubscriptionId", SubscriptionId },
            { "Name", Name },
            { "Location", Location },
            { "ResourceGroupName", ResourceGroupName },
        };
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleNames)))                      { parameters.Add("RuleNames", RuleNames!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleDirections)))                 { parameters.Add("RuleDirections", RuleDirections!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleProtocols)))                  { parameters.Add("RuleProtocols", RuleProtocols!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RulePriorities)))                 { parameters.Add("RulePriorities", RulePriorities!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleSourceAddressPrefixes)))      { parameters.Add("RuleSourceAddressPrefixes", RuleSourceAddressPrefixes!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleSourcePortRanges)))           { parameters.Add("RuleSourcePortRanges", RuleSourcePortRanges!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleDestinationPortRanges)))      { parameters.Add("RuleDestinationPortRanges", RuleDestinationPortRanges!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleDestinationAddressPrefixes))) { parameters.Add("RuleDestinationAddressPrefixes", RuleDestinationAddressPrefixes!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(RuleAccesses)))                   { parameters.Add("RuleAccesses", RuleAccesses!); }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Compute", "New-NetworkSecurityGroup", parameters, false, moduleName, moduleVersion);
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
