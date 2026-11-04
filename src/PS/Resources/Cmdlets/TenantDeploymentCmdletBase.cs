// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Resources;

/// <summary>Shared engine plumbing for the *-AzTenantDeployment* cmdlets: forwards bound parameters to the Resources extension.</summary>
public abstract class TenantDeploymentCmdletBase : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected virtual bool EnumerateOutput => false;

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "TenantDeploymentError", ErrorCategory.NotSpecified, enumerateOutput: EnumerateOutput);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected Dictionary<string, object> GetBoundParameters() =>
        MyInvocation.BoundParameters
            .Where(p => !CommonParameters.Contains(p.Key) && !OptionalCommonParameters.Contains(p.Key))
            .ToDictionary(p => p.Key, p => p.Value is SwitchParameter s ? s.IsPresent : p.Value);

    protected void Invoke(string commandName, Dictionary<string, object> parameters)
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var invocation = new CommandInvocation("Resources", commandName, parameters, false, MyInvocation.MyCommand.ModuleName, MyInvocation.MyCommand.Version?.ToString());
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
