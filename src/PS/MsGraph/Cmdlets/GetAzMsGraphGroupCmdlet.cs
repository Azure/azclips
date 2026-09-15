// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.MsGraph;

[Cmdlet(VerbsCommon.Get, "AzMsGraphGroup", DefaultParameterSetName = ListParameterSet)]
public class GetAzMsGraphGroupCmdlet : PSCmdlet
{
    private const string ListParameterSet = "List";
    private const string DisplayNameParameterSet = "DisplayName";
    private const string DisplayNameStartsWithParameterSet = "DisplayNameStartsWith";
    private const string GetParameterSet = "Get";

    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(ParameterSetName = GetParameterSet, Mandatory = true)]
    [Alias("Id")]
    public string ObjectId { get; set; } = "";

    [Parameter(ParameterSetName = DisplayNameParameterSet, Mandatory = true)]
    public string DisplayName { get; set; } = "";

    [Parameter(ParameterSetName = DisplayNameStartsWithParameterSet, Mandatory = true)]
    [Alias("SearchString")]
    public string DisplayNameStartsWith { get; set; } = "";

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    public string? Filter { get; set; }

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    public string[]? Orderby { get; set; }

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    public string? Search { get; set; }

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    public string? ConsistencyLevel { get; set; }

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = DisplayNameParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = DisplayNameStartsWithParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = GetParameterSet, Mandatory = false)]
    public string[]? Select { get; set; }

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = DisplayNameParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = DisplayNameStartsWithParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = GetParameterSet, Mandatory = false)]
    public string[]? Expand { get; set; }

    [Parameter(ParameterSetName = ListParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = DisplayNameParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = DisplayNameStartsWithParameterSet, Mandatory = false)]
    [Parameter(ParameterSetName = GetParameterSet, Mandatory = false)]
    public SwitchParameter AppendSelected { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "GraphGroupError", ErrorCategory.NotSpecified, enumerateOutput: true);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        var commandName = $"Get-AzMsGraphGroup_{ParameterSetName}";

        var parameters = new Dictionary<string, object>();
        foreach (var boundParam in new[] { nameof(ObjectId), nameof(DisplayName), nameof(DisplayNameStartsWith), nameof(Filter), nameof(Orderby), nameof(Search), nameof(ConsistencyLevel), nameof(Select), nameof(Expand) })
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
