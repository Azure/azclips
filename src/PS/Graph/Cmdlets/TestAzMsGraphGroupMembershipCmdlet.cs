// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Graph;

[Cmdlet(VerbsDiagnostic.Test, "AzMsGraphGroupMembership")]
public class TestAzMsGraphGroupMembershipCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true)]
    public string Group { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string MemberId { get; set; } = "";

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "GraphGroupMembershipError", ErrorCategory.NotSpecified, enumerateOutput: true);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        var parameters = new Dictionary<string, object>();
        foreach (var boundParam in new[] { nameof(Group), nameof(MemberId) })
        {
            if (MyInvocation.BoundParameters.TryGetValue(boundParam, out var value) && value is not null)
            {
                parameters[boundParam] = value;
            }
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Graph", "Test-AzMsGraphGroupMembership", parameters, false, moduleName, moduleVersion);
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
