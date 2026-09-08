// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.MsGraph;

[Cmdlet(VerbsCommon.New, "AzMsGraphApplication")]
public class NewAzMsGraphApplicationCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true)]
    public string DisplayName { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string SignInAudience { get; set; } = "";

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "GraphApplicationError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(DisplayName)))
        {
            parameters.Add("DisplayName", DisplayName);
        }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SignInAudience)))
        {
            parameters.Add("SignInAudience", SignInAudience);
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("MsGraph", "New-AzMsGraphApplication", parameters, false, moduleName, moduleVersion);
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
