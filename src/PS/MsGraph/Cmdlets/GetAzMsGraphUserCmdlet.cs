// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.MsGraph;

[Cmdlet(VerbsCommon.Get, "AzMsGraphUser", DefaultParameterSetName = ListParameterSet)]
public class GetAzMsGraphUserCmdlet : PSCmdlet
{
    private const string ListParameterSet = "List";
    private const string MailParameterSet = "Mail";
    private const string DisplayNameParameterSet = "DisplayName";
    private const string StartsWithParameterSet = "StartsWith";
    private const string GetParameterSet = "Get";
    private const string SignedInParameterSet = "SignedIn";

    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(ParameterSetName = GetParameterSet, Mandatory = true)]
    public string Id { get; set; } = "";

    [Parameter(ParameterSetName = MailParameterSet, Mandatory = true)]
    public string Mail { get; set; } = "";

    [Parameter(ParameterSetName = DisplayNameParameterSet, Mandatory = true)]
    public string DisplayName { get; set; } = "";

    [Parameter(ParameterSetName = StartsWithParameterSet, Mandatory = true)]
    public string StartsWith { get; set; } = "";

    [Parameter(ParameterSetName = SignedInParameterSet, Mandatory = true)]
    public SwitchParameter SignedIn { get; set; }

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    public string? Filter { get; set; }

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    public string[]? Orderby { get; set; }

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    public string? Search { get; set; }

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    public string? ConsistencyLevel { get; set; }

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = MailParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = DisplayNameParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = StartsWithParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = GetParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = SignedInParameterSet, Mandatory = false)]
    public string[]? Select { get; set; }

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = MailParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = DisplayNameParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = StartsWithParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = GetParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = SignedInParameterSet, Mandatory = false)]
    public string[]? Expand { get; set; }

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = MailParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = DisplayNameParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = StartsWithParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = GetParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = SignedInParameterSet, Mandatory = false)]
    public SwitchParameter AppendSelected { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "GraphUserError", ErrorCategory.NotSpecified, enumerateOutput: true);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        var commandName = $"Get-AzMsGraphUser_{ParameterSetName}";

        var parameters = new Dictionary<string, object>();
        foreach (var boundParam in new[] { nameof(Id), nameof(Mail), nameof(DisplayName), nameof(StartsWith), nameof(Filter), nameof(Orderby), nameof(Search), nameof(ConsistencyLevel), nameof(Select), nameof(Expand) })
        {
            if (MyInvocation.BoundParameters.TryGetValue(boundParam, out var value) && value is not null)
            {
                parameters[boundParam] = value;
            }
        }
        if (AppendSelected.IsPresent)
        {
            parameters[nameof(AppendSelected)] = true;
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("MsGraph", commandName, parameters, false, moduleName, moduleVersion);
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
