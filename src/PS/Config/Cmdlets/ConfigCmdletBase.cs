// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Cirrus.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Config;

public abstract class ConfigCmdletBase : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    protected OutputStreams Streams = null!;
    protected IEngine Engine = null!;

    protected override void BeginProcessing()
    {
        Streams = CmdletOutputStreamsFactory.Create(this, "ConfigError", ErrorCategory.NotSpecified);

        Engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected void ExecuteInvocation(Dictionary<string, object> parameters)
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var commandName = MyInvocation.MyCommand.Name;

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Config", commandName, parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = Engine.OpenExecutionSession(invocation, Streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();
}
