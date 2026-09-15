// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.MsGraph;

[Cmdlet(VerbsCommon.Get, "AzMsGraphGroupMember", DefaultParameterSetName = ObjectIdParameterSet)]
public class GetAzMsGraphGroupMemberCmdlet : PSCmdlet
{
    private const string ObjectIdParameterSet = "ObjectId";
    private const string DisplayNameParameterSet = "DisplayName";

    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(ParameterSetName = ObjectIdParameterSet, Mandatory = true)]
    [Alias("Id", "ObjectId")]
    public string GroupObjectId { get; set; } = "";

    [Parameter(ParameterSetName = DisplayNameParameterSet, Mandatory = true)]
    public string GroupDisplayName { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? Filter { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? Select { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? Expand { get; set; }

    [Parameter(Mandatory = false)]
    public string[]? Orderby { get; set; }

    [Parameter(Mandatory = false)]
    public string? Search { get; set; }

    [Parameter(Mandatory = false)]
    public string? ConsistencyLevel { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "GraphGroupMemberError", ErrorCategory.NotSpecified, enumerateOutput: true);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        var commandName = $"Get-AzMsGraphGroupMember_{ParameterSetName}";

        var parameters = new Dictionary<string, object>();
        foreach (var boundParam in new[] { nameof(GroupObjectId), nameof(GroupDisplayName), nameof(Filter), nameof(Select), nameof(Expand), nameof(Orderby), nameof(Search), nameof(ConsistencyLevel) })
        {
            if (MyInvocation.BoundParameters.TryGetValue(boundParam, out var value) && value is not null)
            {
                parameters[boundParam] = value;
            }
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
